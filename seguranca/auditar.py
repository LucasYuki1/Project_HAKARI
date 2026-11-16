"""Auditoria de privacidade do Hakari.

Uso (na raiz do repositório, com o Python do venv da pasta seguranca):
    seguranca/.venv/Scripts/python seguranca/auditar.py                      # código atual
    seguranca/.venv/Scripts/python seguranca/auditar.py --ref 4470f4f --rotulo antes --ignorar-aceites
    seguranca/.venv/Scripts/python seguranca/auditar.py --sem-ia             # sem chamar o Claude

Etapas: Semgrep com as regras de seguranca/regras (mapeamento de PII, taint e segredos), testes de
privacidade da API (motor de PII e canários), Risco Cidadão, tradução via IA e o retrato em JSON
(seguranca/resultados) que o painel lê. Termina com código 1 se houver achado ERROR aberto: é o que
bloqueia o pipeline de CI.
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
import tempfile
from datetime import datetime
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from auditoria import config, risco, testes_dotnet, varredura  # noqa: E402
from auditoria.ia import Tradutor  # noqa: E402


def _git(*args: str, cwd: Path = config.RAIZ) -> str:
    return subprocess.run(["git", *args], cwd=cwd, capture_output=True, text=True, check=False).stdout.strip()


def auditar(alvo: Path, rotulo: str, usar_ia: bool, usar_registro: bool) -> dict:
    agora = datetime.now().astimezone()  # fuso da máquina; sem depender do pacote tzdata no Windows
    print(f"[1/5] Semgrep em {alvo} ...", flush=True)
    bruto = varredura.rodar_semgrep(alvo)
    mapa, achados = varredura.normalizar(bruto, alvo, usar_registro)

    print("[2/5] Testes de privacidade da API (motor de PII e canários) ...", flush=True)
    testes = testes_dotnet.rodar(alvo / "api")
    dinamico, simulado = None, False
    if testes.get("executado"):
        dinamico = testes_dotnet.execucao(testes.get("taint"), "protegido")
    else:
        # Versão antiga, sem o motor: o "controle" dos canários (motor sem regras) reproduz o
        # comportamento dela, então ele entra como o retrato dinâmico desta versão.
        atual = testes_dotnet.rodar(config.RAIZ / "api")
        dinamico, simulado = testes_dotnet.execucao(atual.get("taint"), "controle"), True
    if dinamico is not None:
        dinamico = dinamico | {"simulado": simulado}

    print("[3/5] Risco Cidadão ...", flush=True)
    indicador = risco.calcular(achados, (dinamico or {}).get("vazamentos", []))

    retrato = {
        "versao": 1,
        "rotulo": rotulo,
        "gerado_em": agora.isoformat(timespec="seconds"),
        "commit": _git("rev-parse", "--short", "HEAD", cwd=alvo),
        "alteracoes_nao_commitadas": bool(_git("status", "--porcelain", "--", ".", ":!seguranca/resultados", cwd=alvo)),
        "ferramentas": {"semgrep": varredura.versao_semgrep(), "modelo_ia": config.MODELO_IA},
        "semgrep": varredura.resumo_da_varredura(bruto),
        "risco": indicador,
        "achados": achados,
        "mapa": mapa,
        "dinamico": dinamico,
        "motor": testes.get("motor"),
        "testes": {k: testes.get(k) for k in ("executado", "total", "aprovados", "falhas", "motivo") if k in testes},
    }

    print(f"[4/5] Tradução via IA ({config.MODELO_IA}) ...", flush=True)
    tradutor = Tradutor(usar_ia)
    alertas = tradutor.traduzir(achados)
    for a in achados:
        a["alerta"] = alertas.get(a["id"])
    retrato["resumo"] = tradutor.resumir(retrato)
    retrato["ia"] = tradutor.relatorio_de_uso()

    print("[5/5] Gravando o retrato ...", flush=True)
    config.HISTORICO.mkdir(parents=True, exist_ok=True)
    nome = f"{agora:%Y%m%d-%H%M%S}-{rotulo}.json"
    conteudo = json.dumps(retrato, ensure_ascii=False, indent=1)
    (config.HISTORICO / nome).write_text(conteudo, encoding="utf-8")
    config.ULTIMO.write_text(conteudo, encoding="utf-8")
    retrato["_arquivo"] = nome
    return retrato


def imprimir(r: dict) -> None:
    abertos = [a for a in r["achados"] if a["status"] == "aberto"]
    aceitos = [a for a in r["achados"] if a["status"] == "aceito"]
    din = r.get("dinamico") or {}
    print()
    print(f"Risco Cidadão: {r['risco']['valor']} ({r['risco']['faixa']})   commit {r['commit']}   rótulo {r['rotulo']}")
    print(f"Dados pessoais mapeados: {len(r['mapa'])} campos   Achados abertos: {len(abertos)}   Riscos aceitos: {len(aceitos)}")
    for a in abertos:
        print(f"  [{a['severidade']:7}] {a['titulo']}  ({a['arquivo']}:{a['linha']})")
    if din:
        extra = " (simulado com o filtro desligado)" if din.get("simulado") else ""
        print(f"Canários: {din.get('saidas_verificadas')} saídas verificadas, {len(din.get('vazamentos', []))} vazamentos{extra}")
    if r.get("motor"):
        m = r["motor"]
        print(f"Motor de PII: {m['casos']} casos, precisão {m['precisao']:.0%}, cobertura {m['cobertura']:.0%}")
    ia = r["ia"]
    if ia["sem_ia"]:
        print(f"IA: não usada ({ia['sem_ia']})")
    else:
        print(f"IA: {ia['chamadas']} chamada(s), {ia['do_cache']} do cache, ~US$ {ia['custo_estimado_usd']:.2f}")
    print(f"Retrato: seguranca/resultados/historico/{r['_arquivo']}")


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--ref", help="audita outro commit (numa cópia temporária com git worktree)")
    p.add_argument("--rotulo", default="atual", help="nome do retrato no painel (ex.: antes, depois)")
    p.add_argument("--sem-ia", action="store_true", help="não chama o Claude")
    p.add_argument("--ignorar-aceites", action="store_true", help="não aplica o registro de riscos aceitos")
    args = p.parse_args()

    if args.ref:
        with tempfile.TemporaryDirectory(prefix="hakari-auditoria-") as pasta:
            alvo = Path(pasta) / "codigo"
            subprocess.run(["git", "worktree", "add", "--detach", str(alvo), args.ref], cwd=config.RAIZ,
                           check=True, capture_output=True)
            try:
                r = auditar(alvo, args.rotulo, not args.sem_ia, not args.ignorar_aceites)
            finally:
                subprocess.run(["git", "worktree", "remove", "--force", str(alvo)], cwd=config.RAIZ, check=False)
    else:
        r = auditar(config.RAIZ, args.rotulo, not args.sem_ia, not args.ignorar_aceites)

    imprimir(r)
    bloqueia = any(a["status"] == "aberto" and a["severidade"] == "ERROR" for a in r["achados"])
    falhou_teste = (r["testes"].get("falhas") or 0) > 0
    return 1 if bloqueia or falhou_teste else 0


if __name__ == "__main__":
    sys.exit(main())
