import { useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';

import { mensagem } from '@/api/cliente';

/**
 * Busca dados da API sempre que a tela ganha foco (ao abrir e ao voltar para ela).
 * `buscar` precisa ser estável (função do módulo api ou useCallback).
 */
export function useDados<T>(buscar: () => Promise<T>) {
  const [dados, setDados] = useState<T | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [atualizando, setAtualizando] = useState(false);

  const carregar = useCallback(async () => {
    setAtualizando(true);
    try {
      setDados(await buscar());
      setErro(null);
    } catch (e) {
      setErro(mensagem(e));
    } finally {
      setAtualizando(false);
    }
  }, [buscar]);

  useFocusEffect(
    useCallback(() => {
      carregar();
    }, [carregar]),
  );

  return { dados, setDados, erro, atualizando, carregar };
}
