"""Captura os prints do Azure Boards (listas e detalhe de cada item) e monta o PDF de entrega de Testing.

Abre o Microsoft Edge com um perfil dedicado; o login na conta Microsoft é feito por você na janela
que abrir. Os itens vêm da própria API do Azure (tag hakari-gs) e são conferidos contra o
azure-boards.json: se faltar algum item ou houver título repetido, nada é capturado.

Uso:
    python capturar-prints.py --nome "Seu Nome" --rm 123456
    python capturar-prints.py --nome "Seu Nome" --rm 123456 --so-pdf   # remonta o PDF com os prints já salvos
"""
import argparse
import json
import sys
from collections import Counter
from pathlib import Path
from urllib.parse import quote

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
TAG = 'hakari-gs'
PAGE_WIDTH = 1654  # A4 a 150 dpi
FONT = 'C:/Windows/Fonts/arial.ttf'
FONT_BOLD = 'C:/Windows/Fonts/arialbd.ttf'

# Roda dentro da página do Azure, autenticada pelos cookies da sessão.
FETCH_ITEMS_JS = """
async ([org, project, tag]) => {
  const api = `https://dev.azure.com/${org}/${encodeURIComponent(project)}/_apis/wit`;
  const opts = { credentials: 'include', headers: { 'X-TFS-FedAuthRedirect': 'Suppress' } };
  const wiql = { query: `SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.Tags] CONTAINS '${tag}'` };
  const r = await fetch(`${api}/wiql?api-version=7.1`, {
    ...opts, method: 'POST',
    headers: { ...opts.headers, 'Content-Type': 'application/json' },
    body: JSON.stringify(wiql),
  });
  if (!r.ok) return { status: r.status };
  const ids = (await r.json()).workItems.map(w => w.id);
  const items = [];
  for (let i = 0; i < ids.length; i += 200) {
    const batch = ids.slice(i, i + 200).join(',');
    const r2 = await fetch(`${api}/workitems?ids=${batch}&fields=System.Title,System.WorkItemType&api-version=7.1`, opts);
    if (!r2.ok) return { status: r2.status };
    for (const w of (await r2.json()).value) {
      items.push({ id: w.id, type: w.fields['System.WorkItemType'], title: w.fields['System.Title'] });
    }
  }
  return { status: 200, items };
}
"""

# Espera o formulário do item mostrar o título (no campo de edição ou no texto da página).
TITLE_VISIBLE_JS = """
(title) => [...document.querySelectorAll('input, textarea')].some(e => e.value === title)
        || document.body.innerText.includes(title)
"""


def expected_items():
    data = json.loads((HERE / 'azure-boards.json').read_text(encoding='utf-8'))
    expected = [('Epic', data['epic']['title'])]
    expected += [('Feature', f['title']) for f in data['features']]
    expected += [('Test Case', f"{tc['id']} {tc['title']}") for tc in data['testCases']]
    return expected


def validate(found, expected):
    """Garante um item por título esperado, sem repetidos e sem faltantes."""
    counts = Counter((i['type'], i['title']) for i in found)
    duplicated = [k for k, n in counts.items() if n > 1]
    missing = [k for k in expected if k not in counts]
    extra = [k for k in counts if k not in set(expected)]
    if duplicated or missing or extra:
        for label, keys in (('Repetidos', duplicated), ('Faltando', missing), ('Inesperados', extra)):
            for key in keys:
                print(f'  {label}: {key[0]} "{key[1]}"')
        sys.exit('Os itens no Azure não batem com o azure-boards.json. Corrija antes de capturar.')
    by_key = {(i['type'], i['title']): i for i in found}
    return [by_key[k] for k in expected]


def query_url(base, item_type):
    wiql = (
        'SELECT [System.Id], [System.Title], [System.State], [System.Tags] FROM WorkItems '
        f"WHERE [System.TeamProject] = @project AND [System.WorkItemType] = '{item_type}' "
        f"AND [System.Tags] CONTAINS '{TAG}' ORDER BY [System.Title]"
    )
    return f'{base}/_queries/query/?wiql={quote(wiql)}'


def capture(args, prints):
    from playwright.sync_api import sync_playwright

    base = f'https://dev.azure.com/{args.org}/{quote(args.project)}'
    expected = expected_items()
    profile = Path.home() / 'AppData' / 'Local' / 'hakari-azure-profile'

    with sync_playwright() as p:
        ctx = p.chromium.launch_persistent_context(
            str(profile), channel='msedge', headless=False, viewport={'width': 1400, 'height': 1800})
        page = ctx.pages[0] if ctx.pages else ctx.new_page()
        page.goto(base)

        print('Se pedir, faça login na janela do Edge. Aguardando acesso ao projeto...')
        for _ in range(100):  # até ~5 minutos
            if page.url.startswith('https://dev.azure.com/'):
                result = page.evaluate(FETCH_ITEMS_JS, [args.org, args.project, TAG])
                if result['status'] == 200:
                    break
            page.wait_for_timeout(3000)
        else:
            sys.exit('Não consegui acessar o projeto em 5 minutos. Confira o login e o nome da organização.')

        items = validate(result['items'], expected)
        print(f'{len(items)} itens conferidos, sem repetidos. Capturando...')

        # Espera o último título da lista para garantir que todas as linhas carregaram.
        lists = [
            ('00a-lista-features.png', 'Feature', items[8]['title']),
            ('00b-lista-test-cases.png', 'Test Case', items[-1]['title']),
        ]
        for name, item_type, last_title in lists:
            page.set_viewport_size({'width': 1400, 'height': 2600 if item_type == 'Test Case' else 1000})
            page.goto(query_url(base, item_type))
            page.wait_for_function(TITLE_VISIBLE_JS, arg=last_title, timeout=60000)
            page.wait_for_timeout(1500)
            page.screenshot(path=str(prints / name))
            print(f'  {name}')

        page.set_viewport_size({'width': 1400, 'height': 1800})
        for n, item in enumerate(items, start=1):
            name = f"{n:02d}-{item['type'].replace(' ', '')}-{item['id']}.png"
            page.goto(f"{base}/_workitems/edit/{item['id']}")
            page.wait_for_function(TITLE_VISIBLE_JS, arg=item['title'], timeout=60000)
            page.wait_for_timeout(1500)
            page.screenshot(path=str(prints / name))
            print(f"  {name}  {item['title']}")

        ctx.close()


def font(size, bold=False):
    return ImageFont.truetype(FONT_BOLD if bold else FONT, size)


def cover(args):
    page = Image.new('RGB', (PAGE_WIDTH, 2339), 'white')
    draw = ImageDraw.Draw(page)
    lines = [
        ('FIAP — Global Solution 2026/2 — CitizenTech', 40, False),
        ('Testing, Compliance & Quality Assurance', 56, True),
        ('', 30, False),
        ('Hakari — iluminação pública que se reporta sozinha', 44, True),
        ('', 30, False),
        (f'Aluno: {args.nome}', 36, False),
        (f'RM: {args.rm}', 36, False),
        ('', 30, False),
        ('Projeto no Azure DevOps:', 36, True),
        (f'https://dev.azure.com/{args.org}/{quote(args.project)}', 34, False),
        ('', 30, False),
        ('Conteúdo: lista e detalhe das 8 Features e dos 43 Test Cases', 32, False),
        ('(funcionais, caixa-preta, positivos e negativos).', 32, False),
    ]
    y = 500
    for text, size, bold in lines:
        draw.text((140, y), text, fill='black', font=font(size, bold))
        y += size + 28
    return page


def labeled(image_path):
    """Página com uma faixa de identificação acima do print."""
    shot = Image.open(image_path).convert('RGB')
    scale = PAGE_WIDTH / shot.width
    shot = shot.resize((PAGE_WIDTH, round(shot.height * scale)))
    band = 90
    page = Image.new('RGB', (PAGE_WIDTH, shot.height + band), 'white')
    ImageDraw.Draw(page).text((40, 25), image_path.stem, fill='black', font=font(34, True))
    page.paste(shot, (0, band))
    return page


def build_pdf(args, prints, output):
    shots = sorted(prints.glob('*.png'))
    if not shots:
        sys.exit(f'Nenhum print em {prints}.')
    pages = [labeled(s) for s in shots]
    cover(args).save(output, save_all=True, append_images=pages, resolution=150)
    print(f'PDF gerado: {output} ({len(pages) + 1} páginas)')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--org', default='ProjectHAKARI')
    parser.add_argument('--project', default='HAKARI_')
    parser.add_argument('--nome', required=True)
    parser.add_argument('--rm', required=True)
    parser.add_argument('--saida', default=str(HERE / 'entrega'))
    parser.add_argument('--so-pdf', action='store_true', help='não captura, só monta o PDF com os prints existentes')
    args = parser.parse_args()

    out = Path(args.saida)
    prints = out / 'prints'
    prints.mkdir(parents=True, exist_ok=True)
    if not args.so_pdf:
        for old in prints.glob('*.png'):
            old.unlink()
        capture(args, prints)
    build_pdf(args, prints, out / 'Hakari - Testing.pdf')


if __name__ == '__main__':
    main()
