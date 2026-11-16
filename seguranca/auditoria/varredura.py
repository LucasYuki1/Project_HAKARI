"""Roda o Semgrep com as regras do Hakari e transforma o resultado em achados do relatório."""
from __future__ import annotations

import hashlib
import json
import os
import subprocess
import tempfile
import tomllib
from datetime import date
from pathlib import Path

from . import config
from .redacao import redigir

_LINGUAGEM = {".cs": "csharp", ".ts": "typescript", ".tsx": "tsx", ".json": "json", ".yml": "yaml",
              ".yaml": "yaml", ".sh": "bash", ".py": "python", ".ino": "cpp"}


def versao_semgrep() -> str:
    r = subprocess.run([config.executavel_semgrep(), "--version"], capture_output=True, text=True,
                       env=_ambiente(), check=False)
    return (r.stdout.strip().splitlines() or ["?"])[-1]


def _ambiente() -> dict[str, str]:
    # O Semgrep grava um arquivo de configuração a cada execução. Um arquivo temporário por processo
    # evita disputa (no Windows, o antivírus às vezes trava o arquivo recém-gravado) e não suja a
    # pasta do usuário.
    configuracao = Path(tempfile.gettempdir()) / f"hakari-semgrep-{os.getpid()}.yml"
    return os.environ | {"SEMGREP_ENABLE_VERSION_CHECK": "0", "SEMGREP_SETTINGS_FILE": str(configuracao),
                         "PYTHONUTF8": "1", "PYTHONIOENCODING": "utf-8"}


def rodar_semgrep(alvo: Path) -> dict:
    """Varre o alvo (uma cópia do repositório) e devolve o JSON do Semgrep."""
    comando = [config.executavel_semgrep(), "scan", "--metrics=off", "--json", "--quiet",
               *(f"--config={r}" for r in config.REGRAS_SEMGREP),
               "--exclude=seguranca", "."]
    r = subprocess.run(comando, cwd=alvo, capture_output=True, env=_ambiente(), check=False)
    if r.returncode not in (0, 1):
        raise SystemExit(f"Semgrep falhou ({r.returncode}): {r.stderr.decode('utf-8', 'replace')[-800:]}")
    return json.loads(r.stdout.decode("utf-8"))


def _tipo(regra: str) -> str:
    if regra.startswith("hakari.mapa."):
        return "mapa"
    if regra.startswith("hakari.taint."):
        return "taint"
    if regra.startswith("hakari.segredo."):
        return "segredo"
    return "infra"


def _camada(caminho: str) -> str:
    if "Hakari.Domain/Entidades" in caminho:
        return "Banco de dados (entidade)"
    if "Contratos" in caminho:
        return "API (entrada e saída)"
    if caminho.endswith("tipos.ts"):
        return "App (dados da API)"
    if caminho.endswith("banco.ts"):
        return "App (SQLite no celular)"
    if caminho.startswith("mobile/"):
        return "App"
    if caminho.startswith("api/"):
        return "API"
    return caminho.split("/")[0]


def _trecho(alvo: Path, caminho: str, inicio: int, fim: int) -> str:
    try:
        linhas = (alvo / caminho).read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError:
        return ""
    de, ate = max(1, inicio - 2), min(len(linhas), max(fim, inicio) + 2, inicio + 12)
    return redigir("\n".join(f"{n:>4}  {linhas[n - 1]}" for n in range(de, ate + 1)))


def _registro() -> list[dict]:
    if not config.REGISTRO_DE_RISCOS.exists():
        return []
    return tomllib.loads(config.REGISTRO_DE_RISCOS.read_text(encoding="utf-8")).get("risco", [])


def achados_manuais(usar_registro: bool) -> list[dict]:
    """Achados da revisão manual, que nenhuma regra automática pega, registrados no TOML."""
    if not config.REGISTRO_DE_RISCOS.exists():
        return []
    dados = tomllib.loads(config.REGISTRO_DE_RISCOS.read_text(encoding="utf-8"))
    saida = []
    for m in dados.get("achado_manual", []):
        aceito = usar_registro and m.get("status") == "aceito"
        saida.append({
            "id": "manual-" + m["id"], "regra": "revisao-manual." + m["id"], "tipo": "manual",
            "severidade": m["severidade"], "titulo": m["titulo"], "mensagem": m["mensagem"],
            "categoria": m["categoria"], "saida": m["saida"], "lgpd": m.get("lgpd", ""), "cwe": m.get("cwe", ""),
            "arquivo": m["arquivo"], "linha": m.get("linha", 1), "linha_fim": m.get("linha", 1),
            "trecho": "", "linguagem": "", "camada": _camada(m["arquivo"]), "campo": "",
            "status": "aceito" if aceito else "aberto",
            "aceite": {k: str(m[k]) for k in ("justificativa", "responsavel", "aceito_em", "revisar_em") if k in m} if aceito else None,
        })
    return saida


def normalizar(bruto: dict, alvo: Path, usar_registro: bool) -> tuple[list[dict], list[dict]]:
    """Separa o mapa de dados pessoais (INFO) dos achados de risco e aplica o registro de aceites."""
    registro = _registro() if usar_registro else []
    hoje = date.today().isoformat()
    mapa, riscos = [], []

    for r in bruto.get("results", []):
        regra = r["check_id"].split("regras.")[-1]
        meta = r["extra"].get("metadata", {})
        caminho = r["path"].replace("\\", "/")
        inicio, fim = r["start"]["line"], r["end"]["line"]
        mensagem = " ".join(r["extra"]["message"].split())
        trecho = _trecho(alvo, caminho, inicio, fim)
        impressao = hashlib.sha1(f"{regra}|{caminho}|{''.join(trecho.split())}".encode()).hexdigest()[:12]

        base = {
            "id": impressao, "regra": regra, "tipo": _tipo(regra), "severidade": r["extra"]["severity"],
            "titulo": meta.get("titulo") or mensagem.split(":")[0], "mensagem": mensagem,
            "categoria": meta.get("categoria", ""), "saida": meta.get("saida", ""),
            "lgpd": meta.get("lgpd", ""), "cwe": meta.get("cwe", ""),
            "arquivo": caminho, "linha": inicio, "linha_fim": fim, "trecho": trecho,
            "linguagem": _LINGUAGEM.get(Path(caminho).suffix, ""), "camada": _camada(caminho),
            "campo": mensagem.split(":")[0] if regra.startswith("hakari.mapa.") else "",
        }
        if base["tipo"] == "mapa":
            mapa.append(base)
            continue

        aceite = next((a for a in registro
                       if a["regra"] == regra and a["arquivo"] == caminho and str(a.get("revisar_em", "9999")) >= hoje), None)
        base["status"] = "aceito" if aceite else "aberto"
        base["aceite"] = {k: str(aceite[k]) for k in ("justificativa", "responsavel", "aceito_em", "revisar_em")
                          if k in aceite} if aceite else None
        riscos.append(base)

    riscos.extend(achados_manuais(usar_registro))
    ordem = {"ERROR": 0, "WARNING": 1, "INFO": 2}
    riscos.sort(key=lambda a: (a["status"] != "aberto", ordem.get(a["severidade"], 3), a["arquivo"], a["linha"]))
    mapa.sort(key=lambda a: (a["categoria"], a["arquivo"], a["linha"]))
    return mapa, riscos


def resumo_da_varredura(bruto: dict) -> dict:
    erros_parse = [e for e in bruto.get("errors", []) if "PartialParsing" in str(e.get("type"))]
    return {
        "arquivos": len(bruto.get("paths", {}).get("scanned", [])),
        "arquivos_com_parse_parcial": len(erros_parse),
        "regras": sum(c.read_text(encoding="utf-8").count("\n  - id:") for c in config.REGRAS_SEMGREP),
    }
