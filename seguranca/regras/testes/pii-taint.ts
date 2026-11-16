// Casos de teste das regras de taint do app (semgrep --test). Não é código do Hakari.

export function salvarSessaoInsegura(s: Sessao) {
  // ruleid: hakari.taint.credencial-em-armazenamento-local
  banco.runSync('INSERT INTO sessao (id, token) VALUES (1, ?)', [s.token]);
}

export function salvarSenhaInsegura(form: { senha: string }) {
  // ruleid: hakari.taint.credencial-em-armazenamento-local
  AsyncStorage.setItem('senha', form.senha);
}

export function salvarSessaoSegura(s: Sessao) {
  // ok: hakari.taint.credencial-em-armazenamento-local
  SecureStore.setItem('hakari.sessao', JSON.stringify(s));
  // ok: hakari.taint.credencial-em-armazenamento-local
  banco.runSync('INSERT INTO postes_cache (id, codigo) VALUES (?, ?)', [p.id, p.codigo]);
}

export function depurar(dados: { email: string; descricao: string }, poste: { codigo: string }) {
  // ruleid: hakari.taint.pii-em-console
  console.log('cadastro', dados.email);
  // ruleid: hakari.taint.pii-em-console
  console.warn(`reporte: ${dados.descricao}`);
  // ok: hakari.taint.pii-em-console
  console.log('poste carregado', poste.codigo);
}

export const api = {
  // ruleid: hakari.taint.pii-na-url
  buscarConta: (form: { email: string }) => requisitar('GET', `/api/contas?email=${form.email}`),
  // ruleid: hakari.taint.pii-na-url
  verificar: (form: { cpf: string }) => fetch(`https://api.exemplo.com/cpf/${form.cpf}`),
  // ok: hakari.taint.pii-na-url
  poste: (id: number) => requisitar('GET', `/api/postes/${id}`),
  // ok: hakari.taint.pii-na-url
  entrar: (form: { email: string; senha: string }) =>
    requisitar('POST', '/api/auth/login', { email: form.email, senha: form.senha }),
};
