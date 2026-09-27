# -*- coding: utf-8 -*-
"""Build the "desk scene" sprite:
   scene.png - the whole picture with only the purple background removed,
               and the creature's left arm + paw + mouse taken out and the
               background behind them reconstructed.
   arm.png   - that arm + paw + mouse piece (moves with the real cursor).
   armw.png  - weight map for the rubber-arm warp (0 at the shoulder, 1 at the paw).
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "assets", "sprites")
PRE = os.path.join(ROOT, "preview")
os.makedirs(OUT, exist_ok=True)

TOP = 8                      # black letterbox rows at the top of the source
src = Image.open(os.path.join(HERE, "source.png")).convert("RGB")
A = np.asarray(src).astype(np.int16)[TOP:, :, :]          # cropped
H, W, _ = A.shape
mx = A.max(axis=2)
print("cropped source", W, "x", H)

BG = np.array([70.0, 26.0, 111.0])                        # the purple background
black = mx < 85
purple = (np.abs(A[:, :, 0] - 70) < 45) & (np.abs(A[:, :, 1] - 26) < 45) & \
         (np.abs(A[:, :, 2] - 111) < 55) & (A[:, :, 2] > A[:, :, 0] + 15)
print("purple fraction %.3f" % purple.mean())


def dil(m, r):
    return np.asarray(Image.fromarray((m * 255).astype(np.uint8), "L")
                      .filter(ImageFilter.MaxFilter(2 * r + 1))) > 127


def ero(m, r):
    return np.asarray(Image.fromarray((m * 255).astype(np.uint8), "L")
                      .filter(ImageFilter.MinFilter(2 * r + 1))) > 127


def flood(maskimg, seed):
    f = Image.fromarray(maskimg, "L").copy()
    ImageDraw.floodfill(f, seed, 128, thresh=0)
    return np.asarray(f) == 128


# ---------------------------------------------------------------- the lines
def desk_y(x):      # top of the thick black desk edge
    return 0.1735 * x + 867.7


def pad_y(x):       # top of the mousepad's thin outline
    return 0.2000 * x + 887.0


DESK_T = 16.0
PAD_T = 4.5
DESK_COL = np.array([254.0, 254.0, 254.0])
PAD_COL = np.array([190.0, 191.0, 186.0])
LINE_COL = np.array([8.0, 8.0, 8.0])

# ---- the creature's silhouette (same method as step4, verified) ------
BARRIER_T = 115
fill_free = np.where(mx >= BARRIER_T, 255, 0).astype(np.uint8)
body = flood(fill_free, (600 - TOP, 800))
band = black & dil(body, 12)
pet = flood(np.where(body | band, 0, 255).astype(np.uint8), (0, 0)) if False else None
inv = np.where(body | band, 0, 255).astype(np.uint8)
reach = flood(inv, (0, 0))
pet = (body | band) | ~reach
print("pet px", pet.sum())

# the piece sits on the hand side of a cut through the elbow stroke
P1 = np.array([418.0, 924.0 - TOP])
PAW = np.array([300.0, 1015.0 - TOP])
v = PAW - P1
axis = v / np.linalg.norm(v)
perp = np.array([-axis[1], axis[0]])
print("arm axis", np.round(axis, 3))

h = (np.arange(W)[None, :] - P1[0]) * axis[0] + (np.arange(H)[:, None] - P1[1]) * axis[1]

cutimg = Image.new("L", (W, H), 0)
ImageDraw.Draw(cutimg).line([tuple(P1 - perp * 700), tuple(P1 + perp * 700)], fill=255, width=11)
cutmask = np.asarray(cutimg) > 127

inner_black = black & ero(pet, 3)
free2 = np.where(cutmask | inner_black, 0, 255).astype(np.uint8)
seed0 = np.round(PAW + axis * 18).astype(int)
seed = None
for r in range(0, 80):
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            y2, x2 = seed0[1] + dy, seed0[0] + dx
            if 0 <= y2 < H and 0 <= x2 < W and free2[y2, x2] == 255 and h[y2, x2] > 10:
                seed = (y2, x2)
                break
        if seed: break
    if seed: break
print("paw seed", seed, "asked", tuple(seed0))
arm_int = flood(free2, (seed[1], seed[0]))   # PIL wants (x, y)
print("arm interior px", arm_int.sum())
ys, xs = np.nonzero(arm_int)
print("arm interior bbox x", xs.min(), xs.max(), "y", ys.min(), ys.max())

# mouse interior (its own white body)
free3 = np.where(black, 0, 255).astype(np.uint8)
mouse_int = flood(free3, (215, 1040 - TOP))   # PIL wants (x, y)
print("mouse interior px", mouse_int.sum())

piece = (pet & (arm_int | dil(arm_int, 10))) | mouse_int | (black & dil(mouse_int, 13))
piece &= ~purple
# keep only the component that holds the paw (drops stray fragments)
piece = flood(np.where(piece, 255, 0).astype(np.uint8), (seed[1], seed[0]))
ys, xs = np.nonzero(piece)
print("piece px", piece.sum(), "bbox x", xs.min(), xs.max(), "y", ys.min(), ys.max())

# ---------------------------------------------------------------- weights
L1 = 110.0                    # px along the arm over which the warp ramps 0 -> 1
w = np.clip(h / L1, 0.0, 1.0)
w = np.where(piece, w, 0.0)
print("weight stats", w[piece].min(), w[piece].max())

# ---------------------------------------------------------------- scene rgba
alpha = np.where(ero(~purple, 2), 1.0, 0.0)
ring = (~purple) & ~ero(~purple, 2)
with np.errstate(divide="ignore", invalid="ignore"):
    ratio = A / BG[None, None, :]
med = np.median(ratio, axis=2)
alpha[ring] = np.clip(1.0 - med[ring], 0.0, 1.0)
alpha = np.where(purple, 0.0, alpha)
# the top row of the crop is the black letterbox: treat as background
alpha[0, :] = 0.0
alpha[-1, :] = 0.0
col = A.astype(float).copy()
with np.errstate(divide="ignore", invalid="ignore"):
    col = (col - (1 - alpha[..., None]) * BG[None, None, :]) / np.maximum(alpha[..., None], 1e-6)
col = np.clip(col, 0, 255)
scene = np.dstack([col, alpha * 255]).astype(np.uint8)

# ---- put the background back where the piece was ---------------------
# Both the desk edge and the mousepad's top edge are straight lines with a
# clean, unoccluded run to the left of the creature, and the areas between
# them are flat, so the background can be rebuilt by copying a clean column
# shifted along each line.  That keeps the hand drawn wobble of the thick
# desk edge, the pad's own gradient and the antialiased purple border.
yy, xx = np.mgrid[0:H, 0:W]


def shifted(colx, slope):
    fy = yy - slope * (xx - colx)
    y0 = np.clip(np.floor(fy).astype(np.int32), 0, H - 2)
    fr = np.clip(fy - y0, 0, 1)[..., None]
    c = A[:, colx].astype(float)
    a = np.where(purple[:, colx], 0.0, 1.0)
    return (c[y0] * (1 - fr) + c[y0 + 1] * fr), np.where(a[y0] > 0.5, 255.0, 0.0)


bg_up, opa_up = shifted(100, 0.1735)        # purple + desk edge + white desk
bg_dn, opa_dn = shifted(140, 0.2000)        # mousepad edge + pad surface (x=140 clears the mouse)
split = pad_y(xx) - 3.0
upper = yy < split
bg = np.where(upper[..., None], bg_up, bg_dn)
opa = np.where(upper, opa_up, opa_dn)
fill_zone = dil(piece, 3) & ~(pet & ~piece)
scene[fill_zone, 0:3] = np.clip(bg[fill_zone], 0, 255).astype(np.uint8)
scene[fill_zone, 3] = opa[fill_zone].astype(np.uint8)

# ---- close the body where the arm used to be -------------------------
# The arm *is* the creature's lower left lobe, so taking it away would leave a
# straight bite.  Fill that bite with the body's own colour, bounded by the
# convex hull of what is left, and give the new silhouette a black outline.
pet_static = pet & ~piece
ys2, xs2 = np.nonzero(pet_static)
pts = np.unique(np.column_stack([xs2, ys2]), axis=0)


def convex_hull(p):
    p = p[np.lexsort((p[:, 1], p[:, 0]))]

    def half(pp):
        out = []
        for q in pp:
            while len(out) >= 2 and (out[-1][0] - out[-2][0]) * (q[1] - out[-2][1]) - \
                    (out[-1][1] - out[-2][1]) * (q[0] - out[-2][0]) <= 0:
                out.pop()
            out.append(q)
        return out

    return np.array(half(p)[:-1] + half(p[::-1])[:-1])


hull = convex_hull(pts.astype(float))
himg = Image.new("L", (W, H), 0)
ImageDraw.Draw(himg).polygon([tuple(v) for v in hull], fill=255)
hullmask = np.asarray(himg) > 127
stub = hullmask & piece
print("hull pts", len(hull), "stub px", stub.sum())

# colours: carry the body's own pixels sideways into the stub
sy0, sy1 = np.nonzero(stub.any(1))[0][[0, -1]]
sx0, sx1 = np.nonzero(stub.any(0))[0][[0, -1]]
stub_rgb = np.zeros((H, W, 3), float)
have = np.zeros((H, W), bool)
for y in range(sy0, sy1 + 1):
    xs_stub = np.nonzero(stub[y, sx0:sx1 + 1])[0] + sx0
    if len(xs_stub) == 0:
        continue
    idx = np.nonzero(pet_static[y])[0]
    if len(idx) == 0:
        continue
    j = np.clip(np.searchsorted(idx, xs_stub), 0, len(idx) - 1)
    stub_rgb[y, xs_stub] = A[y, idx[j]]
    have[y, xs_stub] = True
# rows with no body pixels at all inherit the row above
for y in range(sy0 + 1, sy1 + 1):
    fill = stub[y] & ~have[y]
    if fill.any():
        stub_rgb[y, fill] = stub_rgb[y - 1, fill]
        have[y, fill] = True
print("stub rows filled", int(have[stub].sum()), "of", int(stub.sum()))
scene[stub, 0:3] = np.clip(stub_rgb[stub], 0, 255).astype(np.uint8)
scene[stub, 3] = 255

# black outline along the new silhouette (only above the desk edge, which is
# already drawn as a black line by the background reconstruction)
edge = stub & ~ero(hullmask, 11) & (yy < desk_y(xx) + 4)
scene[edge, 0:3] = 8
scene[edge, 3] = 255
print("outline px", edge.sum())

Image.fromarray(scene, "RGBA").save(os.path.join(OUT, "scene.png"))

# ---- the moving piece -------------------------------------------------
px0, px1 = xs.min(), xs.max() + 1
py0, py1 = ys.min(), ys.max() + 1
arm_rgba = scene.copy()          # placeholder (piece pixels were wiped in scene)
arm_rgba[..., 0:3] = np.clip(col, 0, 255).astype(np.uint8)
arm_rgba[..., 3] = (alpha * 255).astype(np.uint8)
arm_rgba[~piece] = 0
armimg = Image.fromarray(arm_rgba[py0:py1, px0:px1], "RGBA")
armimg.save(os.path.join(OUT, "arm.png"))

wm = np.zeros((H, W), np.uint8)
wm[piece] = (w[piece] * 255).astype(np.uint8)
Image.fromarray(wm[py0:py1, px0:px1], "L").save(os.path.join(OUT, "armw.png"))
arr = np.array([px0, py0, px1, py1, P1[0], P1[1], axis[0], axis[1], L1])
np.save(os.path.join(HERE, "piece_box.npy"), arr)
print("piece box", (px0, py0, px1, py1), "size", (px1 - px0, py1 - py0))

# ---------------------------------------------------------------- previews
def checker(size):
    c = Image.new("RGB", size, (205, 205, 212))
    d = ImageDraw.Draw(c)
    for yy2 in range(0, size[1], 24):
        for xx2 in range(0, size[0], 24):
            if (xx2 // 24 + yy2 // 24) % 2:
                d.rectangle([xx2, yy2, xx2 + 23, yy2 + 23], fill=(178, 178, 188))
    return c


sc = Image.open(os.path.join(OUT, "scene.png"))
ar = Image.open(os.path.join(OUT, "arm.png"))
pv = checker((W, H))
pv.paste(sc, (0, 0), sc)
pv.crop((0, 780 - TOP, 700, 1220 - TOP)).save(os.path.join(PRE, "scene_nopiece.png"))
pv2 = pv.copy()
pv2.paste(ar, (px0, py0), ar)
pv2.crop((0, 780 - TOP, 700, 1220 - TOP)).save(os.path.join(PRE, "scene_rest.png"))
print("done")
