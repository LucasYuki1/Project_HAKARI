import * as SQLite from 'expo-sqlite';

import type { PosteResumo, Sessao } from '@/api/tipos';

// Banco SQLite local do celular. Guarda:
// - configuracao: o endereço do servidor da API;
// - sessao: o login, para o cidadão não precisar entrar toda vez;
// - postes_cache: a última lista de postes, para consultar sem internet;
// - rascunhos: o reporte que o cidadão começou a escrever, para não perder o texto.

const banco = SQLite.openDatabaseSync('hakari.db');

banco.execSync(`
  PRAGMA journal_mode = WAL;
  CREATE TABLE IF NOT EXISTS configuracao (
    chave TEXT PRIMARY KEY NOT NULL,
    valor TEXT NOT NULL
  );
  CREATE TABLE IF NOT EXISTS sessao (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    token TEXT NOT NULL,
    expira_em TEXT NOT NULL,
    usuario_id INTEGER NOT NULL,
    nome TEXT NOT NULL,
    perfil TEXT NOT NULL
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

// ---------- Sessão ----------

type LinhaSessao = { token: string; expira_em: string; usuario_id: number; nome: string; perfil: string };

export function lerSessao(): Sessao | null {
  const l = banco.getFirstSync<LinhaSessao>('SELECT * FROM sessao WHERE id = 1');
  if (!l) return null;
  if (new Date(l.expira_em).getTime() <= Date.now()) {
    apagarSessao();
    return null;
  }
  return {
    token: l.token,
    expiraEm: l.expira_em,
    usuario: { id: l.usuario_id, nome: l.nome, perfil: l.perfil as Sessao['usuario']['perfil'] },
  };
}

export function salvarSessao(s: Sessao) {
  banco.runSync(
    `INSERT INTO sessao (id, token, expira_em, usuario_id, nome, perfil) VALUES (1, ?, ?, ?, ?, ?)
     ON CONFLICT(id) DO UPDATE SET token = excluded.token, expira_em = excluded.expira_em,
       usuario_id = excluded.usuario_id, nome = excluded.nome, perfil = excluded.perfil`,
    [s.token, s.expiraEm, s.usuario.id, s.usuario.nome, s.usuario.perfil],
  );
}

export function apagarSessao() {
  banco.runSync('DELETE FROM sessao');
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
  banco.execSync('DELETE FROM sessao; DELETE FROM rascunhos; DELETE FROM postes_cache;');
}
