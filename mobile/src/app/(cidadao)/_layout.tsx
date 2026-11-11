import { Tabs } from 'expo-router';

import { iconeDaAba, opcoesDasAbas } from '@/ui/abas';

export default function AbasDoCidadao() {
  return (
    <Tabs screenOptions={opcoesDasAbas}>
      <Tabs.Screen
        name="index"
        options={{ title: 'Postes', tabBarIcon: iconeDaAba('bulb-outline'), tabBarAccessibilityLabel: 'Postes da cidade' }}
      />
      <Tabs.Screen
        name="ocorrencias"
        options={{
          title: 'Meus reportes',
          tabBarIcon: iconeDaAba('document-text-outline'),
          tabBarAccessibilityLabel: 'Meus reportes',
        }}
      />
      <Tabs.Screen
        name="perfil"
        options={{ title: 'Perfil', tabBarIcon: iconeDaAba('person-circle-outline'), tabBarAccessibilityLabel: 'Meu perfil' }}
      />
    </Tabs>
  );
}
