# -*- coding: utf-8 -*-
"""Segment the source illustration into transparent sprites:
   pet.png   - the yellow creature only (background / desk / pad / keyboard removed)
   mouse.png - the computer mouse, reconstructed so it is complete
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "source.png")
OUT = HERE
PRE = os.path.join(os.path.dirname(HERE), "preview")

im = Image.open(SRC).convert("RGB")
A = np.asarray(im).astype(np.int16)
H, W, _ = A.shape
R, G, B = A[:, :, 0], A[:, :, 1], A[:, :, 2]
mx = A.max(axis=2)
mn = A.min(axis=2)


def save(mask, name):
    Image.fromarray((mask.astype(np.uint8) * 255)).save(os.path.join(PRE, name))


def save_rgba(rgb, alpha, name):
    a = np.clip(alpha, 0, 255).astype(np.uint8)
    out = np.dstack([rgb.astype(np.uint8), a])
    Image.fromarray(out, "RGBA").save(os.path.join(OUT, name))


# ---------- colour classes ----------
purple = (np.abs(R - 70) < 45) & (np.abs(G - 26) < 45) & (np.abs(B - 111) < 55) & (B > R + 15) & (R > G + 10)
black = mx < 110
whiteish = mn > 200
yellow = (R > 170) & (G > 140) & (B < 185) & (R - B > 60) & (G - B > 40)
green = (G > 120) & (G - R > 30) & (G - B > 30)
grayish = (np.abs(R - G) < 18) & (np.abs(G - B) < 22) & (mx < 225) & (mn > 90)

print("purple", purple.sum(), "black", black.sum(), "white", whiteish.sum(),
      "yellow", yellow.sum(), "green", green.sum(), "gray", grayish.sum())

save(purple, "m_purple.png")
save(black, "m_black.png")
save(whiteish, "m_white.png")
save(yellow, "m_yellow.png")
save(grayish, "m_gray.png")

vis = np.zeros((H, W, 3), np.uint8)
vis[purple] = (128, 0, 255)
vis[grayish] = (90, 90, 90)
vis[whiteish] = (255, 255, 255)
vis[black] = (255, 0, 0)
vis[yellow] = (255, 220, 0)
vis[green] = (0, 255, 0)
Image.fromarray(vis).resize((W // 3, H // 3)).save(os.path.join(PRE, "classes.png"))
print("saved previews")
