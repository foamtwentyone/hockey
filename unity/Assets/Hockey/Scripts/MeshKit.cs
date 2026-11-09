// Процедурные меши, текстуры и материалы — проекту не нужны внешние модели и картинки.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hockey
{
    public class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<Color> C = new List<Color>();
        public readonly List<int> T = new List<int>();

        /// Треугольник с проверкой ориентации: лицевая сторона смотрит по направлению desired.
        public void Tri(int a, int b, int c, Vector3 desired)
        {
            var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (Vector3.Dot(n, desired) < 0) { T.Add(a); T.Add(c); T.Add(b); }
            else { T.Add(a); T.Add(b); T.Add(c); }
        }

        public int Vert(Vector3 p, Vector3 n, Vector2 uv) { V.Add(p); N.Add(n); UV.Add(uv); C.Add(Color.white); return V.Count - 1; }
        public int Vert(Vector3 p, Vector3 n, Vector2 uv, Color c) { V.Add(p); N.Add(n); UV.Add(uv); C.Add(c); return V.Count - 1; }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Vector3 desired,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i0 = Vert(a, na, ua), i1 = Vert(b, nb, ub), i2 = Vert(c, nc, uc), i3 = Vert(d, nd, ud);
            Tri(i0, i1, i2, desired); Tri(i0, i2, i3, desired);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            => Quad(a, b, c, d, n, n, n, n, n, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));

        public void DoubleQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Vector2 uvScale)
        {
            var u0 = Vector2.zero; var u1 = new Vector2(uvScale.x, 0); var u2 = uvScale; var u3 = new Vector2(0, uvScale.y);
            Quad(a, b, c, d, n, n, n, n, n, u0, u1, u2, u3);
            Quad(a, b, c, d, -n, -n, -n, -n, -n, u0, u1, u2, u3);
        }

        public Mesh ToMesh(string name = "mesh")
        {
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetColors(C); m.SetTriangles(T, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    public static class MeshKit
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        // ------------------------------------------------ скруглённый кубик
        /// shade — насколько темнее низ кубика (мягкое «запечённое» затенение, как у игрушечных моделей).
        public static void RoundedBoxData(MeshBuilder mb, Vector3 size, float r, int m, Matrix4x4 xf, Vector2 uvOverride, bool useUvOverride, float shade = 0.2f)
        {
            Vector3 half = size * 0.5f;
            r = Mathf.Min(r, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) - 0.002f);
            r = Mathf.Max(r, 0.001f);
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var n in axes)
            {
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(n, u);
                float hn = Mathf.Abs(Vector3.Dot(half, n)), hu = Mathf.Abs(Vector3.Dot(half, u)), hv = Mathf.Abs(Vector3.Dot(half, v));
                var su = Samples(hu, r, m); var sv = Samples(hv, r, m);
                int baseIdx = mb.V.Count;
                for (int j = 0; j < sv.Count; j++)
                    for (int i = 0; i < su.Count; i++)
                    {
                        Vector3 p = n * hn + u * su[i] + v * sv[j];
                        Vector3 inner = new Vector3(Mathf.Clamp(p.x, -half.x + r, half.x - r), Mathf.Clamp(p.y, -half.y + r, half.y - r), Mathf.Clamp(p.z, -half.z + r, half.z - r));
                        Vector3 dir = (p - inner);
                        dir = dir.sqrMagnitude < 1e-10f ? n : dir.normalized;
                        Vector3 pos = inner + dir * r;
                        Vector2 uv = useUvOverride ? uvOverride : new Vector2((float)i / (su.Count - 1), (float)j / (sv.Count - 1));
                        float t = Mathf.Clamp01((pos.y + half.y) / Mathf.Max(1e-4f, size.y));
                        float g = 1 - shade * (1 - Mathf.SmoothStep(0, 1, t));
                        mb.Vert(xf.MultiplyPoint3x4(pos), xf.MultiplyVector(dir).normalized, uv, new Color(g, g, g, 1));
                    }
                Vector3 desired = xf.MultiplyVector(n);
                int w = su.Count;
                for (int j = 0; j < sv.Count - 1; j++)
                    for (int i = 0; i < w - 1; i++)
                    {
                        int a = baseIdx + j * w + i, b = a + 1, c = a + w + 1, d = a + w;
                        mb.Tri(a, b, c, desired); mb.Tri(a, c, d, desired);
                    }
            }
        }

        // точки вдоль грани: плотнее у скруглений (равномерно по углу)
        static List<float> Samples(float h, float r, int m)
        {
            var s = new List<float>();
            float inner = h - r;
            for (int k = m; k >= 1; k--) s.Add(-(inner + r * Mathf.Tan(k / (float)m * Mathf.PI / 4)));
            s.Add(-inner);
            if (inner > 1e-4f) s.Add(inner);
            for (int k = 1; k <= m; k++) s.Add(inner + r * Mathf.Tan(k / (float)m * Mathf.PI / 4));
            return s;
        }

        public static Mesh RoundedBox(float w, float h, float d, float r, int m = 3, float shade = 0.2f)
        {
            string key = $"rb{w:F3}|{h:F3}|{d:F3}|{r:F3}|{m}|{shade:F2}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var mb = new MeshBuilder();
            RoundedBoxData(mb, new Vector3(w, h, d), r, m, Matrix4x4.identity, Vector2.zero, false, shade);
            mesh = mb.ToMesh(key);
            cache[key] = mesh;
            return mesh;
        }

        static float SPow(float v, float e) => Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), e);

        /// Гладкий суперэллипсоид (от «подушки» до шара). e — «квадратность» (1 — эллипсоид, 0.3 — почти кубик).
        /// latFrom/latTo — диапазон широты в радианах (-π/2..π/2), lonSkip — вырезать сектор спереди (+Z) на этот полуугол.
        public static Mesh Superellipsoid(float rx, float ry, float rz, float e1 = 0.7f, float e2 = 0.7f, int seg = 28,
            float latFrom = -Mathf.PI / 2, float latTo = Mathf.PI / 2, float lonSkip = 0, float shade = 0.18f)
        {
            string key = $"se{rx:F3}|{ry:F3}|{rz:F3}|{e1:F2}|{e2:F2}|{seg}|{latFrom:F2}|{latTo:F2}|{lonSkip:F2}|{shade:F2}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            int rings = seg / 2, cols = seg;
            bool closedLon = lonSkip <= 0;
            var V = new List<Vector3>(); var C = new List<Color>(); var T = new List<int>();
            for (int i = 0; i <= rings; i++)
            {
                float u = Mathf.Lerp(latFrom, latTo, (float)i / rings);
                for (int j = 0; j <= cols; j++)
                {
                    float v = closedLon ? -Mathf.PI + j * Mathf.PI * 2 / cols : Mathf.Lerp(lonSkip, Mathf.PI * 2 - lonSkip, (float)j / cols);
                    // v=0 соответствует +Z (вперёд)
                    float cu = Mathf.Cos(u), su = Mathf.Sin(u);
                    var p = new Vector3(rx * SPow(cu, e1) * SPow(Mathf.Sin(v), e2), ry * SPow(su, e1), rz * SPow(cu, e1) * SPow(Mathf.Cos(v), e2));
                    V.Add(p);
                    float g = 1 - shade * (1 - Mathf.SmoothStep(0, 1, su * 0.5f + 0.5f));
                    C.Add(new Color(g, g, g, 1));
                }
            }
            int w = cols + 1;
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < cols; j++)
                {
                    int a0 = i * w + j, b = a0 + 1, c = a0 + w + 1, d = a0 + w;
                    // наружу смотрит направление от центра
                    var center = (V[a0] + V[c]) * 0.5f;
                    var n = Vector3.Cross(V[b] - V[a0], V[c] - V[a0]);
                    if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(V[c] - V[a0], V[d] - V[a0]);
                    if (Vector3.Dot(n, center) < 0) { T.Add(a0); T.Add(c); T.Add(b); T.Add(a0); T.Add(d); T.Add(c); }
                    else { T.Add(a0); T.Add(b); T.Add(c); T.Add(a0); T.Add(c); T.Add(d); }
                }
            mesh = new Mesh { name = key };
            mesh.SetVertices(V); mesh.SetColors(C); mesh.SetTriangles(T, 0);
            mesh.RecalculateNormals();
            // шов по долготе: усредняем нормали первой и последней колонки
            if (closedLon)
            {
                var N = mesh.normals;
                for (int i = 0; i <= rings; i++) { var avg = (N[i * w] + N[i * w + cols]).normalized; N[i * w] = N[i * w + cols] = avg; }
                mesh.normals = N;
            }
            mesh.RecalculateBounds();
            cache[key] = mesh;
            return mesh;
        }

        /// Тело вращения по профилю (r, y) снизу вверх — гладкое, с усреднёнными нормалями.
        public static Mesh Lathe(Vector2[] profile, int seg, System.Func<float, float, float> radiusMod = null, string key = null)
        {
            if (key != null && cache.TryGetValue(key, out var cached)) return cached;
            var V = new List<Vector3>(); var T = new List<int>();
            int n = profile.Length, w = seg + 1;
            for (int i = 0; i < n; i++)
                for (int j = 0; j <= seg; j++)
                {
                    float a = j * Mathf.PI * 2 / seg;
                    float r = profile[i].x * (radiusMod != null ? radiusMod(a, profile[i].y) : 1);
                    V.Add(new Vector3(Mathf.Cos(a) * r, profile[i].y, Mathf.Sin(a) * r));
                }
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a0 = i * w + j, b = a0 + 1, c = a0 + w + 1, d = a0 + w;
                    // при обходе снизу вверх против часовой стрелки нормаль смотрит наружу
                    T.Add(a0); T.Add(c); T.Add(b); T.Add(a0); T.Add(d); T.Add(c);
                }
            var m = new Mesh { name = key ?? "lathe" };
            m.SetVertices(V); m.SetTriangles(T, 0);
            m.RecalculateNormals();
            var N = m.normals;
            for (int i = 0; i < n; i++) { var avg = (N[i * w] + N[i * w + seg]).normalized; N[i * w] = N[i * w + seg] = avg; }
            m.normals = N;
            var C = new Color[V.Count]; for (int i = 0; i < C.Length; i++) C[i] = Color.white; m.colors = C;
            m.RecalculateBounds();
            if (key != null) cache[key] = m;
            return m;
        }

        /// Изогнутый крюк клюшки: лофт скруглённого прямоугольника вдоль дуги (длина по +Z, изгиб в сторону +X).
        public static Mesh Blade(float len, float height, float thick, float curve, int seg = 14)
        {
            string key = $"blade{len:F2}|{height:F2}|{thick:F3}|{curve:F2}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            const int P = 12;
            var prof = new Vector2[P];
            for (int k = 0; k < P; k++)
            {
                float a = k * Mathf.PI * 2 / P;
                prof[k] = new Vector2(SPow(Mathf.Cos(a), 0.35f) * thick / 2, SPow(Mathf.Sin(a), 0.35f) * height / 2);
            }
            var V = new List<Vector3>(); var T = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float t = (float)i / seg;
                var c = new Vector3(curve * t * t, 0, t * len);
                var tan = new Vector3(2 * curve * t, 0, len).normalized;
                var side = Vector3.Cross(Vector3.up, tan).normalized;
                float taper = Mathf.Lerp(1, 0.85f, t) * (t > 0.9f ? Mathf.Lerp(1, 0.75f, (t - 0.9f) / 0.1f) : 1);
                for (int k = 0; k <= P; k++) { var q = prof[k % P]; V.Add(c + side * q.x + Vector3.up * q.y * taper); }
            }
            int w = P + 1;
            for (int i = 0; i < seg; i++)
                for (int k = 0; k < P; k++)
                {
                    int a0 = i * w + k, b = a0 + 1, cc = a0 + w + 1, d = a0 + w;
                    T.Add(a0); T.Add(b); T.Add(cc); T.Add(a0); T.Add(cc); T.Add(d);
                }
            // торцы
            int c0 = V.Count; V.Add(Vector3.zero); for (int k = 0; k < P; k++) { T.Add(c0); T.Add(k + 1); T.Add(k); }
            var end = new Vector3(curve, 0, len); int c1 = V.Count; V.Add(end);
            for (int k = 0; k < P; k++) { T.Add(c1); T.Add(seg * w + k); T.Add(seg * w + k + 1); }
            mesh = new Mesh { name = key };
            mesh.SetVertices(V); mesh.SetTriangles(T, 0);
            // ориентацию граней выравниваем по направлению от оси
            var tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                var pa = V[tris[i]]; var pb = V[tris[i + 1]]; var pc = V[tris[i + 2]];
                var mid = (pa + pb + pc) / 3;
                float t = Mathf.Clamp01(mid.z / len);
                var axis = new Vector3(curve * t * t, 0, t * len);
                var outward = mid - axis;
                if (i >= seg * P * 6) outward = i < seg * P * 6 + P * 3 ? Vector3.back : Vector3.forward;
                if (Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), outward) < 0) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
            }
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            var C = new Color[V.Count]; for (int i = 0; i < C.Length; i++) C[i] = Color.white; mesh.colors = C;
            mesh.RecalculateBounds();
            cache[key] = mesh;
            return mesh;
        }

        /// Плоская звёздочка (для «ПОВ!»-вспышек и звёзд над головой).
        public static Mesh Star(float r, float inner = 0.45f, int points = 5)
        {
            string key = $"star{r:F2}|{inner:F2}|{points}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var mb = new MeshBuilder();
            int c = mb.Vert(Vector3.zero, Vector3.back, new Vector2(0.5f, 0.5f));
            int n = points * 2;
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2 / n + Mathf.PI / 2;
                float rr = i % 2 == 0 ? r : r * inner;
                mb.Vert(new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, 0), Vector3.back, Vector2.zero);
            }
            for (int i = 0; i < n; i++) { mb.Tri(c, c + 1 + i, c + 2 + i, Vector3.back); mb.Tri(c, c + 2 + i, c + 1 + i, Vector3.forward); }
            mesh = mb.ToMesh(key);
            cache[key] = mesh;
            return mesh;
        }

        static readonly Dictionary<Mesh, Mesh> flatCache = new Dictionary<Mesh, Mesh>();

        /// Копия меша с плоским затенением по граням (стиль KayKit).
        public static Mesh Faceted(Mesh src)
        {
            if (src == null) return null;
            if (flatCache.TryGetValue(src, out var m)) return m;
            var v = src.vertices; var t = src.triangles; var c = src.colors;
            bool hasC = c != null && c.Length == v.Length;
            var V = new List<Vector3>(t.Length); var N = new List<Vector3>(t.Length); var C = new List<Color>(t.Length); var T = new List<int>(t.Length);
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], cc = v[t[i + 2]];
                var n = Vector3.Cross(b - a, cc - a);
                if (n.sqrMagnitude < 1e-16f) continue;
                n.Normalize();
                var col = hasC ? (c[t[i]] + c[t[i + 1]] + c[t[i + 2]]) / 3 : Color.white;
                for (int k = 0; k < 3; k++) { T.Add(V.Count); V.Add(v[t[i + k]]); N.Add(n); C.Add(col); }
            }
            m = new Mesh { name = src.name + "_flat" };
            if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(V); m.SetNormals(N); m.SetColors(C); m.SetTriangles(T, 0);
            m.RecalculateBounds();
            flatCache[src] = m;
            return m;
        }

        /// Вывернутая наизнанку копия меша (для купола неба).
        public static Mesh Flip(Mesh src)
        {
            var m = Object.Instantiate(src);
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            m.triangles = t;
            var n = m.normals;
            for (int i = 0; i < n.Length; i++) n[i] = -n[i];
            m.normals = n;
            return m;
        }

        public static Mesh Box(float w, float h, float d) => RoundedBox(w, h, d, 0.004f, 1, 0);

        /// Сфера (UV-сфера), с лёгким затенением снизу.
        public static Mesh Sphere(float r, int seg = 16, float shade = 0.2f)
        {
            string key = $"sph{r:F3}|{seg}|{shade:F2}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var mb = new MeshBuilder();
            int rings = seg / 2;
            for (int i = 0; i <= rings; i++)
                for (int j = 0; j <= seg; j++)
                {
                    float th = i * Mathf.PI / rings, ph = j * Mathf.PI * 2 / seg;
                    var n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    float g = 1 - shade * (1 - Mathf.SmoothStep(0, 1, n.y * 0.5f + 0.5f));
                    mb.Vert(n * r, n, new Vector2((float)j / seg, 1 - (float)i / rings), new Color(g, g, g, 1));
                }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * (seg + 1) + j, b = a + 1, c = a + seg + 2, d = a + seg + 1;
                    var center = (mb.V[a] + mb.V[c]) * 0.5f;
                    mb.Tri(a, b, c, center); mb.Tri(a, c, d, center);
                }
            mesh = mb.ToMesh(key);
            cache[key] = mesh;
            return mesh;
        }

        /// Капсула-«боб» вдоль Y (для глаз, рук и т.п.).
        public static Mesh Capsule(float r, float h, int seg = 14)
        {
            string key = $"cap{r:F3}|{h:F3}|{seg}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var mb = new MeshBuilder();
            int rings = seg / 2;
            float half = Mathf.Max(0, h / 2 - r);
            for (int i = 0; i <= rings + 1; i++)
            {
                int ii = i <= rings / 2 ? i : i - 1;
                float th = ii * Mathf.PI / rings;
                float yo = i <= rings / 2 ? half : -half;
                for (int j = 0; j <= seg; j++)
                {
                    float ph = j * Mathf.PI * 2 / seg;
                    var n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    mb.Vert(n * r + Vector3.up * yo, n, new Vector2((float)j / seg, (float)i / (rings + 1)));
                }
            }
            for (int i = 0; i < rings + 1; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * (seg + 1) + j, b = a + 1, c = a + seg + 2, d = a + seg + 1;
                    var nrm = mb.N[a] + mb.N[c];
                    if (nrm.sqrMagnitude < 1e-6f) nrm = mb.V[a] + mb.V[c];
                    mb.Tri(a, b, c, nrm); mb.Tri(a, c, d, nrm);
                }
            mesh = mb.ToMesh(key);
            cache[key] = mesh;
            return mesh;
        }

        public static Mesh Cylinder(float radius, float height, int seg = 14)
        {
            string key = $"cyl{radius:F3}|{height:F3}|{seg}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var mb = new MeshBuilder();
            float hh = height / 2;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                var n0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var n1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                mb.Quad(n0 * radius + Vector3.down * hh, n1 * radius + Vector3.down * hh, n1 * radius + Vector3.up * hh, n0 * radius + Vector3.up * hh,
                    n0, n1, n1, n0, (n0 + n1) * 0.5f, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                int c = mb.Vert(Vector3.up * hh, Vector3.up, new Vector2(0.5f, 0.5f));
                int p0 = mb.Vert(n0 * radius + Vector3.up * hh, Vector3.up, Vector2.zero), p1 = mb.Vert(n1 * radius + Vector3.up * hh, Vector3.up, Vector2.zero);
                mb.Tri(c, p0, p1, Vector3.up);
                int cb = mb.Vert(Vector3.down * hh, Vector3.down, new Vector2(0.5f, 0.5f));
                int q0 = mb.Vert(n0 * radius + Vector3.down * hh, Vector3.down, Vector2.zero), q1 = mb.Vert(n1 * radius + Vector3.down * hh, Vector3.down, Vector2.zero);
                mb.Tri(cb, q0, q1, Vector3.down);
            }
            mesh = mb.ToMesh(key);
            cache[key] = mesh;
            return mesh;
        }

        public static Mesh Cone(float radius, float height, int seg = 4)
        {
            var mb = new MeshBuilder();
            var tip = Vector3.down * height / 2;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg + Mathf.PI / 4, a1 = (i + 1) * Mathf.PI * 2 / seg + Mathf.PI / 4;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, height / 2, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, height / 2, Mathf.Sin(a1) * radius);
                var n = Vector3.Cross(p1 - p0, tip - p0).normalized;
                var outward = new Vector3(Mathf.Cos((a0 + a1) / 2), 0, Mathf.Sin((a0 + a1) / 2));
                if (Vector3.Dot(n, outward) < 0) n = -n;
                int i0 = mb.Vert(p0, n, Vector2.zero), i1 = mb.Vert(p1, n, Vector2.zero), i2 = mb.Vert(tip, n, Vector2.zero);
                mb.Tri(i0, i1, i2, n);
                int c = mb.Vert(Vector3.up * height / 2, Vector3.up, Vector2.zero);
                int j0 = mb.Vert(p0, Vector3.up, Vector2.zero), j1 = mb.Vert(p1, Vector3.up, Vector2.zero);
                mb.Tri(c, j0, j1, Vector3.up);
            }
            return mb.ToMesh("cone");
        }

        /// Плоское кольцо на льду (для подсветки игроков и шайбы).
        public static Mesh FlatRing(float r0, float r1, int seg = 40)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                mb.Quad(d0 * r0, d1 * r0, d1 * r1, d0 * r1, Vector3.up);
            }
            return mb.ToMesh("ring");
        }

        public static Mesh Plane(float w, float h)
        {
            var mb = new MeshBuilder();
            // плоскость в XY, лицом к -Z (как стандартный Quad)
            mb.Quad(new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0), new Vector3(w / 2, h / 2, 0), new Vector3(-w / 2, h / 2, 0),
                Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            return mb.ToMesh("plane");
        }

        // ------------------------------------------------ контур площадки
        public struct PerimPoint { public float X, Z, Nx, Nz, S; }

        /// Точки по периметру скруглённого прямоугольника, смещённого наружу на off.
        public static List<PerimPoint> Perimeter(float off, int arcSeg = 14)
        {
            float hx = Rink.L / 2 + off, hz = Rink.W / 2 + off, r = Rink.R + off;
            float cx = hx - r, cz = hz - r;
            var pts = new List<PerimPoint>();
            int[] sx = { 1, -1, -1, 1 }, sz = { 1, 1, -1, -1 };
            float[] start = { 0, Mathf.PI / 2, Mathf.PI, Mathf.PI * 1.5f };
            for (int c = 0; c < 4; c++)
            {
                for (int k = 0; k <= arcSeg; k++)
                {
                    float a = start[c] + k / (float)arcSeg * Mathf.PI / 2;
                    float nx = Mathf.Cos(a), nz = Mathf.Sin(a);
                    pts.Add(new PerimPoint { X = sx[c] * cx + nx * r, Z = sz[c] * cz + nz * r, Nx = nx, Nz = nz });
                }
            }
            float s = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                if (i > 0) s += Mathf.Sqrt((p.X - pts[i - 1].X) * (p.X - pts[i - 1].X) + (p.Z - pts[i - 1].Z) * (p.Z - pts[i - 1].Z));
                p.S = s; pts[i] = p;
            }
            return pts;
        }

        /// Точка на контуре на расстоянии s от начала (с интерполяцией по прямым участкам).
        /// Полная длина замкнутого контура (включая отрезок от последней точки к первой).
        public static float Length(List<PerimPoint> pts)
        {
            var l = pts[pts.Count - 1]; var f = pts[0];
            return l.S + Mathf.Sqrt((l.X - f.X) * (l.X - f.X) + (l.Z - f.Z) * (l.Z - f.Z));
        }

        public static PerimPoint PointAt(List<PerimPoint> pts, float s)
        {
            for (int i = 1; i <= pts.Count; i++)
            {
                var b = i < pts.Count ? pts[i] : pts[0];
                float bs = i < pts.Count ? b.S : Length(pts);
                if (bs < s) continue;
                var a = pts[i - 1];
                b.S = bs;
                float t = (b.S - a.S) < 1e-5f ? 0 : (s - a.S) / (b.S - a.S);
                var nx = Mathf.Lerp(a.Nx, b.Nx, t); var nz = Mathf.Lerp(a.Nz, b.Nz, t);
                float l = Mathf.Sqrt(nx * nx + nz * nz);
                return new PerimPoint { X = Mathf.Lerp(a.X, b.X, t), Z = Mathf.Lerp(a.Z, b.Z, t), Nx = nx / l, Nz = nz / l, S = s };
            }
            return pts[pts.Count - 1];
        }

        /// Кольцевая стенка вокруг площадки (борта, трибуны).
        public static Mesh RingWall(float innerOff, float outerOff, float y0, float y1, bool inner = true, bool outer = true, bool top = true, float uMeters = 4)
        {
            var mb = new MeshBuilder();
            var pi = Perimeter(innerOff); var po = Perimeter(outerOff);
            int n = pi.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var a = pi[i]; var b = pi[j]; var c = po[i]; var d = po[j];
                var na = new Vector3(a.Nx, 0, a.Nz); var nb = new Vector3(b.Nx, 0, b.Nz);
                float u0 = a.S / uMeters, u1 = (j == 0 ? Length(pi) : b.S) / uMeters;
                if (inner)
                    mb.Quad(new Vector3(a.X, y0, a.Z), new Vector3(b.X, y0, b.Z), new Vector3(b.X, y1, b.Z), new Vector3(a.X, y1, a.Z),
                        -na, -nb, -nb, -na, -(na + nb), new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1));
                if (outer)
                    mb.Quad(new Vector3(c.X, y0, c.Z), new Vector3(d.X, y0, d.Z), new Vector3(d.X, y1, d.Z), new Vector3(c.X, y1, c.Z),
                        na, nb, nb, na, na + nb, new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1));
                if (top)
                    mb.Quad(new Vector3(a.X, y1, a.Z), new Vector3(b.X, y1, b.Z), new Vector3(d.X, y1, d.Z), new Vector3(c.X, y1, c.Z),
                        Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1));
            }
            return mb.ToMesh("ringwall");
        }

        public static Mesh Ice()
        {
            var mb = new MeshBuilder();
            var pts = Perimeter(0, 20);
            Vector2 UV(float x, float z) => new Vector2((x + Rink.L / 2) / Rink.L, (z + Rink.W / 2) / Rink.W);
            int c = mb.Vert(Vector3.zero, Vector3.up, UV(0, 0));
            int first = mb.V.Count;
            foreach (var p in pts) mb.Vert(new Vector3(p.X, 0, p.Z), Vector3.up, UV(p.X, p.Z));
            for (int i = 0; i < pts.Count; i++) mb.Tri(c, first + i, first + (i + 1) % pts.Count, Vector3.up);
            return mb.ToMesh("ice");
        }
    }

    // ------------------------------------------------ материалы
    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Material baseToon, baseStandard, baseCutout, baseFade, baseFadeEm;
        static bool init;
        public static bool ToonAvailable { get; private set; }

        static void Init()
        {
            if (init) return;
            init = true;
            // Материалы из Resources гарантируют, что шейдеры и нужные варианты попадут в сборку.
            baseToon = Resources.Load<Material>("HockeyToon");
            baseStandard = Resources.Load<Material>("HockeyOpaque");
            baseCutout = Resources.Load<Material>("HockeyCutout");
            baseFade = Resources.Load<Material>("HockeyFade");
            baseFadeEm = Resources.Load<Material>("HockeyFadeEmissive");
            var std = Shader.Find("Standard");
            if (baseStandard == null) baseStandard = new Material(std);
            if (baseCutout == null) { baseCutout = new Material(std); SetupCutout(baseCutout); }
            if (baseFade == null) { baseFade = new Material(std); SetupFade(baseFade); }
            if (baseFadeEm == null) baseFadeEm = baseFade;
            if (baseToon == null)
            {
                var toon = Shader.Find("Hockey/Toon");
                if (toon != null && toon.isSupported) baseToon = new Material(toon);
            }
            ToonAvailable = baseToon != null;
            if (baseToon == null) baseToon = baseStandard;
            else
            {
                // мягкое естественное затенение: широкий плавный переход, нейтрально-сиреневая тень
                // (без резкого синего), неяркий тёплый ободок — как свет арены, отражённый ото льда
                baseToon = new Material(baseToon);
                baseToon.SetColor("_ShadeColor", new Color(0.7f, 0.71f, 0.82f, 1));
                baseToon.SetFloat("_ShadeSoft", 0.62f);
                baseToon.SetColor("_RimColor", new Color(1f, 0.97f, 0.92f, 0.16f));
                baseToon.SetFloat("_RimPower", 3.6f);
            }
        }

        public static void SetupCutout(Material m)
        {
            m.SetFloat("_Mode", 1);
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.SetInt("_SrcBlend", (int)BlendMode.One); m.SetInt("_DstBlend", (int)BlendMode.Zero); m.SetInt("_ZWrite", 1);
            m.EnableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetFloat("_Cutoff", 0.5f);
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }

        public static void SetupFade(Material m)
        {
            m.SetFloat("_Mode", 2);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        static Material NewToon(Color c, float gloss, Texture tex = null)
        {
            Init();
            var m = new Material(baseToon) { color = c };
            if (tex != null) m.mainTexture = tex;
            if (ToonAvailable) m.SetFloat("_Gloss", gloss);
            else { m.SetFloat("_Glossiness", gloss * 0.8f); m.SetFloat("_Metallic", 0); }
            return m;
        }

        /// Основной материал: мягкий мультяшный шейдер. smooth — сила блика.
        public static Material Get(string hex, float smooth = 0.25f, float metal = 0)
        {
            string key = $"o{hex}|{smooth}|{metal}";
            if (cache.TryGetValue(key, out var m)) return m;
            m = NewToon(Hex(hex), Mathf.Clamp01(smooth * 0.9f + metal * 0.5f));
            cache[key] = m;
            return m;
        }

        public static Material Toon(Texture tex, string hex = "#ffffff", float gloss = 0.15f)
        {
            string key = $"t{tex.GetHashCode()}|{hex}|{gloss}";
            if (cache.TryGetValue(key, out var m)) return m;
            m = NewToon(Hex(hex), gloss, tex);
            cache[key] = m;
            return m;
        }

        static readonly Dictionary<Material, Material> outlined = new Dictionary<Material, Material>();

        /// Копия материала с мультяшной обводкой (только для мультяшного шейдера).
        public static Material Outlined(Material src, float width = 1f)
        {
            if (src == null || !ToonAvailable || !src.HasProperty("_Outline")) return src;
            if (outlined.TryGetValue(src, out var m)) return m;
            m = new Material(src);
            m.SetFloat("_Outline", width);
            outlined[src] = m;
            return m;
        }

        /// Материал для текстурированных моделей без цвета вершин (KayKit).
        public static Material ToonModel(Texture tex, float gloss = 0.12f)
        {
            var m = NewToon(Color.white, gloss, tex);
            if (ToonAvailable) { m.SetFloat("_UseVColor", 0); m.SetFloat("_ShadeSoft", 0.62f); }
            return m;
        }

        public static Material Emissive(string hex, float intensity = 1)
        {
            string key = $"e{hex}|{intensity}";
            if (cache.TryGetValue(key, out var m)) return m;
            m = NewToon(Hex(hex), 0.2f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Hex(hex) * intensity);
            cache[key] = m;
            return m;
        }

        /// Без освещения (небо, экраны). Цвет вершин учитывается.
        public static Material Unlit(Texture tex, string hex = "#ffffff")
        {
            Init();
            var m = new Material(baseToon) { color = Hex(hex) };
            if (tex != null) m.mainTexture = tex;
            if (ToonAvailable) m.SetFloat("_Unlit", 1);
            else { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white); if (tex != null) m.SetTexture("_EmissionMap", tex); m.color = Color.black; }
            return m;
        }

        /// Стандартный PBR-материал (лёд с отражениями).
        public static Material Standard(Texture tex, float smooth)
        {
            Init();
            var m = new Material(baseStandard) { mainTexture = tex, color = Color.white };
            m.SetFloat("_Glossiness", smooth); m.SetFloat("_Metallic", 0);
            return m;
        }

        public static Material Textured(Texture tex, float smooth = 0.3f) => NewToon(Color.white, smooth * 0.5f, tex);

        public static Material TexturedEmissive(Texture tex) => Unlit(tex);

        public static Material Cutout(Texture tex, Color color)
        {
            Init();
            var m = new Material(baseCutout) { mainTexture = tex, color = color };
            m.SetFloat("_Glossiness", 0.1f);
            return m;
        }

        public static Material Fade(string hex, float alpha, bool emissive = false)
        {
            string key = $"f{hex}|{alpha}|{emissive}";
            if (cache.TryGetValue(key, out var m)) return m;
            Init();
            var c = Hex(hex); c.a = alpha;
            m = new Material(emissive ? baseFadeEm : baseFade) { color = c };
            if (emissive) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Hex(hex) * 0.8f); }
            m.SetFloat("_Glossiness", 0.8f);
            cache[key] = m;
            return m;
        }
    }

    // ------------------------------------------------ текстуры
    public static class Tex
    {
        public static Texture2D Ice()
        {
            const int ppm = 24;
            int w = (int)Rink.L * ppm, h = (int)Rink.W * ppm;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 8 };
            var px = new Color32[w * h];
            Color32 baseC = new Color32(243, 249, 255, 255), red = new Color32(217, 54, 62, 255), blue = new Color32(47, 111, 214, 255), crease = new Color32(150, 196, 246, 255);
            var rnd = new System.Random(7);
            for (int y = 0; y < h; y++)
            {
                float z = (y + 0.5f) / ppm - Rink.W / 2;
                for (int x = 0; x < w; x++)
                {
                    float wx = (x + 0.5f) / ppm - Rink.L / 2;
                    Color32 c = baseC;
                    float vig = Mathf.Clamp01((wx * wx / 900f + z * z / 225f) * 0.5f);
                    c = Color32.Lerp(c, new Color32(226, 237, 248, 255), vig);
                    float ax = Mathf.Abs(wx);
                    // площадь ворот
                    float gdx = ax - Rink.GoalX;
                    if (gdx < 0 && gdx * gdx + z * z < 1.8f * 1.8f) c = crease;
                    if (gdx < 0 && Mathf.Abs(Mathf.Sqrt(gdx * gdx + z * z) - 1.8f) < 0.05f) c = red;
                    if (ax < 0.15f) c = red;
                    if (Mathf.Abs(ax - Rink.BlueX) < 0.15f) c = blue;
                    if (Mathf.Abs(ax - Rink.GoalX) < 0.05f) c = red;
                    float dc = Mathf.Sqrt(wx * wx + z * z);
                    if (Mathf.Abs(dc - 4.5f) < 0.08f || dc < 0.3f) c = blue;
                    float fx = ax - 20, fz = Mathf.Abs(z) - 7;
                    float df = Mathf.Sqrt(fx * fx + fz * fz);
                    if (Mathf.Abs(df - 4.5f) < 0.08f || df < 0.35f) c = red;
                    float nx = ax - 5;
                    if (Mathf.Sqrt(nx * nx + fz * fz) < 0.3f) c = red;
                    px[y * w + x] = c;
                }
            }
            // царапины от коньков
            for (int i = 0; i < 500; i++)
            {
                float cx = rnd.Next(w), cy = rnd.Next(h), r = 20 + rnd.Next(150), a0 = (float)rnd.NextDouble() * 6, len = 0.3f + (float)rnd.NextDouble();
                for (float a = a0; a < a0 + len; a += 1f / r)
                {
                    int x = (int)(cx + Mathf.Cos(a) * r), y = (int)(cy + Mathf.Sin(a) * r);
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    var c = px[y * w + x];
                    px[y * w + x] = Color32.Lerp(c, new Color32(170, 195, 220, 255), 0.18f);
                }
            }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        public static Texture2D Net()
        {
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = Mathf.Abs(Mathf.Repeat(x + 0.5f, 32) - 16) , dy = Mathf.Abs(Mathf.Repeat(y + 0.5f, 32) - 16);
                    float d = 16 - Mathf.Max(dx, dy);           // расстояние до ближайшей нити
                    float a = Mathf.Clamp01(2.6f - d);
                    px[y * S + x] = new Color(0.98f, 0.98f, 0.98f, a);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        public static Texture2D Solid(Color c)
        {
            var t = new Texture2D(2, 2);
            t.SetPixels(new[] { c, c, c, c }); t.Apply();
            return t;
        }

        /// Палитра цветов (одна строка), чтобы раскрашивать объединённые меши через UV.
        public static Texture2D Palette(IList<Color> colors)
        {
            var t = new Texture2D(colors.Count, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels(new List<Color>(colors).ToArray()); t.Apply();
            return t;
        }

        // Пиксельный шрифт 3x5 для цифр
        static readonly string[] Digits =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001010010010", "111101111101111", "111101111001111",
        };

        public static void DrawText(Color32[] px, int w, int h, string s, int x0, int y0, int scale, Color32 col)
        {
            int x = x0;
            foreach (char ch in s)
            {
                if (ch == ':')
                {
                    for (int yy = 0; yy < scale; yy++) for (int xx = 0; xx < scale; xx++) { Put(px, w, h, x + xx, y0 + scale + yy, col); Put(px, w, h, x + xx, y0 + 3 * scale + yy, col); }
                    x += scale * 2;
                    continue;
                }
                if (ch < '0' || ch > '9') { x += scale * 4; continue; }
                string g = Digits[ch - '0'];
                for (int r = 0; r < 5; r++)
                    for (int c = 0; c < 3; c++)
                        if (g[r * 3 + c] == '1')
                            for (int yy = 0; yy < scale; yy++)
                                for (int xx = 0; xx < scale; xx++)
                                    Put(px, w, h, x + c * scale + xx, y0 + (4 - r) * scale + yy, col);
                x += scale * 4;
            }
        }

        public static int TextWidth(string s, int scale)
        {
            int n = 0; foreach (char ch in s) n += ch == ':' ? scale * 2 : scale * 4;
            return n - scale;
        }

        static void Put(Color32[] px, int w, int h, int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c; }

        // ---------- гладкие цифры: сегменты-капсулы со сглаживанием краёв ----------
        static readonly int[] Seg7 = { 0b1110111, 0b0010010, 0b1011101, 0b1011011, 0b0111010, 0b1101011, 0b1101111, 0b1010010, 0b1111111, 0b1111011 };
        // сегменты: 0 верх, 1 лево-верх, 2 право-верх, 3 середина, 4 лево-низ, 5 право-низ, 6 низ (в единицах цифры 1×2)
        static readonly Vector4[] SegLines =
        {
            new Vector4(0, 2, 1, 2), new Vector4(0, 1, 0, 2), new Vector4(1, 1, 1, 2), new Vector4(0, 1, 1, 1),
            new Vector4(0, 0, 0, 1), new Vector4(1, 0, 1, 1), new Vector4(0, 0, 1, 0),
        };

        static float SegDist(Vector2 p, Vector4 s)
        {
            var a = new Vector2(s.x, s.y); var b = new Vector2(s.z, s.w);
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
            return (pa - ba * h).magnitude;
        }

        /// Строка из цифр и «:» гладкими сегментами. (cx, cy) — центр строки в пикселях, height — высота цифры.
        public static void DrawSmooth(Color[] px, int w, int h, string s, float cx, float cy, float height, Color col, float thick = 0.17f)
        {
            float dw = height * 0.5f, gap = height * 0.22f;
            float total = 0;
            foreach (char ch in s) total += ch == ':' ? gap * 1.2f : dw + gap;
            total -= gap;
            float x = cx - total / 2;
            foreach (char ch in s)
            {
                if (ch == ':')
                {
                    for (int k = 0; k < 2; k++) Dot(px, w, h, x + gap * 0.6f, cy + (k == 0 ? -0.25f : 0.25f) * height, height * thick * 0.9f, col);
                    x += gap * 1.2f; continue;
                }
                if (ch < '0' || ch > '9') { x += dw + gap; continue; }
                int mask = Seg7[ch - '0'];
                float r = height * thick * 0.5f;
                int x0 = Mathf.Max(0, (int)(x - r - 2)), x1 = Mathf.Min(w - 1, (int)(x + dw + r + 2));
                int y0 = Mathf.Max(0, (int)(cy - height / 2 - r - 2)), y1 = Mathf.Min(h - 1, (int)(cy + height / 2 + r + 2));
                for (int py = y0; py <= y1; py++)
                    for (int qx = x0; qx <= x1; qx++)
                    {
                        var p = new Vector2((qx + 0.5f - x) / dw, (py + 0.5f - (cy - height / 2)) / dw);
                        float d = 1e9f;
                        for (int sgi = 0; sgi < 7; sgi++) if ((mask & (1 << (6 - sgi))) != 0) d = Mathf.Min(d, SegDist(p, SegLines[sgi]));
                        float a = Mathf.Clamp01((r - d * dw) + 0.5f);
                        if (a > 0) px[py * w + qx] = Color.Lerp(px[py * w + qx], col, a * col.a);
                    }
                x += dw + gap;
            }
        }

        static void Dot(Color[] px, int w, int h, float cx, float cy, float r, Color col)
        {
            for (int y = Mathf.Max(0, (int)(cy - r - 2)); y <= Mathf.Min(h - 1, (int)(cy + r + 2)); y++)
                for (int x = Mathf.Max(0, (int)(cx - r - 2)); x <= Mathf.Min(w - 1, (int)(cx + r + 2)); x++)
                {
                    float a = Mathf.Clamp01(r - Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) + 0.5f);
                    if (a > 0) px[y * w + x] = Color.Lerp(px[y * w + x], col, a);
                }
        }

        public static Texture2D Number(int num)
        {
            const int S = 128;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1, 1, 1, 0);
            DrawSmooth(px, S, S, num.ToString(), S / 2f, S / 2f, S * 0.62f, Color.white, 0.24f);
            t.SetPixels(px); t.Apply(true);
            return t;
        }
    }
}
