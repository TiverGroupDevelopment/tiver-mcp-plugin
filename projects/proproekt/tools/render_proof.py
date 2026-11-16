"""Картинки проверки семейств ПроПроекта: изометрия и три проекции с габаритами по сетке треугольников
из verify_families (Revit 2019 ExportImage открывает документы в окнах, поэтому рисуем вне Revit).

    python -I render_proof.py <папка проверки> <jobs.json>
Вход: <папка>/verify.jsonl и <папка>/сетки/*.json. Выход: <папка>/картинки/<семейство>.jpg
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

FONT = r'C:\Windows\Fonts\arial.ttf'
BG = (255, 255, 255)
BASE = np.array([120, 150, 190], dtype=float)


def font(size):
    try:
        return ImageFont.truetype(FONT, size)
    except OSError:
        return ImageFont.load_default()


def camera(direction, up=(0, 0, 1)):
    f = np.array(direction, dtype=float)
    f /= np.linalg.norm(f)
    u = np.array(up, dtype=float)
    r = np.cross(f, u)
    if np.linalg.norm(r) < 1e-9:
        r = np.cross(f, np.array([0, 1.0, 0]))
    r /= np.linalg.norm(r)
    u2 = np.cross(r, f)
    return f, r, u2


def draw_view(tris, box, size, direction, up=(0, 0, 1)):
    """tris: (n,3,3) мм. Возвращает картинку size×size."""
    f, r, u = camera(direction, up)
    img = Image.new('RGB', (size, size), BG)
    if len(tris) == 0:
        return img, None
    pts2 = np.stack([tris @ r, tris @ u], axis=-1)  # (n,3,2)
    depth = (tris @ f).mean(axis=1)
    lo = pts2.reshape(-1, 2).min(axis=0)
    hi = pts2.reshape(-1, 2).max(axis=0)
    span = max(hi[0] - lo[0], hi[1] - lo[1], 1e-6)
    pad = size * 0.08
    k = (size - 2 * pad) / span
    off = np.array([(size - (hi[0] - lo[0]) * k) / 2, (size - (hi[1] - lo[1]) * k) / 2])
    normals = np.cross(tris[:, 1] - tris[:, 0], tris[:, 2] - tris[:, 0])
    nl = np.linalg.norm(normals, axis=1)
    nl[nl == 0] = 1
    normals = normals / nl[:, None]
    light = -f + np.array([0.3, -0.2, 0.6])
    light /= np.linalg.norm(light)
    shade = 0.45 + 0.55 * np.abs(normals @ light)
    order = np.argsort(-depth)  # дальние первыми
    d = ImageDraw.Draw(img)
    for i in order:
        p = (pts2[i] - lo) * k + off
        poly = [(float(x), float(size - y)) for x, y in p]
        c = tuple(int(v) for v in np.clip(BASE * shade[i], 0, 255))
        d.polygon(poly, fill=c, outline=c)
    return img, (lo, k, off)


def ortho(tris, size, axes, labels):
    """Проекция по двум осям (индексы), контур — заливка всех треугольников."""
    img = Image.new('RGB', (size, size), BG)
    d = ImageDraw.Draw(img)
    if len(tris) == 0:
        return img
    p = tris[:, :, axes]
    lo = p.reshape(-1, 2).min(axis=0)
    hi = p.reshape(-1, 2).max(axis=0)
    span = max(hi[0] - lo[0], hi[1] - lo[1], 1e-6)
    pad = size * 0.2
    k = (size - 2 * pad) / span
    w, h = (hi - lo) * k
    ox, oy = (size - w) / 2, (size - h) / 2
    for t in p:
        poly = [(float(ox + (x - lo[0]) * k), float(size - (oy + (y - lo[1]) * k))) for x, y in t]
        d.polygon(poly, fill=(200, 210, 225), outline=(200, 210, 225))
    x0, x1 = ox, ox + w
    y0, y1 = size - oy, size - (oy + h)
    d.rectangle([x0, y1, x1, y0], outline=(60, 60, 60))
    fnt = font(15)
    tw = f'{hi[0] - lo[0]:.0f}'
    th = f'{hi[1] - lo[1]:.0f}'
    d.line([x0, y0 + 14, x1, y0 + 14], fill=(200, 40, 40), width=1)
    d.text(((x0 + x1) / 2 - d.textlength(tw, font=fnt) / 2, y0 + 16), tw, fill=(200, 40, 40), font=fnt)
    d.line([x1 + 14, y0, x1 + 14, y1], fill=(200, 40, 40), width=1)
    d.text((x1 + 18, (y0 + y1) / 2 - 8), th, fill=(200, 40, 40), font=fnt)
    d.text((6, 6), labels, fill=(90, 90, 90), font=font(14))
    return img


def render(family, tris, verify, out_path):
    W, H = 1100, 560
    sheet = Image.new('RGB', (W, H), BG)
    iso, _ = draw_view(tris, None, 500, (-1, 1, -1))
    sheet.paste(iso, (10, 50))
    sheet.paste(ortho(tris, 270, [0, 2], 'Фасад (X–Z)'), (530, 50))
    sheet.paste(ortho(tris, 270, [1, 2], 'Сбоку (Y–Z)'), (810, 50))
    sheet.paste(ortho(tris, 270, [0, 1], 'План (X–Y)'), (530, 300))
    d = ImageDraw.Draw(sheet)
    d.text((12, 10), family, fill=(20, 20, 20), font=font(18))
    lines = [
        f"Габарит X×Y×Z: {' × '.join(f'{x:g}' for x in verify.get('size') or [])} мм",
        f"Категория: {verify.get('category')}",
        f"Форм: {verify.get('solidForms')} тв. / {verify.get('voidForms')} пуст.",
        f"Сохранено: {verify.get('savedInVersion')}",
    ]
    for i, t in enumerate(lines):
        d.text((815, 330 + i * 22), t, fill=(40, 40, 40), font=font(14))
    sheet.save(out_path, 'JPEG', quality=82)


def main(proof_dir, jobs_path):
    out = os.path.join(proof_dir, 'картинки')
    os.makedirs(out, exist_ok=True)
    rows = [json.loads(line) for line in open(os.path.join(proof_dir, 'verify.jsonl'), encoding='utf-8')]
    n = 0
    for v in rows:
        mesh = v.get('mesh')
        if not mesh or not os.path.exists(mesh):
            continue
        data = json.load(open(mesh, encoding='utf-8'))
        tris = np.array(data, dtype=float).reshape(-1, 3, 3) if data else np.zeros((0, 3, 3))
        render(v['family'], tris, v, os.path.join(out, v['family'] + '.jpg'))
        n += 1
    print('картинок', n)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
