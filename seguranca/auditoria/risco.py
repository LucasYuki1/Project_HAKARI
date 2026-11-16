"""Indicador Risco Cidadão: de 0 a 100, a chance de um dado pessoal do cidadão parar onde não devia.

Cada achado aberto vira uma probabilidade p = ALFA x sensibilidade do dado x exposição da saída x
gravidade. O risco é a chance de pelo menos um deles acontecer: 100 x (1 - produto de (1 - p)).
Assim um achado grave pesa muito, vários achados somam sem passar de 100, e corrigir qualquer um
derruba o número. Risco aceito no registro continua contando, com 25% do peso (risco residual).
Vazamentos confirmados pelo teste de canários entram como achados certos (gravidade 1).
"""
from __future__ import annotations

import math

ALFA = 0.3
RESIDUAL_ACEITO = 0.25

SENSIBILIDADE = {
    "saude": 1.0, "credencial": 0.9, "financeiro": 0.9, "identificacao": 0.8, "texto-livre": 0.8,
    "dado-pessoal": 0.7, "localizacao": 0.7, "contato": 0.6, "infraestrutura": 0.5,
}
EXPOSICAO = {
    "outros-usuarios": 1.0, "repositorio": 1.0, "rede": 0.8, "token": 0.7, "logs": 0.6,
    "respostas-de-erro": 0.6, "armazenamento-do-aparelho": 0.6, "banco-de-dados": 0.5,
}
GRAVIDADE = {"ERROR": 1.0, "WARNING": 0.7, "INFO": 0.0}
CATEGORIA_DO_CANARIO = {
    "CPF": "identificacao", "telefone": "contato", "e-mail": "contato",
    "endereço residencial": "localizacao", "dado de saúde": "saude", "cartão do SUS": "saude",
}
FAIXAS = [(70, "Crítico"), (40, "Alto"), (20, "Moderado"), (0, "Baixo")]


def faixa(valor: float) -> str:
    return next(nome for limite, nome in FAIXAS if valor >= limite)


def probabilidade(achado: dict) -> float:
    p = (ALFA * SENSIBILIDADE.get(achado["categoria"], 0.6) * EXPOSICAO.get(achado["saida"], 0.5)
         * GRAVIDADE.get(achado["severidade"], 0.5))
    return p * (RESIDUAL_ACEITO if achado.get("status") == "aceito" else 1.0)


def calcular(riscos: list[dict], vazamentos_dinamicos: list[dict]) -> dict:
    termos: list[tuple[str, float]] = [(a["categoria"], probabilidade(a)) for a in riscos]
    for dado in sorted({v["dado"] for v in vazamentos_dinamicos}):
        categoria = CATEGORIA_DO_CANARIO.get(dado, "dado-pessoal")
        termos.append((categoria, ALFA * SENSIBILIDADE[categoria] * EXPOSICAO["outros-usuarios"]))

    termos = [(c, min(p, 0.95)) for c, p in termos if p > 0]
    sobra = math.prod(1 - p for _, p in termos)
    valor = round(100 * (1 - sobra), 1)

    # Quanto cada categoria explica do risco (decomposição em log, soma igual ao total).
    pesos: dict[str, float] = {}
    for categoria, p in termos:
        pesos[categoria] = pesos.get(categoria, 0.0) - math.log(1 - p)
    total = sum(pesos.values()) or 1.0
    por_categoria = {c: round(valor * w / total, 1) for c, w in sorted(pesos.items(), key=lambda x: -x[1])}

    return {
        "valor": valor,
        "faixa": faixa(valor),
        "por_categoria": por_categoria,
        "achados_considerados": len(termos),
        "formula": "100 × (1 − Π(1 − p)), p = 0,3 × sensibilidade × exposição × gravidade",
    }
