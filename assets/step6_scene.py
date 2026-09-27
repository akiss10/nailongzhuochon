# -*- coding: utf-8 -*-
"""Measure the background structure hidden behind the creature's arm:
   the desk edge line, the mousepad's top edge, and the arm/paw/mouse region."""
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
PRE = os.path.join(os.path.dirname(HERE), "preview")
src = Image.open(os.path.join(HERE, "source.png")).convert("RGB")
A = np.asarray(src).astype(np.int16)
H, W, _ = A.shape
mx = A.max(axis=2)
black = mx < 85

print("== image border ==")
print("top row sample   ", [tuple(A[0, x]) for x in (0, 300, 600, 900, 1217)])
print("row 1 sample     ", [tuple(A[1, x]) for x in (0, 300, 600, 900, 1217)])
print("row 2 sample     ", [tuple(A[2, x]) for x in (0, 300, 600, 900, 1217)])
print("left col sample  ", [tuple(A[y, 0]) for y in (0, 300, 600, 900, 1228)])
print("bottom row sample", [tuple(A[1228, x]) for x in (0, 300, 600, 900, 1217)])
print("right col sample ", [tuple(A[y, 1217]) for y in (0, 300, 600, 900, 1228)])

# how many border rows/cols are dark?
for name, line in (("top", A[0]), ("bottom", A[H - 1]), ("left", A[:, 0]), ("right", A[:, W - 1])):
    dark = (line.max(axis=1) < 85).mean()
    print("border %-6s dark fraction %.2f" % (name, dark))

# ---------------------------------------------------------------- desk edge
print("\n== desk edge line (first black run from y=780 down) ==")
pts = []
for x in list(range(0, 240, 10)) + list(range(430, 900, 10)):
    col = np.nonzero(black[780:1120, x])[0]
    if len(col):
        pts.append((x, int(col[0]) + 780))
pts = np.array(pts)
print("left  pts", pts[pts[:, 0] < 240][:8].tolist())
print("right pts", pts[pts[:, 0] > 430][:8].tolist())
left = pts[pts[:, 0] < 240]
right = pts[pts[:, 0] > 430]
if len(left) > 2:
    sl, ic = np.polyfit(left[:, 0], left[:, 1], 1)
    print("left  fit  y = %.4f x + %.2f" % (sl, ic))
if len(right) > 2:
    sr, irc = np.polyfit(right[:, 0], right[:, 1], 1)
    print("right fit  y = %.4f x + %.2f" % (sr, irc))

# ------------------------------------------------------- mousepad top edge
print("\n== mousepad top edge ==")
for x in (140, 160, 180, 200, 420, 440, 460):
    col = np.nonzero(black[880:1010, x])[0]
    runs = []
    if len(col):
        start = col[0]
        prev = col[0]
        for v in col[1:]:
            if v != prev + 1:
                runs.append((start + 880, prev + 880))
                start = v
            prev = v
        runs.append((start + 880, prev + 880))
    print("x=%4d black runs y:" % x, runs)
