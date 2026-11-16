"""Tradução via IA: o Claude transforma cada falha técnica num alerta que o cidadão entende e numa
orientação de correção para a equipe, e escreve o resumo executivo do painel.

Privacidade do próprio pipeline: o trecho de código vai mascarado (redacao.py), nunca com senha,
chave ou dado pessoal. A chave da API vem de ANTHROPIC_API_KEY ou de seguranca/.env e não é exibida.
As respostas ficam em cache (resultados/ia-cache.json): repetir a auditoria só paga pelas falhas novas.
Sem chave, o painel usa o texto da própria regra e avisa que não houve IA.
"""
from __future__ import annotations

import hashlib
import json
import os
from concurrent.futures import ThreadPoolExecutor

from . import config

SISTEMA = """Você é o analista de privacidade do Hakari. Sua tarefa é traduzir falhas técnicas encontradas \
pela auditoria automática em alertas que qualquer cidadão entende, e em orientação objetiva para a equipe \
que vai corrigir.

<contexto>
O Hakari cuida da iluminação pública de São Paulo. Postes com um ESP32 avisam sozinhos quando a lâmpada \
apaga. Cidadãos se cadastram no app com nome, e-mail, telefone e senha, reportam problemas num poste com \
uma descrição em texto livre e acompanham as ocorrências. Técnicos da manutenção assumem e resolvem as \
ocorrências e escrevem o que foi feito. Outros cidadãos veem as ocorrências de um poste, sem saber quem \
abriu; o técnico vê só o nome abreviado do autor. A API é .NET com PostgreSQL, o app é React Native e o \
código fica num repositório público do GitHub.
</contexto>

Para o cidadão:
- Português do Brasil, frases curtas, tom calmo e direto, sem alarmismo e sem minimizar.
- Troque termos técnicos por palavras comuns: em vez de "log", "registro interno do sistema"; em vez de \
"token" ou "JWT", "chave de acesso que o app guarda"; em vez de "repositório", "código publicado na internet".
- Diga qual dado está em jogo (telefone, CPF, condição de saúde...), quem poderia vê-lo e o que de ruim \
poderia acontecer na vida real.
- Use só o que a falha informa; se o dano depende de uma condição (alguém pegar o celular, ter acesso ao \
servidor), diga a condição.
- Se o status for "aceito", explique o que limita o dano e por que o risco ficou baixo.

Para a equipe:
- Em 2 a 4 frases, diga o que mudar e onde, e proponha o trecho corrigido no estilo do código mostrado.
- Cite o artigo da LGPD que justifica a correção."""

ESQUEMA_ALERTA = {
    "type": "object",
    "properties": {
        "titulo_cidadao": {"type": "string", "description": "Até 10 palavras, linguagem comum"},
        "alerta_cidadao": {"type": "string", "description": "2 ou 3 frases para o cidadão"},
        "o_que_pode_acontecer": {"type": "string", "description": "Consequência concreta, 1 frase"},
        "quem_e_afetado": {"type": "string", "description": "Quem corre o risco, 1 frase"},
        "nivel": {"type": "string", "enum": ["baixo", "moderado", "alto", "critico"]},
        "o_que_o_cidadao_pode_fazer": {"type": "string", "description": "1 frase prática"},
        "correcao_tecnica": {"type": "string", "description": "2 a 4 frases para a equipe"},
        "trecho_corrigido": {"type": "string", "description": "Código sugerido, curto"},
        "referencia_lgpd": {"type": "string", "description": "Artigo e o motivo, 1 frase"},
    },
    "required": ["titulo_cidadao", "alerta_cidadao", "o_que_pode_acontecer", "quem_e_afetado", "nivel",
                 "o_que_o_cidadao_pode_fazer", "correcao_tecnica", "trecho_corrigido", "referencia_lgpd"],
    "additionalProperties": False,
}

ESQUEMA_RESUMO = {
    "type": "object",
    "properties": {
        "manchete": {"type": "string", "description": "Uma frase para o topo do painel"},
        "resumo_executivo": {"type": "string", "description": "3 a 5 frases para a prefeitura"},
        "principais_riscos": {"type": "array", "items": {"type": "string"}},
        "proximos_passos": {"type": "array", "items": {"type": "string"}},
    },
    "required": ["manchete", "resumo_executivo", "principais_riscos", "proximos_passos"],
    "additionalProperties": False,
}


class SemIA(Exception):
    """A IA não está disponível nesta execução (sem chave, sem pacote ou chave recusada)."""


def _carregar_chave() -> None:
    if os.environ.get("ANTHROPIC_API_KEY") or not config.ARQUIVO_ENV.exists():
        return
    for linha in config.ARQUIVO_ENV.read_text(encoding="utf-8").splitlines():
        nome, _, valor = linha.strip().partition("=")
        if nome.strip() == "ANTHROPIC_API_KEY" and valor.strip():
            os.environ["ANTHROPIC_API_KEY"] = valor.strip().strip('"').strip("'")


class Tradutor:
    def __init__(self, usar_ia: bool = True):
        self.cache: dict[str, dict] = (json.loads(config.CACHE_IA.read_text(encoding="utf-8"))
                                       if config.CACHE_IA.exists() else {})
        self.uso = {"chamadas": 0, "do_cache": 0, "tokens_entrada": 0, "tokens_saida": 0, "modelos": []}
        self.cliente = None
        self.motivo_sem_ia = ""
        if not usar_ia:
            self.motivo_sem_ia = "auditoria rodada com --sem-ia"
            return
        _carregar_chave()
        if not os.environ.get("ANTHROPIC_API_KEY"):
            self.motivo_sem_ia = "ANTHROPIC_API_KEY não definida"
            return
        try:
            import anthropic
        except ImportError:
            self.motivo_sem_ia = "pacote anthropic não instalado no venv"
            return
        self._anthropic = anthropic
        self.cliente = anthropic.Anthropic(max_retries=3, timeout=180)

    # ------------------------------------------------------------------ chamadas
    def _chamar(self, prompt: str, esquema: dict) -> dict:
        if self.cliente is None:
            raise SemIA(self.motivo_sem_ia)
        anthropic = self._anthropic
        try:
            resposta = self.cliente.beta.messages.create(
                model=config.MODELO_IA,
                max_tokens=16000,
                betas=["server-side-fallback-2026-07-01"],
                fallbacks="default",  # se o modelo recusar, a própria API tenta um modelo substituto
                system=SISTEMA,
                messages=[{"role": "user", "content": prompt}],
                output_config={"effort": "medium", "format": {"type": "json_schema", "schema": esquema}},
            )
        except anthropic.AuthenticationError as e:
            self.cliente, self.motivo_sem_ia = None, "a chave da API foi recusada"
            raise SemIA(self.motivo_sem_ia) from e
        except anthropic.PermissionDeniedError as e:
            self.cliente, self.motivo_sem_ia = None, "a chave não tem permissão (verifique o crédito da conta)"
            raise SemIA(self.motivo_sem_ia) from e

        self.uso["chamadas"] += 1
        self.uso["tokens_entrada"] += resposta.usage.input_tokens
        self.uso["tokens_saida"] += resposta.usage.output_tokens
        if resposta.model not in self.uso["modelos"]:
            self.uso["modelos"].append(resposta.model)
        if resposta.stop_reason == "refusal":
            raise RuntimeError("o modelo recusou o pedido")
        texto = next(b.text for b in resposta.content if b.type == "text")
        return json.loads(texto) | {"origem": resposta.model}

    def _chave_cache(self, *partes: str) -> str:
        return hashlib.sha256("|".join((config.VERSAO_PROMPT, config.MODELO_IA, *partes)).encode()).hexdigest()[:20]

    # ------------------------------------------------------------------ alertas
    def traduzir(self, riscos: list[dict]) -> dict[str, dict]:
        """Devolve um alerta por achado (id do achado -> alerta)."""
        pendentes, resultado = [], {}
        for a in riscos:
            chave = self._chave_cache(a["id"], a["status"], a["mensagem"], a["trecho"])
            if chave in self.cache:
                resultado[a["id"]] = self.cache[chave]
                self.uso["do_cache"] += 1
            else:
                pendentes.append((chave, a))

        def um(item):
            chave, a = item
            try:
                return chave, a["id"], self._chamar(_prompt_alerta(a), ESQUEMA_ALERTA), None
            except Exception as e:  # noqa: BLE001 - um achado sem IA não derruba a auditoria
                return chave, a["id"], _alerta_sem_ia(a), f"{type(e).__name__}: {e}"

        with ThreadPoolExecutor(max_workers=4) as pool:
            for chave, id_, alerta, erro in pool.map(um, pendentes):
                resultado[id_] = alerta
                if erro is None:
                    self.cache[chave] = alerta
                elif not self.motivo_sem_ia:
                    self.motivo_sem_ia = erro
        self._salvar_cache()
        return resultado

    def resumir(self, retrato: dict) -> dict:
        prompt = _prompt_resumo(retrato)
        chave = self._chave_cache("resumo", prompt)
        if chave in self.cache:
            self.uso["do_cache"] += 1
            return self.cache[chave]
        try:
            resumo = self._chamar(prompt, ESQUEMA_RESUMO)
        except Exception as e:  # noqa: BLE001
            if not self.motivo_sem_ia:
                self.motivo_sem_ia = f"{type(e).__name__}: {e}"
            return _resumo_sem_ia(retrato)
        self.cache[chave] = resumo
        self._salvar_cache()
        return resumo

    def relatorio_de_uso(self) -> dict:
        custo = (self.uso["tokens_entrada"] * config.PRECO_ENTRADA_MTOK
                 + self.uso["tokens_saida"] * config.PRECO_SAIDA_MTOK) / 1_000_000
        return self.uso | {"custo_estimado_usd": round(custo, 4), "modelo_pedido": config.MODELO_IA,
                           "sem_ia": self.motivo_sem_ia or None}

    def _salvar_cache(self) -> None:
        config.CACHE_IA.parent.mkdir(parents=True, exist_ok=True)
        config.CACHE_IA.write_text(json.dumps(self.cache, ensure_ascii=False, indent=1), encoding="utf-8")


def _prompt_alerta(a: dict) -> str:
    categoria = config.ROTULO_CATEGORIA.get(a["categoria"], a["categoria"])
    saida = config.ROTULO_SAIDA.get(a["saida"], a["saida"])
    aceite = ""
    if a.get("aceite"):
        aceite = f"\n- Risco aceito pela equipe. Justificativa: {a['aceite'].get('justificativa', '')}"
    trecho = f"\n<trecho arquivo=\"{a['arquivo']}\">\n{a['trecho']}\n</trecho>" if a["trecho"] else ""
    return f"""<falha>
- Regra: {a['regra']} ({a['titulo']})
- Gravidade técnica: {a['severidade']}
- Status: {a['status']}{aceite}
- Tipo de dado: {categoria}
- Para onde o dado vai: {saida}
- LGPD: {a['lgpd']} | {a['cwe']}
- Local: {a['arquivo']}, linha {a['linha']} ({a['camada']})
- Explicação da regra: {a['mensagem']}{trecho}
</falha>

Escreva o alerta para o cidadão e a orientação para a equipe sobre esta falha. O trecho já vem com
senhas e dados pessoais mascarados por ***."""


def _prompt_resumo(r: dict) -> str:
    abertos = [a for a in r["achados"] if a["status"] == "aberto"]
    aceitos = [a for a in r["achados"] if a["status"] == "aceito"]
    din = r.get("dinamico") or {}
    motor = r.get("motor") or {}
    linhas = "\n".join(f"- [{a['severidade']}] {a['titulo']} ({a['arquivo']})" for a in abertos) or "- nenhum"
    linhas_aceitas = "\n".join(f"- {a['titulo']}: {a['aceite'].get('justificativa', '')}" for a in aceitos) or "- nenhum"
    return f"""<auditoria rotulo="{r['rotulo']}" commit="{r['commit']}">
Risco Cidadão: {r['risco']['valor']} de 100 ({r['risco']['faixa']}). Contribuição por tipo de dado: {r['risco']['por_categoria']}
Dados pessoais mapeados no código: {len(r['mapa'])} campos.
Achados abertos ({len(abertos)}):
{linhas}
Riscos aceitos ({len(aceitos)}):
{linhas_aceitas}
Teste dinâmico com canários: {din.get('saidas_verificadas', 0)} saídas verificadas, {len(din.get('vazamentos', []))} vazamentos ({'simulado com o filtro desligado' if din.get('simulado') else 'código atual'}).
Motor de PII: {('precisão ' + str(motor.get('precisao')) + ', cobertura ' + str(motor.get('cobertura')) + ' em ' + str(motor.get('casos')) + ' casos') if motor else 'ainda não existia nesta versão'}.
</auditoria>

Escreva o resumo executivo deste retrato da auditoria para a gestão da prefeitura: uma manchete, um
resumo de 3 a 5 frases, até 3 principais riscos para o cidadão e até 4 próximos passos."""


def _alerta_sem_ia(a: dict) -> dict:
    nivel = {"ERROR": "alto", "WARNING": "moderado"}.get(a["severidade"], "baixo")
    if a["status"] == "aceito":
        nivel = "baixo"
    return {
        "titulo_cidadao": a["titulo"], "alerta_cidadao": a["mensagem"], "o_que_pode_acontecer": "",
        "quem_e_afetado": "", "nivel": nivel, "o_que_o_cidadao_pode_fazer": "",
        "correcao_tecnica": "", "trecho_corrigido": "", "referencia_lgpd": a["lgpd"],
        "origem": "texto da regra (sem IA)",
    }


def _resumo_sem_ia(r: dict) -> dict:
    abertos = sum(1 for a in r["achados"] if a["status"] == "aberto")
    return {
        "manchete": (f"Risco Cidadão {str(r['risco']['valor']).replace('.', ',')} ({r['risco']['faixa']}) "
                     f"com {abertos} achado(s) aberto(s)"),
        "resumo_executivo": "Resumo gerado sem IA: defina ANTHROPIC_API_KEY e rode a auditoria de novo para o texto completo.",
        "principais_riscos": [a["titulo"] for a in r["achados"] if a["status"] == "aberto"][:3],
        "proximos_passos": [],
        "origem": "texto automático (sem IA)",
    }
