#!/usr/bin/env python3
"""Thornwick texture checker. Run from the repo root:  python3 tools/check_textures.py
Checks every building sprite in assets/ for the mistakes that keep recurring:
  - size matches the footprint (1x1 32x48, 2x2 64x88, 3x3 96x128)
  - nothing touches the left/right/bottom edge (margin >= MARGIN px)
  - stone base / lowest pixels sit on the footprint diamond (within TOL px)
  - no pink/magenta fringe left over from the background
  - the 4 views of one building do not differ much in width
Ground tiles (assets/grass_N.png, grassfert_N.png) get their own check:
  - canvas is 2:1 (a diamond touching all four edges is only 2:1 if the canvas is)
  - magenta/transparent outside the diamond, no magenta inside it, diamond fills the image
  - the band along the diamond edge is one flat base colour (so tiles blend when repeated)
Exit code 1 if any FAIL, so it can run in CI or before a push."""
import glob, os, re, sys
from PIL import Image
import numpy as np

SIZES = {1: (32, 48), 2: (64, 88), 3: (96, 128)}
BUILDING = {'hall': 3, 'barracks': 2}          # everything else named house_* is 1x1
MARGIN = {1: 3, 2: 2, 3: 2}                    # min empty px at left/right/bottom
TOL = {1: 2, 2: 2, 3: 2}                       # max px the base may be off the diamond edge
fails = warns = 0

def size_of(name):
    for k, v in BUILDING.items():
        if name.startswith(k): return v
    return 1 if name.startswith('house') else None

def diamond_y(x, W, H, dw, dh):
    return H - dh / 2 + (dh / 2) * (1 - abs(x + 0.5 - W / 2) / (dw / 2))

groups = {}
for f in sorted(glob.glob('assets/*_[1-4].png')):
    base = os.path.basename(f)[:-4]
    name, view = base.rsplit('_', 1)
    sz = size_of(name)
    if not sz: continue
    W, H = SIZES[sz]
    im = Image.open(f).convert('RGBA')
    msgs = []
    if im.width != W: msgs.append(('FAIL', f'width {im.width}, expected {W}'))
    a = np.array(im)
    op = a[:, :, 3] > 128
    cols = np.nonzero(op.any(0))[0]
    if len(cols):
        l, r = cols[0], im.width - 1 - cols[-1]
        b = im.height - 1 - np.nonzero(op.any(1))[0][-1]
        if l < MARGIN[sz]: msgs.append(('FAIL', f'left margin {l}px (need {MARGIN[sz]}); {int(op[:, 0].sum())} px touch left edge'))
        if r < MARGIN[sz]: msgs.append(('FAIL', f'right margin {r}px (need {MARGIN[sz]}); {int(op[:, -1].sum())} px touch right edge'))
        if int(op[-1].sum()) > 8: msgs.append(('FAIL', f'{int(op[-1].sum())} px on the bottom row (cut off)'))
        groups.setdefault(name, []).append(cols[-1] - cols[0] + 1)
    # base vs diamond (skipped for 1x1: roof overhang makes the silhouette unreliable)
    if sz > 1:
        dw, dh = W, W // 2
        offs = []
        for fx in (0.15, 0.3, 0.7, 0.85):
            x = int(W / 2 + (fx - 0.5) * dw)
            ys = np.nonzero(op[:, x])[0]
            if len(ys): offs.append(ys.max() + 1 - diamond_y(x, W, H, dw, dh))
        if offs:
            worst = max(offs, key=abs)
            if abs(worst) > TOL[sz]: msgs.append(('FAIL', f'base is {worst:+.1f}px off the footprint edge (max {TOL[sz]})'))
        # no part below the bottom point, no steps poking out sideways handled by margin check
    # pink / magenta fringe
    r_, g_, b_ = a[:, :, 0].astype(int), a[:, :, 1].astype(int), a[:, :, 2].astype(int)
    pink = int((op & (r_ > 150) & (b_ > 100) & (b_ > g_ + 20) & (g_ < r_ - 40)).sum())
    if pink > 2: msgs.append(('FAIL', f'{pink} pink/magenta pixels'))
    # near-white speckle in the lowest 16 rows (noisy stone base)
    if sz > 1:
        lo = a[H - 16:]
        sp = int(((lo[:, :, 3] > 128) & (lo[:, :, 0] > 200) & (lo[:, :, 1] > 190) & (lo[:, :, 2] > 150)).sum())
        if sp > 10: msgs.append(('WARN', f'{sp} near-white speckle px in the base (should be ~0)'))
    for lvl, m in msgs:
        print(f'{lvl:4} {base}: {m}')
        if lvl == 'FAIL': fails += 1
        else: warns += 1

# ---------- ground tiles ----------
GROUND_BASE = {'grass': (0x5F, 0x9E, 0x48)}     # expected base colour, warn if the edge band is far from it

def is_bg(a):
    r, g, b, al = (a[:, :, i].astype(int) for i in range(4))
    return (al < 128) | ((r > 200) & (b > 200) & (g < 90))

def check_ground(f):
    base = os.path.basename(f)[:-4]
    msgs = []
    try:
        im = Image.open(f); im.load(); im = im.convert('RGBA')
    except Exception as ex:
        return base, [('FAIL', f'cannot decode the PNG ({ex}); the file is corrupt or truncated, re-export it and upload it again (the game ignores it)')]
    a = np.array(im); H, W = a.shape[:2]
    if abs(W / H - 2) > 0.02:
        msgs.append(('FAIL', f'canvas {W}x{H} is {W / H:.2f}:1, must be 2:1 (use 2048x1024); the diamond would be the wrong shape'))
    ys, xs = np.mgrid[0:H, 0:W]
    d = np.abs(2 * (xs + 0.5) / W - 1) + np.abs(2 * (ys + 0.5) / H - 1)     # 1.0 on the diamond edge
    bg = is_bg(a)
    out = d > 1.03; inn = d < 0.97
    if bg[out].mean() < 0.97:
        msgs.append(('FAIL', f'{100 * (1 - bg[out].mean()):.1f}% of the area outside the diamond is not magenta/transparent (ground or shadow around the tile?)'))
    if bg[inn].mean() > 0.001:
        msgs.append(('FAIL', f'{100 * bg[inn].mean():.1f}% of the diamond is magenta/transparent (diamond must fill the image edge to edge)'))
    band = (d > 0.86) & (d < 0.97) & ~bg
    if band.sum() > 100:
        px = a[:, :, :3][band].astype(int)
        med = np.median(px, axis=0)
        flat = (np.abs(px - med).sum(1) < 40).mean()
        if flat < 0.80: msgs.append(('FAIL', f'edge band is only {100 * flat:.0f}% one flat colour (keep ~2 game px along the edges plain; no tufts, outline or rim)'))
        elif flat < 0.92: msgs.append(('WARN', f'edge band is {100 * flat:.0f}% one flat colour; keep it plainer so tiles blend'))
        for k, c in GROUND_BASE.items():
            if base.startswith(k) and not base.startswith('grassfert') and np.abs(med - np.array(c)).sum() > 60:
                msgs.append(('WARN', f'edge colour {tuple(int(v) for v in med)} is far from the palette base {c}'))
        # tiles repeat, so busy contrast shows as a grid: report luminance spread inside the diamond
        lum = (0.3 * a[:, :, 0] + 0.59 * a[:, :, 1] + 0.11 * a[:, :, 2])[inn & ~bg]
        if lum.std() > 22: msgs.append(('WARN', f'high contrast (luminance std {lum.std():.0f}); tiles may show a visible grid when repeated'))
    return base, msgs

for f in sorted(glob.glob('assets/grass_[0-9].png') + glob.glob('assets/grassfert_[0-9].png')):
    base, msgs = check_ground(f)
    for lvl, m in msgs:
        print(f'{lvl:4} {base}: {m}')
        if lvl == 'FAIL': fails += 1
        else: warns += 1

for name, ws in groups.items():
    if max(ws) - min(ws) > 4:
        print(f'WARN {name}: view widths differ ({min(ws)}..{max(ws)} px); views should be the same size')
        warns += 1

print(f'\n{fails} FAIL, {warns} WARN')
sys.exit(1 if fails else 0)
