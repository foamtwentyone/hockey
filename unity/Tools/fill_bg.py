# Заполняет пустое место текстуры (между UV-островами) цветом ближайшего острова.
# Иначе на мип-уровнях фон (белый, а он красится в цвет команды) просачивается тонкими «трещинами» по швам.
# python3 fill_bg.py <путь без расширения>  (нужны .obj и .png)
import sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

base = sys.argv[1]
img = Image.open(base + '.png').convert('RGB'); w, h = img.size
vt, faces = [], []
for line in open(base + '.obj'):
    if line.startswith('vt '):
        u, v = map(float, line.split()[1:3]); vt.append((u * w, (1 - v) * h))
    elif line.startswith('f '):
        faces.append([int(p.split('/')[1]) - 1 for p in line.split()[1:]])
mask = Image.new('L', (w, h), 0); dm = ImageDraw.Draw(mask)
for f in faces:
    pts = [vt[i] for i in f]
    dm.polygon(pts, fill=255, outline=255)
m = np.asarray(mask) > 0
m = ndimage.binary_erosion(m, iterations=1, border_value=1) | (ndimage.binary_dilation(m, iterations=1) & m)
arr = np.asarray(img).copy()
_, (iy, ix) = ndimage.distance_transform_edt(~m, return_indices=True)
arr = arr[iy, ix]
Image.fromarray(arr).save(base + '.png', optimize=True)
print(base, 'покрыто', round(m.mean() * 100, 1), '%')
