"""Teste de fumaça da API Hakari: percorre as Features F3 a F8 pela HTTP e confere os códigos.

Uso: python scripts/smoke_test.py [http://localhost:5080]
A senha do técnico de demonstração é lida de src/Hakari.Api/appsettings.Development.json.
"""
import json
import sys
import uuid
from pathlib import Path

import requests

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080").rstrip("/")
CONFIG = Path(__file__).resolve().parents[1] / "src/Hakari.Api/appsettings.Development.json"
falhas = 0


def checar(nome, resposta, esperado):
    global falhas
    ok = resposta.status_code == esperado
    falhas += not ok
    detalhe = ""
    if not ok or resposta.status_code >= 400:
        try:
            detalhe = resposta.json().get("detail") or ""
        except ValueError:
            pass
    print(f"[{'OK' if ok else 'FALHOU'}] {nome}: {resposta.status_code} (esperado {esperado}) {detalhe}")
    return resposta


def auth(token):
    return {"Authorization": f"Bearer {token}"}


def cadastrar(nome):
    email = f"{nome.split()[0].lower()}.{uuid.uuid4().hex[:6]}@exemplo.com"
    r = checar(f"F3 cadastro de {nome}", requests.post(f"{BASE}/api/auth/cadastro", json={
        "nome": nome, "email": email, "telefone": "(11) 98888-7777", "senha": "senhaTeste1", "aceitouTermo": True}), 201)
    return r.json()["token"], email


# ---------- F3: cadastro e login ----------
ana, email_ana = cadastrar("Ana Paula Souza")
bruno, _ = cadastrar("Bruno Lima")
checar("F3 e-mail repetido", requests.post(f"{BASE}/api/auth/cadastro", json={
    "nome": "Outra Ana", "email": email_ana, "telefone": "11988887777", "senha": "senhaTeste1", "aceitouTermo": True}), 409)
checar("F3 senha fraca", requests.post(f"{BASE}/api/auth/cadastro", json={
    "nome": "Carlos", "email": "carlos@exemplo.com", "telefone": "11988887777", "senha": "abc", "aceitouTermo": True}), 400)
checar("F3 termo não aceito", requests.post(f"{BASE}/api/auth/cadastro", json={
    "nome": "Carlos", "email": "carlos@exemplo.com", "telefone": "11988887777", "senha": "senhaTeste1", "aceitouTermo": False}), 400)
checar("F3 login inválido", requests.post(f"{BASE}/api/auth/login", json={"email": email_ana, "senha": "errada123"}), 401)

senha_tecnico = json.loads(CONFIG.read_text(encoding="utf-8"))["Seed"]["SenhaTecnico"]
tecnico = checar("F3 login do técnico", requests.post(f"{BASE}/api/auth/login", json={
    "email": "tecnico@hakari.dev", "senha": senha_tecnico}), 200).json()["token"]

# ---------- F4: postes ----------
checar("F4 sem token", requests.get(f"{BASE}/api/postes"), 401)
postes = checar("F4 lista de postes", requests.get(f"{BASE}/api/postes", headers=auth(ana)), 200).json()
print("     ", [(p["codigo"], p["status"]) for p in postes])
alvo = next(p for p in postes if p["codigo"] == "HKR-004")
checar("F4 detalhe do poste", requests.get(f"{BASE}/api/postes/{alvo['id']}", headers=auth(ana)), 200)
checar("F4 poste inexistente", requests.get(f"{BASE}/api/postes/99999", headers=auth(ana)), 404)

# ---------- F5: reporte ----------
url_rep = f"{BASE}/api/postes/{alvo['id']}/reportes"
checar("F5 descrição vazia", requests.post(url_rep, headers=auth(ana), json={"categoria": "LampadaPiscando", "descricao": " "}), 400)
checar("F5 poste inexistente", requests.post(f"{BASE}/api/postes/99999/reportes", headers=auth(ana),
                                             json={"categoria": "Outro", "descricao": "teste"}), 404)
rep = requests.post(url_rep, headers=auth(ana), json={"categoria": "LampadaPiscando", "descricao": "Lâmpada piscando a noite toda"})
if rep.status_code == 200:
    print("      (o poste já tinha ocorrência ativa de uma execução anterior)")
oc_id = rep.json()["ocorrenciaId"]
print(f"      ocorrência #{oc_id}: {rep.json()['mensagem']}")
apoio = checar("F5 segundo cidadão vira apoio", requests.post(url_rep, headers=auth(bruno),
                                                            json={"categoria": "LampadaPiscando", "descricao": "Também vi"}), 200)
print("     ", apoio.json()["mensagem"])
checar("F5 apoio repetido", requests.post(f"{BASE}/api/ocorrencias/{oc_id}/apoios", headers=auth(bruno)), 409)

# ---------- F6: minhas ocorrências e privacidade ----------
minhas = checar("F6 minhas ocorrências (Bruno)", requests.get(f"{BASE}/api/me/ocorrencias", headers=auth(bruno)), 200).json()
assert any(o["id"] == oc_id for o in minhas), "apoio deveria aparecer em Minhas ocorrências"
outro, _ = cadastrar("Carla Dias")
det = checar("F6 detalhe visto por outro cidadão", requests.get(f"{BASE}/api/ocorrencias/{oc_id}", headers=auth(outro)), 200).json()
print(f"      autor visto por outro cidadão: {det['autor']!r}")

# ---------- F7: painel da manutenção ----------
checar("F7 cidadão no painel", requests.get(f"{BASE}/api/manutencao/fila", headers=auth(ana)), 403)
fila = checar("F7 fila do técnico", requests.get(f"{BASE}/api/manutencao/fila", headers=auth(tecnico)), 200).json()
print("     ", [(o["id"], o["severidade"], o["posteCodigo"]) for o in fila])
checar("F7 resolver sem assumir", requests.post(f"{BASE}/api/ocorrencias/{oc_id}/resolver", headers=auth(tecnico),
                                                json={"solucao": "trocada"}), 422)
det = checar("F7 assumir", requests.post(f"{BASE}/api/ocorrencias/{oc_id}/assumir", headers=auth(tecnico)), 200).json()
print(f"      status: {det['resumo']['status']} | autor visto pelo técnico: {det['autor']!r}")
det = checar("F7 resolver", requests.post(f"{BASE}/api/ocorrencias/{oc_id}/resolver", headers=auth(tecnico),
                                          json={"solucao": "Reator da lâmpada substituído"}), 200).json()
for h in det["historico"]:
    print(f"        {h['registradoEm'][:19]}  {h['status']:<15} {h['descricao']}")
checar("F7 resumo por bairro", requests.get(f"{BASE}/api/manutencao/bairros", headers=auth(tecnico)), 200)

# ---------- F8: meus dados e exclusão ----------
dados = checar("F8 meus dados", requests.get(f"{BASE}/api/me", headers=auth(ana)), 200).json()
print(f"      {dados['nome']} | {dados['email']} | {dados['telefone']}")
checar("F8 excluir conta", requests.delete(f"{BASE}/api/me", headers=auth(ana)), 204)
det = checar("F8 ocorrência continua sem autor", requests.get(f"{BASE}/api/ocorrencias/{oc_id}", headers=auth(tecnico)), 200).json()
print(f"      autor depois da exclusão: {det['autor']!r}")
checar("F8 conta excluída não entra", requests.post(f"{BASE}/api/auth/login", json={"email": email_ana, "senha": "senhaTeste1"}), 401)

checar("health", requests.get(f"{BASE}/health"), 200)
print(f"\n{'Tudo certo' if not falhas else f'{falhas} verificação(ões) falharam'}")
sys.exit(1 if falhas else 0)
