import { lerConfiguracao, salvarConfiguracao } from '@/dados/banco';

// Cliente HTTP da API Hakari: monta a URL, envia o token e traduz os erros (ProblemDetails)
// em mensagens que o cidadão entende.

const SERVIDOR_PADRAO = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5080';
const TEMPO_LIMITE_MS = 10_000;

export class ErroApi extends Error {
  constructor(
    public readonly status: number,
    mensagem: string,
  ) {
    super(mensagem);
  }

  /** true quando nem chegou ao servidor (sem internet, servidor desligado, IP errado). */
  get semConexao() {
    return this.status === 0;
  }
}

let token: string | null = null;
let aoExpirar: (() => void) | null = null;

export function definirToken(novo: string | null) {
  token = novo;
}

export function quandoSessaoExpirar(acao: () => void) {
  aoExpirar = acao;
}

export function servidorAtual() {
  return lerConfiguracao('servidor') ?? SERVIDOR_PADRAO;
}

export function definirServidor(url: string) {
  salvarConfiguracao('servidor', url.trim().replace(/\/+$/, ''));
}

type Metodo = 'GET' | 'POST' | 'DELETE';

export async function requisitar<T>(metodo: Metodo, caminho: string, corpo?: unknown): Promise<T> {
  const controle = new AbortController();
  const limite = setTimeout(() => controle.abort(), TEMPO_LIMITE_MS);

  let resposta: Response;
  try {
    resposta = await fetch(servidorAtual() + caminho, {
      method: metodo,
      headers: {
        Accept: 'application/json',
        ...(corpo !== undefined ? { 'Content-Type': 'application/json' } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: corpo !== undefined ? JSON.stringify(corpo) : undefined,
      signal: controle.signal,
    });
  } catch {
    throw new ErroApi(0, 'Não foi possível falar com o servidor. Confira a internet e tente de novo.');
  } finally {
    clearTimeout(limite);
  }

  if (resposta.status === 401 && token) {
    aoExpirar?.();
    throw new ErroApi(401, 'Sua sessão expirou. Entre de novo.');
  }

  if (!resposta.ok) throw new ErroApi(resposta.status, await mensagemDeErro(resposta));

  const texto = await resposta.text();
  return (texto ? JSON.parse(texto) : undefined) as T;
}

async function mensagemDeErro(resposta: Response): Promise<string> {
  if (resposta.status === 403) return 'Você não tem permissão para fazer isso.';
  try {
    const problema = await resposta.json();
    // Erros de validação automática do ASP.NET vêm em "errors": { campo: [mensagens] }.
    const primeiraValidacao = problema?.errors ? Object.values<string[]>(problema.errors)[0]?.[0] : undefined;
    return problema?.detail ?? primeiraValidacao ?? problema?.title ?? 'Algo deu errado. Tente de novo.';
  } catch {
    return 'Algo deu errado. Tente de novo.';
  }
}

export function mensagem(erro: unknown) {
  return erro instanceof ErroApi ? erro.message : 'Algo deu errado. Tente de novo.';
}
