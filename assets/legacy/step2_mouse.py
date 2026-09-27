# -*- coding: utf-8 -*-
"""Step 2: fit the mouse's outline with a geometric ellipse fit, ignoring the bite
taken out by the pet's paw.  Saves ellipse.npy (cx, cy, w1, w2, theta) and the
visible interior mask."""
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
P = np.column_stack([xs, ys]).astype(float)

# --- initialise from second moments ---
c0 = P.mean(0)
Q = P - c0
cov = Q.T @ Q / len(Q)
w, v = np.linalg.eigh(cov)
p = v[:, np.argmax(w)]
th0 = np.arctan2(p[1], p[0])
a0, b0 = 2 * np.sqrt(max(w)), 2 * np.sqrt(min(w))
par = np.array([c0[0], c0[1], a0, b0, th0])
print("init  centre %.1f %.1f axes %.1f %.1f th %.1f" % (c0[0], c0[1], a0, b0, np.degrees(th0)))


def resid(par, P):
    cx, cy, a, b, th = par
    a = max(a, 6.0)
    b = max(b, 6.0)
    ct, st = np.cos(th), np.sin(th)
    dx, dy = P[:, 0] - cx, P[:, 1] - cy
    u = dx * ct + dy * st
    v = -dx * st + dy * ct
    F = (u / a) ** 2 + (v / b) ** 2 - 1.0
    gx = 2 * u / a ** 2
    gy = 2 * v / b ** 2
    g = np.sqrt(gx ** 2 + gy ** 2) + 1e-9
    return F / g


sel = np.ones(len(P), bool)
for it in range(40):
    r = resid(par, P[sel])
    # numerical Jacobian
    J = np.zeros((sel.sum(), 5))
    for k in range(5):
        h = max(abs(par[k]) * 1e-4, 1e-3)
        dp = par.copy()
        dp[k] += h
        J[:, k] = (resid(dp, P[sel]) - r) / h
    JtJ = J.T @ J + np.eye(5) * (1e-6 * np.trace(J.T @ J) + 1e-9)
    step = np.linalg.solve(JtJ, J.T @ r)
    par = par - step
    if it in (0, 5, 12, 25, 39):
        err = np.abs(resid(par, P))
        thr = max(np.percentile(err, 88), 4.0)
        sel = err <= thr
        print("iter %2d centre %.2f %.2f axes %.2f %.2f th %.2f  rms %.2f  used %d/%d"
              % (it, par[0], par[1], par[2], par[3], np.degrees(par[4]),
                 np.sqrt((resid(par, P[sel]) ** 2).mean()), sel.sum(), len(P)))
    if np.abs(step).max() < 1e-4:
        break

cx, cy, a, b, th = par
print("FINAL centre %.2f %.2f axes %.2f %.2f theta %.2f deg" % (cx, cy, a, b, np.degrees(th)))

ov = im.resize((W * 2, H * 2), Image.LANCZOS)
dr = ImageDraw.Draw(ov)
t = np.linspace(0, 2 * np.pi, 720)
ct, st = np.cos(th), np.sin(th)
ex = cx + a * np.cos(t) * ct - b * np.sin(t) * st
ey = cy + a * np.cos(t) * st + b * np.sin(t) * ct
dr.line(list(zip(ex * 2, ey * 2)), fill=(255, 0, 0), width=3)
ov.crop((80 * 2, 920 * 2, 420 * 2, 1140 * 2)).save(os.path.join(PRE, "ellipse_fit.png"))
np.save(os.path.join(HERE, "ellipse.npy"), par)
np.save(os.path.join(HERE, "mouse_int.npy"), mouse_int)
print("saved")
