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
        public readonly List<int> T = new List<int>();

        /// Треугольник с проверкой ориентации: лицевая сторона смотрит по направлению desired.
        public void Tri(int a, int b, int c, Vector3 desired)
        {
            var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (Vector3.Dot(n, desired) < 0) { T.Add(a); T.Add(c); T.Add(b); }
            else { T.Add(a); T.Add(b); T.Add(c); }
        }

        public int Vert(Vector3 p, Vector3 n, Vector2 uv) { V.Add(p); N.Add(n); UV.Add(uv); return V.Count - 1; }

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
            m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetTriangles(T, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    public static class MeshKit
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        // ------------------------------------------------ скруглённый кубик
        public static void RoundedBoxData(MeshBuilder mb, Vector3 size, float r, int m, Matrix4x4 xf, Vector2 uvOverride, bool useUvOverride)
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
                        mb.Vert(xf.MultiplyPoint3x4(pos), xf.MultiplyVector(dir).normalized, uv);
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

        public static Mesh RoundedBox(float w, float h, float d, float r, int m = 3)
        {
            string key = $"rb{w:F3}|{h:F3}|{d:F3}|{r:F3}|{m}";
            if (cache.TryGetValue(key, out var mesh)) return mesh;
            var mb = new MeshBuilder();
            RoundedBoxData(mb, new Vector3(w, h, d), r, m, Matrix4x4.identity, Vector2.zero, false);
            mesh = mb.ToMesh(key);
            cache[key] = mesh;
            return mesh;
        }

        public static Mesh Box(float w, float h, float d) => RoundedBox(w, h, d, 0.004f, 1);

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
        public static Mesh RingWall(float innerOff, float outerOff, float y0, float y1, bool inner = true, bool outer = true, bool top = true)
        {
            var mb = new MeshBuilder();
            var pi = Perimeter(innerOff); var po = Perimeter(outerOff);
            int n = pi.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var a = pi[i]; var b = pi[j]; var c = po[i]; var d = po[j];
                var na = new Vector3(a.Nx, 0, a.Nz); var nb = new Vector3(b.Nx, 0, b.Nz);
                float u0 = a.S / 4, u1 = (j == 0 ? a.S + 1 : b.S) / 4;
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
        static Material baseOpaque, baseCutout, baseFade, baseOpaqueEm, baseFadeEm;
        static bool init;

        static void Init()
        {
            if (init) return;
            init = true;
            // Материалы из Resources гарантируют, что шейдер и нужные варианты попадут в сборку.
            baseOpaque = Resources.Load<Material>("HockeyOpaque");
            baseCutout = Resources.Load<Material>("HockeyCutout");
            baseFade = Resources.Load<Material>("HockeyFade");
            var std = Shader.Find("Standard");
            if (baseOpaque == null) baseOpaque = new Material(std);
            if (baseCutout == null) { baseCutout = new Material(std); SetupCutout(baseCutout); }
            if (baseFade == null) { baseFade = new Material(std); SetupFade(baseFade); }
            baseOpaqueEm = Resources.Load<Material>("HockeyOpaqueEmissive") ?? baseOpaque;
            baseFadeEm = Resources.Load<Material>("HockeyFadeEmissive") ?? baseFade;
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

        public static Material Get(string hex, float smooth = 0.25f, float metal = 0)
        {
            string key = $"o{hex}|{smooth}|{metal}";
            if (cache.TryGetValue(key, out var m)) return m;
            Init();
            m = new Material(baseOpaque) { color = Hex(hex) };
            m.SetFloat("_Glossiness", smooth); m.SetFloat("_Metallic", metal);
            cache[key] = m;
            return m;
        }

        public static Material Emissive(string hex, float intensity = 1)
        {
            string key = $"e{hex}|{intensity}";
            if (cache.TryGetValue(key, out var m)) return m;
            Init();
            m = new Material(baseOpaqueEm) { color = Hex(hex) };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Hex(hex) * intensity);
            cache[key] = m;
            return m;
        }

        public static Material Textured(Texture tex, float smooth = 0.3f)
        {
            Init();
            var m = new Material(baseOpaque) { mainTexture = tex, color = Color.white };
            m.SetFloat("_Glossiness", smooth);
            return m;
        }

        public static Material TexturedEmissive(Texture tex)
        {
            Init();
            var m = new Material(baseOpaqueEm) { mainTexture = tex, color = Color.white };
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", Color.white);
            return m;
        }

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
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    px[y * 32 + x] = (x % 8 < 1 || y % 8 < 1) ? new Color32(250, 250, 250, 255) : new Color32(0, 0, 0, 0);
            t.SetPixels32(px); t.Apply(true);
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

        public static Texture2D Number(int num)
        {
            const int S = 32;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            string s = num.ToString();
            int scale = 4;
            int tw = TextWidth(s, scale);
            DrawText(px, S, S, s, (S - tw) / 2, (S - 5 * scale) / 2, scale, new Color32(255, 255, 255, 255));
            t.SetPixels32(px); t.Apply();
            return t;
        }
    }
}
