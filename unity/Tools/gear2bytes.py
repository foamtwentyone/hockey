# Конвертер модели экипировки (glb/obj) в компактный .bytes для игры:
# упрощение сетки, срез ниже cutY, гладкие нормали, категории цвета (0 — основной, 1 — тёмная подкладка внутри).
import sys, struct, numpy as np, trimesh, fast_simplification
src, dst, target, cutY = sys.argv[1], sys.argv[2], int(sys.argv[3]), float(sys.argv[4])
m = trimesh.load(src, force='mesh')
v = np.asarray(m.vertices, dtype=np.float64); f = np.asarray(m.faces)
# срез: убираем треугольники, целиком лежащие ниже cutY
keep = ~(v[f][:, :, 1] < cutY).all(axis=1)
f = f[keep]
v2, f2 = fast_simplification.simplify(v.astype(np.float32), f.astype(np.int32), target_reduction=1 - target / len(f))
mm = trimesh.Trimesh(v2, f2, process=True)
mm.remove_unreferenced_vertices()
# мелкие обрывки после среза
v, f = mm.vertices, mm.faces
n = mm.vertex_normals
center = v.mean(axis=0)
cat = np.zeros(len(v), dtype=np.uint8)
inward = ((v - center) * n).sum(axis=1) < 0
cat[inward] = 1
# glTF (правая система) -> Unity (левая): отражаем X и меняем обход треугольников
v = v.copy(); v[:, 0] *= -1; n = n.copy(); n[:, 0] *= -1
f = f[:, [0, 2, 1]]
with open(dst, 'wb') as o:
    o.write(b'PKM1'); o.write(struct.pack('<ii', len(v), len(f)))
    for i in range(len(v)):
        o.write(struct.pack('<6fB', *v[i], *n[i], cat[i]))
    o.write(np.asarray(f, dtype='<i4').tobytes())
print('verts', len(v), 'tris', len(f), 'bounds', v.min(0).round(2), v.max(0).round(2), 'inner', int(cat.sum()))
