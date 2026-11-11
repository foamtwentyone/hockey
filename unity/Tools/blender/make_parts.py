# Детали «игрушечного» хоккеиста PUCKO для ProBody.cs. Запуск: python3 make_parts.py  (нужен пакет bpy — Blender как модуль)
# Каждая деталь — OBJ в Assets/Hockey/Resources/Body. Цвет задаётся через UV: u = (индекс палитры + 0.5) / 8.
# Палитра: 0 основной цвет команды, 1 тёмный, 2 белый, 3 шорты, 4 чёрный, 5 сталь, 6 акцент (жёлтый), 7 запас.
# Оси в файле: x вправо, y вверх, z вперёд. Лимбы и корпус — в единичном кубе (их растягивает код), конёк — в метрах.
import os, math, numpy as np, bpy, bmesh
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../Assets/Hockey/Resources/Body')

def write(name, V, F, C):
    """V — вершины (x,y,z), F — грани (списки индексов, обход против часовой снаружи), C — индекс палитры на грань."""
    with open(os.path.join(OUT, name + '.obj'), 'w') as f:
        f.write('# PUCKO part: %s\n' % name)
        for v in V: f.write('v %.5f %.5f %.5f\n' % tuple(v))
        for i in range(8): f.write('vt %.4f 0.5\n' % ((i + 0.5) / 8))
        f.write('s 1\n')
        for face, c in zip(F, C): f.write('f ' + ' '.join('%d/%d' % (i + 1, c + 1) for i in face) + '\n')
    print(name, len(V), 'verts', len(F), 'faces')

def lathe(name, prof, seg=28):
    """Тело вращения вдоль Y: prof — [(t 0..1, радиус, цвет)], цвет действует от этой точки до следующей."""
    V, F, C = [], [], []
    for t, r, _ in prof:
        for k in range(seg):
            a = 2 * math.pi * k / seg
            V.append((r * math.cos(a), t - 0.5, r * math.sin(a)))
    for i in range(len(prof) - 1):
        for k in range(seg):
            a, b = i * seg + k, i * seg + (k + 1) % seg
            F.append([a, a + seg, b + seg, b]); C.append(prof[i][2])
    bot, top = len(V), len(V) + 1
    V += [(0, prof[0][0] - 0.5, 0), (0, prof[-1][0] - 0.5, 0)]
    last = (len(prof) - 1) * seg
    for k in range(seg):
        F.append([bot, k, (k + 1) % seg]); C.append(prof[0][2])
        F.append([top, last + (k + 1) % seg, last + k]); C.append(prof[-1][2])
    write(name, V, F, C)

def smooth_prof(pts, n=40):
    """Плавный профиль через опорные точки (t, r, цвет): косинусная интерполяция радиуса."""
    out = []
    for i in range(n + 1):
        t = i / n
        j = max(k for k in range(len(pts)) if pts[k][0] <= t + 1e-9); j = min(j, len(pts) - 2)
        t0, r0, c = pts[j]; t1, r1, _ = pts[j + 1]
        u = (t - t0) / max(1e-9, t1 - t0); u = (1 - math.cos(math.pi * min(1, max(0, u)))) / 2
        out.append((t, r0 + (r1 - r0) * u, c))
    return out

def recolor(prof, bands):
    """bands — [(t0, t1, цвет)]: полосы поверх профиля."""
    res = []
    for t, r, c in prof:
        for t0, t1, cc in bands:
            if t0 <= t < t1: c = cc
        res.append((t, r, c))
    return res

# ---------- руки и ноги (вдоль Y: −0.5 — сустав «a», +0.5 — сустав «b») ----------
lathe('uarm', recolor(smooth_prof([(0, 0.3, 0), (0.06, 0.56, 0), (0.3, 0.55, 0), (0.75, 0.5, 0), (0.94, 0.5, 0), (1, 0.34, 0)]), [(0.56, 0.66, 2), (0.69, 0.74, 1)]))
lathe('farm', recolor(smooth_prof([(0, 0.36, 1), (0.07, 0.6, 1), (0.22, 0.62, 1), (0.34, 0.5, 0), (0.8, 0.46, 0), (0.92, 0.5, 0), (1, 0.36, 0)]), [(0.5, 0.6, 2), (0.84, 1.01, 2)]))
lathe('thigh', smooth_prof([(0, 0.34, 3), (0.08, 0.56, 3), (0.5, 0.58, 3), (0.86, 0.54, 3), (0.94, 0.56, 1), (1, 0.4, 1)]))
lathe('shin', recolor(smooth_prof([(0, 0.4, 2), (0.07, 0.64, 2), (0.2, 0.66, 2), (0.3, 0.56, 0), (0.75, 0.5, 0), (0.93, 0.46, 0), (1, 0.34, 0)]), [(0.42, 0.52, 2), (0.56, 0.61, 1), (0.9, 1.01, 2)]))

# ---------- корпус-свитер (единичный куб): плечи шире талии, расклешённый подол, воротник ----------
def torso():
    seg, rings = 40, 44
    V, F, C = [], [], []
    def width(t):   # t: 0 низ … 1 верх
        ks = [(0, 0.47), (0.06, 0.5), (0.2, 0.47), (0.55, 0.47), (0.8, 0.5), (0.92, 0.44), (1, 0.2)]
        for (a, wa), (b, wb) in zip(ks, ks[1:]):
            if a <= t <= b:
                u = (1 - math.cos(math.pi * (t - a) / (b - a))) / 2; return wa + (wb - wa) * u
        return 0.2
    for j in range(rings + 1):
        t = j / rings; w = width(t); d = w * (0.94 if t < 0.85 else 0.9)
        for k in range(seg):
            a = 2 * math.pi * k / seg; c, s = math.cos(a), math.sin(a)
            e = 0.62   # «квадратность» сечения
            x = w * math.copysign(abs(c) ** e, c); z = d * math.copysign(abs(s) ** e, s)
            if s > 0 and 0.35 < t < 0.9: z *= 1.05     # грудь чуть выпуклее
            V.append((x, t - 0.5, z))
    for j in range(rings):
        t = (j + 0.5) / rings
        col = 0
        if 0.03 <= t < 0.085: col = 2
        elif 0.085 <= t < 0.115: col = 1
        elif t < 0.03: col = 1
        for k in range(seg):
            a = 2 * math.pi * (k + 0.5) / seg
            cc = col
            if t > 0.8 and col == 0: cc = 1                                    # тёмная кокетка на плечах
            if 0.76 < t <= 0.8 and col == 0: cc = 2                             # белый кант под кокеткой
            p, q = j * seg + k, j * seg + (k + 1) % seg
            F.append([p, q, q + seg, p + seg]); C.append(cc)
    bot, top = len(V), len(V) + 1
    V += [(0, -0.5, 0), (0, 0.5, 0)]
    for k in range(seg):
        F.append([bot, (k + 1) % seg, k]); C.append(1)
        F.append([top, rings * seg + k, rings * seg + (k + 1) % seg]); C.append(2)
    # воротник: белое кольцо вокруг шеи
    n0 = len(V); R, r, ts, ps = 0.2, 0.045, 28, 10
    for i in range(ts):
        A = 2 * math.pi * i / ts
        for jj in range(ps):
            B = 2 * math.pi * jj / ps
            V.append(((R + r * math.cos(B)) * math.cos(A), 0.47 + r * 1.4 * math.sin(B), (R * 0.9 + r * math.cos(B)) * math.sin(A)))
    for i in range(ts):
        for jj in range(ps):
            a = n0 + i * ps + jj; b = n0 + ((i + 1) % ts) * ps + jj; c = n0 + ((i + 1) % ts) * ps + (jj + 1) % ps; d = n0 + i * ps + (jj + 1) % ps
            F.append([a, d, c, b]); C.append(2)
    write('torso', V, F, C)
torso()

# ---------- шорты (единичный куб): пухлые, с белой боковой полосой ----------
def pants():
    seg, rings = 36, 20
    V, F, C = [], [], []
    for j in range(rings + 1):
        t = j / rings
        w = 0.5 * (math.sin(math.pi * (0.12 + 0.76 * t)) ** 0.5) * (0.92 + 0.08 * (1 - t))
        for k in range(seg):
            a = 2 * math.pi * k / seg; c, s = math.cos(a), math.sin(a); e = 0.7
            V.append((w * math.copysign(abs(c) ** e, c), t - 0.5, w * math.copysign(abs(s) ** e, s)))
    for j in range(rings):
        for k in range(seg):
            a = 2 * math.pi * (k + 0.5) / seg
            side = abs(math.sin(a)) < 0.16
            p, q = j * seg + k, j * seg + (k + 1) % seg
            F.append([p, q, q + seg, p + seg]); C.append(2 if side else 3)
    bot, top = len(V), len(V) + 1
    V += [(0, -0.5, 0), (0, 0.5, 0)]
    for k in range(seg):
        F.append([bot, (k + 1) % seg, k]); C.append(3)
        F.append([top, rings * seg + k, rings * seg + (k + 1) % seg]); C.append(3)
    write('pants', V, F, C)
pants()

# ---------- конёк (метры; ноль — голеностоп, носок по +Z): ботинок, белый стакан, стальное лезвие ----------
def from_bpy(objs):
    """Собрать объекты Blender (Z вверх, перед −Y) в общую сетку в осях файла."""
    V, F, C = [], [], []
    dg = bpy.context.evaluated_depsgraph_get()
    for ob, col in objs:
        me = ob.evaluated_get(dg).to_mesh(); n0 = len(V); M = ob.matrix_world
        for v in me.vertices:
            p = M @ v.co; V.append((p.x, p.z, -p.y))
        for poly in me.polygons:
            F.append([n0 + i for i in poly.vertices][::-1]); C.append(col)
    return V, F, C

def box(size, loc, bevel, sub=2, taper=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    ob = bpy.context.object; ob.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if taper:
        for v in ob.data.vertices: taper(v)
    m = ob.modifiers.new('b', 'BEVEL'); m.width = bevel; m.segments = 3
    if sub:
        s = ob.modifiers.new('s', 'SUBSURF'); s.levels = sub
    return ob

def skate():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    def boot_shape(v):   # задник выше (берец), носок ниже и уже
        f = (-v.co.y + 0.115) / 0.23          # 0 — пятка, 1 — носок
        if v.co.z > 0: v.co.z *= 1.0 - 0.55 * f
        v.co.x *= 1.0 - 0.18 * f
    boot = box((0.115, 0.235, 0.12), (0, -0.035, -0.005), 0.03, 2, boot_shape)
    cuff = box((0.118, 0.1, 0.05), (0, 0.045, 0.06), 0.02, 2)                      # берец
    toe = box((0.108, 0.07, 0.062), (0, -0.125, -0.028), 0.025, 2)                 # белый носок
    tongue = box((0.06, 0.08, 0.03), (0, -0.03, 0.045), 0.012, 2)                  # язычок
    hold = box((0.035, 0.225, 0.035), (0, -0.035, -0.078), 0.012, 1)               # стакан
    blade = box((0.012, 0.275, 0.022), (0, -0.035, -0.103), 0.005, 1)              # лезвие
    laces = [box((0.07, 0.012, 0.012), (0, -0.07 + i * 0.03, 0.035 + i * 0.012), 0.004, 1) for i in range(3)]
    V, F, C = from_bpy([(boot, 4), (cuff, 4), (toe, 2), (tongue, 0), (hold, 2), (blade, 5)] + [(l, 2) for l in laces])
    write('skate', V, F, C)
skate()

# ---------- клюшка: древко (метры; ноль — верхний хват, вниз по −Y до −1.12), набалдашник, цветные вставки ----------
def stick_shaft(L=1.12):
    seg = 20
    keys = [(0.1, 0.9, 2), (0.085, 1.35, 2), (0.045, 1.35, 2), (0.03, 1.0, 2), (-0.05, 1.0, 2), (-0.05, 1.0, 4), (-0.3, 1.0, 4), (-0.3, 1.04, 0), (-0.36, 1.04, 0),
            (-0.36, 1.04, 2), (-0.385, 1.04, 2), (-0.385, 1.0, 4), (-0.62, 1.0, 4), (-0.62, 1.04, 6), (-0.64, 1.04, 6), (-0.64, 1.04, 0), (-0.86, 1.02, 0),
            (-0.86, 1.02, 2), (-0.885, 1.02, 2), (-0.885, 1.0, 4), (-L, 1.08, 4)]
    V, F, C = [], [], []
    for y, k, _ in keys:
        for i in range(seg):
            a = 2 * math.pi * i / seg; c, s = math.cos(a), math.sin(a); e = 0.45
            V.append((0.016 * k * math.copysign(abs(c) ** e, c), y, 0.0125 * k * math.copysign(abs(s) ** e, s)))
    for j in range(len(keys) - 1):
        for i in range(seg):
            a, b = j * seg + i, j * seg + (i + 1) % seg
            F.append([a, b, b + seg, a + seg]); C.append(keys[j + 1][2])
    top, bot = len(V), len(V) + 1
    V += [(0, keys[0][0], 0), (0, keys[-1][0], 0)]
    last = (len(keys) - 1) * seg
    for i in range(seg):
        F.append([top, (i + 1) % seg, i]); C.append(2)
        F.append([bot, last + i, last + (i + 1) % seg]); C.append(4)
    write('stick', V, F, C)
stick_shaft()

# ---------- вратарь: щиток, блин, ловушка (метры, центр детали в нуле, лицевая сторона по +Z) ----------
def goalie_gear():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    base = box((0.23, 0.11, 0.56), (0, 0, 0), 0.045, 2)
    rolls = [box((0.2, 0.05, 0.085), (0, -0.05, -0.2 + i * 0.105), 0.03, 2) for i in range(5)]          # горизонтальные валики
    side = [box((0.045, 0.1, 0.5), (sx * 0.1, -0.035, 0.0), 0.02, 2) for sx in (-1, 1)]                  # боковые валики
    knee = box((0.25, 0.09, 0.12), (0, -0.02, 0.24), 0.04, 2)                                           # наколенник
    toe = box((0.24, 0.16, 0.09), (0, -0.045, -0.265), 0.035, 2)                                        # «ботинок» щитка
    V, F, C = from_bpy([(base, 2)] + [(r, 2 if i != 2 else 0) for i, r in enumerate(rolls)] + [(s, 1) for s in side] + [(knee, 0), (toe, 2)])
    write('gpad', V, F, C)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    board = box((0.21, 0.05, 0.36), (0, 0, 0.02), 0.022, 2)
    stripe = box((0.215, 0.055, 0.07), (0, 0, 0.1), 0.02, 2)
    edge = box((0.215, 0.055, 0.04), (0, 0, -0.13), 0.015, 2)
    cuff = box((0.15, 0.1, 0.12), (0, 0.06, -0.03), 0.04, 2)
    V, F, C = from_bpy([(board, 2), (stripe, 0), (edge, 1), (cuff, 4)])
    write('gblocker', V, F, C)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    def mitt(v):                                     # ловушка: шире сверху, «клюв» в сторону большого пальца
        v.co.x *= 1.0 + 0.35 * max(0, v.co.z / 0.2)
    body = box((0.3, 0.1, 0.36), (0, 0, 0.03), 0.07, 2, mitt)
    pocket = box((0.17, 0.05, 0.2), (0.02, -0.05, 0.07), 0.05, 2)
    web = [box((0.012, 0.03, 0.17), (-0.05 + i * 0.035, -0.07, 0.08), 0.004, 1) for i in range(5)]
    cuff2 = box((0.2, 0.1, 0.12), (0, 0.02, -0.19), 0.04, 2)
    band = box((0.205, 0.105, 0.035), (0, 0.02, -0.16), 0.012, 2)
    V, F, C = from_bpy([(body, 2), (pocket, 4), (cuff2, 2), (band, 0)] + [(w, 1) for w in web])
    write('gcatcher', V, F, C)
goalie_gear()
