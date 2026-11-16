"""Caminhos e constantes da auditoria."""
from __future__ import annotations

import os
import shutil
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[2]
SEGURANCA = RAIZ / "seguranca"
PASTA_REGRAS = SEGURANCA / "regras"
REGRAS_SEMGREP = [
    PASTA_REGRAS / "pii-mapeamento.yaml",
    PASTA_REGRAS / "pii-taint.yaml",
    PASTA_REGRAS / "segredos.yaml",
]
REGISTRO_DE_RISCOS = PASTA_REGRAS / "riscos-aceitos.toml"

RESULTADOS = SEGURANCA / "resultados"
HISTORICO = RESULTADOS / "historico"
ULTIMO = RESULTADOS / "ultimo.json"
CACHE_IA = RESULTADOS / "ia-cache.json"
ARQUIVO_ENV = SEGURANCA / ".env"

VENV = SEGURANCA / ".venv"
_BIN = VENV / ("Scripts" if os.name == "nt" else "bin")


def executavel_semgrep() -> str:
    local = _BIN / ("semgrep.exe" if os.name == "nt" else "semgrep")
    if local.exists():
        return str(local)
    no_path = shutil.which("semgrep")
    if no_path:
        return no_path
    raise SystemExit("Semgrep não encontrado. Rode: python -m venv seguranca/.venv && "
                     "seguranca/.venv/Scripts/pip install -r seguranca/requirements.txt")


def python_do_venv() -> str:
    local = _BIN / ("python.exe" if os.name == "nt" else "python")
    return str(local) if local.exists() else shutil.which("python") or "python"


# IA: modelo, versão do prompt (faz parte da chave do cache) e preço por milhão de tokens (US$).
MODELO_IA = "claude-opus-5-5"
VERSAO_PROMPT = "2026-11-14.1"
PRECO_ENTRADA_MTOK = 4.00
PRECO_SAIDA_MTOK = 20.00

# Nomes legíveis para o painel.
ROTULO_CATEGORIA = {
    "identificacao": "Identificação",
    "contato": "Contato",
    "credencial": "Credencial",
    "saude": "Saúde (sensível)",
    "localizacao": "Localização pessoal",
    "texto-livre": "Texto livre",
    "consentimento": "Consentimento",
    "dado-pessoal": "Dado pessoal",
    "financeiro": "Financeiro",
    "infraestrutura": "Infraestrutura",
    "app": "App",
    "armazenamento-local": "Guardado no celular",
}
ROTULO_SAIDA = {
    "outros-usuarios": "outros cidadãos e a manutenção",
    "repositorio": "repositório público no GitHub",
    "rede": "rede local",
    "token": "token de acesso",
    "logs": "logs",
    "respostas-de-erro": "mensagens de erro",
    "armazenamento-do-aparelho": "armazenamento do celular",
    "banco-de-dados": "banco de dados",
}
