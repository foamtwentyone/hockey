# Перекраска ладони краги в бежевую кожу: грани с нормалью к ладони (-Z) ниже раструба.
#   python3 paint_palm.py dir/glove [cuffY=0.25] [color=d6b080]
import sys, numpy as np, trimesh
from PIL import Image, ImageDraw
base = sys.argv[1]; cuff = float(sys.argv[2]) if len(sys.argv) > 2 else 0.25
col = sys.argv[3] if len(sys.argv) > 3 else 'd6b080'
rgb = tuple(int(col[i:i + 2], 16) for i in (0, 2, 4))
m = trimesh.load(base + '.obj', force='mesh')
img = Image.open(base + '.png').convert('RGB'); w, h = img.size
a = np.asarray(img)
uv = m.visual.uv; F = m.faces; V = m.vertices
fn = m.face_normals; cy = V[F].mean(1)[:, 1]
cuv = uv[F].mean(1)
cc = a[np.clip(((1 - cuv[:, 1] % 1) * (h - 1)).astype(int), 0, h - 1), np.clip(((cuv[:, 0] % 1) * (w - 1)).astype(int), 0, w - 1)].astype(int)
white = (cc.min(1) > 170) & (cc.max(1) - cc.min(1) < 40)
sel = (fn[:, 2] < -0.5) & (cy < cuff) & white
d = ImageDraw.Draw(img)
for t in np.where(sel)[0]:
    pts = [((uv[j, 0] % 1) * w, (1 - uv[j, 1] % 1) * h) for j in F[t]]
    d.polygon(pts, fill=rgb, outline=rgb)
img.save(base + '.png')
print('painted faces', int(sel.sum()), 'of', len(F))
