# -*- coding: utf-8 -*-
"""Build the transparent sprites.

  pet.png   : the creature, with the part hidden by the desk reconstructed
  mouse.png : the computer mouse, with the part hidden by the paw reconstructed

Both are cut from source.png; the background (purple / desk / mousepad /
keyboard) and the AA fringing around the black outline are removed.
"""
import os
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "assets", "sprites")
PRE = os.path.join(ROOT, "preview")
os.makedirs(OUT, exist_ok=True)

BULGE = float(sys.argv[1]) if len(sys.argv) > 1 else 90.0

src = Image.open(os.path.join(HERE, "source.png")).convert("RGB")
A = np.asarray(src).astype(np.int16)
H, W, _ = A.shape
mx = A.max(axis=2)

BARRIER_T = 115          # purple + black + AA all block the flood fill
BLACK_T = 85             # the drawn black outline only (purple is ~111 max)
black = mx < BLACK_T
fill_free = np.where(mx >= BARRIER_T, 255, 0).astype(np.uint8)


def flood(mask_img, seed):
    f = Image.fromarray(mask_img, "L").copy()
    ImageDraw.floodfill(f, seed, 128, thresh=0)
    return np.asarray(f) == 128


def dil(m, r):
    return np.asarray(Image.fromarray((m * 255).astype(np.uint8), "L")
                      .filter(ImageFilter.MaxFilter(2 * r + 1))) > 127


def ero(m, r):
    return np.asarray(Image.fromarray((m * 255).astype(np.uint8), "L")
                      .filter(ImageFilter.MinFilter(2 * r + 1))) > 127


def fill_holes(m):
    """pixels not reachable from the border through ~m"""
    inv = np.where(m, 0, 255).astype(np.uint8)
    reach = flood(inv, (0, 0))
    return m | ~reach


def dilate_arr(a, r):
    return np.asarray(Image.fromarray(a.astype(np.uint8), "L")
                      .filter(ImageFilter.MaxFilter(2 * r + 1)))


# =====================================================================
# 1. the creature
# =====================================================================
body = flood(fill_free, (600, 800))
print("body interior", body.sum())
band = black & dil(body, 12)
pet = fill_holes(body | band)
print("pet mask", pet.sum(), "bbox y", np.where(pet.any(1))[0][[0, -1]],
      "x", np.where(pet.any(0))[0][[0, -1]])

# ---- desk line under the creature -----------------------------------
prof = {}
for x in range(410, 741, 5):
    col = np.nonzero(body[:, x])[0]
    prof[x] = col.max()
xs_p = np.array(sorted(prof))
ys_p = np.array([prof[x] for x in xs_p], float)
slope, icept = np.polyfit(xs_p, ys_p, 1)
print("desk line  y = %.4f x + %.2f" % (slope, icept))


def desk(x):
    return slope * x + icept


xA, xB = 402.0, 748.0
P0 = np.array([xA, desk(xA)])
P2 = np.array([xB, desk(xB)])
P1 = (P0 + P2) / 2 + np.array([0.0, 2 * BULGE])


def bez(t):
    return (1 - t) ** 2 * P0 + 2 * (1 - t) * t * P1 + t ** 2 * P2


ts = np.linspace(0, 1, 1200)
curve = np.array([bez(t) for t in ts])
ybez = np.interp(np.arange(W), curve[:, 0], curve[:, 1])
print("belly bottom y = %.1f (desk line there %.1f)" % (ybez[int((xA + xB) / 2)], desk((xA + xB) / 2)))

# ---- belly fill colours: extend each column downwards ---------------
TOP = 4          # the belly polygon starts this many px above the desk line
fill_rgb = np.zeros((H, W, 3), np.uint8)
for x in range(int(xA) - 6, int(xB) + 7):
    y0 = int(round(desk(x))) - TOP
    col = A[max(y0 - 40, 0):y0 + 1, x]
    good = np.nonzero(col.max(axis=1) >= 120)[0]
    rgb = col[good[-1]] if len(good) else np.array([245, 225, 120])
    fill_rgb[:, x] = rgb

# ---- the added belly, rendered with 4x supersampling ----------------
SS = 4
X0, X1 = int(xA) - 3, int(xB) + 4
Y0 = int(desk(xA)) - TOP - 2
Y1 = int(max(ybez[int(xA)], ybez[int(xB)], ybez[int((xA + xB) / 2)])) + 6
w, h = (X1 - X0) * SS, (Y1 - Y0) * SS
belly = Image.new("L", (w, h), 0)
dr = ImageDraw.Draw(belly)
poly = [(x - X0, desk(x) - TOP - Y0) for x in np.arange(X0, X1, 0.5)]
poly = [(px * SS, py * SS) for px, py in poly]
poly += [((x - X0) * SS, (ybez[x] - Y0) * SS) for x in range(X1 - 1, X0 - 1, -1)]
dr.polygon(poly, fill=255)
belly_cov = np.asarray(belly.resize((X1 - X0, Y1 - Y0), Image.BOX)).astype(float) / 255.0

outline = Image.new("L", (w, h), 0)
dr = ImageDraw.Draw(outline)
ov = 10.0
OX0, OX1 = int(xA) - 8, int(xB) + 9
poly2 = [((x - X0) * SS, (ybez[x] - Y0) * SS) for x in range(OX0, OX1)]
poly2 += [((x - X0) * SS, (ybez[x] - ov - Y0) * SS) for x in range(OX1 - 1, OX0 - 1, -1)]
dr.polygon(poly2, fill=255)
outline_cov = np.asarray(outline.resize((X1 - X0, Y1 - Y0), Image.BOX)).astype(float) / 255.0

# ---- assemble the creature RGBA -------------------------------------
# background propagation for the AA ring
outside = ~pet
BG = np.zeros((H, W, 3), float)
known = outside.copy()
BG[outside] = A[outside]
for _ in range(40):
    if known.all():
        break
    acc = np.zeros((H, W, 3), float)
    cnt = np.zeros((H, W), float)
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        sh_k = np.roll(known, (dy, dx), (0, 1))
        sh_b = np.roll(BG, (dy, dx), (0, 1))
        acc += sh_b * sh_k[..., None]
        cnt += sh_k
    new = (~known) & (cnt > 0)
    BG[new] = acc[new] / cnt[new][:, None]
    known |= new
    if not new.any():
        break
BG[~known] = 111.0

core = ero(pet, 3)
ring = pet & ~core
ratio = np.zeros((H, W))
with np.errstate(divide="ignore", invalid="ignore"):
    r = A / np.maximum(BG, 1.0)
    ratio = np.median(np.clip(r, 0, 1), axis=2)
alpha = np.where(pet, 1.0, 0.0)
a_ring = np.clip(1.0 - ratio, 0, 1)
# pixels that are already dark keep full opacity; bright ones fade out
alpha[ring] = np.minimum(1.0, np.maximum(a_ring[ring], np.clip(1.0 - mx[ring] / 90.0, 0, 1)))
col = A.astype(float).copy()
with np.errstate(divide="ignore", invalid="ignore"):
    un = (col - (1 - alpha[..., None]) * BG) / np.maximum(alpha[..., None], 1e-6)
col = np.clip(un, 0, 255)
pet_rgba = np.dstack([col, alpha * 255]).astype(np.uint8)

# composite the reconstructed belly on top
belly_layer = np.zeros((Y1 - Y0, X1 - X0, 4), np.uint8)
belly_layer[..., 0:3] = fill_rgb[Y0:Y1, X0:X1]
belly_layer[..., 3] = (belly_cov * 255).astype(np.uint8)
belly_img = Image.fromarray(belly_layer, "RGBA")
base = Image.fromarray(pet_rgba[Y0:Y1, X0:X1], "RGBA")
merged = Image.alpha_composite(base, belly_img).convert("RGBA")
pet_rgba[Y0:Y1, X0:X1] = np.asarray(merged)

# paint the new black outline over the belly
oc = outline_cov * (belly_cov > 0.02)
reg = pet_rgba[Y0:Y1, X0:X1, 0:3].astype(float)
reg = reg * (1 - oc[..., None]) + 8.0 * oc[..., None]
pet_rgba[Y0:Y1, X0:X1, 0:3] = np.clip(reg, 0, 255).astype(np.uint8)
pet_rgba[Y0:Y1, X0:X1, 3] = np.maximum(pet_rgba[Y0:Y1, X0:X1, 3], (oc * 255).astype(np.uint8))

pet_img = Image.fromarray(pet_rgba, "RGBA")
ys, xs = np.nonzero(np.asarray(pet_img)[:, :, 3] > 0)
pb = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
print("pet bbox", pb)
pet_img.crop(pb).save(os.path.join(OUT, "pet.png"))
print("pet.png", pet_img.crop(pb).size)

# =====================================================================
# 2. the mouse
# =====================================================================
cx, cy, ea, eb, eth = np.load(os.path.join(HERE, "ellipse.npy"))
mouse_int = np.load(os.path.join(HERE, "mouse_int.npy"))
print("mouse ellipse centre %.1f %.1f axes %.1f %.1f th %.1f" % (cx, cy, ea, eb, np.degrees(eth)))

# outline thickness measured from the drawing itself
runs = []
for yy in range(int(cy - eb), int(cy + eb), 6):
    row = black[yy, max(int(cx - ea - 20), 0):int(cx + ea + 20)]
    n = 0
    for v in row:
        if v:
            n += 1
        elif n:
            runs.append(n)
            n = 0
print("black runs across the mouse:", sorted(runs)[:8], "median", np.median(runs) if runs else 0)
thick = float(np.median([r for r in runs if r > 3])) if runs else 9.0
print("outline thickness ~ %.1f px" % thick)

M = 8
ex0, ey0 = int(cx - ea - M), int(cy - eb - M)
ex1, ey1 = int(cx + ea + M) + 1, int(cy + eb + M) + 1
cw, ch = ex1 - ex0, ey1 - ey0
ct, st = np.cos(eth), np.sin(eth)
SS = 4
t = np.linspace(0, 2 * np.pi, 2000)


def ellipse_cov(ra, rb):
    im2 = Image.new("L", (cw * SS, ch * SS), 0)
    d2 = ImageDraw.Draw(im2)
    pxa = cx + ra * np.cos(t) * ct - rb * np.sin(t) * st
    pya = cy + ra * np.cos(t) * st + rb * np.sin(t) * ct
    d2.polygon([((x - ex0) * SS, (y - ey0) * SS) for x, y in zip(pxa, pya)], fill=255)
    return np.asarray(im2.resize((cw, ch), Image.BOX)).astype(float) / 255.0


outer_cov = ellipse_cov(ea, eb)
inner_cov = ellipse_cov(ea - thick, eb - thick)

# pixels of the original drawing that really belong to the mouse:
# its white body plus the black detail lines that sit away from the paw
paw_zone = dil(body, 30)[ey0:ey1, ex0:ex1]
mi = mouse_int[ey0:ey1, ex0:ex1]
bmp = black[ey0:ey1, ex0:ex1]
known = mi | (bmp & ~paw_zone)
known &= outer_cov > 0.5
# keep the antialiased fringe of the paw's fingers out of the paste
paste_bad = dil(bmp & paw_zone, 4) & (outer_cov > 0.5)
known &= ~paste_bad
print("mouse known px", known.sum(), "of ellipse", (outer_cov > 0.5).sum())

# ---- inpaint the hidden part with a smooth quadratic surface fit -----
in_ell = outer_cov > 0.5
subrgb = A[ey0:ey1, ex0:ex1].astype(float)
white_known = known & (subrgb.max(axis=2) > 190)
yy, xx = np.nonzero(white_known)
sel = np.ones(len(yy), bool)
for _ in range(3):
    X = np.column_stack([np.ones(sel.sum()), xx[sel], yy[sel],
                         xx[sel] ** 2 / 100.0, xx[sel] * yy[sel] / 100.0, yy[sel] ** 2 / 100.0])
    coef, *_ = np.linalg.lstsq(X, subrgb[yy[sel], xx[sel]], rcond=None)
    pred = np.column_stack([np.ones(len(yy)), xx, yy, xx ** 2 / 100.0, xx * yy / 100.0, yy ** 2 / 100.0]) @ coef
    err = np.abs(pred - subrgb[yy, xx]).max(axis=1)
    sel = err < max(np.percentile(err, 80), 3.0)
ym, xm = np.nonzero(in_ell)
Xm = np.column_stack([np.ones(len(ym)), xm, ym, xm ** 2 / 100.0, xm * ym / 100.0, ym ** 2 / 100.0])
fit = Xm @ coef
print("surface fit: %d known white px used, spread %.0f..%.0f" % (sel.sum(), fit.min(), fit.max()))

mouse_rgb = np.zeros((ch, cw, 3), float)
mouse_rgb[ym, xm] = fit
mouse_rgb[known] = subrgb[known]

# ---- complete the mouse's button line, hidden behind the paw ---------
deep_cov = ellipse_cov(ea - thick - 6, eb - thick - 6)
div = bmp & (deep_cov > 0.5) & ~paw_zone
dy_, dx_ = np.nonzero(div)
if len(dy_) > 20:
    ptsv = np.column_stack([dx_, dy_]).astype(float)
    mean = ptsv.mean(0)
    u, s, vt = np.linalg.svd(ptsv - mean)
    dirv = vt[0]
    tproj = (ptsv - mean) @ dirv
    perp = (ptsv - mean) @ vt[1]
    halfwidth = np.median(np.abs(perp)) * 1.6 + 1.5
    tmax = tproj.max()
    tmin = tproj.min()

    def walk(start, step):
        ts = start
        while abs(ts - start) < 400:
            p = mean + dirv * (ts + step)
            uu = (p[0] + ex0 - cx) * ct + (p[1] + ey0 - cy) * st
            vv = -(p[0] + ex0 - cx) * st + (p[1] + ey0 - cy) * ct
            if (uu / (ea - thick)) ** 2 + (vv / (eb - thick)) ** 2 > 0.93:
                break
            ts += step
        return ts

    a_end, b_end = walk(tmax, 1), walk(tmin, -1)
    if abs(b_end - tmin) > abs(a_end - tmax):
        p_start, p_end = mean + dirv * tmin, mean + dirv * b_end
        grown = abs(b_end - tmin)
    else:
        p_start, p_end = mean + dirv * tmax, mean + dirv * a_end
        grown = abs(a_end - tmax)
    print("divider: %d px, direction (%.2f %.2f), extend %.0f px, halfwidth %.1f"
          % (len(dy_), dirv[0], dirv[1], grown, halfwidth))
    seg = Image.new("L", (cw * SS, ch * SS), 0)
    ds = ImageDraw.Draw(seg)
    ds.line([tuple(p_start * SS), tuple(p_end * SS)], fill=255,
            width=max(3, int(round(halfwidth * 2 * SS))))
    seg_cov = np.asarray(seg.resize((cw, ch), Image.BOX)).astype(float) / 255.0
    seg_cov *= (~known).astype(float)
    mouse_rgb = mouse_rgb * (1 - seg_cov[..., None]) + 8.0 * seg_cov[..., None]

# black outline of the reconstructed mouse
scov = np.clip(outer_cov - inner_cov, 0, 1)
mouse_rgb = mouse_rgb * (1 - scov[..., None]) + 8.0 * scov[..., None]
mouse_a = outer_cov.copy()
mouse_img = Image.fromarray(np.dstack([np.clip(mouse_rgb, 0, 255), mouse_a * 255]).astype(np.uint8), "RGBA")
ys, xs = np.nonzero(np.asarray(mouse_img)[:, :, 3] > 2)
mb = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
print("mouse bbox", mb, "size", (mb[2] - mb[0], mb[3] - mb[1]))
mouse_img.crop(mb).save(os.path.join(OUT, "mouse.png"))
anchor = (cx - (ex0 + mb[0]), (cy - np.sqrt((ea * st) ** 2 + (eb * ct) ** 2)) - (ey0 + mb[1]))
print("mouse anchor (top centre) in sprite = %.1f %.1f  of %s" % (anchor[0], anchor[1], mouse_img.crop(mb).size))
np.save(os.path.join(HERE, "mouse_anchor.npy"), np.array(anchor))

# ---- previews -------------------------------------------------------
def checker(size):
    c = Image.new("RGB", size, (210, 210, 215))
    d = ImageDraw.Draw(c)
    for y in range(0, size[1], 24):
        for x in range(0, size[0], 24):
            if (x // 24 + y // 24) % 2:
                d.rectangle([x, y, x + 23, y + 23], fill=(180, 180, 190))
    return c


pv = checker((W, H))
pv.paste(Image.fromarray(pet_rgba, "RGBA"), (0, 0), Image.fromarray(pet_rgba, "RGBA"))
pv.crop((150, 500, 1000, 1150)).save(os.path.join(PRE, "pet_preview.png"))
Image.fromarray(pet_rgba, "RGBA").crop(pb).save(os.path.join(PRE, "pet_flat.png"))

mp = checker((cw + 40, ch + 40))
mp.paste(mouse_img, (20, 20), mouse_img)
mp.resize((mp.width * 2, mp.height * 2), Image.LANCZOS).save(os.path.join(PRE, "mouse_preview.png"))
print("done")
