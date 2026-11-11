import { Redirect, type Href } from 'expo-router';

import { useSessao } from '@/sessao/sessao';

// Rede de segurança: se o app cair numa rota que não existe para o perfil atual,
// volta para a tela inicial certa em vez de mostrar "page could not be found".

export default function RotaNaoEncontrada() {
  const { sessao } = useSessao();
  if (!sessao) return <Redirect href="/entrar" />;
  return <Redirect href={(sessao.usuario.perfil === 'Tecnico' ? '/manutencao' : '/') as Href} />;
}
