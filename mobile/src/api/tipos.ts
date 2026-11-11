// Formato do JSON da API (espelha os DTOs em api/src/Hakari.Api/Contratos/Contratos.cs).

export type Perfil = 'Cidadao' | 'Tecnico';
export type Severidade = 'Aviso' | 'Alta' | 'Desastre';
export type Categoria =
  | 'LampadaApagada'
  | 'LampadaPiscando'
  | 'PosteDanificado'
  | 'FiacaoExposta'
  | 'SemComunicacao'
  | 'Outro';

export type UsuarioResumo = { id: number; nome: string; perfil: Perfil };

export type Sessao = { token: string; expiraEm: string; usuario: UsuarioResumo };

export type MeusDados = {
  id: number;
  nome: string;
  email: string;
  telefone: string;
  perfil: Perfil;
  criadoEm: string;
  termoAceitoEm: string | null;
};

export type PosteResumo = {
  id: number;
  codigo: string;
  endereco: string;
  bairro: string;
  latitude: number;
  longitude: number;
  status: string;
  ultimaComunicacao: string | null;
};

export type Alerta = {
  trigger: 'LampadaSemResposta' | 'SinalFraco' | 'NoIsolado';
  severidade: Severidade;
  estado: string;
  abertoEm: string;
  normalizadoEm: string | null;
  detalhe: string | null;
};

export type OcorrenciaResumo = {
  id: number;
  posteId: number;
  posteCodigo: string;
  endereco: string;
  bairro: string;
  status: string;
  severidade: Severidade;
  origem: string;
  categoria: Categoria;
  abertaEm: string;
  apoios: number;
};

export type PosteDetalhe = PosteResumo & {
  brilho: number | null;
  rssi: number | null;
  alertasAtivos: Alerta[];
  ocorrenciaAtiva: OcorrenciaResumo | null;
};

export type Historico = { registradoEm: string; status: string; descricao: string };

export type OcorrenciaDetalhe = {
  resumo: OcorrenciaResumo;
  descricao: string;
  autor: string | null;
  responsavel: string | null;
  solucao: string | null;
  resolvidaEm: string | null;
  historico: Historico[];
};

export type ReporteResposta = { ocorrenciaId: number; apoioAdicionado: boolean; mensagem: string };

export type ResumoBairro = {
  bairro: string;
  postes: number;
  funcionando: number;
  apagados: number;
  semComunicacao: number;
  aguardandoInstalacao: number;
  ocorrenciasAbertas: number;
};
