import { FlatList, RefreshControl, Text, View } from 'react-native';

import { api } from '@/api/hakari';
import { Aviso, Carregando, Cartao, Selo, estilos } from '@/ui/componentes';
import { cores } from '@/ui/tema';
import { visualDoPoste } from '@/ui/textos';
import { useDados } from '@/ui/useDados';

// Resumo da iluminação por bairro, para a manutenção planejar a rota do dia.

export default function Bairros() {
  const { dados, erro, atualizando, carregar } = useDados(api.bairros);

  if (!dados && !erro) return <Carregando />;

  return (
    <FlatList
      data={dados ?? []}
      keyExtractor={(b) => b.bairro}
      contentContainerStyle={estilos.tela}
      refreshControl={<RefreshControl refreshing={atualizando} onRefresh={carregar} colors={[cores.primaria]} />}
      ListHeaderComponent={erro ? <Aviso tipo="erro" texto={erro} /> : undefined}
      renderItem={({ item: b }) => (
        <Cartao>
          <Text style={estilos.textoForte} accessibilityRole="header">
            {b.bairro}
          </Text>
          <Text style={estilos.textoSecundario}>
            {b.postes} {b.postes === 1 ? 'poste' : 'postes'} · {b.ocorrenciasAbertas}{' '}
            {b.ocorrenciasAbertas === 1 ? 'ocorrência aberta' : 'ocorrências abertas'}
          </Text>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {b.funcionando > 0 && <Selo visual={visualDoPoste('Funcionando')} texto={`${b.funcionando} funcionando`} />}
            {b.apagados > 0 && <Selo visual={visualDoPoste('Apagado')} texto={`${b.apagados} apagado(s)`} />}
            {b.semComunicacao > 0 && (
              <Selo visual={visualDoPoste('Sem comunicação')} texto={`${b.semComunicacao} sem comunicação`} />
            )}
            {b.aguardandoInstalacao > 0 && (
              <Selo visual={visualDoPoste('Aguardando instalação')} texto={`${b.aguardandoInstalacao} aguardando instalação`} />
            )}
          </View>
        </Cartao>
      )}
    />
  );
}
