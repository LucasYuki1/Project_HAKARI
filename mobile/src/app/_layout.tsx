import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { SessaoProvider, useSessao } from '@/sessao/sessao';
import { cores } from '@/ui/tema';

// Navegação principal. Cada grupo só existe para quem pode vê-lo:
// sem login → entrar/cadastro; cidadão → abas do cidadão; técnico → abas da manutenção.

export default function Raiz() {
  return (
    <SafeAreaProvider>
      <SessaoProvider>
        <StatusBar style="light" />
        <Navegacao />
      </SessaoProvider>
    </SafeAreaProvider>
  );
}

function Navegacao() {
  const { sessao } = useSessao();
  const perfil = sessao?.usuario.perfil;

  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: cores.primaria },
        headerTintColor: cores.branco,
        headerTitleStyle: { fontSize: 20, fontWeight: '700' },
        headerBackTitle: 'Voltar',
        contentStyle: { backgroundColor: cores.fundo },
      }}>
      <Stack.Protected guard={!sessao}>
        <Stack.Screen name="entrar" options={{ headerShown: false }} />
        <Stack.Screen name="cadastro" options={{ title: 'Criar conta' }} />
      </Stack.Protected>

      <Stack.Protected guard={perfil === 'Cidadao'}>
        <Stack.Screen name="(cidadao)" options={{ headerShown: false }} />
        <Stack.Screen name="reportar/[id]" options={{ title: 'Reportar problema' }} />
      </Stack.Protected>

      <Stack.Protected guard={perfil === 'Tecnico'}>
        <Stack.Screen name="(tecnico)" options={{ headerShown: false }} />
      </Stack.Protected>

      <Stack.Protected guard={!!sessao}>
        <Stack.Screen name="poste/[id]" options={{ title: 'Poste' }} />
        <Stack.Screen name="ocorrencia/[id]" options={{ title: 'Ocorrência' }} />
      </Stack.Protected>
    </Stack>
  );
}
