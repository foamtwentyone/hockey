// Детальные «вылепленные» модели экипировки: шлем и краги.
// Форма задаётся полем расстояний (SDF) с плавным смешиванием частей, сетка строится алгоритмом Surface Nets,
// нормали — по градиенту поля (гладкие), цвета деталей запекаются в цвет вершин.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hockey
{
    public static class GearModels
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        // ---------------- примитивы поля расстояний ----------------
        static float Ellipsoid(Vector3 p, Vector3 r)
        {
            float k0 = new Vector3(p.x / r.x, p.y / r.y, p.z / r.z).magnitude;
            float k1 = new Vector3(p.x / (r.x * r.x), p.y / (r.y * r.y), p.z / (r.z * r.z)).magnitude;
            return k1 < 1e-6f ? -Mathf.Min(r.x, Mathf.Min(r.y, r.z)) : k0 * (k0 - 1) / k1;
        }

        static float Capsule(Vector3 p, Vector3 a, Vector3 b, float r)
        {
            Vector3 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        static float RoundBox(Vector3 p, Vector3 b, float r)
        {
            var q = new Vector3(Mathf.Abs(p.x) - b.x + r, Mathf.Abs(p.y) - b.y + r, Mathf.Abs(p.z) - b.z + r);
            return new Vector3(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0), Mathf.Max(q.z, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0) - r;
        }

        static float SMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1 - h);
        }

        static float SMax(float a, float b, float k) => -SMin(-a, -b, k);

        // ---------------- Surface Nets ----------------
        /// Строит гладкую сетку по полю расстояний внутри границ. res — число ячеек по самой длинной оси.
        public static Mesh Build(string key, Func<Vector3, float> sdf, Func<Vector3, Color> color, Bounds bounds, int res)
        {
            if (cache.TryGetValue(key, out var cached)) return cached;
            float cell = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) / res;
            int nx = Mathf.CeilToInt(bounds.size.x / cell) + 1, ny = Mathf.CeilToInt(bounds.size.y / cell) + 1, nz = Mathf.CeilToInt(bounds.size.z / cell) + 1;
            var o = bounds.min;
            var val = new float[nx * ny * nz];
            int Idx(int x, int y, int z) => (z * ny + y) * nx + x;
            for (int z = 0; z < nz; z++)
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++)
                        val[Idx(x, y, z)] = sdf(o + new Vector3(x, y, z) * cell);

            // вершина в каждой ячейке, через которую проходит поверхность
            var cellVert = new int[(nx - 1) * (ny - 1) * (nz - 1)];
            for (int i = 0; i < cellVert.Length; i++) cellVert[i] = -1;
            int CIdx(int x, int y, int z) => (z * (ny - 1) + y) * (nx - 1) + x;
            var V = new List<Vector3>(); var N = new List<Vector3>(); var C = new List<Color>();
            int[,] edges = { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
            var corner = new float[8];
            var cpos = new Vector3[8];
            for (int z = 0; z < nz - 1; z++)
                for (int y = 0; y < ny - 1; y++)
                    for (int x = 0; x < nx - 1; x++)
                    {
                        int inside = 0;
                        for (int c = 0; c < 8; c++)
                        {
                            int dx = c & 1, dy = (c >> 1) & 1, dz = (c >> 2) & 1;
                            corner[c] = val[Idx(x + dx, y + dy, z + dz)];
                            cpos[c] = new Vector3(x + dx, y + dy, z + dz);
                            if (corner[c] < 0) inside++;
                        }
                        if (inside == 0 || inside == 8) continue;
                        var sum = Vector3.zero; int cnt = 0;
                        for (int e = 0; e < 12; e++)
                        {
                            float a = corner[edges[e, 0]], b = corner[edges[e, 1]];
                            if ((a < 0) == (b < 0)) continue;
                            float t = a / (a - b);
                            sum += Vector3.Lerp(cpos[edges[e, 0]], cpos[edges[e, 1]], t);
                            cnt++;
                        }
                        var p = o + sum / cnt * cell;
                        cellVert[CIdx(x, y, z)] = V.Count;
                        V.Add(p);
                        float h = cell * 0.5f;
                        var g = new Vector3(sdf(p + Vector3.right * h) - sdf(p - Vector3.right * h),
                                            sdf(p + Vector3.up * h) - sdf(p - Vector3.up * h),
                                            sdf(p + Vector3.forward * h) - sdf(p - Vector3.forward * h));
                        N.Add(g.sqrMagnitude > 1e-12f ? g.normalized : Vector3.up);
                        C.Add(color(p));
                    }

            // грани: на каждое ребро сетки со сменой знака — четырёхугольник из соседних ячеек
            var T = new List<int>();
            void Quad(int a, int b, int c, int d)
            {
                if (a < 0 || b < 0 || c < 0 || d < 0) return;
                var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                var avgN = N[a] + N[b] + N[c] + N[d];
                if (Vector3.Dot(n, avgN) < 0) { T.Add(a); T.Add(c); T.Add(b); T.Add(a); T.Add(d); T.Add(c); }
                else { T.Add(a); T.Add(b); T.Add(c); T.Add(a); T.Add(c); T.Add(d); }
            }
            for (int z = 1; z < nz - 1; z++)
                for (int y = 1; y < ny - 1; y++)
                    for (int x = 0; x < nx - 1; x++)
                        if ((val[Idx(x, y, z)] < 0) != (val[Idx(x + 1, y, z)] < 0))
                            Quad(cellVert[CIdx(x, y - 1, z - 1)], cellVert[CIdx(x, y, z - 1)], cellVert[CIdx(x, y, z)], cellVert[CIdx(x, y - 1, z)]);
            for (int z = 1; z < nz - 1; z++)
                for (int y = 0; y < ny - 1; y++)
                    for (int x = 1; x < nx - 1; x++)
                        if ((val[Idx(x, y, z)] < 0) != (val[Idx(x, y + 1, z)] < 0))
                            Quad(cellVert[CIdx(x - 1, y, z - 1)], cellVert[CIdx(x, y, z - 1)], cellVert[CIdx(x, y, z)], cellVert[CIdx(x - 1, y, z)]);
            for (int z = 0; z < nz - 1; z++)
                for (int y = 1; y < ny - 1; y++)
                    for (int x = 1; x < nx - 1; x++)
                        if ((val[Idx(x, y, z)] < 0) != (val[Idx(x, y, z + 1)] < 0))
                            Quad(cellVert[CIdx(x - 1, y - 1, z)], cellVert[CIdx(x, y - 1, z)], cellVert[CIdx(x, y, z)], cellVert[CIdx(x - 1, y, z)]);

            var mesh = new Mesh { name = key };
            if (V.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetColors(C); mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds();
            cache[key] = mesh;
            return mesh;
        }

        // ---------------- шлем (в единицах головы: голова ≈ куб 1×1×1 с центром в нуле) ----------------
        static readonly Vector3 HelmC = new Vector3(0, 0.07f, -0.03f);

        static float HelmetShell(Vector3 p, out float inner, out float rib, out float ear)
        {
            var q = p - HelmC;
            float outer = Ellipsoid(q, new Vector3(0.6f, 0.57f, 0.63f));
            inner = Ellipsoid(q, new Vector3(0.53f, 0.505f, 0.56f));
            float shell = Mathf.Max(outer, -inner);
            // два рельефных ребра жёсткости сверху
            rib = Mathf.Max(Ellipsoid(q, new Vector3(0.635f, 0.605f, 0.665f)), Mathf.Abs(Mathf.Abs(q.x) - 0.17f) - 0.04f);
            rib = Mathf.Max(rib, 0.02f - q.y);
            shell = SMin(shell, Mathf.Max(rib, -inner), 0.025f);
            // нижний край: спереди выше (над бровями), сзади ниже (закрывает затылок)
            float edge = q.z > 0 ? 0.0f + 0.12f * q.z : -0.02f + 0.5f * q.z;
            shell = SMax(shell, edge - q.y, 0.03f);
            // ушные раковины
            var eq = new Vector3(Mathf.Abs(q.x) - 0.555f, q.y + 0.13f, q.z + 0.03f);
            ear = Ellipsoid(eq, new Vector3(0.065f, 0.2f, 0.19f));
            shell = SMin(shell, ear, 0.05f);
            // отверстия в ушах
            if (Mathf.Abs(q.x) > 0.45f)
            {
                float hole = new Vector2(q.y + 0.14f, q.z + 0.03f).magnitude - 0.045f;
                shell = SMax(shell, -hole, 0.01f);
            }
            // вентиляционные прорези между рёбрами
            for (int i = 0; i < 3; i++)
            {
                float vz = -0.25f + i * 0.24f;
                float slot = RoundBox(q - new Vector3(0, 0.6f, vz), new Vector3(0.05f, 0.2f, 0.07f), 0.03f);
                shell = SMax(shell, -slot, 0.015f);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                float slot = RoundBox(q - new Vector3(s * 0.38f, 0.42f, -0.12f), new Vector3(0.05f, 0.14f, 0.09f), 0.03f);
                shell = SMax(shell, -slot, 0.015f);
            }
            return shell;
        }

        public static Mesh Helmet(Color main, Color dark, Color padding)
        {
            string key = "helmet" + ColorUtility.ToHtmlStringRGB(main) + ColorUtility.ToHtmlStringRGB(dark);
            return Build(key,
                p => HelmetShell(p, out _, out _, out _),
                p =>
                {
                    HelmetShell(p, out float inner, out float rib, out float ear);
                    var q = p - HelmC;
                    if (inner > -0.035f && inner < 0.03f) return padding;               // мягкая подкладка внутри
                    if (rib < 0.012f && q.y > 0.05f) return dark;                       // рёбра
                    if (q.z > 0.5f && Mathf.Abs(q.x) < 0.11f && q.y > 0.06f && q.y < 0.2f) return Color.white; // наклейка спереди
                    if (ear < 0.01f && Mathf.Abs(q.x) > 0.5f) return Color.Lerp(main, dark, 0.35f);
                    // лёгкое затенение к нижнему краю
                    float g = Mathf.Lerp(0.82f, 1f, Mathf.Clamp01((q.y + 0.3f) / 0.7f));
                    return main * g;
                },
                new Bounds(HelmC + new Vector3(0, 0.05f, 0), new Vector3(1.45f, 1.35f, 1.5f)), 84);
        }

        /// Прозрачный визор, повторяющий форму шлема спереди.
        public static Mesh Visor()
        {
            return Build("visor", p =>
            {
                var q = p - HelmC;
                float d = Mathf.Max(Ellipsoid(q, new Vector3(0.635f, 0.6f, 0.675f)), -Ellipsoid(q, new Vector3(0.595f, 0.565f, 0.635f)));
                d = SMax(d, 0.18f - q.z, 0.03f);              // только спереди
                d = SMax(d, q.y - 0.04f, 0.02f);              // сверху — под краем шлема
                d = SMax(d, -0.24f - q.y, 0.03f);             // снизу — до носа
                return d;
            }, p => Color.white, new Bounds(HelmC + new Vector3(0, -0.1f, 0.4f), new Vector3(1.4f, 0.5f, 0.6f)), 96);
        }

        // ---------------- крага (пальцы вдоль +Y, тыльная сторона +Z, большой палец +X) ----------------
        static float GloveSdf(Vector3 p, out float cuff, out float finger, out float pad)
        {
            // раструб: расширяется к краю
            float t = Mathf.Clamp01(-(p.y + 0.18f) / 0.5f);
            var cq = new Vector2(p.x / (0.31f + 0.07f * t), p.z / (0.27f + 0.06f * t));
            float radial = (cq.magnitude - 1) * 0.29f;
            cuff = SMax(radial, Mathf.Abs(p.y + 0.43f) - 0.25f, 0.06f);
            // ладонь-«кулак»
            float hand = RoundBox(p - new Vector3(0, 0.04f, 0), new Vector3(0.28f, 0.22f, 0.18f), 0.11f);
            // мягкие валики на тыльной стороне
            pad = 1e3f;
            for (int i = 0; i < 3; i++)
                pad = Mathf.Min(pad, Capsule(p, new Vector3(-0.2f, -0.08f + i * 0.13f, 0.15f), new Vector3(0.2f, -0.08f + i * 0.13f, 0.15f), 0.07f));
            // пальцы-«колбаски», загнутые к ладони
            finger = 1e3f;
            for (int i = 0; i < 4; i++)
            {
                float fx = -0.2f + i * 0.13f;
                var a = new Vector3(fx, 0.2f, 0.04f);
                var b = new Vector3(fx, 0.46f - Mathf.Abs(i - 1.5f) * 0.03f, -0.06f);
                var c = new Vector3(fx, 0.52f - Mathf.Abs(i - 1.5f) * 0.03f, -0.2f);
                float f = Mathf.Min(Capsule(p, a, b, 0.075f), Capsule(p, b, c, 0.07f));
                finger = Mathf.Min(finger, f);
            }
            float thumb = Capsule(p, new Vector3(0.27f, -0.02f, 0.0f), new Vector3(0.38f, 0.22f, -0.1f), 0.085f);
            float d = SMin(cuff, hand, 0.07f);
            d = SMin(d, pad, 0.04f);
            d = SMin(d, finger, 0.035f);
            d = SMin(d, thumb, 0.05f);
            return d;
        }

        public static Mesh Glove(Color main, Color dark, Color palm)
        {
            string key = "glove" + ColorUtility.ToHtmlStringRGB(main) + ColorUtility.ToHtmlStringRGB(dark);
            return Build(key,
                p => GloveSdf(p, out _, out _, out _),
                p =>
                {
                    GloveSdf(p, out float cuff, out float finger, out float pad);
                    if (cuff < 0.02f && p.y < -0.18f)
                    {
                        if (p.y < -0.64f) return Color.Lerp(dark, Color.black, 0.5f);       // торец раструба
                        return p.y > -0.36f && p.y < -0.28f ? Color.white : dark;           // раструб с белой полосой
                    }
                    if (p.z < -0.08f && p.y > -0.18f && finger > 0.01f) return palm; // кожаная ладонь
                    if (pad < 0.015f && p.z > 0.12f) return Color.Lerp(main, Color.white, 0.18f);
                    if (finger < 0.012f) return Color.Lerp(main, dark, 0.15f);
                    return main;
                },
                new Bounds(new Vector3(0.05f, -0.06f, 0), new Vector3(1.0f, 1.45f, 0.75f)), 64);
        }
    }
}
