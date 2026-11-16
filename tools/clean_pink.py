#!/usr/bin/env python3
"""Thornwick: make stray pink/purple pixels see-through.  Run from the repo root:  python3 tools/clean_pink.py [files...]
Finds the same pixels tools/check_no_pink.py complains about and sets them to the flat magenta background colour (255,0,255). The game
removes magenta on load, so those pixels become transparent in game, and files keep the same format ChatGPT delivers (RGB on magenta).
Files that already have a real alpha channel get alpha 0 on those pixels instead. Nothing else in a file is touched."""
import glob, sys
from PIL import Image
import numpy as np
sys.path.insert(0, __file__.rsplit('/', 1)[0] if '/' in __file__ else '.')
from check_no_pink import key_out, pinkish

def clean(path):
    im = Image.open(path); has_alpha = im.mode in ('RGBA', 'LA') or 'transparency' in im.info
    raw = np.array(im.convert('RGBA')); a = raw.copy(); key_out(a)
    m = pinkish(a)
    # key_out clears background alpha only on the working copy; only pixels that are still visible AND pink are ours to fix
    n = int(m.sum())
    if not n: return 0
    if has_alpha: raw[m, 3] = 0
    else: raw[m, 0], raw[m, 1], raw[m, 2], raw[m, 3] = 255, 0, 255, 255
    Image.fromarray(raw if has_alpha else raw[:, :, :3]).save(path, optimize=True)
    return n

if __name__ == '__main__':
    files = sys.argv[1:] or sorted(glob.glob('assets/**/*.png', recursive=True))
    total = 0
    for f in files:
        n = clean(f)
        if n: print('%s: %d pixel(s) made see-through' % (f, n)); total += n
    print('%d pixel(s) cleaned' % total)
