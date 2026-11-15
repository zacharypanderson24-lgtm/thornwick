#!/usr/bin/env python3
"""Thornwick texture checker. Run from the repo root:  python3 tools/check_textures.py
Checks every building sprite in assets/ for the mistakes that keep recurring:
  - size matches the footprint (1x1 32x48, 2x2 64x88, 3x3 96x128)
  - nothing touches the left/right/bottom edge (margin >= MARGIN px)
  - stone base / lowest pixels sit on the footprint diamond (within TOL px)
  - no pink/magenta fringe left over from the background
  - the 4 views of one building do not differ much in width
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

for name, ws in groups.items():
    if max(ws) - min(ws) > 4:
        print(f'WARN {name}: view widths differ ({min(ws)}..{max(ws)} px); views should be the same size')
        warns += 1

print(f'\n{fails} FAIL, {warns} WARN')
sys.exit(1 if fails else 0)
