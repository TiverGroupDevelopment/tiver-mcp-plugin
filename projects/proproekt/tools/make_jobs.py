"""Задания на моделирование семейств ПроПроекта в Revit 2019.

Вход: выгрузка листа «Уникальные позиции» (JSON get_values, A1:K) и снимки исходных семейств
(`исходные семейства/<семейство>/family.json`). Выход: jobs.json — по заданию на семейство.

    python -I make_jobs.py <values.json> <папка исходных семейств> <jobs.json>
"""
import glob
import json
import os
import re
import sys

TEMPLATES = r'C:\ProgramData\Autodesk\RVT 2019\Family Templates\Russian'
LEVEL_TEMPLATE = os.path.join(TEMPLATES, 'Метрическая система, типовая модель.rft')
FACE_TEMPLATE = os.path.join(TEMPLATES, 'Метрическая система, типовая модель на основе грани.rft')
OUT_DIR = r'D:\Семейства\ПроПроект3\rvt\rfa'

# Категории 2019 вместо отсутствующих (PP-10)
CAT2019 = {'OST_MedicalEquipment': 'OST_SpecialityEquipment'}
# Исходники на основе грани, которые висят на стене: X — ширина, Y — высота, Z — глубина от стены
WALL_FACE_SOURCES = ('Т39_', 'Т7_', 'Сушка для рук', 'бн06_бн07_бн37_')

# Позиции без габаритов в спецификации: допущение LOD200 (PP-08), Ш×Г×В, мм
ASSUMED = {
    ('4.44', 'Пипетка автоматическая 12-канальная'): ([130, 50, 300], 'по аналогу 12-канальной пипетки'),
    ('6.01', 'Сканер штрих-кодов беспроводной'): ([80, 190, 170], 'сканер с базовой станцией, по аналогу'),
    ('7.04', 'Принтер штрих-кода'): ([230, 280, 190], 'по габаритам принтера этикеток XD3-40t'),
    ('7.10', 'Крепление настенное для телевизора'): ([600, 50, 420], 'кронштейн VESA 400x400'),
    ('б/п', 'Ершик для унитаза'): ([100, 100, 380], 'по аналогу'),
    ('б/п', 'Крючок двойной'): ([30, 60, 80], 'по аналогу'),
    ('б/п', 'Модуль для видеостены'): ([1250, 600, 1900], 'стойка под панель 55", по аналогу'),
    ('б/п', 'Кронштейн поворотный для телевизора'): ([100, 350, 100], 'вынос от стены 350'),
    ('б/п', 'Кронштейн для микроволновой печи'): ([500, 310, 200], 'вынос от стены 310 по спецификации'),
    ('7.09', 'Телевизор ЖК 55 дюймов'): ([1245, 88, 726], 'корпус телевизора 55" без подставки'),
    ('1.09', 'Телевизор'): ([542, 73, 320], 'в спецификации «542±10х320±1073±10»: принято 542×320×73 (Ш×В×Г)'),
    ('б/п', 'Ведро для мусора с педалью 15 л'): ([250, 250, 400], 'ведро 15 л, по аналогу'),
}

# Габариты в спецификации записаны не в порядке Ш×Г×В: имя семейства — по спецификации, геометрия — по изделию (PP-08)
REORDERED = {
    ('7.11', 'ЖК панель для видеостены 49 дюймов'): ([1078, 86, 608], 'в спецификации «1077,6 х 607,8 х 86,2» — Ш×В×Г панели; принято Ш 1078, Г 86, В 608'),
    ('1.05', 'Печь микроволновая'): ([440, 330, 260], 'в спецификации «260х440х330» — В×Ш×Г (Midea MM720: Ш 440, Г 330, В 260); принято 440×330×260'),
}

FURNITURE_WORDS = ('стол', 'стул', 'кресл', 'шкаф', 'тумб', 'стеллаж', 'скам', 'вешалк', 'тележк', 'доска', 'топ ', 'двери',
                   'опора', 'гарнитур')
EQUIPMENT_EXCEPTIONS = ('шкаф управления',)


def parse_dims(text):
    text = (text or '').strip()
    if not text:
        return None
    cyl = text.startswith('Ø')
    nums = [float(x.replace(',', '.')) for x in re.findall(r'\d+(?:[.,]\d+)?', text)]
    return {'cyl': cyl, 'nums': nums}


def kubik_category(name):
    low = name.lower()
    if 'рукомойник' in low or 'мойк' in low:
        return 'OST_PlumbingFixtures'
    if any(w in low for w in EQUIPMENT_EXCEPTIONS):
        return 'OST_SpecialityEquipment'
    if any(w in low for w in FURNITURE_WORDS):
        return 'OST_Furniture'
    return 'OST_SpecialityEquipment'


def is_template_host(form):
    if not form.get('bbMin'):
        return False
    size = [round(b - a, 1) for a, b in zip(form['bbMin'], form['bbMax'])]
    return form['kind'] == 'Extrusion' and size[0] == 2438.4 and size[1] == 2438.4 and size[2] == 304.8


def source_box(dump):
    boxes = [(f['bbMin'], f['bbMax']) for f in dump['forms'] if f['solid'] and f.get('bbMin') and not is_template_host(f)]
    boxes += [(i['bbMin'], i['bbMax']) for i in dump['nestedInstances'] if i.get('bbMin')]
    mn = [min(b[0][k] for b in boxes) for k in range(3)]
    mx = [max(b[1][k] for b in boxes) for k in range(3)]
    return mn, mx


def main(values_path, sources_dir, jobs_path):
    values = json.load(open(values_path, encoding='utf-8'))['values']
    head, rows = values[0], [r + [''] * (11 - len(r)) for r in values[1:] if r and r[0]]
    sources = {}
    for path in glob.glob(os.path.join(sources_dir, '*', 'family.json')):
        dump = json.load(open(path, encoding='utf-8'))
        sources[dump['familyName']] = (os.path.dirname(path), dump)

    jobs, seen = [], {}
    for row_no, r in enumerate(rows, start=2):
        pos, spec_name, spec_mark, source, rooms, decision, adsk_name, dims_text, adsk_pos, adsk_mark, family = r[:11]
        if family in seen:
            seen[family]['rows'].append(row_no)
            continue
        dims = parse_dims(dims_text)
        notes = []
        job = {
            'family': family, 'rows': [row_no], 'position': adsk_pos, 'adskName': adsk_name, 'adskMark': adsk_mark,
            'decision': decision, 'source': None, 'output': os.path.join(OUT_DIR, family + '.rfa'),
        }
        target = None
        if dims and not dims['cyl'] and len(dims['nums']) == 3:
            target = dims['nums']
        elif dims and dims['cyl'] and source in sources:  # круглое изделие с исходником (бак Ø440×470)
            target = [dims['nums'][0], dims['nums'][0], dims['nums'][1]]
        elif (pos, adsk_name) in ASSUMED:
            target, why = ASSUMED[(pos, adsk_name)]
            notes.append('габаритов нет в спецификации; принято ' + '×'.join(f'{x:g}' for x in target) + ' — ' + why)
        if (pos, adsk_name) in REORDERED:
            target, why = REORDERED[(pos, adsk_name)]
            notes.append('порядок габаритов: ' + why)

        low = decision.lower()
        if low.startswith('кубик'):
            if target is None:
                raise SystemExit(f'строка {row_no}: кубик без габаритов — {family}')
            job.update(kind='box', template=LEVEL_TEMPLATE, category=kubik_category(adsk_name), faceBased=False, size=target)
        elif low.startswith('цилиндр') or (dims and dims['cyl'] and source not in sources):
            d, h = dims['nums'][0], dims['nums'][1]
            job.update(kind='cylinder', template=LEVEL_TEMPLATE, faceBased=False, size=[d, d, h],
                       category='OST_SpecialityEquipment' if not low.startswith('доработать') else None)
        else:
            if source not in sources:
                raise SystemExit(f'строка {row_no}: нет снимка исходного семейства «{source}»')
            src_dir, dump = sources[source]
            mn, mx = source_box(dump)
            src_size = [mx[k] - mn[k] for k in range(3)]
            hosted = any(is_template_host(f) for f in dump['forms'])
            wall = hosted and source.startswith(WALL_FACE_SOURCES)
            category = CAT2019.get(dump['builtInCategory'], dump['builtInCategory'])
            # Целевой габарит в осях семейства. На стене: X — ширина, Y — высота, Z — глубина.
            rotate = False
            if wall:
                want = None if target is None else [target[0], target[2], target[1]]
                if dims and not dims['cyl'] and len(dims['nums']) == 2:  # штора: окно Ш×В, глубина — исходника
                    want = [dims['nums'][0], dims['nums'][1], src_size[2]]
            else:
                want = target
                if want is not None:
                    src_long_x = src_size[0] >= src_size[1]
                    want_long_x = want[0] >= want[1]
                    ratio = lambda a, b: max(a, b) / max(min(a, b), 1e-6)
                    if src_long_x != want_long_x and ratio(*src_size[:2]) > 1.15 and ratio(*want[:2]) > 1.15:
                        rotate = True
                        notes.append('исходник повёрнут на 90° в плане: длинная сторона — вдоль ширины')
            if rotate:
                # поворот на +90° вокруг Z: (x, y) → (−y, x)
                mn, mx = [-mx[1], mn[0], mn[2]], [-mn[1], mx[0], mx[2]]
                src_size = [mx[k] - mn[k] for k in range(3)]
            if want is None:
                want = src_size
                notes.append('габаритов нет в спецификации; оставлен габарит исходного семейства ' +
                             '×'.join(f'{x:.0f}' for x in src_size))
            scale = [want[k] / src_size[k] if src_size[k] > 1e-6 else 1.0 for k in range(3)]
            center = [(mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, mn[2]]
            if wall:
                center = [(mn[0] + mx[0]) / 2, 0.0, 0.0]
            job.update(kind='rebuild', template=FACE_TEMPLATE if wall else LEVEL_TEMPLATE, faceBased=wall, category=category,
                       source=source, sourceJson=os.path.join(src_dir, 'family.json'), size=[round(x, 2) for x in want],
                       map={'rotate': rotate, 'center': [round(x, 3) for x in center], 'scale': [round(x, 6) for x in scale]})
        if job.get('category') is None:
            job['category'] = 'OST_SpecialityEquipment'
        job['notes'] = notes
        seen[family] = job
        jobs.append(job)

    json.dump(jobs, open(jobs_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    kinds = {}
    for j in jobs:
        kinds[j['kind']] = kinds.get(j['kind'], 0) + 1
    print(len(jobs), 'семейств', kinds)


if __name__ == '__main__':
    main(*sys.argv[1:4])
