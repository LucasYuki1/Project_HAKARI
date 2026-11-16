// Casos de teste das regras de mapeamento do app (semgrep --test). Não é código do Hakari.
banco.execSync(`
  CREATE TABLE IF NOT EXISTS sessao (
    id INTEGER PRIMARY KEY,
    // ruleid: hakari.mapa.app-armazenamento
    token TEXT NOT NULL,
    // ok: hakari.mapa.app-armazenamento
    perfil TEXT NOT NULL
  );
`);

// ruleid: hakari.mapa.app-tipo
export type Sessao = { token: string; expiraEm: string };
// ok: hakari.mapa.app-tipo
export type Poste = { id: number; codigo: string; bairro: string };
