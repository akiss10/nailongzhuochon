# -*- coding: utf-8 -*-
"""Compose a realistic desktop preview: pet at display scale on several backgrounds."""
import os
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "assets", "sprites")
PRE = os.path.join(ROOT, "preview")

pet = Image.open(os.path.join(OUT, "pet.png"))
mouse = Image.open(os.path.join(OUT, "mouse.png"))
anchor = np.load(os.path.join(ROOT, "assets", "mouse_anchor.npy"))
S = 0.45
pw, ph = int(pet.width * S), int(pet.height * S)
pet_s = pet.resize((pw, ph), Image.LANCZOS)
mouse_s = mouse.resize((max(1, int(mouse.width * S)), max(1, int(mouse.height * S))), Image.LANCZOS)
print("pet display size", pet_s.size, "mouse display size", mouse_s.size, "anchor", anchor * S)

mains = [(28, 38, 60), (235, 235, 240), (60, 90, 70)]
for i, bg in enumerate(mains):
    canvas = Image.new("RGB", (760, 520), bg)
    d = ImageDraw.Draw(canvas)
    if i == 0:
        for y in range(0, 520, 4):
            c = int(30 + y * 0.06)
            d.line([(0, y), (760, y)], fill=(c, c + 8, c + 18))
    canvas.paste(pet_s, (30, 520 - ph - 10), pet_s)
    mx, my = 600, 470
    ax, ay = int(anchor[0] * S), int(anchor[1] * S)
    canvas.paste(mouse_s, (mx - ax, my - ay), mouse_s)
    canvas.save(os.path.join(PRE, "desktop_%d.png" % i))

# zoom on the pet's outline over a dark background to look for fringing
z = Image.new("RGB", (500, 380), (25, 34, 52))
big = pet.resize((int(pet.width * 0.7), int(pet.height * 0.7)), Image.LANCZOS)
z.paste(big, (20, 380 - big.height - 10), big)
z.crop((0, 60, 500, 300)).resize((1000, 480), Image.NEAREST).save(os.path.join(PRE, "fringe.png"))
print("ok")
