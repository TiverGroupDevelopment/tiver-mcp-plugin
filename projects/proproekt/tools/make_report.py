"""Отчёт по семействам ПроПроекта: проверка каждого семейства по требованиям + картинки.

    python -I make_report.py <jobs.json> <build-log.jsonl> <проверка: verify.jsonl, rules.jsonl, картинки/> <ОТЧЁТ.md> <отчёт.html>
"""
import base64
import html
import io
import json
import os
import sys
from collections import Counter

from PIL import Image

CAT = {'OST_Furniture': 'Мебель', 'OST_SpecialityEquipment': 'Специальное оборудование', 'OST_GenericModel': 'Обобщенные модели',
       'OST_Casework': 'Шкафы', 'OST_MechanicalEquipment': 'Оборудование', 'OST_PlumbingFixtures': 'Сантехнические приборы',
       'OST_ElectricalFixtures': 'Электрические приборы'}
KIND = {'box': 'кубик', 'cylinder': 'цилиндр', 'rebuild': 'перенос'}
REQ = [
    ('file', 'Файл .rfa в rvt/rfa, имя = имя семейства (PP-03, PP-05)'),
    ('v2019', 'Сохранён в Revit 2019 (PP-03)'),
    ('category', 'Категория по заданию, допустима в 2019 (PP-10)'),
    ('name', 'ADSK_Наименование: общий, GUID из ФОП, тип, значение = колонка G (PP-04)'),
    ('mark', 'ADSK_Марка: общий, GUID из ФОП, тип, значение = колонка J (PP-04)'),
    ('pos', 'ADSK_Позиция: общий, GUID из ФОП, тип, значение = колонка I (PP-04)'),
    ('type', 'Один типоразмер с именем семейства (PP-05)'),
    ('size', 'Габарит = заданию: ±1 мм по спецификации, ±5 мм при допущении (PP-08)'),
    ('origin', 'Точка вставки: центр в плане, низ на 0 / на грани (PP-09)'),
    ('geo', 'Нет импорта, свободных форм, DirectShape (GEO-01)'),
    ('rules', 'check_family_rules профиль proproekt: нет fail'),
]


def load_jsonl(path):
    rows = {}
    if os.path.exists(path):
        for line in open(path, encoding='utf-8'):
            d = json.loads(line)
            rows[d['family']] = d
    return rows


def evaluate(job, b, v, r):
    res, why = {}, {}
    fam = job['family']
    assumed = any('габаритов нет' in n for n in job['notes'])
    if not v or v.get('error'):
        for key, _ in REQ:
            res[key] = False
        why['file'] = (v or {}).get('error', 'нет проверки')
        return res, why, assumed
    res['file'] = os.path.basename(v['path']) == fam + '.rfa' and v.get('title') == fam
    why['file'] = v.get('title')
    res['v2019'] = '2019' in (v.get('savedInVersion') or '')
    why['v2019'] = v.get('savedInVersion')
    res['category'] = v.get('builtInCategory') == job['category'] and job['category'] in CAT
    why['category'] = v.get('category')
    a = v.get('adsk') or {}

    def par(name, instance, want):
        p = a.get(name)
        ok = bool(p) and p['shared'] and p['guidMatchesFop'] and p['instance'] == instance and (p['value'] or '') == want
        return ok, (f"«{p['value']}», {'экз.' if p['instance'] else 'тип'}, GUID {'= ФОП' if p['guidMatchesFop'] else '≠ ФОП'}" if p else 'нет')

    res['name'], why['name'] = par('ADSK_Наименование', False, job['adskName'])
    res['mark'], why['mark'] = par('ADSK_Марка', False, job['adskMark'])
    res['pos'], why['pos'] = par('ADSK_Позиция', False, job['position'])
    res['type'] = v.get('types') == [fam]
    why['type'] = ', '.join(v.get('types') or [])
    size = v.get('size') or [0, 0, 0]
    want = job['size']
    dev = max(abs(size[k] - want[k]) for k in range(3))
    tol = 5.0 if assumed else 1.0
    res['size'] = dev <= tol
    why['size'] = f"{'×'.join(f'{x:g}' for x in size)} (Δ {dev:.1f})"
    c = v.get('center') or [99, 99, 99]
    if job['faceBased']:
        res['origin'] = abs(c[0]) <= 1 and abs(c[2]) <= 1
    else:
        res['origin'] = abs(c[0]) <= 1 and abs(c[1]) <= 1 and abs(c[2]) <= 1
    why['origin'] = f"центр X {c[0]:g}, Y {c[1]:g}, низ {c[2]:g}"
    res['geo'] = v.get('imports') == 0 and v.get('freeForms') == 0 and v.get('directShapes') == 0
    why['geo'] = f"импорт {v.get('imports')}, свободных {v.get('freeForms')}, DirectShape {v.get('directShapes')}"
    resp = ((r or {}).get('response') or {}).get('Response') or {}
    summary = resp.get('summary') or {}
    res['rules'] = bool(summary) and summary.get('fail', 1) == 0
    why['rules'] = f"pass {summary.get('pass', 0)}, warn {summary.get('warn', 0)}, fail {summary.get('fail', '?')}" if summary else 'нет ответа'
    return res, why, assumed


def thumb(path, width=640):
    if not path or not os.path.exists(path):
        return None
    im = Image.open(path)
    im.thumbnail((width, width))
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=66)
    return 'data:image/jpeg;base64,' + base64.b64encode(buf.getvalue()).decode()


def main(jobs_path, build_path, proof_dir, md_path, html_path):
    jobs = json.load(open(jobs_path, encoding='utf-8'))
    build = load_jsonl(build_path)
    verify = load_jsonl(os.path.join(proof_dir, 'verify.jsonl'))
    rules = load_jsonl(os.path.join(proof_dir, 'rules.jsonl'))
    rows = []
    for i, job in enumerate(jobs, 1):
        b = build.get(job['family'], {})
        res, why, assumed = evaluate(job, b, verify.get(job['family']), rules.get(job['family']))
        forms = b.get('forms') or {}
        rows.append(dict(i=i, job=job, b=b, res=res, why=why, assumed=assumed, forms=forms,
                         ok=all(res.values()), img=os.path.join(proof_dir, 'картинки', job['family'] + '.jpg')))

    total = len(rows)
    passed = sum(r['ok'] for r in rows)
    per_req = {k: sum(r['res'][k] for r in rows) for k, _ in REQ}
    kinds = Counter(r['job']['kind'] for r in rows)
    simplified = [r for r in rows if (r['forms'].get('simplified') or 0) > 0 or (r['forms'].get('skipped') or 0) > 0]
    forms_ok = sum(r['forms'].get('ok', 0) for r in rows)
    forms_simpl = sum(r['forms'].get('simplified', 0) for r in rows)
    forms_skip = sum(r['forms'].get('skipped', 0) for r in rows)
    assumed = [r for r in rows if r['assumed']]
    mark = lambda ok: '✓' if ok else '✗'

    # ---------- Markdown ----------
    L = ['# Отчёт: семейства ПроПроекта в Revit 2019', '',
         f'Семейств по плану: **{total}**, построено и прошло все проверки: **{passed}**.', '',
         'Каждое семейство открыто заново в Revit 2019 скриптом `verify_families` и проверено командой плагина `check_family_rules` '
         '(профиль `proproekt`). Доказательства по каждому семейству — в таблице ниже и в HTML-отчёте с картинками '
         '(`ПроПроект3/моделирование/отчёт.html`): изометрия и три проекции с габаритами из геометрии готового `.rfa`.', '',
         '## Требования и результат', '', '| Требование | Выполнено |', '|---|---|']
    L += [f'| {title} | {per_req[k]} из {total} |' for k, title in REQ]
    L += ['', '## Как построены', '',
          f'- Кубиков: {kinds["box"]}, цилиндров: {kinds["cylinder"]}, перенесено исходных семейств: {kinds["rebuild"]}.',
          f'- Форм исходников: точно {forms_ok}, упрощено {forms_simpl} (сдвиг → брусья по пути или короб по габариту формы), пропущено {forms_skip}.',
          f'- Семейств с упрощёнными формами: {len(simplified)} — список ниже.',
          f'- Допущений по габаритам (нет в спецификации): {len(assumed)} — см. [план моделирования](план-моделирования.md).', '',
          '## Семейства', '',
          '| № | Семейство | Способ | ' + ' | '.join(k for k, _ in REQ) + ' | Габарит факт (Δ) | Правила | Формы т/у/п |',
          '|---|---|---|' + '---|' * len(REQ) + '---|---|---|']
    for r in rows:
        f = r['forms']
        L.append(f"| {r['i']} | {r['job']['family']} | {KIND[r['job']['kind']]} | " + ' | '.join(mark(r['res'][k]) for k, _ in REQ) +
                 f" | {r['why'].get('size', '')} | {r['why'].get('rules', '')} | {f.get('ok', 0)}/{f.get('simplified', 0)}/{f.get('skipped', 0)} |")
    L += ['', 'Столбцы: ' + '; '.join(f'**{k}** — {t}' for k, t in REQ) + '. «т/у/п» — форм построено точно / упрощено / пропущено.', '',
          '## Упрощения LOD200', '']
    for r in simplified:
        L.append(f"- **{r['job']['family']}**: " + '; '.join(r['b'].get('log') or []))
    failed = [r for r in rows if not r['ok']]
    if failed:
        L += ['', '## Не прошли проверку', '']
        for r in failed:
            bad = [k for k, _ in REQ if not r['res'][k]]
            L.append(f"- **{r['job']['family']}**: " + '; '.join(f'{k}: {r["why"].get(k)}' for k in bad))
    open(md_path, 'w', encoding='utf-8').write('\n'.join(L) + '\n')

    # ---------- HTML ----------
    css = """
/* Layout: инженерный лист проверки — сводка требований сверху, ниже карточка на семейство (картинка | протокол). */
:root {
  --bg: #f6f7f8; --surface: #ffffff; --fg: #1b2129; --muted: #5b6573; --line: #dde1e6;
  --accent: #2c5f7a; --ok: #1e7a45; --bad: #b3261e;
  --font-body: "IBM Plex Sans", "Segoe UI", Arial, sans-serif;
  --font-mono: "IBM Plex Mono", Consolas, monospace;
}
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {
  --bg: #121519; --surface: #1a1e24; --fg: #e6e9ed; --muted: #9aa4b1; --line: #2b3139;
  --accent: #7fb3d1; --ok: #62c98d; --bad: #ff8a80; color-scheme: dark } }
:root[data-theme="dark"] {
  --bg: #121519; --surface: #1a1e24; --fg: #e6e9ed; --muted: #9aa4b1; --line: #2b3139;
  --accent: #7fb3d1; --ok: #62c98d; --bad: #ff8a80; color-scheme: dark }
body { background: var(--bg); color: var(--fg); font: 14px/1.5 var(--font-body); }
main { max-width: 1180px; margin: 0 auto; padding-block: 28px 48px;
  padding-left: max(16px, env(safe-area-inset-left, 0px)); padding-right: max(16px, env(safe-area-inset-right, 0px)); }
h1 { font-size: 26px; font-weight: 600; margin: 0 0 6px; text-wrap: balance; }
h2 { font-size: 17px; font-weight: 600; margin: 32px 0 12px; }
.lead { color: var(--muted); max-width: 68ch; margin: 0; }
.eyebrow { font: 500 12px/1 var(--font-mono); letter-spacing: .06em; text-transform: uppercase; color: var(--accent); margin-bottom: 10px; }
.figures { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 12px; margin-top: 20px; }
.figure { background: var(--surface); border: 1px solid var(--line); border-radius: 6px; padding: 12px 14px; }
.figure b { display: block; font: 600 24px/1.1 var(--font-mono); font-variant-numeric: tabular-nums; }
.figure span { color: var(--muted); font-size: 13px; }
.scroll { overflow-x: auto; }
table { border-collapse: collapse; width: 100%; }
th, td { text-align: left; vertical-align: top; padding: 6px 10px; border-bottom: 1px solid var(--line); }
th { font-weight: 600; color: var(--muted); font-size: 13px; }
td.num { font-family: var(--font-mono); font-variant-numeric: tabular-nums; white-space: nowrap; }
.ok { color: var(--ok); font-weight: 600; } .bad { color: var(--bad); font-weight: 600; }
.tools { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; margin-bottom: 12px; }
.tools input[type=search] { flex: 1 1 260px; max-width: 420px; padding: 8px 10px; border: 1px solid var(--line); border-radius: 6px;
  background: var(--surface); color: var(--fg); font: inherit; }
.tools label { color: var(--muted); display: flex; gap: 6px; align-items: center; }
input:focus-visible { outline: 2px solid var(--accent); outline-offset: 1px; }
.cards { display: grid; gap: 14px; }
.card { background: var(--surface); border: 1px solid var(--line); border-radius: 8px; overflow: hidden; }
.card header { display: flex; flex-wrap: wrap; gap: 6px 12px; align-items: baseline; padding: 10px 14px; border-bottom: 1px solid var(--line); }
.card header .pos { font: 600 13px var(--font-mono); color: var(--accent); }
.card header h3 { margin: 0; font-size: 15px; font-weight: 600; min-width: 0; overflow-wrap: anywhere; flex: 1 1 320px; }
.card .body { display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr); }
.card figure { margin: 0; background: #ffffff; border-right: 1px solid var(--line); }
.card figure img { display: block; width: 100%; height: auto; max-width: 100%; }
.card .proto { min-width: 0; }
.card .proto td { font-size: 13px; padding: 5px 12px; }
.card .proto td:first-child { color: var(--muted); width: 38%; }
.note { color: var(--muted); }
@media (max-width: 760px) { .card .body { grid-template-columns: 1fr; } .card figure { border-right: 0; border-bottom: 1px solid var(--line); } }
"""
    H = ['<title>Семейства ПроПроекта 2019</title>',
         '<meta name="claude:viewport-fit-sides" content="cover">',
         '<link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>',
         '<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@500;600&family=IBM+Plex+Sans:wght@400;600&display=swap">',
         '<style>' + css + '</style><main>',
         '<div class="eyebrow">1747-Э/ПИР-2025 · ИОС6.ТХ1 · Revit 2019 · LOD200</div>',
         '<h1>Семейства ПроПроекта в Revit 2019</h1>',
         '<p class="lead">Протокол проверки готовых файлов .rfa. Каждое семейство заново открыто в Revit 2019 (скрипт verify_families) и проверено '
         'командой check_family_rules с правилами профиля proproekt. Картинки нарисованы по геометрии готового файла: изометрия, фасад, вид сбоку и план с габаритами.</p>',
         '<div class="figures">',
         f'<div class="figure"><b>{total}</b><span>семейств по плану</span></div>',
         f'<div class="figure"><b>{passed}</b><span>прошли все проверки</span></div>',
         f'<div class="figure"><b>{kinds["rebuild"]}</b><span>перенесено исходных семейств</span></div>',
         f'<div class="figure"><b>{kinds["box"] + kinds["cylinder"]}</b><span>кубиков и цилиндров</span></div>',
         f'<div class="figure"><b>{forms_ok} / {forms_simpl} / {forms_skip}</b><span>форм точно / упрощено / пропущено</span></div>',
         '</div>',
         '<h2>Требования</h2><div class="scroll"><table><thead><tr><th>Требование</th><th>Выполнено</th></tr></thead><tbody>']
    for k, title in REQ:
        cls = 'ok' if per_req[k] == total else 'bad'
        H.append(f'<tr><td>{html.escape(title)}</td><td class="num {cls}">{per_req[k]} из {total}</td></tr>')
    H.append('</tbody></table></div>')
    H.append('<h2>Семейства</h2><div class="tools"><input type="search" id="q" placeholder="Позиция или наименование" aria-label="Фильтр">'
             '<label for="onlybad"><input type="checkbox" id="onlybad"> только с замечаниями</label>'
             '<label for="onlysimpl"><input type="checkbox" id="onlysimpl"> только с упрощениями</label></div><div class="cards">')
    for r in rows:
        j = r['job']
        img = thumb(r['img'], 560)
        state = '<span class="ok">все проверки ✓</span>' if r['ok'] else '<span class="bad">есть замечания</span>'
        simpl = 1 if (r['forms'].get('simplified') or r['forms'].get('skipped')) else 0
        H.append(f'<article class="card" data-name="{html.escape(j["family"].lower())}" data-bad="{0 if r["ok"] else 1}" data-simpl="{simpl}">'
                 f'<header><span class="pos">{r["i"]:03d}</span><h3>{html.escape(j["family"])}</h3>{state}</header><div class="body">')
        H.append('<figure>' + (f'<img loading="lazy" alt="Изометрия и проекции {html.escape(j["family"])}" src="{img}">' if img else '<p class="note">нет картинки</p>') + '</figure>')
        H.append('<div class="proto"><table><tbody>')
        how = KIND[j['kind']] + (' на основе грани' if j['faceBased'] else '') + ('; исходник: ' + j['source'] if j['source'] else '')
        H.append(f'<tr><td>Способ</td><td>{html.escape(how)}</td></tr>')
        H.append(f'<tr><td>Задано Ш×Г×В</td><td class="num">{"×".join(f"{x:g}" for x in j["size"])}</td></tr>')
        for k, title in REQ:
            cls = 'ok' if r['res'][k] else 'bad'
            H.append(f'<tr><td>{html.escape(title.split(" (")[0])}</td><td><span class="{cls}">{mark(r["res"][k])}</span> {html.escape(str(r["why"].get(k, "")))}</td></tr>')
        f = r['forms']
        H.append(f'<tr><td>Формы</td><td>точно {f.get("ok", 0)}, упрощено {f.get("simplified", 0)}, пропущено {f.get("skipped", 0)}</td></tr>')
        if j['notes']:
            H.append(f'<tr><td>Допущения</td><td>{html.escape("; ".join(j["notes"]))}</td></tr>')
        if r['b'].get('log'):
            H.append(f'<tr><td>Упрощения</td><td class="note">{html.escape("; ".join(r["b"]["log"]))}</td></tr>')
        H.append('</tbody></table></div></div></article>')
    H.append('</div></main>')
    H.append('<script>(function(){var q=document.getElementById("q"),b=document.getElementById("onlybad"),s=document.getElementById("onlysimpl");'
             'function f(){var t=q.value.toLowerCase();document.querySelectorAll(".card").forEach(function(c){'
             'c.hidden=(t&&c.dataset.name.indexOf(t)<0)||(b.checked&&c.dataset.bad!=="1")||(s.checked&&c.dataset.simpl!=="1");});}'
             'q.addEventListener("input",f);b.addEventListener("change",f);s.addEventListener("change",f);})();</script>')
    open(html_path, 'w', encoding='utf-8').write('\n'.join(H))
    print(f'семейств {total}, прошли всё {passed}; ' + ', '.join(f'{k} {per_req[k]}' for k, _ in REQ))


if __name__ == '__main__':
    main(*sys.argv[1:6])
