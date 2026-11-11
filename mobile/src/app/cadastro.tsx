import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, Switch, Text, View } from 'react-native';

import { mensagem } from '@/api/cliente';
import { api } from '@/api/hakari';
import { useSessao } from '@/sessao/sessao';
import { Aviso, Botao, Campo, estilos } from '@/ui/componentes';
import { cores } from '@/ui/tema';

export default function Cadastro() {
  const { entrar } = useSessao();
  const [nome, setNome] = useState('');
  const [email, setEmail] = useState('');
  const [telefone, setTelefone] = useState('');
  const [senha, setSenha] = useState('');
  const [aceitou, setAceitou] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function aoCadastrar() {
    if (!nome.trim() || !email.trim() || !telefone.trim() || !senha) {
      setErro('Preencha todos os campos.');
      return;
    }
    if (!aceitou) {
      setErro('Para criar a conta, aceite o uso dos seus dados.');
      return;
    }
    setEnviando(true);
    setErro(null);
    try {
      entrar(await api.cadastrar({ nome: nome.trim(), email: email.trim(), telefone, senha, aceitouTermo: aceitou }));
    } catch (e) {
      setErro(mensagem(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={estilos.tela} keyboardShouldPersistTaps="handled">
        <Text style={estilos.textoCorpo}>
          Com a conta você reporta postes apagados e acompanha o conserto até o fim.
        </Text>

        <Campo rotulo="Nome completo" value={nome} onChangeText={setNome} autoComplete="name" textContentType="name" />
        <Campo
          rotulo="E-mail"
          value={email}
          onChangeText={setEmail}
          keyboardType="email-address"
          autoCapitalize="none"
          autoComplete="email"
          textContentType="emailAddress"
        />
        <Campo
          rotulo="Telefone com DDD"
          value={telefone}
          onChangeText={setTelefone}
          keyboardType="phone-pad"
          autoComplete="tel"
          textContentType="telephoneNumber"
          placeholder="11 98765-4321"
        />
        <Campo
          rotulo="Senha"
          value={senha}
          onChangeText={setSenha}
          secureTextEntry
          autoComplete="new-password"
          textContentType="newPassword"
          ajuda="Pelo menos 8 caracteres, com letras e números."
        />

        <View
          style={{ flexDirection: 'row', gap: 12, alignItems: 'center', backgroundColor: cores.superficie,
            borderRadius: 12, borderWidth: 1, borderColor: cores.borda, padding: 14 }}>
          <Text style={[estilos.textoCorpo, { flex: 1 }]} nativeID="termo">
            Concordo que o Hakari guarde meu nome, e-mail e telefone só para acompanhar meus reportes. Posso
            excluir minha conta quando quiser.
          </Text>
          <Switch
            value={aceitou}
            onValueChange={setAceitou}
            accessibilityLabel="Aceito o uso dos meus dados"
            accessibilityLabelledBy="termo"
            trackColor={{ true: cores.primaria, false: cores.borda }}
            thumbColor={cores.branco}
          />
        </View>

        {erro && <Aviso tipo="erro" texto={erro} />}

        <Botao texto="Criar conta" aoTocar={aoCadastrar} carregando={enviando} />
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
