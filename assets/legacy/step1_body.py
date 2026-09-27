# -*- coding: utf-8 -*-
"""Step 1: isolate the yellow creature with a flood-fill / outline-barrier method."""
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
fim = Image.fromarray(free, "L")
seeds = {"body": (600, 800), "pawL": (300, 1015), "pad": (120, 1000), "desk": (40, 1150), "kbd": (700, 1150)}
labels = {}
for name, s in seeds.items():
    f = fim.copy()
    ImageDraw.floodfill(f, s, 128, thresh=0)
    labels[name] = np.asarray(f) == 128
    print(name, s, "->", int(labels[name].sum()))

body = labels["body"]
pawL = labels["pawL"]
print("body bbox", np.where(body.any(1))[0][[0, -1]], np.where(body.any(0))[0][[0, -1]])
print("pawL bbox", np.where(pawL.any(1))[0][[0, -1]], np.where(pawL.any(0))[0][[0, -1]])
print("body&pawL overlap", int((body & pawL).sum()))

vis = np.zeros((H, W, 3), np.uint8)
vis[body] = (255, 220, 0)
vis[pawL] = (0, 200, 255)
vis[labels["pad"]] = (120, 120, 120)
vis[labels["kbd"]] = (0, 255, 0)
vis[labels["desk"]] = (255, 255, 255)
Image.fromarray(vis).resize((W // 3, H // 3)).save(os.path.join(PRE, "flood.png"))
np.save(os.path.join(HERE, "body.npy"), body)
print("done")
