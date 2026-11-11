import { router, useFocusEffect } from 'expo-router';
import { useCallback, useMemo, useState } from 'react';
import { FlatList, RefreshControl, Text, View } from 'react-native';

import { mensagem } from '@/api/cliente';
import { api } from '@/api/hakari';
import type { PosteResumo } from '@/api/tipos';
import { lerPostesCache, salvarPostesCache } from '@/dados/banco';
import { useSessao } from '@/sessao/sessao';
import { Aviso, Campo, Carregando, Cartao, Selo, Vazio, estilos } from '@/ui/componentes';
import { cores } from '@/ui/tema';
import { dataHora, haQuantoTempo, normalizar, visualDoPoste } from '@/ui/textos';

// Lista de postes com o status de cada um. Se a internet cair, mostra a última lista salva no celular.

export default function Postes() {
  const { sessao } = useSessao();
  const [postes, setPostes] = useState<PosteResumo[] | null>(null);
  const [busca, setBusca] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [salvoEm, setSalvoEm] = useState<string | null>(null);
  const [atualizando, setAtualizando] = useState(false);

  const carregar = useCallback(async () => {
    setAtualizando(true);
    try {
      const lista = await api.postes();
      setPostes(lista);
      salvarPostesCache(lista);
      setSalvoEm(null);
      setErro(null);
    } catch (e) {
      const cache = lerPostesCache();
      if (cache.postes.length > 0) {
        setPostes(cache.postes);
        setSalvoEm(cache.salvoEm);
      } else {
        setErro(mensagem(e));
      }
    } finally {
      setAtualizando(false);
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      carregar();
    }, [carregar]),
  );

  const filtrados = useMemo(() => {
    const termo = normalizar(busca.trim());
    if (!postes || !termo) return postes ?? [];
    return postes.filter((p) => normalizar(`${p.bairro} ${p.endereco} ${p.codigo}`).includes(termo));
  }, [postes, busca]);

  const apagados = postes?.filter((p) => p.status !== 'Funcionando' && p.status !== 'Aguardando instalação').length ?? 0;
  const primeiroNome = sessao?.usuario.nome.split(' ')[0];

  if (!postes && !erro) return <Carregando texto="Buscando os postes…" />;

  return (
    <FlatList
      data={filtrados}
      keyExtractor={(p) => String(p.id)}
      contentContainerStyle={estilos.tela}
      refreshControl={<RefreshControl refreshing={atualizando} onRefresh={carregar} colors={[cores.primaria]} />}
      keyboardShouldPersistTaps="handled"
      ListHeaderComponent={
        <View style={{ gap: 12 }}>
          <Text style={estilos.titulo} accessibilityRole="header">
            Olá, {primeiroNome}
          </Text>
          {postes && (
            <Text style={estilos.textoCorpo}>
              {apagados === 0
                ? 'Todos os postes monitorados estão funcionando.'
                : `${apagados} ${apagados === 1 ? 'poste precisa' : 'postes precisam'} de atenção.`}
            </Text>
          )}
          {salvoEm && (
            <Aviso tipo="info" texto={`Sem conexão. Mostrando a lista salva em ${dataHora(salvoEm)}.`} />
          )}
          {erro && <Aviso tipo="erro" texto={erro} />}
          <Campo
            rotulo="Procurar por bairro ou rua"
            value={busca}
            onChangeText={setBusca}
            placeholder="Ex.: Bela Vista"
            returnKeyType="search"
          />
        </View>
      }
      ListEmptyComponent={postes ? <Vazio icone="search" texto="Nenhum poste encontrado com essa busca." /> : undefined}
      renderItem={({ item }) => <CartaoPoste poste={item} />}
    />
  );
}

function CartaoPoste({ poste }: { poste: PosteResumo }) {
  const visual = visualDoPoste(poste.status);
  return (
    <Cartao
      aoTocar={() => router.push(`/poste/${poste.id}`)}
      rotulo={`Poste ${poste.codigo}, ${poste.endereco}, ${poste.bairro}. Situação: ${poste.status}.`}
      dica="Abre os detalhes do poste">
      <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
        <Text style={estilos.textoForte}>{poste.endereco}</Text>
      </View>
      <Text style={estilos.textoSecundario}>
        {poste.bairro} · {poste.codigo}
      </Text>
      <Selo visual={visual} texto={poste.status} />
      {poste.ultimaComunicacao && (
        <Text style={estilos.textoSecundario}>Último sinal {haQuantoTempo(poste.ultimaComunicacao)}</Text>
      )}
    </Cartao>
  );
}
