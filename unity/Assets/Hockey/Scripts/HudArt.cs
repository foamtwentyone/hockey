// Процедурные текстуры хоккейного HUD: шайба-часы, гербы команд, клюшка (сила броска), лезвие конька (рывок),
// мини-площадка с настоящей разметкой, лучи «гол-лампы». Всё рисуется по формулам со сглаживанием краёв.
using System;
using UnityEngine;

namespace Hockey
{
    public static class HudArt
    {
        // ---------- общие функции расстояний ----------
        static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
            return (pa - ba * h).magnitude - r;
        }

        static float Ellipse(Vector2 p, Vector2 c, Vector2 r)
        {
            var q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y);
            return (q.magnitude - 1) * Mathf.Min(r.x, r.y);
        }

        static float RoundBox(Vector2 p, Vector2 c, Vector2 half, float rad)
        {
            var d = new Vector2(Mathf.Abs(p.x - c.x) - half.x + rad, Mathf.Abs(p.y - c.y) - half.y + rad);
            return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0) - rad;
        }

        static float A(float d) => Mathf.Clamp01(0.5f - d);   // покрытие пикселя по расстоянию

        static Color Over(Color under, Color over, float a)
        {
            a *= over.a;
            float outA = a + under.a * (1 - a);
            if (outA < 1e-5f) return new Color(0, 0, 0, 0);
            var c = (over * a + under * under.a * (1 - a)) / outA;
            c.a = outA;
            return c;
        }

        static Texture2D Make(int w, int h, Func<Vector2, Color> f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f(new Vector2(x + 0.5f, y + 0.5f));
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        // ---------- шайба-часы ----------
        /// Шайба, видимая чуть сверху: тёмная боковина с насечкой, крышка с бликом, мягкая тень.
        public static Texture2D Puck()
        {
            const int W = 320, H = 200;
            var cTop = new Vector2(160, 118); var cBot = new Vector2(160, 66); var r = new Vector2(148, 66);
            return Make(W, H, p =>
            {
                var col = new Color(0, 0, 0, 0);
                // тень
                float sh = Ellipse(p, new Vector2(164, 52), new Vector2(150, 50));
                col = Over(col, new Color(0, 0, 0, 0.35f), Mathf.Clamp01(-sh / 18));
                // силуэт цилиндра
                float side = Mathf.Min(Mathf.Min(Ellipse(p, cTop, r), Ellipse(p, cBot, r)),
                                       Mathf.Max(Mathf.Abs(p.x - 160) - r.x, Mathf.Max(cBot.y - p.y, p.y - cTop.y)));
                float outline = side - 4;
                col = Over(col, new Color(0.02f, 0.02f, 0.05f, 1), A(outline));
                if (side < 0)
                {
                    // боковина: тёмная, с вертикальной насечкой и бликом слева
                    float u = (p.x - 160) / r.x;
                    var bc = Color.Lerp(new Color(0.07f, 0.08f, 0.11f), new Color(0.2f, 0.21f, 0.27f), Mathf.Clamp01(1 - Mathf.Abs(u + 0.35f) * 1.6f));
                    float ang = Mathf.Asin(Mathf.Clamp(u, -1, 1));
                    float knurl = Mathf.Abs(Mathf.Sin(ang * 34));
                    bc = Color.Lerp(bc, bc * 1.7f, Mathf.Clamp01((knurl - 0.75f) * 4) * 0.5f);
                    bc.a = 1;
                    col = Over(col, bc, A(side));
                }
                float top = Ellipse(p, cTop, r - new Vector2(4, 4));
                if (top < 1)
                {
                    float v = (p.y - cTop.y) / r.y;
                    var tc = Color.Lerp(new Color(0.13f, 0.14f, 0.19f), new Color(0.24f, 0.25f, 0.33f), v * 0.5f + 0.5f);
                    // светлый ободок крышки и блик
                    float rim = Mathf.Clamp01(1 - Mathf.Abs(top + 6) / 4);
                    tc = Color.Lerp(tc, new Color(0.45f, 0.48f, 0.62f), rim * 0.8f);
                    float gl = Ellipse(p, cTop + new Vector2(-55, 30), new Vector2(60, 16));
                    tc = Color.Lerp(tc, new Color(0.6f, 0.64f, 0.82f), Mathf.Clamp01(-gl / 10) * 0.35f);
                    tc.a = 1;
                    col = Over(col, tc, A(top));
                }
                return col;
            });
        }

        // ---------- герб команды ----------
        static float ShieldSdf(Vector2 p)
        {
            const float half = 66;
            float body = RoundBox(p, new Vector2(80, 130), new Vector2(half, 46), 18);
            float t = Mathf.Clamp01((p.y - 16) / 100);   // 0 — остриё, 1 — плечи
            float w = half * Mathf.Pow(t, 0.62f);
            float lower = Mathf.Max(Mathf.Abs(p.x - 80) - w, Mathf.Max(16 - p.y, p.y - 132));
            return Mathf.Min(body, lower);
        }

        /// Щит-герб: цвет команды с белой каймой, тёмным контуром и звездой; сверху «корона» из трёх зубцов.
        public static Texture2D Shield(Color main, Color dark)
        {
            const int W = 160, H = 190;
            return Make(W, H, p =>
            {
                float sh = ShieldSdf(p);
                var col = new Color(0, 0, 0, 0);
                col = Over(col, new Color(0, 0, 0, 0.3f), A(ShieldSdf(p + new Vector2(-4, 7)) - 5));   // тень
                col = Over(col, new Color(0.04f, 0.05f, 0.15f, 1), A(sh - 6));
                col = Over(col, Color.white, A(sh - 1));
                var fill = Color.Lerp(dark, main, Mathf.Clamp01(p.y / 170f));
                // диагональная полоса
                float stripe = Mathf.Abs((p.x - 80) * 0.7f + (p.y - 100) * 0.7f);
                fill = Color.Lerp(fill, Color.Lerp(fill, Color.white, 0.22f), A(stripe - 12));
                fill.a = 1;
                col = Over(col, fill, A(sh + 9));
                // звезда
                Vector2 s = (p - new Vector2(80, 100)) / 38;
                float a = Mathf.Atan2(s.y, s.x) - Mathf.PI / 2;
                float seg = Mathf.PI * 2 / 5;
                float k = Mathf.Repeat(a, seg) / seg;
                float edge = Mathf.Lerp(1, 0.45f, 1 - Mathf.Abs(k * 2 - 1));
                float sd = (s.magnitude - edge) * 38;
                col = Over(col, new Color(0.04f, 0.05f, 0.15f, 0.9f), A(sd - 3));
                col = Over(col, new Color(1, 0.93f, 0.55f), A(sd));
                // блик
                float gl = Ellipse(p, new Vector2(56, 160), new Vector2(26, 9));
                col = Over(col, new Color(1, 1, 1, 0.35f), A(gl) * A(sh + 9));
                return col;
            });
        }

        // ---------- клюшка ----------
        static float StickSdf(Vector2 p)
        {
            float shaft = Capsule(p, new Vector2(18, 70), new Vector2(420, 56), 12);
            float hosel = Capsule(p, new Vector2(420, 56), new Vector2(450, 30), 12);
            float blade = Capsule(p, new Vector2(450, 28), new Vector2(530, 22), 14);
            return Mathf.Min(shaft, Mathf.Min(hosel, blade));
        }

        /// Клюшка лёжа: «пустая» (тёмная, полупрозрачная) или «заряженная» (жёлто-красный градиент).
        public static Texture2D Stick(bool full)
        {
            const int W = 560, H = 100;
            return Make(W, H, p =>
            {
                float d = StickSdf(p);
                var col = new Color(0, 0, 0, 0);
                if (!full) col = Over(col, new Color(0, 0, 0, 0.3f), A(StickSdf(p + new Vector2(-3, 5)) - 2));
                col = Over(col, full ? new Color(0.35f, 0.05f, 0.08f, 1) : Color.white, A(d - 4));
                Color fill;
                if (full)
                {
                    float u = p.x / W;
                    fill = u < 0.5f ? Color.Lerp(new Color(1, 0.92f, 0.35f), new Color(1, 0.62f, 0.15f), u * 2) : Color.Lerp(new Color(1, 0.62f, 0.15f), new Color(1, 0.2f, 0.25f), (u - 0.5f) * 2);
                    // блик вдоль древка
                    float gl = Capsule(p, new Vector2(30, 74), new Vector2(410, 61), 3);
                    fill = Color.Lerp(fill, Color.white, A(gl) * 0.45f);
                }
                else fill = new Color(0.1f, 0.12f, 0.3f, 0.85f);
                // набалдашник и обмотка крюка
                float knob = Capsule(p, new Vector2(18, 70), new Vector2(46, 69), 13);
                if (knob < 0) fill = full ? new Color(0.25f, 0.06f, 0.1f, 1) : new Color(0.05f, 0.06f, 0.16f, 0.9f);
                float tape = Capsule(p, new Vector2(466, 26), new Vector2(520, 22), 9);
                if (tape < 0 && Mathf.Repeat(p.x - p.y * 0.6f, 12) < 2.5f) fill = Color.Lerp(fill, Color.black, 0.25f);
                col = Over(col, fill, A(d));
                return col;
            });
        }

        // ---------- лезвие конька ----------
        static float BladeSdf(Vector2 p)
        {
            float run = Capsule(p, new Vector2(16, 30), new Vector2(470, 30), 13);
            float toe = Capsule(p, new Vector2(470, 30), new Vector2(500, 50), 11);
            return Mathf.Min(run, toe);
        }

        /// Лезвие конька: «пустое» (стальной контур) или «полное» (ледяной градиент со снежным бликом).
        public static Texture2D Blade(bool full)
        {
            const int W = 520, H = 72;
            return Make(W, H, p =>
            {
                float d = BladeSdf(p);
                var col = new Color(0, 0, 0, 0);
                if (!full) col = Over(col, new Color(0, 0, 0, 0.3f), A(BladeSdf(p + new Vector2(-2, 4)) - 2));
                col = Over(col, full ? new Color(0.03f, 0.25f, 0.4f, 1) : new Color(0.85f, 0.9f, 1f, 1), A(d - 4));
                Color fill;
                if (full)
                {
                    fill = Color.Lerp(new Color(0.15f, 0.75f, 0.95f), new Color(0.6f, 1f, 0.95f), Mathf.Clamp01((p.y - 18) / 26));
                    float gl = Capsule(p, new Vector2(24, 36), new Vector2(460, 36), 2.5f);
                    fill = Color.Lerp(fill, Color.white, A(gl) * 0.7f);
                }
                else fill = new Color(0.1f, 0.12f, 0.3f, 0.85f);
                col = Over(col, fill, A(d));
                return col;
            });
        }

        // ---------- мини-площадка ----------
        /// Площадка сверху с бортами и настоящей разметкой: красная центральная, синие, линии ворот, круги вбрасывания, вратарские зоны.
        public static Texture2D Rink(int W)
        {
            int H = Mathf.RoundToInt(W * Rink2.W / Rink2.L);
            float s = W / Rink2.L;
            float border = 7;
            var red = new Color(0.9f, 0.25f, 0.24f); var blue = new Color(0.2f, 0.45f, 0.9f);
            return Make(W, H, px =>
            {
                // мировые координаты
                float x = (px.x - W / 2f) / s, z = (px.y - H / 2f) / s;
                float outer = RoundBox(px, new Vector2(W / 2f, H / 2f), new Vector2(W / 2f - 1, H / 2f - 1), Rink2.R * s);
                var col = new Color(0, 0, 0, 0);
                col = Over(col, new Color(0.04f, 0.05f, 0.15f, 1), A(outer));
                col = Over(col, new Color(1f, 0.82f, 0.25f, 1), A(outer + 2.5f));                 // жёлтый борт
                float ice = outer + border;
                if (ice < 1)
                {
                    var c = Color.Lerp(new Color(0.86f, 0.93f, 1f), new Color(0.97f, 0.99f, 1f), px.y / H);
                    float pxw = 1 / s;
                    // линии
                    c = Color.Lerp(c, red, A((Mathf.Abs(x) - 0.35f) * s));
                    c = Color.Lerp(c, blue, A((Mathf.Abs(Mathf.Abs(x) - Rink2.BlueX) - 0.3f) * s));
                    c = Color.Lerp(c, red, A((Mathf.Abs(Mathf.Abs(x) - Rink2.GoalX) - 0.12f) * s) * 0.9f);
                    // круги вбрасывания и центральный круг
                    float ring(float cx, float cz, float r) => Mathf.Abs(new Vector2(x - cx, z - cz).magnitude - r) * s - 0.9f;
                    c = Color.Lerp(c, blue, A(ring(0, 0, 4.5f)));
                    foreach (float fx in new[] { -20f, 20f })
                        foreach (float fz in new[] { -7f, 7f })
                        {
                            c = Color.Lerp(c, red, A(ring(fx, fz, 4.5f)) * 0.85f);
                            c = Color.Lerp(c, red, A((new Vector2(x - fx, z - fz).magnitude - 0.5f) * s));
                        }
                    // вратарские зоны (голубые полукруги перед воротами)
                    foreach (float gx in new[] { -Rink2.GoalX, Rink2.GoalX })
                    {
                        float inward = gx > 0 ? gx - x : x - gx;
                        if (inward > 0)
                        {
                            float dz = new Vector2(x - gx, z).magnitude - 1.8f;
                            c = Color.Lerp(c, new Color(0.55f, 0.78f, 1f), A(dz * s) * 0.8f);
                        }
                        // ворота
                        float g = RoundBox(new Vector2(x, z), new Vector2(gx + Mathf.Sign(gx) * 0.6f, 0), new Vector2(0.6f, Rink2.GoalW / 2), 0.2f) * s;
                        c = Color.Lerp(c, red, A(g));
                    }
                    c.a = 1;
                    col = Over(col, c, A(ice));
                }
                return col;
            });
        }

        // ---------- лучи гол-лампы ----------
        public static Texture2D Rays()
        {
            const int N = 256;
            return Make(N, N, p =>
            {
                var d = p - new Vector2(N / 2f, N / 2f);
                float r = d.magnitude / (N / 2f);
                float a = Mathf.Atan2(d.y, d.x);
                float ray = Mathf.Clamp01((Mathf.Cos(a * 12) - 0.2f) * 2.5f);
                float fade = Mathf.Clamp01(1 - r) * Mathf.Clamp01(r * 4);
                return new Color(1, 1, 1, ray * fade * 0.9f);
            });
        }

        // ---------- круг вбрасывания ----------
        public static Texture2D FaceoffRing()
        {
            const int N = 256;
            return Make(N, N, p =>
            {
                float d = (p - new Vector2(N / 2f, N / 2f)).magnitude;
                var col = new Color(0, 0, 0, 0);
                col = Over(col, Color.white, A(Mathf.Abs(d - 112) - 9));
                col = Over(col, new Color(1, 1, 1, 0.25f), A(d - 104));
                // «усики» круга вбрасывания
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sy in new[] { -1f, 1f })
                    {
                        float hx = Mathf.Abs(p.x - (N / 2f + sx * 30)) - 4, hy = Mathf.Abs(p.y - (N / 2f + sy * 124)) - 9;
                        col = Over(col, Color.white, A(Mathf.Max(hx, hy)));
                    }
                return col;
            });
        }

        // Размеры площадки (копия из Sim, чтобы не путать с методом Rink выше)
        static class Rink2
        {
            public const float L = Hockey.Rink.L, W = Hockey.Rink.W, R = Hockey.Rink.R, BlueX = Hockey.Rink.BlueX, GoalX = Hockey.Rink.GoalX, GoalW = Hockey.Rink.GoalW;
        }
    }
}
