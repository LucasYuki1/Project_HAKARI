import { router } from 'expo-router';
import { Text, View } from 'react-native';

import type { OcorrenciaResumo } from '@/api/tipos';
import { Cartao, Selo, estilos } from './componentes';
import { dataHora, textoCategoria, visualDaOcorrencia, visualDaSeveridade } from './textos';

/** Cartão de ocorrência usado em "Meus reportes" e na fila do técnico. */
export function CartaoOcorrencia({ ocorrencia: o, mostrarSeveridade = false }: {
  ocorrencia: OcorrenciaResumo;
  mostrarSeveridade?: boolean;
}) {
  const categoria = textoCategoria(o.categoria);
  return (
    <Cartao
      aoTocar={() => router.push(`/ocorrencia/${o.id}`)}
      rotulo={`Ocorrência ${o.id}: ${categoria} em ${o.endereco}, ${o.bairro}. ` +
        `${mostrarSeveridade ? `Severidade ${o.severidade}. ` : ''}Situação: ${o.status}.`}
      dica="Abre os detalhes e o histórico">
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
        {mostrarSeveridade && <Selo visual={visualDaSeveridade(o.severidade)} texto={o.severidade} />}
        <Selo visual={visualDaOcorrencia(o.status)} texto={o.status} />
      </View>
      <Text style={estilos.textoForte}>
        #{o.id} · {categoria}
      </Text>
      <Text style={estilos.textoCorpo}>{o.endereco}</Text>
      <Text style={estilos.textoSecundario}>
        {o.bairro} · {o.posteCodigo} · origem {o.origem.toLowerCase()}
      </Text>
      <Text style={estilos.textoSecundario}>
        Aberta em {dataHora(o.abertaEm)}
        {o.apoios > 0 ? ` · ${o.apoios} ${o.apoios === 1 ? 'apoio' : 'apoios'}` : ''}
      </Text>
    </Cartao>
  );
}
