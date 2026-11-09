# Конвертер моделей экипировки (glb/obj, в т.ч. из Meshy) в компактный .bytes для игры.
#   python3 gear2bytes.py in.glb out_mesh.bytes [tris=8000] [cutY=-2] [preview.png] [rotY=0]
# rotY — поворот вокруг вертикали в градусах, чтобы лицевая сторона смотрела в +Z (вперёд).
# Упрощает сетку, срезает всё ниже cutY (в долях высоты от -1 до 1), переносит цвета текстуры в цвета вершин,
# переводит систему координат glTF -> Unity. Формат PKM2: позиции, нормали, цвет RGBA.
# Без текстуры пишет PKM1 с категориями (0 — цвет команды, 1 — тёмная подкладка).
import sys, struct, numpy as np, trimesh, fast_simplification
from scipy.spatial import cKDTree

src, dst = sys.argv[1], sys.argv[2]
target = int(sys.argv[3]) if len(sys.argv) > 3 else 8000
cutY = float(sys.argv[4]) if len(sys.argv) > 4 else -1e9
preview = sys.argv[5] if len(sys.argv) > 5 and sys.argv[5] != '-' else None
rotY = float(sys.argv[6]) if len(sys.argv) > 6 else 0
# clean — убрать внутренние слои и пятна: dark — тёмное допустимо только там, где по краске его большинство
clean = len(sys.argv) > 7 and sys.argv[7] == 'clean'

m = trimesh.load(src, force='mesh')
if rotY: m.apply_transform(trimesh.transformations.rotation_matrix(np.radians(rotY), [0, 1, 0]))
v = np.asarray(m.vertices, dtype=np.float64); f = np.asarray(m.faces)
col = None; img = None; uv = None
vis = m.visual
if isinstance(vis, trimesh.visual.TextureVisuals) and vis.uv is not None and getattr(vis.material, 'baseColorTexture', None) is not None:
    img = np.asarray(vis.material.baseColorTexture.convert('RGB'))
    h, w = img.shape[:2]
    uv = np.asarray(vis.uv)
    px = np.clip((uv[:, 0] % 1) * (w - 1), 0, w - 1).astype(int)
    py = np.clip((1 - uv[:, 1] % 1) * (h - 1), 0, h - 1).astype(int)
    col = img[py, px]
lo, hi = v[:, 1].min(), v[:, 1].max()
cut = lo + (cutY + 1) / 2 * (hi - lo) if cutY > -1e8 else -1e9
keep = ~(v[f][:, :, 1] < cut).all(axis=1)
v2, f2 = fast_simplification.simplify(v.astype(np.float32), f[keep].astype(np.int32), target_reduction=max(0.0, 1 - target / keep.sum()))
mm = trimesh.Trimesh(v2, f2, process=True)
mm.remove_unreferenced_vertices()
V, F, N = mm.vertices.copy(), mm.faces.copy(), mm.vertex_normals.copy()
if col is not None:
    # цвет каждого треугольника — из текстуры в ближайшей точке исходной поверхности (по центру и чуть внутрь углов),
    # затем вершины размножаются, чтобы цвета не «растекались» между соседними деталями
    src = trimesh.Trimesh(v, f[keep], process=False)
    uvk = uv
    tri = V[F]
    samples = np.concatenate([tri.mean(axis=1)[:, None], tri * 0.6 + tri.mean(axis=1)[:, None] * 0.4], axis=1)  # центр + 3 точки
    pts = samples.reshape(-1, 3)
    cp, dist, tid = trimesh.proximity.closest_point(src, pts)
    bary = trimesh.triangles.points_to_barycentric(src.triangles[tid], cp)
    fuv = (uvk[f[keep][tid]] * bary[:, :, None]).sum(axis=1)
    px = np.clip((fuv[:, 0] % 1) * (img.shape[1] - 1), 0, img.shape[1] - 1).astype(int)
    py = np.clip((1 - fuv[:, 1] % 1) * (img.shape[0] - 1), 0, img.shape[0] - 1).astype(int)
    sc = img[py, px].reshape(len(F), 4, 3).astype(float)
    fcol = np.median(sc, axis=1).astype(np.uint8)
    if clean:
        # 1) внутренние слои (смотрят внутрь модели) — выбрасываем
        cen = tri.mean(axis=1); fnorm = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
        outward = ((cen - V.mean(axis=0)) * fnorm).sum(axis=1) > 0
        # 2) сглаживание цвета по соседям: каждый треугольник принимает преобладающий класс в радиусе
        lum = fcol.mean(axis=1)
        fc = fcol.astype(int)
        red = (fc[:, 0] - (fc[:, 1] + fc[:, 2]) / 2 > 70) & (fc[:, 1] < 140)
        cls = np.where(red, 2, np.where(lum < 90, 1, 0))  # 0 светлый, 1 тёмный, 2 красный
        tree = cKDTree(cen)
        r = (V.max(0) - V.min(0)).max() * 0.04
        nb = tree.query_ball_point(cen, r)
        newcls = cls.copy()
        for i, ids in enumerate(nb):
            if not outward[i]: continue
            ids = [j for j in ids if outward[j]]
            if ids: newcls[i] = np.bincount(cls[ids], minlength=3).argmax()
        palette = np.array([[236, 236, 232], [34, 34, 40], [214, 48, 52]], dtype=np.uint8)
        fcol = palette[newcls]
        sel = outward
        tri = tri[sel]; fcol = fcol[sel]; N = N[F][sel]
        V = tri.reshape(-1, 3); N = N.reshape(-1, 3); C = np.repeat(fcol, 3, axis=0)
        F = np.arange(len(V)).reshape(-1, 3)
    if not clean:
        V = tri.reshape(-1, 3); N = N[F].reshape(-1, 3); C = np.repeat(fcol, 3, axis=0)
        F = np.arange(len(V)).reshape(-1, 3)
else:
    center = V.mean(axis=0)
    inward = ((V - center) * N).sum(axis=1) < 0
    C = None
# glTF -> Unity: отражение X и смена обхода треугольников
V[:, 0] *= -1; N[:, 0] *= -1; F = F[:, [0, 2, 1]]
with open(dst, 'wb') as o:
    o.write(b'PKM2' if C is not None else b'PKM1'); o.write(struct.pack('<ii', len(V), len(F)))
    for i in range(len(V)):
        if C is not None: o.write(struct.pack('<6f4B', *V[i], *N[i], *C[i], 255))
        else: o.write(struct.pack('<6fB', *V[i], *N[i], 1 if inward[i] else 0))
    o.write(np.asarray(F, dtype='<i4').tobytes())
print('verts', len(V), 'tris', len(F), 'bounds', V.min(0).round(2), V.max(0).round(2), 'colors' if C is not None else 'categories')

if preview:
    from PIL import Image, ImageDraw
    S = 360
    out = Image.new('RGB', (S * 3, S), (200, 200, 205))
    d = ImageDraw.Draw(out)
    fc = (C[F].mean(axis=1) if C is not None else np.full((len(F), 3), 220)).astype(int)
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-9
    ext = np.abs(V).max()
    # виды: спереди (смотрим из +Z), сбоку (из +X), сверху-спереди
    for k, (ax, ay, depth, sgn) in enumerate([(0, 1, 2, 1), (2, 1, 0, 1), (0, 2, 1, 1)]):
        order = np.argsort(sgn * V[F].mean(axis=1)[:, depth])
        for t in order:
            if sgn * fn[t, depth] < 0: continue
            pts = [(k * S + S / 2 + (-1 if ax == 0 else 1) * V[j, ax] / ext * S * 0.45, S / 2 - V[j, ay] / ext * S * 0.45 * (1 if ay == 1 else -1)) for j in F[t]]
            light = 0.55 + 0.45 * max(0, fn[t] @ np.array([0.3, 0.6, 0.75]) * (1 if True else 0))
            d.polygon(pts, fill=tuple(int(c * light) for c in fc[t]))
    out.save(preview)
