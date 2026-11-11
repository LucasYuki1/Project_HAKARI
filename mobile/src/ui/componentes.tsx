import Ionicons from '@expo/vector-icons/Ionicons';
import type { ReactNode } from 'react';
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps,
  type ViewStyle,
} from 'react-native';

import { alvoMinimo, cores, espaco, fonte } from './tema';
import type { Visual } from './textos';

// Componentes básicos reaproveitados por todas as telas. Cada um já vem com os rótulos de
// acessibilidade, para o leitor de tela (TalkBack/VoiceOver) anunciar o que é e o que faz.

type VarianteBotao = 'primario' | 'secundario' | 'perigo';

export function Botao({
  texto,
  aoTocar,
  variante = 'primario',
  carregando = false,
  desabilitado = false,
  icone,
  dica,
}: {
  texto: string;
  aoTocar: () => void;
  variante?: VarianteBotao;
  carregando?: boolean;
  desabilitado?: boolean;
  icone?: Visual['icone'];
  dica?: string;
}) {
  const inativo = desabilitado || carregando;
  const corTexto = variante === 'secundario' ? cores.primaria : cores.branco;
  return (
    <Pressable
      onPress={aoTocar}
      disabled={inativo}
      accessibilityRole="button"
      accessibilityLabel={texto}
      accessibilityHint={dica}
      accessibilityState={{ disabled: inativo, busy: carregando }}
      style={({ pressed }) => [
        estilos.botao,
        variante === 'primario' && { backgroundColor: cores.primaria },
        variante === 'perigo' && { backgroundColor: cores.perigo },
        variante === 'secundario' && estilos.botaoSecundario,
        inativo && { opacity: 0.55 },
        pressed && { opacity: 0.8 },
      ]}>
      {carregando ? (
        <ActivityIndicator color={corTexto} />
      ) : (
        <>
          {icone && <Ionicons name={icone} size={22} color={corTexto} />}
          <Text style={[estilos.textoBotao, { color: corTexto }]}>{texto}</Text>
        </>
      )}
    </Pressable>
  );
}

export function Campo({
  rotulo,
  ajuda,
  erro,
  ...props
}: TextInputProps & { rotulo: string; ajuda?: string; erro?: string }) {
  return (
    <View style={{ gap: 6 }}>
      <Text style={estilos.rotulo}>{rotulo}</Text>
      <TextInput
        accessibilityLabel={rotulo}
        accessibilityHint={ajuda}
        placeholderTextColor={cores.textoSecundario}
        style={[estilos.campo, props.multiline && estilos.campoMultilinha, erro ? { borderColor: cores.perigo } : null]}
        {...props}
      />
      {ajuda && !erro && <Text style={estilos.ajuda}>{ajuda}</Text>}
      {erro && (
        <Text style={estilos.erroCampo} accessibilityLiveRegion="polite">
          {erro}
        </Text>
      )}
    </View>
  );
}

/** Caixa de mensagem. O leitor de tela anuncia o texto assim que ele aparece. */
export function Aviso({ tipo, texto }: { tipo: 'erro' | 'sucesso' | 'info'; texto: string }) {
  const visual =
    tipo === 'erro'
      ? { cor: cores.perigo, fundo: cores.perigoSuave, icone: 'alert-circle' as const }
      : tipo === 'sucesso'
        ? { cor: cores.sucesso, fundo: cores.sucessoSuave, icone: 'checkmark-circle' as const }
        : { cor: cores.primaria, fundo: cores.primariaSuave, icone: 'information-circle' as const };
  return (
    <View
      style={[estilos.aviso, { backgroundColor: visual.fundo, borderColor: visual.cor }]}
      accessibilityRole={tipo === 'erro' ? 'alert' : 'text'}
      accessibilityLiveRegion="polite">
      <Ionicons name={visual.icone} size={24} color={visual.cor} />
      <Text style={[estilos.textoAviso, { color: visual.cor }]}>{texto}</Text>
    </View>
  );
}

/** Etiqueta de status: ícone + texto + cor. */
export function Selo({ visual, texto, grande = false }: { visual: Visual; texto: string; grande?: boolean }) {
  return (
    <View
      style={[estilos.selo, { backgroundColor: visual.fundo, borderColor: visual.cor }, grande && estilos.seloGrande]}
      accessible
      accessibilityLabel={texto}>
      <Ionicons name={visual.icone} size={grande ? 22 : 18} color={visual.cor} />
      <Text style={[estilos.textoSelo, { color: visual.cor }, grande && { fontSize: fonte.destaque }]}>{texto}</Text>
    </View>
  );
}

export function Cartao({
  children,
  aoTocar,
  rotulo,
  dica,
  estilo,
}: {
  children: ReactNode;
  aoTocar?: () => void;
  rotulo?: string;
  dica?: string;
  estilo?: ViewStyle;
}) {
  if (!aoTocar) return <View style={[estilos.cartao, estilo]}>{children}</View>;
  return (
    <Pressable
      onPress={aoTocar}
      accessibilityRole="button"
      accessibilityLabel={rotulo}
      accessibilityHint={dica}
      style={({ pressed }) => [estilos.cartao, estilo, pressed && { backgroundColor: cores.primariaSuave }]}>
      {children}
    </Pressable>
  );
}

export function Linha({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <View style={estilos.linha} accessible accessibilityLabel={`${rotulo}: ${valor}`}>
      <Text style={estilos.linhaRotulo}>{rotulo}</Text>
      <Text style={estilos.linhaValor}>{valor}</Text>
    </View>
  );
}

export function Titulo({ children }: { children: ReactNode }) {
  return (
    <Text style={estilos.titulo} accessibilityRole="header">
      {children}
    </Text>
  );
}

export function Carregando({ texto = 'Carregando…' }: { texto?: string }) {
  return (
    <View style={estilos.centro} accessibilityLiveRegion="polite" accessibilityLabel={texto}>
      <ActivityIndicator size="large" color={cores.primaria} />
      <Text style={estilos.ajuda}>{texto}</Text>
    </View>
  );
}

export function Vazio({ icone, texto }: { icone: Visual['icone']; texto: string }) {
  return (
    <View style={estilos.centro} accessible accessibilityLabel={texto}>
      <Ionicons name={icone} size={48} color={cores.textoSecundario} />
      <Text style={[estilos.ajuda, { textAlign: 'center' }]}>{texto}</Text>
    </View>
  );
}

export const estilos = StyleSheet.create({
  tela: { padding: espaco.m, gap: espaco.m, paddingBottom: espaco.g * 2 },
  botao: {
    minHeight: alvoMinimo,
    borderRadius: 12,
    paddingHorizontal: espaco.m,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: espaco.p,
  },
  botaoSecundario: { backgroundColor: cores.superficie, borderWidth: 2, borderColor: cores.primaria },
  textoBotao: { fontSize: fonte.destaque, fontWeight: '700' },
  rotulo: { fontSize: fonte.corpo, fontWeight: '700', color: cores.texto },
  campo: {
    minHeight: alvoMinimo,
    borderWidth: 1.5,
    borderColor: cores.borda,
    borderRadius: 10,
    backgroundColor: cores.superficie,
    paddingHorizontal: 14,
    fontSize: fonte.corpo,
    color: cores.texto,
  },
  campoMultilinha: { minHeight: 120, paddingTop: 12, textAlignVertical: 'top' },
  ajuda: { fontSize: fonte.pequena, color: cores.textoSecundario },
  erroCampo: { fontSize: fonte.pequena, color: cores.perigo, fontWeight: '600' },
  aviso: {
    flexDirection: 'row',
    gap: 10,
    alignItems: 'flex-start',
    padding: 14,
    borderRadius: 12,
    borderWidth: 1.5,
  },
  textoAviso: { flex: 1, fontSize: fonte.corpo, fontWeight: '600' },
  selo: {
    flexDirection: 'row',
    alignItems: 'center',
    alignSelf: 'flex-start',
    gap: 6,
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 999,
    borderWidth: 1.5,
  },
  seloGrande: { paddingHorizontal: 14, paddingVertical: 8 },
  textoSelo: { fontSize: fonte.pequena, fontWeight: '700' },
  cartao: {
    backgroundColor: cores.superficie,
    borderRadius: 14,
    borderWidth: 1,
    borderColor: cores.borda,
    padding: espaco.m,
    gap: 8,
  },
  linha: { gap: 2 },
  linhaRotulo: { fontSize: fonte.pequena, color: cores.textoSecundario },
  linhaValor: { fontSize: fonte.corpo, color: cores.texto, fontWeight: '600' },
  titulo: { fontSize: fonte.titulo, fontWeight: '800', color: cores.texto },
  centro: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 12, padding: espaco.g, minHeight: 240 },
  textoCorpo: { fontSize: fonte.corpo, color: cores.texto, lineHeight: 24 },
  textoForte: { fontSize: fonte.destaque, fontWeight: '700', color: cores.texto },
  textoSecundario: { fontSize: fonte.pequena, color: cores.textoSecundario },
});
