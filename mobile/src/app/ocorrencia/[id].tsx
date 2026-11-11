import { Stack, useLocalSearchParams } from 'expo-router';
import { useCallback, useState } from 'react';
import { RefreshControl, ScrollView, Text, View } from 'react-native';

import { mensagem } from '@/api/cliente';
import { api } from '@/api/hakari';
import type { OcorrenciaDetalhe } from '@/api/tipos';
import { useSessao } from '@/sessao/sessao';
import { Aviso, Botao, Campo, Carregando, Cartao, Linha, Selo, Titulo, estilos } from '@/ui/componentes';
import { cores } from '@/ui/tema';
import { dataHora, textoCategoria, visualDaOcorrencia, visualDaSeveridade } from '@/ui/textos';
import { useDados } from '@/ui/useDados';

// Detalhe da ocorrência com a linha do tempo. O técnico assume e resolve por aqui;
// o cidadão acompanha e pode apoiar.

export default function DetalheDaOcorrencia() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const ocorrenciaId = Number(id);
  const { sessao } = useSessao();
  const ehTecnico = sessao?.usuario.perfil === 'Tecnico';

  const buscar = useCallback(() => api.ocorrencia(ocorrenciaId), [ocorrenciaId]);
  const { dados: o, setDados, erro, atualizando, carregar } = useDados(buscar);

  const [solucao, setSolucao] = useState('');
  const [agindo, setAgindo] = useState(false);
  const [retorno, setRetorno] = useState<{ tipo: 'sucesso' | 'erro'; texto: string } | null>(null);

  async function executar(acao: () => Promise<OcorrenciaDetalhe | void>, sucesso: string) {
    setAgindo(true);
    setRetorno(null);
    try {
      const atualizado = await acao();
      if (atualizado) setDados(atualizado);
      else await carregar();
      setRetorno({ tipo: 'sucesso', texto: sucesso });
    } catch (e) {
      setRetorno({ tipo: 'erro', texto: mensagem(e) });
    } finally {
      setAgindo(false);
    }
  }

  if (!o && !erro) return <Carregando />;

  const r = o?.resumo;
  const resolvida = r?.status === 'Resolvida';

  return (
    <ScrollView
      contentContainerStyle={estilos.tela}
      keyboardShouldPersistTaps="handled"
      refreshControl={<RefreshControl refreshing={atualizando} onRefresh={carregar} colors={[cores.primaria]} />}>
      <Stack.Screen options={{ title: r ? `Ocorrência #${r.id}` : 'Ocorrência' }} />
      {erro && <Aviso tipo="erro" texto={erro} />}

      {o && r && (
        <>
          <Cartao>
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
              <Selo visual={visualDaOcorrencia(r.status)} texto={r.status} grande />
              <Selo visual={visualDaSeveridade(r.severidade)} texto={`Severidade ${r.severidade}`} grande />
            </View>
            <Titulo>{textoCategoria(r.categoria)}</Titulo>
            <Text style={estilos.textoCorpo}>{o.descricao}</Text>
            <Linha rotulo="Local" valor={`${r.endereco}, ${r.bairro} (poste ${r.posteCodigo})`} />
            <Linha rotulo="Aberta em" valor={`${dataHora(r.abertaEm)}, origem ${r.origem.toLowerCase()}`} />
            {o.autor && <Linha rotulo="Quem reportou" valor={o.autor} />}
            <Linha rotulo="Apoios de outros cidadãos" valor={String(r.apoios)} />
            {o.responsavel && <Linha rotulo="Técnico responsável" valor={o.responsavel} />}
            {o.solucao && <Linha rotulo="Solução" valor={o.solucao} />}
            {o.resolvidaEm && <Linha rotulo="Resolvida em" valor={dataHora(o.resolvidaEm)} />}
          </Cartao>

          {ehTecnico && r.status === 'Aberta' && (
            <Botao
              texto="Assumir esta ocorrência"
              icone="hand-right-outline"
              carregando={agindo}
              aoTocar={() => executar(() => api.assumir(r.id), 'Ocorrência assumida. Ela agora está em atendimento.')}
            />
          )}

          {ehTecnico && r.status === 'Em atendimento' && (
            <Cartao>
              <Campo
                rotulo="O que foi feito"
                value={solucao}
                onChangeText={setSolucao}
                multiline
                placeholder="Ex.: troca do relé fotoelétrico"
              />
              <Botao
                texto="Marcar como resolvida"
                icone="checkmark-done"
                carregando={agindo}
                aoTocar={() =>
                  executar(() => api.resolver(r.id, solucao.trim()), 'Ocorrência resolvida. O cidadão já vê a solução.')
                }
              />
            </Cartao>
          )}

          {!ehTecnico && !resolvida && o.autor !== 'Você' && (
            <Botao
              texto="Eu também vi esse problema"
              icone="hand-left-outline"
              carregando={agindo}
              aoTocar={() => executar(() => api.apoiar(r.id), 'Obrigado! Seu apoio foi registrado.')}
            />
          )}

          {retorno && <Aviso tipo={retorno.tipo} texto={retorno.texto} />}

          <Text style={estilos.textoForte} accessibilityRole="header">
            Linha do tempo
          </Text>
          <View style={{ gap: 0 }}>
            {o.historico.map((h, i) => (
              <View
                key={`${h.registradoEm}-${i}`}
                style={{ flexDirection: 'row', gap: 12 }}
                accessible
                accessibilityLabel={`${dataHora(h.registradoEm)}. ${h.status}. ${h.descricao}`}>
                <View style={{ alignItems: 'center', width: 20 }}>
                  <View style={{ width: 14, height: 14, borderRadius: 7, marginTop: 4,
                    backgroundColor: visualDaOcorrencia(h.status).cor }} />
                  {i < o.historico.length - 1 && <View style={{ flex: 1, width: 2, backgroundColor: cores.borda }} />}
                </View>
                <View style={{ flex: 1, paddingBottom: 16, gap: 2 }}>
                  <Text style={estilos.textoSecundario}>{dataHora(h.registradoEm)}</Text>
                  <Text style={[estilos.textoCorpo, { fontWeight: '700' }]}>{h.status}</Text>
                  <Text style={estilos.textoCorpo}>{h.descricao}</Text>
                </View>
              </View>
            ))}
          </View>
        </>
      )}
    </ScrollView>
  );
}
