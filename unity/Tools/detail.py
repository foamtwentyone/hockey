# Детализация текстуры экипировки: затенение впадин (по вогнутости сетки), мягкий свет сверху,
# тёмные швы на границах цветов и лёгкая фактура материала. Форма модели не меняется.
#   python3 detail.py dir/name
import sys, numpy as np, trimesh
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage
base = sys.argv[1]
m = trimesh.load(base + '.obj', force='mesh', process=False)
img = Image.open(base + '.png').convert('RGB'); w, h = img.size
a = np.asarray(img).astype(float)
V = m.vertices; F = m.faces; uv = m.visual.uv
# сварка вершин по позициям, вогнутость: нормаль · (среднее соседей − вершина)
vid = np.unique(np.round(V, 5), axis=0, return_inverse=True)[1].ravel()
n = vid.max() + 1
P = np.zeros((n, 3)); np.add.at(P, vid, V); cnt = np.bincount(vid, minlength=n); P /= cnt[:, None]
Fv = vid[F]
fn = np.cross(P[Fv[:, 1]] - P[Fv[:, 0]], P[Fv[:, 2]] - P[Fv[:, 0]])
N = np.zeros((n, 3))
for k in range(3): np.add.at(N, Fv[:, k], fn)
N /= np.linalg.norm(N, axis=1)[:, None] + 1e-12
S = np.zeros((n, 3)); C = np.zeros(n)
for i, j in ((0, 1), (1, 2), (2, 0)):
    np.add.at(S, Fv[:, i], P[Fv[:, j]]); np.add.at(C, Fv[:, i], 1)
    np.add.at(S, Fv[:, j], P[Fv[:, i]]); np.add.at(C, Fv[:, j], 1)
conc = ((S / np.maximum(C, 1)[:, None] - P) * N).sum(1)
size = np.ptp(P, axis=0).max()
conc = conc / size
for _ in range(3):   # сгладить по соседям
    T = np.zeros(n); K = np.zeros(n)
    for i, j in ((0, 1), (1, 2), (2, 0)):
        np.add.at(T, Fv[:, i], conc[Fv[:, j]]); np.add.at(K, Fv[:, i], 1)
        np.add.at(T, Fv[:, j], conc[Fv[:, i]]); np.add.at(K, Fv[:, j], 1)
    conc = 0.5 * conc + 0.5 * T / np.maximum(K, 1)
shadeV = 1 - np.clip(conc * 60, -0.06, 0.2)            # впадины темнее, выпуклости чуть светлее
shadeV *= 0.93 + 0.07 * (N[:, 1] * 0.5 + 0.5)          # мягкий свет сверху
# карта затенения в UV: треугольники со средним значением, затем размытие
sh = Image.new('F', (w, h), 1.0); mk = Image.new('L', (w, h), 0)
d = ImageDraw.Draw(sh); dm = ImageDraw.Draw(mk)
fs = shadeV[Fv].mean(1)
for t in range(len(F)):
    pts = [((uv[j, 0] % 1) * w, (1 - uv[j, 1] % 1) * h) for j in F[t]]
    d.polygon(pts, fill=float(fs[t]), outline=float(fs[t])); dm.polygon(pts, fill=255, outline=255)
shade = np.asarray(sh).copy(); mask = np.asarray(mk) > 0
_, (iy, ix) = ndimage.distance_transform_edt(~mask, return_indices=True)
shade = ndimage.gaussian_filter(shade[iy, ix], 2.0)
# швы: границы между цветами палитры
q = (a // 16).astype(int); lab = q[..., 0] * 400 + q[..., 1] * 20 + q[..., 2]
edge = (ndimage.maximum_filter(lab, 5) != ndimage.minimum_filter(lab, 5)).astype(float)
edge = ndimage.gaussian_filter(edge, 1.2)
# фактура: мелкое зерно
rng = np.random.default_rng(3)
grain = ndimage.gaussian_filter(rng.normal(0, 1, (h, w)), 1.0) * 0.012
k = np.clip(shade * (1 - edge * 0.16) + grain, 0.76, 1.06)
out = np.clip(a * k[..., None], 0, 255).astype(np.uint8)
Image.fromarray(out).save(base + '.png', optimize=True)
print('detailed', base, 'shade', round(float(k.min()), 2), round(float(k.max()), 2))
