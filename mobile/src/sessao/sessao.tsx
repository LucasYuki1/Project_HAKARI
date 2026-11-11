import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

import { definirToken, quandoSessaoExpirar } from '@/api/cliente';
import type { Sessao } from '@/api/tipos';
import { lerSessao, limparDadosDoUsuario, salvarSessao } from '@/dados/banco';

// Guarda quem está logado. A sessão fica no SQLite, então o app abre já logado.

type ContextoSessao = {
  sessao: Sessao | null;
  entrar: (s: Sessao) => void;
  sair: () => void;
};

const Contexto = createContext<ContextoSessao | null>(null);

function sessaoInicial() {
  const s = lerSessao();
  definirToken(s?.token ?? null);
  return s;
}

export function SessaoProvider({ children }: { children: ReactNode }) {
  const [sessao, setSessao] = useState<Sessao | null>(sessaoInicial);

  const entrar = useCallback((s: Sessao) => {
    salvarSessao(s);
    definirToken(s.token);
    setSessao(s);
  }, []);

  const sair = useCallback(() => {
    limparDadosDoUsuario();
    definirToken(null);
    setSessao(null);
  }, []);

  useEffect(() => quandoSessaoExpirar(sair), [sair]);

  const valor = useMemo(() => ({ sessao, entrar, sair }), [sessao, entrar, sair]);
  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}

export function useSessao() {
  const ctx = useContext(Contexto);
  if (!ctx) throw new Error('useSessao precisa estar dentro de SessaoProvider');
  return ctx;
}
