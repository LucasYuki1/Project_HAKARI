import { FlatList, RefreshControl, Text, View } from 'react-native';

import { api } from '@/api/hakari';
import { CartaoOcorrencia } from '@/ui/CartaoOcorrencia';
import { Aviso, Carregando, Vazio, estilos } from '@/ui/componentes';
import { cores } from '@/ui/tema';
import { useDados } from '@/ui/useDados';

// Ocorrências que o cidadão abriu ou apoiou, para acompanhar até o conserto.

export default function MeusReportes() {
  const { dados, erro, atualizando, carregar } = useDados(api.minhasOcorrencias);

  if (!dados && !erro) return <Carregando />;

  return (
    <FlatList
      data={dados ?? []}
      keyExtractor={(o) => String(o.id)}
      contentContainerStyle={estilos.tela}
      refreshControl={<RefreshControl refreshing={atualizando} onRefresh={carregar} colors={[cores.primaria]} />}
      ListHeaderComponent={
        <View style={{ gap: 12 }}>
          <Text style={estilos.textoCorpo}>Os problemas que você reportou ou apoiou. Puxe para baixo para atualizar.</Text>
          {erro && <Aviso tipo="erro" texto={erro} />}
        </View>
      }
      ListEmptyComponent={
        dados ? (
          <Vazio icone="document-text-outline" texto="Você ainda não reportou nenhum problema. Escolha um poste na aba Postes." />
        ) : undefined
      }
      renderItem={({ item }) => <CartaoOcorrencia ocorrencia={item} />}
    />
  );
}
