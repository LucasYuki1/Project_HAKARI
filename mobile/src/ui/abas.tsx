import Ionicons from '@expo/vector-icons/Ionicons';
import type { ComponentProps } from 'react';
import type { ColorValue } from 'react-native';

import { cores, fonte } from './tema';

// Visual comum das abas do cidadão e do técnico.

export const opcoesDasAbas = {
  headerStyle: { backgroundColor: cores.primaria },
  headerTintColor: cores.branco,
  headerTitleStyle: { fontSize: 20, fontWeight: '700' as const },
  tabBarActiveTintColor: cores.primaria,
  tabBarInactiveTintColor: cores.textoSecundario,
  tabBarLabelStyle: { fontSize: fonte.pequena - 1, fontWeight: '700' as const },
  sceneStyle: { backgroundColor: cores.fundo },
};

type NomeIcone = ComponentProps<typeof Ionicons>['name'];

export function iconeDaAba(nome: NomeIcone) {
  function Icone({ color, size }: { color: ColorValue; size: number }) {
    return <Ionicons name={nome} color={color as string} size={size + 2} />;
  }
  return Icone;
}
