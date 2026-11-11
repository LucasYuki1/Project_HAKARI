import Ionicons from '@expo/vector-icons/Ionicons';
import { router } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { definirServidor, mensagem, servidorAtual } from '@/api/cliente';
import { api } from '@/api/hakari';
import { useSessao } from '@/sessao/sessao';
import { Aviso, Botao, Campo, estilos } from '@/ui/componentes';
import { cores, fonte } from '@/ui/tema';

export default function Entrar() {
  const { entrar } = useSessao();
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const [servidor, setServidor] = useState(servidorAtual);
  const [editandoServidor, setEditandoServidor] = useState(false);

  async function aoEntrar() {
    if (!email.trim() || !senha) {
      setErro('Preencha o e-mail e a senha.');
      return;
    }
    setEnviando(true);
    setErro(null);
    try {
      entrar(await api.entrar(email.trim(), senha));
    } catch (e) {
      setErro(mensagem(e));
    } finally {
      setEnviando(false);
    }
  }

  function salvarServidor() {
    definirServidor(servidor);
    setServidor(servidorAtual());
    setEditandoServidor(false);
  }

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: cores.fundo }}>
      <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <ScrollView contentContainerStyle={[estilos.tela, { flexGrow: 1, justifyContent: 'center' }]}
          keyboardShouldPersistTaps="handled">
          <View style={{ alignItems: 'center', gap: 8, marginBottom: 8 }}>
            <View
              style={{ backgroundColor: cores.primaria, borderRadius: 999, padding: 18 }}
              importantForAccessibility="no-hide-descendants">
              <Ionicons name="bulb" size={44} color="#FFD54A" />
            </View>
            <Text style={[estilos.titulo, { fontSize: 32 }]} accessibilityRole="header">
              Hakari
            </Text>
            <Text style={[estilos.textoCorpo, { textAlign: 'center', color: cores.textoSecundario }]}>
              A iluminação da sua rua, acompanhada de perto.
            </Text>
          </View>

          <Campo
            rotulo="E-mail"
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
            autoComplete="email"
            textContentType="emailAddress"
            placeholder="seu@email.com"
          />
          <Campo
            rotulo="Senha"
            value={senha}
            onChangeText={setSenha}
            secureTextEntry
            autoComplete="current-password"
            textContentType="password"
            onSubmitEditing={aoEntrar}
          />

          {erro && <Aviso tipo="erro" texto={erro} />}

          <Botao texto="Entrar" aoTocar={aoEntrar} carregando={enviando} />

          <Botao texto="Criar minha conta" variante="secundario" aoTocar={() => router.push('/cadastro')} />

          <View style={{ marginTop: 16, gap: 8 }}>
            {editandoServidor ? (
              <>
                <Campo
                  rotulo="Endereço do servidor"
                  value={servidor}
                  onChangeText={setServidor}
                  autoCapitalize="none"
                  keyboardType="url"
                  ajuda="Exemplo: http://192.168.0.10:5080"
                />
                <Botao texto="Salvar endereço" variante="secundario" aoTocar={salvarServidor} />
              </>
            ) : (
              <Text
                style={{ fontSize: fonte.pequena, color: cores.textoSecundario, textAlign: 'center' }}
                accessibilityRole="button"
                accessibilityHint="Toque para alterar o endereço do servidor"
                onPress={() => setEditandoServidor(true)}>
                Servidor: {servidor} · <Text style={{ fontWeight: '700', color: cores.primaria }}>Alterar</Text>
              </Text>
            )}
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
