import { Tabs } from 'expo-router';

import { iconeDaAba, opcoesDasAbas } from '@/ui/abas';

export default function AbasDaManutencao() {
  return (
    <Tabs screenOptions={opcoesDasAbas}>
      <Tabs.Screen
        name="index"
        options={{ title: 'Fila de atendimento', tabBarLabel: 'Fila', tabBarIcon: iconeDaAba('list-outline') }}
      />
      <Tabs.Screen
        name="bairros"
        options={{ title: 'Situação por bairro', tabBarLabel: 'Bairros', tabBarIcon: iconeDaAba('map-outline') }}
      />
      <Tabs.Screen name="conta" options={{ title: 'Conta', tabBarIcon: iconeDaAba('person-circle-outline') }} />
    </Tabs>
  );
}
