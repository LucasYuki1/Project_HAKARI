"""Painel executivo "Risco Cidadão" da auditoria de privacidade do Hakari.

    streamlit run seguranca/painel.py

Lê os retratos gravados por seguranca/auditar.py (seguranca/resultados/historico) e mostra o
indicador, de onde vem o risco, os alertas traduzidos pela IA, o teste de canários, o mapa de dados
pessoais e as métricas do motor de PII. O botão "Rodar auditoria" executa o pipeline na hora.
"""
from __future__ import annotations

import html
import json
import subprocess
import sys
from datetime import datetime
from pathlib import Path

import pandas as pd
import plotly.graph_objects as go
import streamlit as st

PASTA = Path(__file__).resolve().parent
sys.path.insert(0, str(PASTA))
from auditoria import config  # noqa: E402

st.set_page_config(page_title="Risco Cidadão · Hakari", page_icon="💡", layout="wide")

# ---------------------------------------------------------------- cores (paleta de referência)
CLARO = {
    "superficie": "#fcfcfb", "plano": "#f9f9f7", "texto1": "#0b0b0b", "texto2": "#52514e", "mudo": "#898781",
    "grade": "#e1e0d9", "base": "#c3c2b7", "borda": "rgba(11,11,11,0.10)", "serie": "#2a78d6",
    "antes": "#86b6ef", "agora": "#256abf", "texto_ok": "#006300",
}
ESCURO = {
    "superficie": "#1a1a19", "plano": "#0d0d0d", "texto1": "#f0efec", "texto2": "#c3c2b7", "mudo": "#898781",
    "grade": "#2c2c2a", "base": "#383835", "borda": "rgba(255,255,255,0.10)", "serie": "#3987e5",
    "antes": "#1c5cab", "agora": "#6da7ec", "texto_ok": "#0ca30c",
}
STATUS = {"bom": "#0ca30c", "atencao": "#fab219", "serio": "#ec835a", "critico": "#d03b3b"}
FAIXA = {  # faixa -> (status, ícone)
    "Baixo": ("bom", "✓"), "Moderado": ("atencao", "!"), "Alto": ("serio", "▲"), "Crítico": ("critico", "✕"),
}
tema = getattr(st.context.theme, "type", None) or "light"
C = ESCURO if tema == "dark" else CLARO
FONTE = 'system-ui, -apple-system, "Segoe UI", sans-serif'


def num(v: float, casas: int = 1) -> str:
    return f"{v:.{casas}f}".replace(".", ",")


def tinta_em(status: str) -> str:
    return "#0b0b0b" if status == "atencao" else "#ffffff"


def chip(texto: str, status: str, icone: str) -> str:
    cor = STATUS[status]
    return (f'<span style="display:inline-flex;align-items:center;gap:6px;padding:2px 10px;border-radius:999px;'
            f'background:{cor};color:{tinta_em(status)};font-size:0.8rem;font-weight:600;white-space:nowrap">'
            f'<span aria-hidden="true">{icone}</span>{html.escape(texto)}</span>')


st.markdown(f"""
<style>
  .hk-card {{ background:{C['superficie']}; border:1px solid {C['borda']}; border-radius:12px; padding:16px 18px; }}
  .hk-rotulo {{ color:{C['texto2']}; font-size:0.85rem; margin-bottom:4px; }}
  .hk-valor {{ color:{C['texto1']}; font-size:1.9rem; font-weight:600; line-height:1.15; }}
  .hk-sub {{ color:{C['mudo']}; font-size:0.8rem; margin-top:4px; }}
  .hk-heroi {{ font-family:{FONTE}; font-size:4.2rem; font-weight:650; line-height:1; color:{C['texto1']}; }}
  .hk-texto {{ color:{C['texto1']}; }}
  .hk-texto2 {{ color:{C['texto2']}; }}
</style>""", unsafe_allow_html=True)


# ---------------------------------------------------------------- dados
@st.cache_data(show_spinner=False)
def ler_retrato(caminho: str, _mtime: float) -> dict:
    return json.loads(Path(caminho).read_text(encoding="utf-8"))


def listar_retratos() -> list[Path]:
    return sorted(config.HISTORICO.glob("*.json"))


def descrever(p: Path) -> str:
    r = ler_retrato(str(p), p.stat().st_mtime)
    quando = datetime.fromisoformat(r["gerado_em"]).strftime("%d/%m %H:%M")
    return f"{r['rotulo']} · {quando} · commit {r['commit']}{'*' if r.get('alteracoes_nao_commitadas') else ''}"


def rodar_auditoria(rotulo: str, sem_ia: bool) -> None:
    comando = [config.python_do_venv(), str(PASTA / "auditar.py"), "--rotulo", rotulo]
    if sem_ia:
        comando.append("--sem-ia")
    with st.status("Rodando a auditoria...", expanded=True) as caixa:
        processo = subprocess.Popen(comando, cwd=config.RAIZ, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                    text=True, encoding="utf-8", errors="replace",
                                    env={**__import__("os").environ, "PYTHONIOENCODING": "utf-8"})
        for linha in processo.stdout:
            if linha.strip():
                st.write(linha.rstrip())
        processo.wait()
        caixa.update(label="Auditoria concluída" if processo.returncode in (0, 1) else "A auditoria falhou",
                     state="complete" if processo.returncode in (0, 1) else "error")
    st.cache_data.clear()


retratos = listar_retratos()
st.markdown(f'<div class="hk-texto2" style="font-size:0.9rem">Hakari · auditoria de privacidade</div>',
            unsafe_allow_html=True)
st.title("Risco Cidadão")

if not retratos:
    st.info("Nenhuma auditoria ainda. Rode `seguranca/.venv/Scripts/python seguranca/auditar.py`.")
    if st.button("Rodar auditoria agora", type="primary"):
        rodar_auditoria("atual", sem_ia=False)
        st.rerun()
    st.stop()

# ---------------------------------------------------------------- linha de filtros (acima de tudo)
nomes = [descrever(p) for p in retratos]
padrao_comparar = next((i for i in range(len(retratos) - 1, -1, -1)
                        if ler_retrato(str(retratos[i]), retratos[i].stat().st_mtime)["rotulo"] == "antes"), 0)
f1, f2, f3, f4 = st.columns([3, 3, 1.4, 1.6], vertical_alignment="bottom")
i_atual = f1.selectbox("Retrato", range(len(retratos)), index=len(retratos) - 1, format_func=lambda i: nomes[i])
i_comp = f2.selectbox("Comparar com", [None, *range(len(retratos))],
                      index=(padrao_comparar + 1) if padrao_comparar != len(retratos) - 1 else 0,
                      format_func=lambda i: "nada" if i is None else nomes[i])
sem_ia = f3.toggle("Sem IA", value=False, help="Roda a auditoria sem chamar o Claude")
if f4.button("Rodar auditoria agora", type="primary", use_container_width=True):
    rodar_auditoria("atual", sem_ia)
    st.rerun()

R = ler_retrato(str(retratos[i_atual]), retratos[i_atual].stat().st_mtime)
A = None if i_comp is None or i_comp == i_atual else ler_retrato(str(retratos[i_comp]), retratos[i_comp].stat().st_mtime)
quando = datetime.fromisoformat(R["gerado_em"]).strftime("%d/%m/%Y às %H:%M")
st.caption(f"Retrato **{R['rotulo']}** de {quando}, commit `{R['commit']}`"
           + (" com alterações ainda não commitadas" if R.get("alteracoes_nao_commitadas") else "")
           + (f". Comparando com **{A['rotulo']}** (commit `{A['commit']}`)." if A else "."))

# ---------------------------------------------------------------- herói + medidor
valor, faixa = R["risco"]["valor"], R["risco"]["faixa"]
status_faixa, icone_faixa = FAIXA[faixa]
cor = STATUS[status_faixa]
delta = ""
if A:
    d = valor - A["risco"]["valor"]
    cor_delta = C["texto_ok"] if d < 0 else STATUS["critico"] if d > 0 else C["mudo"]
    seta = "▼" if d < 0 else "▲" if d > 0 else "="
    delta = (f'<span style="color:{cor_delta};font-weight:600">{seta} {num(abs(d))} pontos</span>'
             f'<span class="hk-texto2"> desde "{html.escape(A["rotulo"])}" ({num(A["risco"]["valor"])})</span>')

marcas = "".join(
    f'<div style="position:absolute;left:{x}%;top:-4px;bottom:-4px;width:2px;background:{C["superficie"]}"></div>'
    for x in (20, 40, 70))
rotulos = "".join(
    f'<span style="position:absolute;left:{x}%;transform:translateX(-50%)">{t}</span>'
    for x, t in ((10, "Baixo"), (30, "Moderado"), (55, "Alto"), (85, "Crítico")))
heroi, explicacao = st.columns([1.15, 1.85], gap="large")
heroi.markdown(f"""
<div class="hk-card" role="img" aria-label="Risco Cidadão {num(valor)} de 100, faixa {faixa}">
  <div class="hk-rotulo">Risco Cidadão (0 a 100)</div>
  <div style="display:flex;align-items:baseline;gap:14px;flex-wrap:wrap">
    <span class="hk-heroi">{num(valor)}</span>{chip(faixa, status_faixa, icone_faixa)}
  </div>
  <div style="margin-top:8px;font-size:0.9rem">{delta}</div>
  <div style="position:relative;height:10px;border-radius:999px;background:{cor}2e;margin-top:18px">
    <div style="position:absolute;left:0;top:0;bottom:0;width:{max(valor, 1.5)}%;border-radius:999px;background:{cor}"></div>
    {marcas}
  </div>
  <div style="position:relative;height:18px;margin-top:6px;font-size:0.75rem;color:{C['mudo']}">{rotulos}</div>
</div>""", unsafe_allow_html=True)

resumo = R.get("resumo") or {}
origem_resumo = resumo.get("origem", "")
with explicacao:
    st.markdown(f"#### {resumo.get('manchete', '')}")
    st.markdown(resumo.get("resumo_executivo", ""))
    if resumo.get("principais_riscos") or resumo.get("proximos_passos"):
        with st.expander("Principais riscos e próximos passos"):
            r1, r2 = st.columns(2)
            if resumo.get("principais_riscos"):
                r1.markdown("**Principais riscos**\n" + "\n".join(f"- {x}" for x in resumo["principais_riscos"]))
            if resumo.get("proximos_passos"):
                r2.markdown("**Próximos passos**\n" + "\n".join(f"- {x}" for x in resumo["proximos_passos"]))
    st.caption(f"Resumo escrito por {origem_resumo}." if origem_resumo else "")

# ---------------------------------------------------------------- KPIs
abertos = [a for a in R["achados"] if a["status"] == "aberto"]
aceitos = [a for a in R["achados"] if a["status"] == "aceito"]
din = R.get("dinamico") or {}
motor = R.get("motor")


def tile(coluna, rotulo: str, valor_txt: str, sub: str, delta_txt: str = "") -> None:
    coluna.markdown(f"""<div class="hk-card"><div class="hk-rotulo">{rotulo}</div>
      <div class="hk-valor">{valor_txt}</div><div class="hk-sub">{delta_txt}{sub}</div></div>""",
                    unsafe_allow_html=True)


def delta_qtd(atual: int, antes: int | None) -> str:
    if antes is None or antes == atual:
        return ""
    d = atual - antes
    cor_d = C["texto_ok"] if d < 0 else STATUS["critico"]
    return f'<span style="color:{cor_d};font-weight:600">{"▼" if d < 0 else "▲"} {abs(d)}</span> · '


st.write("")
k1, k2, k3, k4 = st.columns(4)
categorias_mapa = sorted({m["categoria"] for m in R["mapa"]})
tile(k1, "Dados pessoais mapeados", f"{len(R['mapa'])} campos",
     f"{len(categorias_mapa)} tipos de dado, de entidades a colunas do celular")
abertos_antes = None if not A else sum(1 for a in A["achados"] if a["status"] == "aberto")
tile(k2, "Achados abertos", str(len(abertos)), f"{len(aceitos)} risco(s) aceito(s) com justificativa",
     delta_qtd(len(abertos), abertos_antes))
vaz = len(din.get("vazamentos", []))
vaz_antes = None if not A else len((A.get("dinamico") or {}).get("vazamentos", []))
tile(k3, "Vazamentos no teste de canários", str(vaz),
     f"{din.get('saidas_verificadas', 0)} saídas verificadas" + (" (simulado)" if din.get("simulado") else ""),
     delta_qtd(vaz, vaz_antes))
if motor:
    tile(k4, "Motor de PII", f"{motor['precisao']:.0%} · {motor['cobertura']:.0%}",
         f"precisão · cobertura em {motor['casos']} casos de teste")
else:
    tile(k4, "Motor de PII", "não havia", "esta versão publicava o texto sem filtro")

# ---------------------------------------------------------------- de onde vem o risco
st.subheader("De onde vem o risco")
st.caption("Pontos do indicador explicados por cada tipo de dado. Cada achado aberto soma; corrigir derruba.")
agora_cat = R["risco"]["por_categoria"]
antes_cat = (A or {}).get("risco", {}).get("por_categoria", {}) if A else {}
cats = sorted(set(agora_cat) | set(antes_cat), key=lambda c: -(antes_cat.get(c, 0) + agora_cat.get(c, 0)))
nome_cat = [config.ROTULO_CATEGORIA.get(c, c) for c in cats]
fig = go.Figure()
if A:
    for c, n in zip(cats, nome_cat):
        fig.add_trace(go.Scatter(x=[agora_cat.get(c, 0), antes_cat.get(c, 0)], y=[n, n], mode="lines",
                                 line=dict(color=C["base"], width=2), hoverinfo="skip", showlegend=False))
    fig.add_trace(go.Scatter(x=[antes_cat.get(c, 0) for c in cats], y=nome_cat, mode="markers",
                             name=f"Antes ({A['rotulo']})",
                             marker=dict(size=13, color=C["antes"], line=dict(color=C["superficie"], width=2)),
                             hovertemplate="%{y}<br>antes: %{x:.1f} pontos<extra></extra>"))
    # Rótulo direto só onde ainda sobra risco: zeros repetidos seriam ruído (o valor está na tabela).
    fig.add_trace(go.Scatter(x=[agora_cat.get(c, 0) for c in cats], y=nome_cat, mode="markers+text",
                             name=f"Agora ({R['rotulo']})",
                             text=[num(agora_cat[c]) if agora_cat.get(c, 0) > 0 else "" for c in cats],
                             textposition="top center", textfont=dict(color=C["texto1"], size=12),
                             marker=dict(size=13, color=C["agora"], line=dict(color=C["superficie"], width=2)),
                             hovertemplate="%{y}<br>agora: %{x:.1f} pontos<extra></extra>"))
    limite = max([*antes_cat.values(), *agora_cat.values(), 1])
else:
    fig.add_trace(go.Bar(x=[agora_cat[c] for c in cats], y=nome_cat, orientation="h", marker=dict(color=C["serie"], cornerradius=4),
                         text=[num(agora_cat[c]) for c in cats], textposition="outside",
                         textfont=dict(color=C["texto2"]), hovertemplate="%{y}: %{x:.1f} pontos<extra></extra>",
                         showlegend=False))
    limite = max([*agora_cat.values(), 1])
fig.update_layout(
    height=110 + 50 * max(len(cats), 1), margin=dict(l=8, r=24, t=40, b=36),
    paper_bgcolor="rgba(0,0,0,0)", plot_bgcolor="rgba(0,0,0,0)",
    font=dict(family=FONTE, color=C["texto2"], size=13),
    legend=dict(orientation="h", yanchor="bottom", y=1.02, x=0, font=dict(color=C["texto2"])),
    xaxis=dict(range=[-limite * 0.04, limite * 1.08], showgrid=True, gridcolor=C["grade"], zeroline=False,
               showline=False, tickfont=dict(color=C["mudo"]), title=None, automargin=True),
    yaxis=dict(autorange="reversed", showgrid=False, tickfont=dict(color=C["texto2"]), automargin=True,
               ticksuffix="   "),
    hoverlabel=dict(bgcolor=C["superficie"], font=dict(color=C["texto1"], family=FONTE), bordercolor=C["base"]),
)
st.plotly_chart(fig, use_container_width=True, theme=None, config={"displayModeBar": False})
with st.expander("Ver como tabela"):
    st.dataframe(pd.DataFrame({"Tipo de dado": nome_cat,
                               **({"Antes": [antes_cat.get(c, 0.0) for c in cats]} if A else {}),
                               "Agora": [agora_cat.get(c, 0.0) for c in cats]}),
                 hide_index=True, use_container_width=True)
    st.caption(f"Fórmula: {R['risco']['formula']}. Risco aceito conta com 25% do peso.")

# ---------------------------------------------------------------- alertas para o cidadão
st.subheader("Alertas para o cidadão")
st.caption("Cada falha técnica traduzida pela IA em linguagem simples, com a orientação de correção para a equipe.")
NIVEL = {"baixo": ("bom", "✓"), "moderado": ("atencao", "!"), "alto": ("serio", "▲"), "critico": ("critico", "✕")}


def card(a: dict, situacao: str) -> None:
    alerta = a.get("alerta") or {}
    if situacao == "corrigido":
        etiqueta = chip("Corrigido", "bom", "✓")
    elif a["status"] == "aceito":
        etiqueta = chip("Risco aceito", "atencao", "◐")
    else:
        etiqueta = chip("Aberto", "critico" if a["severidade"] == "ERROR" else "serio", "✕" if a["severidade"] == "ERROR" else "!")
    n_status, n_icone = NIVEL.get(alerta.get("nivel", ""), ("atencao", "!"))
    nivel = chip(f"Risco {alerta.get('nivel', '?')}", n_status, n_icone) if situacao != "corrigido" else ""
    with st.container(border=True):
        st.markdown(f'<div style="display:flex;gap:8px;flex-wrap:wrap;margin-bottom:6px">{etiqueta}{nivel}</div>',
                    unsafe_allow_html=True)
        st.markdown(f"**{alerta.get('titulo_cidadao') or a['titulo']}**")
        st.markdown(alerta.get("alerta_cidadao") or a["mensagem"])
        detalhes = [("O que pode acontecer", alerta.get("o_que_pode_acontecer")),
                    ("Quem é afetado", alerta.get("quem_e_afetado")),
                    ("O que você pode fazer", alerta.get("o_que_o_cidadao_pode_fazer"))]
        texto = "  \n".join(f"**{t}:** {v}" for t, v in detalhes if v)
        if texto:
            st.markdown(texto)
        if a.get("aceite"):
            st.markdown(f"**Por que foi aceito:** {a['aceite'].get('justificativa', '')}  \n"
                        f"Responsável: {a['aceite'].get('responsavel', '')} · revisar em {a['aceite'].get('revisar_em', '')}")
        with st.expander("Para a equipe técnica"):
            st.markdown(f"`{a['regra']}` em `{a['arquivo']}:{a['linha']}` ({a['camada']}) · {a['lgpd']} · {a['cwe']}")
            if a.get("trecho"):
                st.code(a["trecho"], language=a.get("linguagem") or None)
            if alerta.get("correcao_tecnica"):
                st.markdown(f"**Correção sugerida:** {alerta['correcao_tecnica']}")
            if alerta.get("trecho_corrigido"):
                st.code(alerta["trecho_corrigido"], language=a.get("linguagem") or None)
            if alerta.get("referencia_lgpd"):
                st.markdown(f"**LGPD:** {alerta['referencia_lgpd']}")
            st.caption(f"Texto: {alerta.get('origem', 'sem IA')}")


chave = lambda a: (a["regra"], a["arquivo"])  # noqa: E731
corrigidos = [a for a in (A or {}).get("achados", []) if A and chave(a) not in {chave(x) for x in R["achados"]}]
abas = st.tabs([f"Abertos ({len(abertos)})", f"Aceitos ({len(aceitos)})", f"Corrigidos ({len(corrigidos)})"])
with abas[0]:
    if not abertos:
        st.success("Nenhum achado aberto neste retrato.", icon="✅")
    for a in abertos:
        card(a, "aberto")
with abas[1]:
    for a in aceitos:
        card(a, "aceito")
with abas[2]:
    if not corrigidos:
        st.caption("Escolha um retrato em \"Comparar com\" para ver o que foi corrigido.")
    for a in corrigidos:
        card(a, "corrigido")

# ---------------------------------------------------------------- canários
st.subheader("Teste de vazamento com canários")
st.caption("Dados pessoais fictícios plantados no reporte e na solução do técnico, procurados em tudo que outra "
           "pessoa lê: respostas da API e o que fica no banco.")
saidas = din.get("saidas", [])
if saidas:
    def dados_por_saida(d: dict) -> dict[str, list[str]]:
        mapa_s: dict[str, list[str]] = {}
        for v in d.get("vazamentos", []):
            mapa_s.setdefault(f"{v['saida']} ({v['quem_le']})", []).append(v["dado"])
        return mapa_s

    agora_s = dados_por_saida(din)
    antes_s = dados_por_saida((A or {}).get("dinamico") or {}) if A else {}
    tabela = pd.DataFrame({
        "Saída verificada": saidas,
        **({"Antes": [", ".join(antes_s.get(s, [])) or "—" for s in saidas]} if A else {}),
        "Agora": [", ".join(agora_s.get(s, [])) or "nenhum dado vazou" for s in saidas],
    })
    st.dataframe(tabela, hide_index=True, use_container_width=True,
                 column_config={"Saída verificada": st.column_config.TextColumn(width="medium"),
                                "Antes": st.column_config.TextColumn(width="large"),
                                "Agora": st.column_config.TextColumn(width="medium")})
    if din.get("simulado"):
        st.caption("Este retrato é de uma versão sem o motor; o teste reproduz o comportamento dela com o filtro desligado.")

# ---------------------------------------------------------------- mapa de dados pessoais
st.subheader("Mapa de dados pessoais")
st.caption("Onde o Hakari guarda ou transporta dado pessoal, encontrado pelas regras de mapeamento.")
mapa = pd.DataFrame([{"Tipo de dado": config.ROTULO_CATEGORIA.get(m["categoria"], m["categoria"]),
                      "Campo": m["campo"], "Camada": m["camada"], "Arquivo": f"{m['arquivo']}:{m['linha']}"}
                     for m in R["mapa"]])
m1, m2 = st.columns([1, 2.2], gap="large")
contagem = mapa.groupby("Tipo de dado").size().sort_values(ascending=False).reset_index(name="Campos")
barras = go.Figure(go.Bar(x=contagem["Campos"], y=contagem["Tipo de dado"], orientation="h",
                          marker=dict(color=C["serie"], cornerradius=4), text=contagem["Campos"], textposition="outside",
                          textfont=dict(color=C["texto2"]), cliponaxis=False,
                          hovertemplate="%{y}: %{x} campo(s)<extra></extra>"))
barras.update_layout(height=40 + 34 * len(contagem), margin=dict(l=8, r=28, t=4, b=4), bargap=0.5,
                     paper_bgcolor="rgba(0,0,0,0)", plot_bgcolor="rgba(0,0,0,0)",
                     font=dict(family=FONTE, color=C["texto2"], size=13), showlegend=False,
                     xaxis=dict(visible=False), yaxis=dict(autorange="reversed", automargin=True, ticksuffix="  "),
                     hoverlabel=dict(bgcolor=C["superficie"], font=dict(color=C["texto1"]), bordercolor=C["base"]))
m1.plotly_chart(barras, use_container_width=True, theme=None, config={"displayModeBar": False})
m2.dataframe(mapa, hide_index=True, use_container_width=True, height=300)

# ---------------------------------------------------------------- motor de PII
st.subheader("Motor de PII")
if motor:
    st.caption(f"{motor['casos']} textos de teste ({motor['com_dado_pessoal']} com dado pessoal, "
               f"{motor['sem_dado_pessoal']} sem). O corpus é de regressão: foi escrito junto com as regras, "
               "então mede consistência, não desempenho em texto desconhecido.")
    tipo_pt = {"Identificacao": "Identificação", "Contato": "Contato", "Localizacao": "Localização",
               "Financeiro": "Financeiro", "Saude": "Saúde (sensível)", "Credencial": "Credencial"}
    regras = pd.DataFrame([{"Regra": r["rotulo"], "Tipo": tipo_pt.get(r["categoria"], r["categoria"]),
                            "Acertos": r["acertos"],
                            "Falsos positivos": r["falsos_positivos"], "Falsos negativos": r["falsos_negativos"]}
                           for r in motor["regras"]])
    st.dataframe(regras, hide_index=True, use_container_width=True)
else:
    st.caption("Esta versão não tinha motor de PII: o texto livre era guardado e mostrado como foi digitado.")

# ---------------------------------------------------------------- rodapé
ia = R.get("ia") or {}
st.divider()
st.caption(
    f"Semgrep {R['ferramentas']['semgrep']} · {R['semgrep']['regras']} regras · {R['semgrep']['arquivos']} arquivos "
    f"varridos · testes de privacidade: {R['testes'].get('aprovados', '—')}/{R['testes'].get('total', '—')} "
    f"aprovados · IA: " + (f"não usada ({ia.get('sem_ia')})" if ia.get("sem_ia") else
                          f"{ia.get('chamadas', 0)} chamada(s) ao {', '.join(ia.get('modelos') or [ia.get('modelo_pedido', '')])}, "
                          f"{ia.get('do_cache', 0)} do cache, cerca de US$ {num(ia.get('custo_estimado_usd', 0), 2)}")
    + ". Nenhum trecho enviado à IA leva senha, chave ou dado pessoal: tudo é mascarado antes.")
