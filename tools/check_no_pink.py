#!/usr/bin/env python3
"""Thornwick: no pink or purple may get through in any texture.  Run from the repo root:  python3 tools/check_no_pink.py
ChatGPT art arrives on a flat magenta background. The game removes that background when it loads a file (src/textures.js keyOut:
flood fill from the edges, then every almost-exact magenta pixel, including pockets enclosed by the art). This script does the same,
then FAILS if anything pink, magenta or purple is still visible in any PNG under assets/: leftover background, pink halos around the
sprite, or stray purple pixels. Exit code 1 on any failure, so GitHub can block the push or pull request."""
import glob, os, sys
from PIL import Image
import numpy as np

KEY_TOL = 60            # same distance the game uses for the edge flood fill
HUE_LO, HUE_HI = 262, 338   # purple through pink/magenta, in degrees
SAT_MIN, VAL_MIN = 0.30, 0.30

def key_out(a):
    """Mimic keyOut() in src/textures.js on an RGBA uint8 array (modified in place)."""
    h, w = a.shape[:2]
    corners = [a[0, 0], a[0, w - 1], a[h - 1, 0], a[h - 1, w - 1]]
    if any(c[3] < 200 for c in corners): return
    key = np.round(np.mean([c[:3] for c in corners], axis=0)).astype(int)
    near = lambda px: ((px[:3].astype(int) - key) ** 2).sum() <= KEY_TOL * KEY_TOL
    if not all(near(c) for c in corners): return
    seen = np.zeros((h, w), bool); stack = []
    def push(x, y):
        if x < 0 or y < 0 or x >= w or y >= h or seen[y, x]: return
        seen[y, x] = True
        if near(a[y, x]): a[y, x, 3] = 0; stack.append((x, y))
    for x in range(w): push(x, 0); push(x, h - 1)
    for y in range(h): push(0, y); push(w - 1, y)
    while stack:
        x, y = stack.pop(); push(x + 1, y); push(x - 1, y); push(x, y + 1); push(x, y - 1)
    if key[0] > 200 and key[1] < 60 and key[2] > 200:      # enclosed magenta pockets
        m = (a[:, :, 3] > 0) & (a[:, :, 0] > 225) & (a[:, :, 1] < 40) & (a[:, :, 2] > 225)
        a[m, 3] = 0

def pinkish(a):
    """Boolean mask of visible pixels whose colour is pink, magenta or purple."""
    rgb = a[:, :, :3].astype(float) / 255
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    v, s = mx, np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-9), 0)
    d = np.maximum(mx - mn, 1e-9)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    hue = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    return (a[:, :, 3] > 40) & (hue >= HUE_LO) & (hue <= HUE_HI) & (s >= SAT_MIN) & (v >= VAL_MIN)

def main():
    files = sorted(glob.glob('assets/**/*.png', recursive=True))
    if not files: print('no textures found under assets/'); return 0
    bad_files = 0
    for f in files:
        try: a = np.array(Image.open(f).convert('RGBA'))
        except Exception as e: print('FAIL', f, 'cannot be decoded:', e); bad_files += 1; continue
        key_out(a)
        m = pinkish(a); n = int(m.sum())
        if n:
            ys, xs = np.where(m); ex = ', '.join('(%d,%d)=%s' % (x, y, tuple(int(c) for c in a[y, x, :3])) for y, x in list(zip(ys, xs))[:4])
            print('FAIL %s: %d pink/purple pixel(s) left after the background is removed, e.g. %s' % (f, n, ex)); bad_files += 1
    print('%d texture(s) checked, %d with pink or purple left over' % (len(files), bad_files))
    return 1 if bad_files else 0

if __name__ == '__main__': sys.exit(main())
