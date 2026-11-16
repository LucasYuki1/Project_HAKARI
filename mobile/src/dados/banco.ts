import * as SecureStore from 'expo-secure-store';
import * as SQLite from 'expo-sqlite';

import type { PosteResumo, Sessao } from '@/api/tipos';

// Banco SQLite local do celular. Guarda:
// - configuracao: o endereço do servidor da API;
// - postes_cache: a última lista de postes, para consultar sem internet;
// - rascunhos: o reporte que o cidadão começou a escrever, para não perder o texto.
// A sessão (token de acesso) não fica aqui: vai para o armazenamento seguro do sistema
// (expo-secure-store: Keystore no Android, Keychain no iOS), que criptografa o valor.

const CHAVE_SESSAO = 'hakari.sessao';

const banco = SQLite.openDatabaseSync('hakari.db');

banco.execSync(`
  PRAGMA journal_mode = WAL;
  CREATE TABLE IF NOT EXISTS configuracao (
    chave TEXT PRIMARY KEY NOT NULL,
    valor TEXT NOT NULL
  );
  CREATE TABLE IF NOT EXISTS postes_cache (
    id INTEGER PRIMARY KEY NOT NULL,
    codigo TEXT NOT NULL,
    endereco TEXT NOT NULL,
    bairro TEXT NOT NULL,
    latitude REAL NOT NULL,
    longitude REAL NOT NULL,
    status TEXT NOT NULL,
    ultima_comunicacao TEXT,
    salvo_em TEXT NOT NULL
  );
  CREATE TABLE IF NOT EXISTS rascunhos (
    poste_id INTEGER PRIMARY KEY NOT NULL,
    categoria TEXT,
    descricao TEXT NOT NULL,
    atualizado_em TEXT NOT NULL
  );
`);

migrarSessaoDoSqlite();

// ---------- Configuração ----------

export function lerConfiguracao(chave: string): string | null {
  const linha = banco.getFirstSync<{ valor: string }>('SELECT valor FROM configuracao WHERE chave = ?', [chave]);
  return linha?.valor ?? null;
}

export function salvarConfiguracao(chave: string, valor: string) {
  banco.runSync(
    'INSERT INTO configuracao (chave, valor) VALUES (?, ?) ON CONFLICT(chave) DO UPDATE SET valor = excluded.valor',
    [chave, valor],
  );
}

// ---------- Sessão (armazenamento seguro) ----------

export function lerSessao(): Sessao | null {
  const bruto = SecureStore.getItem(CHAVE_SESSAO);
  if (!bruto) return null;

  let sessao: Sessao;
  try {
    sessao = JSON.parse(bruto) as Sessao;
  } catch {
    apagarSessao();
    return null;
  }
  if (new Date(sessao.expiraEm).getTime() <= Date.now()) {
    apagarSessao();
    return null;
  }
  return sessao;
}

export function salvarSessao(s: Sessao) {
  SecureStore.setItem(CHAVE_SESSAO, JSON.stringify(s));
}

export function apagarSessao() {
  // Esvazia na hora (a próxima leitura já não acha sessão) e remove a chave em seguida.
  SecureStore.setItem(CHAVE_SESSAO, '');
  void SecureStore.deleteItemAsync(CHAVE_SESSAO);
}

/**
 * Versões anteriores do app guardavam o token no SQLite, em texto puro. Na primeira abertura depois
 * da atualização, a sessão vai para o armazenamento seguro, a tabela é apagada e o VACUUM reescreve
 * o arquivo do banco, para o token não sobrar nas páginas livres.
 */
function migrarSessaoDoSqlite() {
  const tabela = banco.getFirstSync<{ n: number }>(
    "SELECT count(*) AS n FROM sqlite_master WHERE type = 'table' AND name = 'sessao'",
  );
  if (!tabela?.n) return;

  type LinhaAntiga = { token: string; expira_em: string; usuario_id: number; nome: string; perfil: string };
  const l = banco.getFirstSync<LinhaAntiga>('SELECT * FROM sessao WHERE id = 1');
  if (l) {
    salvarSessao({
      token: l.token,
      expiraEm: l.expira_em,
      usuario: { id: l.usuario_id, nome: l.nome, perfil: l.perfil as Sessao['usuario']['perfil'] },
    });
  }
  banco.execSync('DROP TABLE sessao;');
  banco.execSync('VACUUM;');
}

// ---------- Postes (cache para uso sem internet) ----------

type LinhaPoste = {
  id: number; codigo: string; endereco: string; bairro: string; latitude: number; longitude: number;
  status: string; ultima_comunicacao: string | null; salvo_em: string;
};

export function salvarPostesCache(postes: PosteResumo[]) {
  const agora = new Date().toISOString();
  banco.withTransactionSync(() => {
    banco.runSync('DELETE FROM postes_cache');
    for (const p of postes) {
      banco.runSync(
        `INSERT INTO postes_cache (id, codigo, endereco, bairro, latitude, longitude, status, ultima_comunicacao, salvo_em)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)`,
        [p.id, p.codigo, p.endereco, p.bairro, p.latitude, p.longitude, p.status, p.ultimaComunicacao, agora],
      );
    }
  });
}

export function lerPostesCache(): { postes: PosteResumo[]; salvoEm: string | null } {
  const linhas = banco.getAllSync<LinhaPoste>('SELECT * FROM postes_cache ORDER BY codigo');
  return {
    postes: linhas.map((l) => ({
      id: l.id, codigo: l.codigo, endereco: l.endereco, bairro: l.bairro, latitude: l.latitude,
      longitude: l.longitude, status: l.status, ultimaComunicacao: l.ultima_comunicacao,
    })),
    salvoEm: linhas[0]?.salvo_em ?? null,
  };
}

// ---------- Rascunhos de reporte ----------

export type Rascunho = { categoria: string | null; descricao: string };

export function lerRascunho(posteId: number): Rascunho | null {
  return banco.getFirstSync<Rascunho>('SELECT categoria, descricao FROM rascunhos WHERE poste_id = ?', [posteId]);
}

export function salvarRascunho(posteId: number, r: Rascunho) {
  banco.runSync(
    `INSERT INTO rascunhos (poste_id, categoria, descricao, atualizado_em) VALUES (?, ?, ?, ?)
     ON CONFLICT(poste_id) DO UPDATE SET categoria = excluded.categoria, descricao = excluded.descricao,
       atualizado_em = excluded.atualizado_em`,
    [posteId, r.categoria, r.descricao, new Date().toISOString()],
  );
}

export function apagarRascunho(posteId: number) {
  banco.runSync('DELETE FROM rascunhos WHERE poste_id = ?', [posteId]);
}

/** Apaga tudo o que é pessoal ao sair ou excluir a conta (o endereço do servidor fica). */
export function limparDadosDoUsuario() {
  apagarSessao();
  banco.execSync('DELETE FROM rascunhos; DELETE FROM postes_cache;');
}
