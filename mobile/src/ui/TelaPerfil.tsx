import { useState } from 'react';
import { Alert, ScrollView, Text } from 'react-native';

import { mensagem } from '@/api/cliente';
import { api } from '@/api/hakari';
import { useSessao } from '@/sessao/sessao';
import { Aviso, Botao, Carregando, Cartao, Linha, Titulo, estilos } from './componentes';
import { dataHora } from './textos';
import { useDados } from './useDados';

/** Perfil compartilhado pelo cidadão e pelo técnico. Só o cidadão pode excluir a conta (LGPD). */
export function TelaPerfil({ podeExcluir }: { podeExcluir: boolean }) {
  const { sair } = useSessao();
  const { dados, erro } = useDados(api.meusDados);
  const [excluindo, setExcluindo] = useState(false);
  const [erroExclusao, setErroExclusao] = useState<string | null>(null);

  function confirmarExclusao() {
    Alert.alert(
      'Excluir minha conta',
      'Seus dados pessoais serão apagados. As ocorrências que você abriu continuam, sem o seu nome. Não dá para desfazer.',
      [
        { text: 'Cancelar', style: 'cancel' },
        { text: 'Excluir', style: 'destructive', onPress: excluir },
      ],
    );
  }

  async function excluir() {
    setExcluindo(true);
    setErroExclusao(null);
    try {
      await api.excluirConta();
      sair();
    } catch (e) {
      setErroExclusao(mensagem(e));
      setExcluindo(false);
    }
  }

  if (!dados && !erro) return <Carregando />;

  return (
    <ScrollView contentContainerStyle={estilos.tela}>
      {erro && <Aviso tipo="erro" texto={erro} />}
      {dados && (
        <Cartao>
          <Titulo>{dados.nome}</Titulo>
          <Text style={estilos.textoSecundario}>
            {dados.perfil === 'Tecnico' ? 'Equipe de manutenção' : 'Cidadão'}
          </Text>
          <Linha rotulo="E-mail" valor={dados.email} />
          <Linha rotulo="Telefone" valor={dados.telefone} />
          <Linha rotulo="Conta criada em" valor={dataHora(dados.criadoEm)} />
          {dados.termoAceitoEm && <Linha rotulo="Termo de uso aceito em" valor={dataHora(dados.termoAceitoEm)} />}
        </Cartao>
      )}

      <Botao texto="Sair" variante="secundario" icone="log-out-outline" aoTocar={sair} />

      {podeExcluir && (
        <>
          <Text style={estilos.textoSecundario}>
            Você pode apagar seus dados a qualquer momento. Os reportes continuam ajudando a cidade, sem identificar você.
          </Text>
          {erroExclusao && <Aviso tipo="erro" texto={erroExclusao} />}
          <Botao
            texto="Excluir minha conta"
            variante="perigo"
            icone="trash-outline"
            carregando={excluindo}
            aoTocar={confirmarExclusao}
            dica="Pede confirmação antes de apagar"
          />
        </>
      )}
    </ScrollView>
  );
}
