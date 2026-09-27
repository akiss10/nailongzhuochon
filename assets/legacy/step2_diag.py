# -*- coding: utf-8 -*-
"""Diagnostics: where is the mouse's visible outer boundary, and how big is the gap?"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
PRE = os.path.join(os.path.dirname(HERE), "preview")
im = Image.open(os.path.join(HERE, "source.png")).convert("RGB")
A = np.asarray(im).astype(np.int16)
H, W, _ = A.shape
mx = A.max(axis=2)
barrier = (mx < 115)
free = np.where(barrier, 0, 255).astype(np.uint8)


def flood(seed):
    f = Image.fromarray(free, "L").copy()
    ImageDraw.floodfill(f, seed, 128, thresh=0)
    return np.asarray(f) == 128


def dil(m, r):
    return np.asarray(Image.fromarray((m * 255).astype(np.uint8), "L").filter(ImageFilter.MaxFilter(2 * r + 1))) > 127


mouse_int = flood((215, 1040))
outside = flood((120, 1000))
ring = dil(mouse_int, 14) & barrier
outer = ring & dil(outside, 2)
ys, xs = np.nonzero(outer)
cx, cy = xs.mean(), ys.mean()
ang = np.degrees(np.arctan2(ys - cy, xs - cx)) % 360
hist, edges = np.histogram(ang, bins=36, range=(0, 360))
print("centre %.1f %.1f" % (cx, cy))
for i in range(36):
    print("%3d-%3d : %s" % (edges[i], edges[i + 1], "#" * (hist[i] // 4) + (" %d" % hist[i] if hist[i] else "")))

# also: full silhouette = interior + ring, its outer contour
sil = mouse_int | ring
print("sil bbox y", np.where(sil.any(1))[0][[0, -1]], "x", np.where(sil.any(0))[0][[0, -1]])
vis = im.crop((100, 920, 400, 1150)).resize((300 * 3, 230 * 3), Image.LANCZOS)
d = ImageDraw.Draw(vis)
for x, y in zip(xs, ys):
    d.point(((x - 100) * 3, (y - 920) * 3), fill=(255, 0, 0))
    d.point(((x - 100) * 3 + 1, (y - 920) * 3), fill=(255, 0, 0))
vis.save(os.path.join(PRE, "outer_pts.png"))
print("saved")
