import { FlatList, RefreshControl, Text, View } from 'react-native';

import { api } from '@/api/hakari';
import { CartaoOcorrencia } from '@/ui/CartaoOcorrencia';
import { Aviso, Carregando, Vazio, estilos } from '@/ui/componentes';
import { cores } from '@/ui/tema';
import { useDados } from '@/ui/useDados';

// Fila da manutenção: a API já entrega ordenada (Desastre primeiro, depois Alta; as mais antigas antes).

export default function Fila() {
  const { dados, erro, atualizando, carregar } = useDados(api.fila);

  if (!dados && !erro) return <Carregando texto="Carregando a fila…" />;

  const desastres = dados?.filter((o) => o.severidade === 'Desastre').length ?? 0;

  return (
    <FlatList
      data={dados ?? []}
      keyExtractor={(o) => String(o.id)}
      contentContainerStyle={estilos.tela}
      refreshControl={<RefreshControl refreshing={atualizando} onRefresh={carregar} colors={[cores.primaria]} />}
      ListHeaderComponent={
        <View style={{ gap: 12 }}>
          {dados && (
            <Text style={estilos.textoCorpo}>
              {dados.length} {dados.length === 1 ? 'ocorrência aberta' : 'ocorrências abertas'}
              {desastres > 0 ? `, ${desastres} de severidade Desastre (poste sem comunicação).` : '.'}
            </Text>
          )}
          {erro && <Aviso tipo="erro" texto={erro} />}
        </View>
      }
      ListEmptyComponent={dados ? <Vazio icone="checkmark-done-circle-outline" texto="Nenhuma ocorrência aberta." /> : undefined}
      renderItem={({ item }) => <CartaoOcorrencia ocorrencia={item} mostrarSeveridade />}
    />
  );
}
