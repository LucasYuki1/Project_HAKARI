import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useCallback, useState } from 'react';
import { RefreshControl, ScrollView, Text, View } from 'react-native';

import { mensagem } from '@/api/cliente';
import { api } from '@/api/hakari';
import { useSessao } from '@/sessao/sessao';
import { CartaoOcorrencia } from '@/ui/CartaoOcorrencia';
import { Aviso, Botao, Carregando, Cartao, Linha, Selo, Titulo, estilos } from '@/ui/componentes';
import { cores } from '@/ui/tema';
import { dataHora, haQuantoTempo, textoAlerta, visualDaSeveridade, visualDoPoste } from '@/ui/textos';
import { useDados } from '@/ui/useDados';

// Detalhe do poste. Se já existe ocorrência aberta, o cidadão só apoia (não duplica o chamado).

export default function DetalheDoPoste() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const posteId = Number(id);
  const { sessao } = useSessao();
  const ehCidadao = sessao?.usuario.perfil === 'Cidadao';

  const buscar = useCallback(() => api.poste(posteId), [posteId]);
  const { dados: poste, erro, atualizando, carregar } = useDados(buscar);

  const [apoiando, setApoiando] = useState(false);
  const [retorno, setRetorno] = useState<{ tipo: 'sucesso' | 'erro'; texto: string } | null>(null);

  async function apoiar(ocorrenciaId: number) {
    setApoiando(true);
    setRetorno(null);
    try {
      await api.apoiar(ocorrenciaId);
      setRetorno({ tipo: 'sucesso', texto: 'Obrigado! Seu apoio foi registrado e ajuda a priorizar o conserto.' });
      carregar();
    } catch (e) {
      setRetorno({ tipo: 'erro', texto: mensagem(e) });
    } finally {
      setApoiando(false);
    }
  }

  if (!poste && !erro) return <Carregando />;

  return (
    <ScrollView
      contentContainerStyle={estilos.tela}
      refreshControl={<RefreshControl refreshing={atualizando} onRefresh={carregar} colors={[cores.primaria]} />}>
      <Stack.Screen options={{ title: poste ? `Poste ${poste.codigo}` : 'Poste' }} />
      {erro && <Aviso tipo="erro" texto={erro} />}

      {poste && (
        <>
          <Cartao>
            <Titulo>{poste.endereco}</Titulo>
            <Text style={estilos.textoSecundario}>
              {poste.bairro} · {poste.codigo}
            </Text>
            <Selo visual={visualDoPoste(poste.status)} texto={poste.status} grande />
            <Linha
              rotulo="Último sinal do poste"
              valor={poste.ultimaComunicacao ? `${haQuantoTempo(poste.ultimaComunicacao)} (${dataHora(poste.ultimaComunicacao)})` : 'Ainda não se comunicou'}
            />
            {poste.brilho !== null && <Linha rotulo="Brilho da lâmpada" valor={`${poste.brilho}%`} />}
          </Cartao>

          {poste.alertasAtivos.length > 0 && (
            <Cartao>
              <Text style={estilos.textoForte} accessibilityRole="header">
                O que o poste detectou
              </Text>
              {poste.alertasAtivos.map((a) => (
                <View key={`${a.trigger}-${a.abertoEm}`} style={{ gap: 6 }}>
                  <Selo visual={visualDaSeveridade(a.severidade)} texto={a.severidade} />
                  <Text style={estilos.textoCorpo}>{textoAlerta[a.trigger] ?? a.trigger}</Text>
                  <Text style={estilos.textoSecundario}>Desde {dataHora(a.abertoEm)}</Text>
                </View>
              ))}
            </Cartao>
          )}

          {poste.ocorrenciaAtiva ? (
            <>
              <Text style={estilos.textoForte} accessibilityRole="header">
                Este problema já está registrado
              </Text>
              <CartaoOcorrencia ocorrencia={poste.ocorrenciaAtiva} />
              {ehCidadao && (
                <Botao
                  texto="Eu também vi esse problema"
                  icone="hand-left-outline"
                  carregando={apoiando}
                  aoTocar={() => apoiar(poste.ocorrenciaAtiva!.id)}
                  dica="Adiciona seu apoio à ocorrência que já existe"
                />
              )}
            </>
          ) : (
            ehCidadao && (
              <Botao
                texto="Reportar um problema"
                icone="megaphone-outline"
                aoTocar={() => router.push(`/reportar/${poste.id}`)}
              />
            )
          )}

          {retorno && <Aviso tipo={retorno.tipo} texto={retorno.texto} />}
        </>
      )}
    </ScrollView>
  );
}
