# Перепекает текстуру модели чистыми однотонными областями — без «рваных» краёв.
#   python3 rebake.py dir/name "ecebe6,4a5266,d6b080" [palm=1] [cuffY=0.25]
# 1) каждой грани — ближайший цвет палитры по средней краске под ней;
# 2) несколько проходов «большинства соседей» по смежности граней — сглаживает границы, убирает пятна;
# 3) (palm) грани ладони (-Z, ниже раструба), бывшие белыми, — в цвет кожи;
# 4) текстура рисуется заново: UV-треугольник каждой грани заливается её цветом, плюс поля (dilation) против швов.
import sys, numpy as np, trimesh
from PIL import Image, ImageDraw, ImageFilter
base = sys.argv[1]
pal = np.array([[int(h[i:i + 2], 16) for i in (0, 2, 4)] for h in sys.argv[2].split(',')], float)
palm = len(sys.argv) > 3 and sys.argv[3] == '1'
cuff = float(sys.argv[4]) if len(sys.argv) > 4 else 0.25
m = trimesh.load(base + '.obj', force='mesh', process=False)
img = Image.open(base + '.png').convert('RGB'); w, h = img.size
a = np.asarray(img).astype(float)
uv = m.visual.uv; F = m.faces; V = m.vertices
# средний цвет под гранью: центр + 3 точки ближе к углам
samp = []
for wts in ([1/3, 1/3, 1/3], [.6, .2, .2], [.2, .6, .2], [.2, .2, .6]):
    p = (uv[F] * np.array(wts)[None, :, None]).sum(1)
    samp.append(a[np.clip(((1 - p[:, 1] % 1) * (h - 1)).astype(int), 0, h - 1), np.clip(((p[:, 0] % 1) * (w - 1)).astype(int), 0, w - 1)])
c = np.median(np.stack(samp), axis=0)
cls = ((c[:, None, :] - pal[None]) ** 2).sum(-1).argmin(1)
# смежность граней по общим рёбрам (по позициям, чтобы склеить UV-швы)
key = {}
vid = np.unique(np.round(V, 5), axis=0, return_inverse=True)[1].ravel()
Fv = vid[F]
edges = {}
for fi, (x, y, z) in enumerate(Fv):
    for e in ((x, y), (y, z), (z, x)):
        edges.setdefault((min(e), max(e)), []).append(fi)
nb = [[] for _ in range(len(F))]
for fs in edges.values():
    for i in fs:
        for j in fs:
            if i != j: nb[i].append(j)
area = m.area_faces
for it in range(4):
    new = cls.copy()
    for i in range(len(F)):
        if not nb[i]: continue
        votes = np.zeros(len(pal)); votes[cls[i]] += area[i] * 1.2
        for j in nb[i]: votes[cls[j]] += area[j]
        new[i] = votes.argmax()
    cls = new
if palm:
    fn = m.face_normals; cy = V[F].mean(1)[:, 1]
    white = 0; skin = len(pal) - 1
    sel = (fn[:, 2] < -0.5) & (cy < cuff) & (cls == white)
    cls[sel] = skin
    # кожа бывает только на стороне ладони; на боковых и тыльных валиках — белый
    cls[(cls == skin) & ((fn[:, 2] > -0.15) | (cy >= cuff))] = white
    for it in range(2):   # подчистить края ладони
        new = cls.copy()
        for i in range(len(F)):
            if cls[i] in (white, skin) and nb[i]:
                cnt = np.bincount(cls[nb[i]], minlength=len(pal))
                if cnt[skin] >= 2 and cls[i] == white and fn[i, 2] < -0.2 and cy[i] < cuff: new[i] = skin
                elif cnt[skin] == 0 and cls[i] == skin: new[i] = white
        cls = new
# новая текстура
out = Image.new('RGB', (w, h), tuple(int(x) for x in pal[0]))
mask = Image.new('L', (w, h), 0)
d = ImageDraw.Draw(out); dm = ImageDraw.Draw(mask)
for t in range(len(F)):
    pts = [((uv[j, 0] % 1) * w, (1 - uv[j, 1] % 1) * h) for j in F[t]]
    col = tuple(int(x) for x in pal[cls[t]])
    d.polygon(pts, fill=col, outline=col); dm.polygon(pts, fill=255, outline=255)
# поля: расширяем закрашенное наружу, чтобы на швах не было фона
arr = np.asarray(out).copy(); mk = np.asarray(mask) > 0
for _ in range(6):
    grown = np.asarray(Image.fromarray(arr).filter(ImageFilter.MaxFilter(3)))
    gm = np.asarray(Image.fromarray((mk * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3))) > 0
    new_px = gm & ~mk
    # для новых пикселей берём цвет соседа через медиану по закрашенным
    med = np.asarray(Image.fromarray(arr).filter(ImageFilter.ModeFilter(3)))
    arr[new_px] = med[new_px]; mk = gm
Image.fromarray(arr).save(base + '.png')
print('rebaked', base, np.bincount(cls, minlength=len(pal)))
