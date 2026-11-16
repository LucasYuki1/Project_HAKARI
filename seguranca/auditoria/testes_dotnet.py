"""Roda os testes de privacidade da API (motor de PII e canários) e lê os relatórios que eles gravam."""
from __future__ import annotations

import json
import os
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path


def rodar(api: Path) -> dict:
    solucao = api / "Hakari.slnx"
    if not (api / "tests" / "Hakari.Tests" / "Privacidade").exists():
        return {"executado": False, "motivo": "esta versão do código não tem testes de privacidade"}

    with tempfile.TemporaryDirectory() as pasta:
        tmp = Path(pasta)
        ambiente = os.environ | {
            "HAKARI_RELATORIO_MOTOR": str(tmp / "motor.json"),
            "HAKARI_RELATORIO_TAINT": str(tmp / "taint.json"),
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        }
        r = subprocess.run(
            ["dotnet", "test", str(solucao), "--nologo", "-v", "q",
             "--filter", "FullyQualifiedName~Hakari.Tests.Privacidade",
             "--logger", "trx;LogFileName=privacidade.trx", "--results-directory", str(tmp)],
            capture_output=True, env=ambiente, check=False)

        contagem = {"total": 0, "aprovados": 0, "falhas": 0}
        for trx in tmp.rglob("*.trx"):
            ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
            c = ET.parse(trx).getroot().find("t:ResultSummary/t:Counters", ns)
            if c is not None:
                contagem = {"total": int(c.get("total", 0)), "aprovados": int(c.get("passed", 0)),
                            "falhas": int(c.get("failed", 0))}

        def ler(nome: str) -> dict | None:
            arquivo = tmp / nome
            return json.loads(arquivo.read_text(encoding="utf-8")) if arquivo.exists() else None

        return {"executado": True, "codigo_saida": r.returncode, **contagem,
                "motor": ler("motor.json"), "taint": ler("taint.json")}


def execucao(taint: dict | None, modo: str) -> dict | None:
    if not taint:
        return None
    return next((e for e in taint.get("execucoes", []) if e.get("modo") == modo), None)
