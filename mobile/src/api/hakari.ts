import { requisitar } from './cliente';
import type {
  Categoria,
  MeusDados,
  OcorrenciaDetalhe,
  OcorrenciaResumo,
  PosteDetalhe,
  PosteResumo,
  ReporteResposta,
  ResumoBairro,
  Sessao,
} from './tipos';

// Uma função por endpoint da API. As telas só falam com este arquivo.

export const api = {
  // Conta
  entrar: (email: string, senha: string) => requisitar<Sessao>('POST', '/api/auth/login', { email, senha }),
  cadastrar: (dados: { nome: string; email: string; telefone: string; senha: string; aceitouTermo: boolean }) =>
    requisitar<Sessao>('POST', '/api/auth/cadastro', dados),
  meusDados: () => requisitar<MeusDados>('GET', '/api/me'),
  excluirConta: () => requisitar<void>('DELETE', '/api/me'),

  // Postes
  postes: () => requisitar<PosteResumo[]>('GET', '/api/postes'),
  poste: (id: number) => requisitar<PosteDetalhe>('GET', `/api/postes/${id}`),
  reportar: (posteId: number, categoria: Categoria, descricao: string) =>
    requisitar<ReporteResposta>('POST', `/api/postes/${posteId}/reportes`, { categoria, descricao }),

  // Ocorrências
  minhasOcorrencias: () => requisitar<OcorrenciaResumo[]>('GET', '/api/me/ocorrencias'),
  ocorrencia: (id: number) => requisitar<OcorrenciaDetalhe>('GET', `/api/ocorrencias/${id}`),
  apoiar: (id: number) => requisitar<void>('POST', `/api/ocorrencias/${id}/apoios`),
  assumir: (id: number) => requisitar<OcorrenciaDetalhe>('POST', `/api/ocorrencias/${id}/assumir`),
  resolver: (id: number, solucao: string) =>
    requisitar<OcorrenciaDetalhe>('POST', `/api/ocorrencias/${id}/resolver`, { solucao }),

  // Manutenção
  fila: () => requisitar<OcorrenciaResumo[]>('GET', '/api/manutencao/fila'),
  bairros: () => requisitar<ResumoBairro[]>('GET', '/api/manutencao/bairros'),
};
