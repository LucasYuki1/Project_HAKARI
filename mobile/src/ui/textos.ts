import type Ionicons from '@expo/vector-icons/Ionicons';
import type { ComponentProps } from 'react';

import type { Categoria, Severidade } from '@/api/tipos';
import { cores } from './tema';

// Traduz os valores da API em texto, ícone e cor. A cor nunca aparece sozinha: sempre vem com
// texto e ícone, para quem não distingue cores.

type NomeIcone = ComponentProps<typeof Ionicons>['name'];
export type Visual = { icone: NomeIcone; cor: string; fundo: string };

export function visualDoPoste(status: string): Visual {
  switch (status) {
    case 'Funcionando':
      return { icone: 'bulb', cor: cores.sucesso, fundo: cores.sucessoSuave };
    case 'Apagado':
      return { icone: 'warning', cor: cores.perigo, fundo: cores.perigoSuave };
    case 'Sem comunicação':
      return { icone: 'cloud-offline', cor: cores.atencao, fundo: cores.atencaoSuave };
    default:
      return { icone: 'construct', cor: cores.neutro, fundo: cores.neutroSuave };
  }
}

export function visualDaOcorrencia(status: string): Visual {
  switch (status) {
    case 'Aberta':
      return { icone: 'alert-circle', cor: cores.perigo, fundo: cores.perigoSuave };
    case 'Em atendimento':
      return { icone: 'build', cor: cores.atencao, fundo: cores.atencaoSuave };
    default:
      return { icone: 'checkmark-circle', cor: cores.sucesso, fundo: cores.sucessoSuave };
  }
}

export function visualDaSeveridade(s: Severidade): Visual {
  switch (s) {
    case 'Desastre':
      return { icone: 'flame', cor: cores.perigo, fundo: cores.perigoSuave };
    case 'Alta':
      return { icone: 'warning', cor: cores.atencao, fundo: cores.atencaoSuave };
    default:
      return { icone: 'information-circle', cor: cores.neutro, fundo: cores.neutroSuave };
  }
}

export const categorias: { valor: Categoria; texto: string }[] = [
  { valor: 'LampadaApagada', texto: 'Lâmpada apagada' },
  { valor: 'LampadaPiscando', texto: 'Lâmpada piscando' },
  { valor: 'PosteDanificado', texto: 'Poste danificado' },
  { valor: 'FiacaoExposta', texto: 'Fiação exposta' },
  { valor: 'SemComunicacao', texto: 'Sem comunicação' },
  { valor: 'Outro', texto: 'Outro problema' },
];

export function textoCategoria(c: Categoria) {
  return categorias.find((x) => x.valor === c)?.texto ?? c;
}

export const textoAlerta: Record<string, string> = {
  LampadaSemResposta: 'A lâmpada recebeu o comando, mas não acendeu',
  SinalFraco: 'Sinal de internet fraco no poste',
  NoIsolado: 'O poste parou de se comunicar',
};

const doisDigitos = (n: number) => String(n).padStart(2, '0');

/** 10/11/2026 às 14:05, no horário do celular. */
export function dataHora(iso: string | null | undefined) {
  if (!iso) return '—';
  const d = new Date(iso);
  return `${doisDigitos(d.getDate())}/${doisDigitos(d.getMonth() + 1)}/${d.getFullYear()} às ${doisDigitos(d.getHours())}:${doisDigitos(d.getMinutes())}`;
}

/** "há 5 minutos", "há 2 horas", ou a data quando passa de um dia. */
export function haQuantoTempo(iso: string | null | undefined) {
  if (!iso) return 'nunca';
  const minutos = Math.floor((Date.now() - new Date(iso).getTime()) / 60_000);
  if (minutos < 1) return 'agora há pouco';
  if (minutos < 60) return `há ${minutos} ${minutos === 1 ? 'minuto' : 'minutos'}`;
  const horas = Math.floor(minutos / 60);
  if (horas < 24) return `há ${horas} ${horas === 1 ? 'hora' : 'horas'}`;
  return `em ${dataHora(iso)}`;
}

/** Busca sem diferenciar maiúsculas nem acentos ("sao joao" encontra "São João"). */
export function normalizar(texto: string) {
  return texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
}
