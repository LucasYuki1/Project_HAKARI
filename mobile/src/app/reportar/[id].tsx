import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Alert, KeyboardAvoidingView, Platform, Pressable, ScrollView, Text, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';

import { ErroApi, mensagem } from '@/api/cliente';
import { api } from '@/api/hakari';
import type { Categoria } from '@/api/tipos';
import { apagarRascunho, lerRascunho, salvarRascunho } from '@/dados/banco';
import { Aviso, Botao, Campo, estilos } from '@/ui/componentes';
import { alvoMinimo, cores, fonte } from '@/ui/tema';
import { categorias } from '@/ui/textos';

// Formulário de reporte. O texto é salvo no celular enquanto o cidadão digita: se a internet cair
// ou o app fechar, o rascunho continua lá.

export default function Reportar() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const posteId = Number(id);
  const [rascunhoInicial] = useState(() => lerRascunho(posteId));

  const [categoria, setCategoria] = useState<Categoria | null>((rascunhoInicial?.categoria as Categoria) ?? null);
  const [descricao, setDescricao] = useState(rascunhoInicial?.descricao ?? '');
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  function atualizar(novaCategoria: Categoria | null, novaDescricao: string) {
    setCategoria(novaCategoria);
    setDescricao(novaDescricao);
    salvarRascunho(posteId, { categoria: novaCategoria, descricao: novaDescricao });
  }

  async function enviar() {
    if (!categoria) {
      setErro('Escolha o tipo do problema.');
      return;
    }
    if (!descricao.trim()) {
      setErro('Descreva o problema em poucas palavras.');
      return;
    }
    setEnviando(true);
    setErro(null);
    try {
      const r = await api.reportar(posteId, categoria, descricao.trim());
      apagarRascunho(posteId);
      Alert.alert(r.apoioAdicionado ? 'Apoio registrado' : 'Reporte enviado', r.mensagem);
      router.replace(`/ocorrencia/${r.ocorrenciaId}`);
    } catch (e) {
      setErro(
        e instanceof ErroApi && e.semConexao
          ? 'Sem conexão. Seu texto ficou salvo neste celular; tente enviar quando a internet voltar.'
          : mensagem(e),
      );
    } finally {
      setEnviando(false);
    }
  }

  return (
    <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={estilos.tela} keyboardShouldPersistTaps="handled">
        {rascunhoInicial && <Aviso tipo="info" texto="Recuperamos o rascunho que você tinha começado." />}

        <Text style={estilos.rotulo} accessibilityRole="header" nativeID="tipo">
          Qual é o problema?
        </Text>
        <View style={{ gap: 10 }} accessibilityRole="radiogroup" accessibilityLabelledBy="tipo">
          {categorias.map((c) => {
            const marcado = categoria === c.valor;
            return (
              <Pressable
                key={c.valor}
                onPress={() => atualizar(c.valor, descricao)}
                accessibilityRole="radio"
                accessibilityState={{ checked: marcado }}
                accessibilityLabel={c.texto}
                style={{
                  minHeight: alvoMinimo,
                  flexDirection: 'row',
                  alignItems: 'center',
                  gap: 12,
                  paddingHorizontal: 14,
                  borderRadius: 12,
                  borderWidth: 2,
                  borderColor: marcado ? cores.primaria : cores.borda,
                  backgroundColor: marcado ? cores.primariaSuave : cores.superficie,
                }}>
                <Ionicons
                  name={marcado ? 'radio-button-on' : 'radio-button-off'}
                  size={24}
                  color={marcado ? cores.primaria : cores.textoSecundario}
                />
                <Text style={{ fontSize: fonte.corpo, color: cores.texto, fontWeight: marcado ? '700' : '400' }}>
                  {c.texto}
                </Text>
              </Pressable>
            );
          })}
        </View>

        <Campo
          rotulo="Conte o que você viu"
          value={descricao}
          onChangeText={(t) => atualizar(categoria, t)}
          multiline
          maxLength={500}
          placeholder="Ex.: a lâmpada está apagada há duas noites"
          ajuda="Não coloque dados pessoais seus ou de outras pessoas."
        />

        {erro && <Aviso tipo="erro" texto={erro} />}

        <Botao texto="Enviar reporte" icone="send" aoTocar={enviar} carregando={enviando} />
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
