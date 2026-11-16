"""Mascara segredos e dados pessoais antes de guardar um trecho de código no relatório ou de mandá-lo
para a IA. O relatório vai para o Git e o LLM é um serviço externo: nenhum dos dois recebe o valor.
"""
from __future__ import annotations

import re

_PADROES: list[tuple[re.Pattern[str], str]] = [
    # "Chave": "valor" e parecidos em JSON
    (re.compile(r'("(?i:[\w-]*(?:chave|senha|password|secret|segredo|token|api_?key)[\w-]*)"\s*:\s*")[^"]+(")'), r"\1***\2"),
    # Password=...; em connection string
    (re.compile(r"(?i)\b((?:password|pwd)=)[^;\"'\s]+"), r"\1***"),
    # ${VAR:-valor padrão}
    (re.compile(r"(\$\{\w+:-)[^}]+(\})"), r"\1***\2"),
    # nome_de_segredo = "valor" (C#, TS, shell)
    (re.compile(r"(?i)\b(\w*(?:senha|password|secret|segredo|apikey|api_key|token|chave)\w*\s*[:=]\s*[@$]?[\"'])[^\"'\n]{4,}([\"'])"), r"\1***\2"),
    # formatos conhecidos de chave e token
    (re.compile(r"\b(?:sk-ant-|sk-|ghp_|AKIA|AIza|xox[abprs]-)[A-Za-z0-9_\-]{8,}"), "***chave***"),
    (re.compile(r"\beyJ[\w-]{8,}\.[\w-]{8,}\.[\w-]{8,}"), "***token***"),
    (re.compile(r"\$2[aby]?\$\d{2}\$[./A-Za-z0-9]{53}"), "***hash***"),
    # dados pessoais comuns
    (re.compile(r"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b"), "***CPF***"),
    (re.compile(r"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}"), "***e-mail***"),
    (re.compile(r"\(?\b[1-9]{2}\)?\s?9?\d{4}-?\d{4}\b"), "***telefone***"),
]


def redigir(texto: str) -> str:
    for padrao, troca in _PADROES:
        texto = padrao.sub(troca, texto)
    return texto
