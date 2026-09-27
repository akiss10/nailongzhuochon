# -*- coding: utf-8 -*-
"""Measure the desk edge line and where it cuts the creature."""
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
im = Image.open(os.path.join(HERE, "source.png")).convert("RGB")
A = np.asarray(im).astype(np.int16)
mx = A.max(axis=2)
barrier = (mx < 115)
free = np.where(barrier, 0, 255).astype(np.uint8)
f = Image.fromarray(free, "L")
draw = ImageDraw.Draw(f)
f2 = f.copy()
ImageDraw.floodfill(f2, (600, 800), 128, thresh=0)
body = np.asarray(f2) == 128

print("column profiles  x : body_max_y")
prof = {}
for x in range(200, 1000, 10):
    col = np.nonzero(body[:, x])[0]
    prof[x] = int(col.max()) if len(col) else -1
print(prof)

print()
print("desk edge (topmost black run below y=800) outside the creature")
for x in list(range(0, 400, 25)) + list(range(760, 1218, 25)):
    col = np.nonzero(barrier[800:1100, x])[0]
    y = int(col.min() + 800) if len(col) else -1
    print(x, y, end="   |  ")
print()
