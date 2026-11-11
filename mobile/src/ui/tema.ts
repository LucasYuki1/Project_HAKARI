// Cores e tamanhos do app. Todos os pares texto/fundo passam do contraste mínimo do WCAG AA (4,5:1).

export const cores = {
  primaria: '#1F3A5F',
  primariaSuave: '#E3EAF3',
  fundo: '#F4F6F9',
  superficie: '#FFFFFF',
  texto: '#14181F',
  textoSecundario: '#4A5361',
  borda: '#C9D1DC',
  sucesso: '#1E6B30',
  sucessoSuave: '#E3F2E6',
  atencao: '#8A4B00',
  atencaoSuave: '#FFF1DC',
  perigo: '#A8201A',
  perigoSuave: '#FCE6E4',
  neutro: '#4A5361',
  neutroSuave: '#ECEEF2',
  branco: '#FFFFFF',
} as const;

export const fonte = {
  pequena: 15,
  corpo: 17,
  destaque: 19,
  titulo: 24,
} as const;

export const espaco = {
  p: 8,
  m: 16,
  g: 24,
} as const;

/** Área mínima de toque recomendada (48 dp no Android). */
export const alvoMinimo = 52;
