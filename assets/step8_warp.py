# -*- coding: utf-8 -*-
"""Prototype the rubber-arm warp and render preview frames at several offsets."""
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "assets", "sprites")
PRE = os.path.join(ROOT, "preview")

scene = Image.open(os.path.join(OUT, "scene.png")).convert("RGBA")
arm = Image.open(os.path.join(OUT, "arm.png")).convert("RGBA")
wm = np.asarray(Image.open(os.path.join(OUT, "armw.png")).convert("L")).astype(float) / 255.0
box = np.load(os.path.join(HERE, "piece_box.npy"))
px0, py0, px1, py1, P1x, P1y, ax, ay, L1 = box
print("box", box)

S = np.asarray(scene).astype(np.uint8)
A = np.asarray(arm).astype(np.float32)
H, W = S.shape[:2]
ah, aw = A.shape[:2]


def render(dx, dy):
    """returns the composited frame (uint8 RGBA)"""
    out = S.astype(np.float32).copy()
    # destination bbox
    x0 = max(0, int(np.floor(px0 + min(0, dx))) - 2)
    x1 = min(W, int(np.ceil(px1 + max(0, dx))) + 2)
    y0 = max(0, int(np.floor(py0 + min(0, dy))) - 2)
    y1 = min(H, int(np.ceil(py1 + max(0, dy))) + 2)
    ys, xs = np.mgrid[y0:y1, x0:x1]
    # solve s = d - delta * w(s)  with w a lookup into the weight map
    sx = xs.astype(np.float32) - dx
    sy = ys.astype(np.float32) - dy
    for _ in range(2):
        ix = np.clip((sx - px0).astype(np.int32), 0, aw - 1)
        iy = np.clip((sy - py0).astype(np.int32), 0, ah - 1)
        w = wm[iy, ix]
        sx = xs - dx * w
        sy = ys - dy * w
    gx = (sx - px0)
    gy = (sy - py0)
    valid = (gx >= -0.5) & (gx <= aw - 0.5) & (gy >= -0.5) & (gy <= ah - 0.5)
    ix = np.clip(np.floor(gx).astype(np.int32), 0, aw - 2)
    iy = np.clip(np.floor(gy).astype(np.int32), 0, ah - 2)
    fx = np.clip(gx - ix, 0, 1)[..., None]
    fy = np.clip(gy - iy, 0, 1)[..., None]
    c00 = A[iy, ix]
    c10 = A[iy, ix + 1]
    c01 = A[iy + 1, ix]
    c11 = A[iy + 1, ix + 1]
    c = (c00 * (1 - fx) * (1 - fy) + c10 * fx * (1 - fy) +
         c01 * (1 - fx) * fy + c11 * fx * fy)
    c[~valid] = 0.0
    a = c[..., 3:4] / 255.0
    dst = out[y0:y1, x0:x1]
    out[y0:y1, x0:x1] = c * a + dst * (1 - a)
    return np.clip(out, 0, 255).astype(np.uint8)


deltas = [(0, 0), (-70, 40), (70, -40), (0, -80), (0, 80), (-95, 55), (95, -55)]
tiles = []
for dx, dy in deltas:
    f = render(dx, dy)
    im = Image.fromarray(f, "RGBA")
    bg = Image.new("RGB", (W, H), (235, 235, 240))
    bg.paste(im, (0, 0), im)
    tiles.append(bg.crop((60, 700, 640, 1180)).resize((290, 240), Image.LANCZOS))

sheet = Image.new("RGB", (290 * 4, 240 * 2 + 20), (255, 255, 255))
d = ImageDraw.Draw(sheet)
for i, t in enumerate(tiles):
    x = (i % 4) * 290
    y = (i // 4) * 250
    sheet.paste(t, (x, y))
    d.text((x + 4, y + 242), "delta=%s" % (deltas[i],), fill=(0, 0, 0))
sheet.save(os.path.join(PRE, "warp_sheet.png"))
print("saved warp_sheet.png")
