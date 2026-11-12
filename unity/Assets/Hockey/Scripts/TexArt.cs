// Процедурные «мультяшные» текстуры: лёд, ткань, дерево, борта с рекламой, небо, элементы интерфейса.
using UnityEngine;

namespace Hockey
{
    public static class TexArt
    {
        // ---------- шум ----------
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static float Noise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static float Fbm(float x, float y, int seed, int oct = 3)
        {
            float s = 0, a = 0.5f, f = 1;
            for (int i = 0; i < oct; i++) { s += Noise(x * f, y * f, seed + i * 17) * a; f *= 2; a *= 0.5f; }
            return s / (1 - Mathf.Pow(0.5f, oct));
        }

        static Color Mix(Color a, Color b, float t) => Color.LerpUnclamped(a, b, Mathf.Clamp01(t));

        /// Покрытие линии с мягким краем (антиалиасинг): d — расстояние до центра линии, w — полуширина, px — размер пикселя.
        static float Cover(float d, float w, float px) => Mathf.Clamp01((w - Mathf.Abs(d)) / px + 0.5f);

        static bool InStar(float x, float y, float r, int points = 5)
        {
            float a = Mathf.Atan2(y, x) + Mathf.PI / 2;
            float seg = Mathf.PI * 2 / points;
            float k = Mathf.Repeat(a, seg) / seg;
            float edge = Mathf.Lerp(r, r * 0.45f, 1 - Mathf.Abs(k * 2 - 1));
            return Mathf.Sqrt(x * x + y * y) < edge;
        }

        // ---------- лёд ----------
        public static Texture2D Ice()
        {
            const int ppm = 34;
            int w = (int)Rink.L * ppm, h = (int)Rink.W * ppm;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 16, filterMode = FilterMode.Trilinear };
            var px = new Color[w * h];
            float pxm = 1f / ppm;
            Color iceA = Mats.Hex("#e6f0ff"), iceB = Mats.Hex("#bcd5f3"), red = Mats.Hex("#e8443c"), blue = Mats.Hex("#2f74e4");
            Color crease = Mats.Hex("#8cc4ff"), logoA = Mats.Hex("#ffd23f"), logoB = Mats.Hex("#3f86e8"), logoC = Mats.Hex("#e2483d");
            for (int y = 0; y < h; y++)
            {
                float z = (y + 0.5f) / ppm - Rink.W / 2;
                for (int x = 0; x < w; x++)
                {
                    float wx = (x + 0.5f) / ppm - Rink.L / 2;
                    float ax = Mathf.Abs(wx), az = Mathf.Abs(z);
                    // основа: лёгкий голубой шум и иней у бортов
                    float n = Fbm(wx * 0.18f, z * 0.18f, 3);
                    float n2 = Noise(wx * 2.3f, z * 2.3f, 9);
                    Color c = Mix(iceA, iceB, n * 0.55f + n2 * 0.12f);
                    float edgeX = Mathf.Max(0, ax - (Rink.L / 2 - 3)), edgeZ = Mathf.Max(0, az - (Rink.W / 2 - 3));
                    c = Mix(c, Color.white, (edgeX + edgeZ) * 0.12f);

                    // площадь ворот
                    float gdx = ax - Rink.GoalX;
                    float dg = Mathf.Sqrt(gdx * gdx + z * z);
                    if (gdx < 0) c = Mix(c, crease, Cover(dg - 0.9f, 0.9f, pxm) * 0.75f);
                    if (gdx < 0) c = Mix(c, red, Cover(dg - 1.8f, 0.05f, pxm));
                    // линии
                    c = Mix(c, red, Cover(wx, 0.15f, pxm) * (Mathf.Repeat(z, 1.2f) < 0.9f ? 1 : 0.55f));
                    c = Mix(c, blue, Cover(ax - Rink.BlueX, 0.16f, pxm));
                    c = Mix(c, red, Cover(ax - Rink.GoalX, 0.05f, pxm) * (az < 12.5f ? 1 : 0));
                    // центр: эмблема-звезда в круге
                    float dc = Mathf.Sqrt(wx * wx + z * z);
                    if (dc < 3.2f)
                    {
                        c = Mix(c, Mix(logoB, Color.white, 0.7f), Cover(dc - 1.5f, 1.5f, pxm) * 0.45f);
                        c = Mix(c, logoB, Cover(dc - 3.0f, 0.1f, pxm) * 0.8f);
                        if (InStar(wx, z, 1.9f)) c = Mix(c, logoA, 0.55f);
                        if (InStar(wx, z, 1.0f)) c = Mix(c, Color.white, 0.6f);
                    }
                    c = Mix(c, blue, Cover(dc - 4.5f, 0.08f, pxm));
                    c = Mix(c, blue, Cover(dc, 0.25f, pxm));
                    // круги вбрасывания в зонах
                    float fx = ax - 20, fz = az - 7;
                    float df = Mathf.Sqrt(fx * fx + fz * fz);
                    c = Mix(c, red, Cover(df - 4.5f, 0.07f, pxm));
                    c = Mix(c, red, Cover(df, 0.32f, pxm));
                    if (Mathf.Abs(df - 4.5f) < 0.6f && Mathf.Abs(fz) > 0.2f && Mathf.Abs(Mathf.Abs(fx) - 0.9f) < 0.06f && df > 4.5f) c = red;
                    // точки в средней зоне
                    float nx = ax - 5;
                    c = Mix(c, red, Cover(Mathf.Sqrt(nx * nx + fz * fz), 0.3f, pxm));
                    // сегменты-«полоски» под ворота: тонкие звёздочки в зонах
                    if (ax > 13 && ax < 15.5f && az < 1.2f && InStar(ax - 14.2f, z, 1.0f)) c = Mix(c, logoC, 0.35f);
                    px[y * w + x] = c;
                }
            }
            // царапины от коньков
            var rnd = new System.Random(7);
            for (int i = 0; i < 900; i++)
            {
                float cx = rnd.Next(w), cy = rnd.Next(h), r = 30 + rnd.Next(260), a0 = (float)rnd.NextDouble() * 6.28f, len = 0.2f + (float)rnd.NextDouble() * 0.8f;
                float alpha = 0.06f + (float)rnd.NextDouble() * 0.1f;
                for (float a = a0; a < a0 + len; a += 0.7f / r)
                {
                    int x = (int)(cx + Mathf.Cos(a) * r), y = (int)(cy + Mathf.Sin(a) * r);
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    px[y * w + x] = Mix(px[y * w + x], Mats.Hex("#b9cde4"), alpha);
                }
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        // ---------- ткань формы (серый детальный слой, умножается на цвет) ----------
        public static Texture2D Fabric()
        {
            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float knit = Mathf.Abs(Mathf.Sin((x + (y % 4 < 2 ? 0 : 1.5f)) * Mathf.PI / 3f)) * 0.5f + 0.5f;
                    float n = Noise(x * 0.3f, y * 0.3f, 5);
                    float g = 0.9f + knit * 0.07f + n * 0.05f;
                    px[y * S + x] = new Color(g, g, g, 1);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        // ---------- дерево клюшки ----------
        public static Texture2D Wood()
        {
            const int W = 32, H = 256;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var px = new Color[W * H];
            Color a = Mats.Hex("#e7b77a"), b = Mats.Hex("#c98f52");
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float grain = Mathf.Sin(x * 0.9f + Fbm(x * 0.1f, y * 0.02f, 2) * 9) * 0.5f + 0.5f;
                    px[y * W + x] = Mix(a, b, grain * 0.6f + Noise(x * 0.5f, y * 0.1f, 4) * 0.2f);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        // ---------- борта: белые панели, жёлтая полоса внизу и цветные «рекламы» ----------
        public const float BoardsMeters = 24f;

        public static Texture2D Boards()
        {
            const int W = 2048, H = 96;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear };
            var px = new Color[W * H];
            Color white = Mats.Hex("#fbfbf6"), yellow = Mats.Hex("#ffc93c");
            string[] ad = { "#e2483d", "#3f86e8", "#2fbf71", "#ff8a3d", "#9b59d0", "#ff5fa2", "#1fb5c9", "#f2c230" };
            float ppmU = W / BoardsMeters, ppmV = H / Rink.BoardH;
            for (int y = 0; y < H; y++)
            {
                float vy = y / ppmV;
                for (int x = 0; x < W; x++)
                {
                    float ux = x / ppmU;
                    Color c = white * (0.96f + Noise(x * 0.05f, y * 0.2f, 8) * 0.04f);
                    if (vy < 0.2f) c = yellow;
                    // шов между панелями каждые 2 м
                    if (Mathf.Repeat(ux, 2f) < 0.025f && vy > 0.2f) c *= 0.88f;
                    // реклама: панель 2.6 м каждые 3 м
                    int idx = Mathf.FloorToInt(ux / 3f);
                    float lx = ux - idx * 3f - 0.2f, ly = vy - 0.36f;
                    if (lx > 0 && lx < 2.6f && ly > 0 && ly < 0.52f)
                    {
                        // скругление углов
                        float rx = Mathf.Max(0, Mathf.Abs(lx - 1.3f) - 1.18f), ry = Mathf.Max(0, Mathf.Abs(ly - 0.26f) - 0.14f);
                        if (rx * rx + ry * ry < 0.012f)
                        {
                            Color ac = Mats.Hex(ad[idx % ad.Length]);
                            c = ac;
                            // значок: чередуем звезду, круг, полоски, шайбу
                            float ix = lx - 0.45f, iy = ly - 0.26f;
                            int kind = idx % 4;
                            bool icon = kind == 0 ? InStar(ix, iy, 0.2f)
                                : kind == 1 ? ix * ix + iy * iy < 0.03f
                                : kind == 2 ? (Mathf.Abs(iy) < 0.18f && Mathf.Repeat(ix * 8, 1) < 0.5f && Mathf.Abs(ix) < 0.2f)
                                : (Mathf.Abs(iy) < 0.06f && Mathf.Abs(ix) < 0.2f);
                            if (icon) c = Color.white;
                            // «надпись» — белые закруглённые полоски
                            if (lx > 0.85f && lx < 2.4f)
                            {
                                float line1 = Mathf.Abs(ly - 0.33f), line2 = Mathf.Abs(ly - 0.19f);
                                if (line1 < 0.045f && lx < 2.3f) c = Mix(c, Color.white, 0.92f);
                                if (line2 < 0.03f && lx < 1.9f) c = Mix(c, Color.white, 0.6f);
                            }
                        }
                    }
                    px[y * W + x] = c;
                }
            }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        // ---------- небо-купол над ареной ----------
        public static Texture2D SkyGradient()
        {
            var t = new Texture2D(4, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color top = Mats.Hex("#0d1430"), mid = Mats.Hex("#2a2f6b"), low = Mats.Hex("#6b5aa8"), hor = Mats.Hex("#ffb3c7");
            for (int y = 0; y < 256; y++)
            {
                float v = y / 255f; // 0 — низ, 1 — верх
                Color c = v < 0.25f ? Mix(hor, low, v / 0.25f) : v < 0.55f ? Mix(low, mid, (v - 0.25f) / 0.3f) : Mix(mid, top, (v - 0.55f) / 0.45f);
                for (int x = 0; x < 4; x++) t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        // ---------- интерфейс ----------
        /// Скруглённая плашка с вертикальным градиентом, обводкой и мягкой тенью снизу (для 9-slice).
        public static Texture2D RoundRect(int size, int radius, Color top, Color bottom, Color border, int borderW, int shadow = 0)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            int body = size - shadow;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // y=0 — низ текстуры
                    float sd = RoundDist(x + 0.5f, y + 0.5f - shadow, size, body, radius);
                    float ssd = RoundDist(x + 0.5f, y + 0.5f, size, body, radius);
                    Color c = new Color(0, 0, 0, 0);
                    if (shadow > 0) c = new Color(0, 0, 0, 0.35f * Mathf.Clamp01(0.5f - ssd));
                    float inside = Mathf.Clamp01(0.5f - sd);
                    if (inside > 0)
                    {
                        float v = Mathf.Clamp01((y - shadow) / (float)(body - 1));
                        Color fill = Color.Lerp(bottom, top, v);
                        float b = Mathf.Clamp01(sd + borderW + 0.5f);
                        fill = Color.Lerp(fill, border, b * border.a);
                        fill.a = Mathf.Max(fill.a, 0);
                        c = Color.Lerp(c, fill, inside);
                        c.a = Mathf.Max(c.a, inside * Mathf.Max(top.a, bottom.a));
                    }
                    px[y * size + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static float RoundDist(float x, float y, int w, int h, int r)
        {
            float cx = Mathf.Clamp(x, r, w - r), cy = Mathf.Clamp(y, r, h - r);
            float dx = x - cx, dy = y - cy;
            if (y < 0 || y > h) return 1e3f;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        public static Texture2D Circle(int size, Color c)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    var cc = c; cc.a *= Mathf.Clamp01(r - d);
                    t.SetPixel(x, y, cc);
                }
            t.Apply();
            return t;
        }

        /// Мягкое пятно (для частиц и подсветки).
        public static Texture2D Soft(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                    float a = Mathf.Clamp01(1 - d); a *= a;
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }
    }
}
