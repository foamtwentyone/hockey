# Модель из Meshy (glb с текстурой) -> OBJ + MTL + чистая PNG-текстура для Unity.
#   python3 meshy2obj.py in.glb outdir name [tris=25000] [rotY=0]
# 1) упрощение с сохранением UV (pymeshlab, quadric edge collapse with texture) — без дыр, без удаления граней;
# 2) текстура очищается: каждый пиксель приводится к ближайшему цвету палитры (белый/тёмный/красный/бежевый/коричневый)
#    с медианным сглаживанием — убирает пятнистость генерации.
import sys, os, numpy as np, trimesh, pymeshlab as ml
from PIL import Image, ImageFilter
src, outdir, name = sys.argv[1], sys.argv[2], sys.argv[3]
target = int(sys.argv[4]) if len(sys.argv) > 4 else 25000
rotY = float(sys.argv[5]) if len(sys.argv) > 5 else 0
cutY = float(sys.argv[6]) if len(sys.argv) > 6 else -1e9   # срезать всё ниже этой высоты (например, свисающий ремешок)
os.makedirs(outdir, exist_ok=True)
tmp = os.path.join(outdir, '_tmp'); os.makedirs(tmp, exist_ok=True)
m = trimesh.load(src, force='mesh')
if rotY: m.apply_transform(trimesh.transformations.rotation_matrix(np.radians(rotY), [0, 1, 0]))
img = m.visual.material.baseColorTexture.convert('RGB')
if cutY > -1e8:
    keep = ~(m.vertices[m.faces][:, :, 1] < cutY).all(axis=1)
    m.update_faces(keep); m.remove_unreferenced_vertices()
# --- чистка текстуры
a = np.asarray(img.filter(ImageFilter.MedianFilter(7))).astype(float)
pal = np.array([[238, 238, 234], [40, 40, 46], [214, 52, 56], [214, 176, 128], [120, 80, 52], [150, 150, 158]], float)
d = ((a[:, :, None, :] - pal[None, None]) ** 2).sum(-1)
q = pal[d.argmin(-1)].astype(np.uint8)
clean = Image.fromarray(q).filter(ImageFilter.ModeFilter(5)).resize((1024, 1024), Image.NEAREST)
m.visual.material.baseColorTexture = clean
m.export(os.path.join(tmp, 'in.obj'))
ms = ml.MeshSet()
ms.load_new_mesh(os.path.join(tmp, 'in.obj'))
ms.meshing_merge_close_vertices()
ms.meshing_decimation_quadric_edge_collapse_with_texture(targetfacenum=target, qualitythr=0.6, preserveboundary=True, preservenormal=True)
out = os.path.join(outdir, name + '.obj')
ms.save_current_mesh(out, save_textures=False)
clean.save(os.path.join(outdir, name + '.png'))
# MTL на нашу текстуру
with open(os.path.join(outdir, name + '.mtl'), 'w') as f:
    f.write(f'newmtl {name}\nKd 1 1 1\nmap_Kd {name}.png\n')
txt = open(out).read().splitlines()
txt = [l for l in txt if not l.startswith('mtllib') and not l.startswith('usemtl')]
open(out, 'w').write(f'mtllib {name}.mtl\nusemtl {name}\n' + '\n'.join(txt) + '\n')
for fn in os.listdir(tmp): os.remove(os.path.join(tmp, fn))
os.rmdir(tmp)
print(name, 'faces', ms.current_mesh().face_number(), 'verts', ms.current_mesh().vertex_number())
