// Арена: лёд с разметкой, борта, стекло, ворота, трибуны со зрителями, табло, свет.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hockey
{
    public class Arena
    {
        public Transform Root;
        readonly List<Transform> crowdGroups = new List<Transform>();
        readonly List<float> crowdPhase = new List<float>();
        public float Excite;
        Texture2D scoreTex;
        Color32[] scorePx;
        string scoreKey;

        static GameObject Obj(string name, Mesh mesh, Material mat, Transform parent, Vector3 pos, bool cast = false, bool receive = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = receive;
            return go;
        }

        public Arena(Transform parent)
        {
            Root = new GameObject("Arena").transform;
            Root.SetParent(parent, false);
            BuildLights();
            BuildRink();
            BuildGoal(1);
            BuildGoal(-1);
            BuildStands();
            BuildScoreboard();
        }

        void BuildLights()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Mats.Hex("#dfeeff") * 0.95f;
            RenderSettings.ambientEquatorColor = Mats.Hex("#aab6cc") * 0.8f;
            RenderSettings.ambientGroundColor = Mats.Hex("#6c7894") * 0.7f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Mats.Hex("#1a2033");
            RenderSettings.fogStartDistance = 55; RenderSettings.fogEndDistance = 120;
            RenderSettings.skybox = null;
            QualitySettings.shadowDistance = 75;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(Root, false);
            sun.type = LightType.Directional;
            sun.color = Mats.Hex("#fff6e8");
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.shadowBias = 0.03f; sun.shadowNormalBias = 0.3f;
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(12, -32, -14));

            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(Root, false);
            fill.type = LightType.Directional;
            fill.color = Mats.Hex("#bcd4ff");
            fill.intensity = 0.3f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.LookRotation(new Vector3(-15, -20, 20));
        }

        void BuildRink()
        {
            var ice = Obj("Ice", MeshKit.Ice(), Mats.Textured(Tex.Ice(), 0.82f), Root, Vector3.zero);
            ice.GetComponent<MeshRenderer>().receiveShadows = true;

            Obj("Boards", MeshKit.RingWall(0, 0.25f, 0, Rink.BoardH), Mats.Get("#f5f5f0", 0.3f), Root, Vector3.zero, true);
            Obj("Kick", MeshKit.RingWall(-0.02f, 0.27f, 0, 0.22f, true, false, true), Mats.Get("#f2c230", 0.3f), Root, Vector3.zero);
            Obj("Cap", MeshKit.RingWall(-0.04f, 0.32f, Rink.BoardH, Rink.BoardH + 0.09f), Mats.Get("#2a4c8f", 0.4f), Root, Vector3.zero);
            Obj("Glass", MeshKit.RingWall(0.08f, 0.12f, Rink.BoardH + 0.09f, Rink.BoardH + 2.2f), Mats.Fade("#cfe8ff", 0.12f), Root, Vector3.zero, false, false);
            Obj("GlassTop", MeshKit.RingWall(0.06f, 0.14f, Rink.BoardH + 2.2f, Rink.BoardH + 2.25f), Mats.Get("#9fb3c8"), Root, Vector3.zero);
            Obj("Floor", MeshKit.RingWall(0.25f, 3f, -0.01f, 0.01f, false, false, true), Mats.Get("#3a4258"), Root, Vector3.zero);

            // рекламные панели на бортах
            string[] ads = { "#e2483d", "#3f86e8", "#2fbf71", "#f2a33a", "#9b59d0" };
            var pts = MeshKit.Perimeter(-0.012f, 40);
            float total = MeshKit.Length(pts), step = 8.4f;
            int ai = 0;
            for (float s = 2; s < total - 2; s += step)
            {
                var p = MeshKit.PointAt(pts, s);
                var go = Obj("Ad", MeshKit.Plane(3.2f, 0.42f), Mats.Get(ads[ai++ % ads.Length], 0.4f), Root, new Vector3(p.X, 0.62f, p.Z));
                // плоскость смотрит в -Z, разворачиваем лицом внутрь площадки
                go.transform.rotation = Quaternion.LookRotation(new Vector3(p.Nx, 0, p.Nz));
            }
        }

        void BuildGoal(int sgn)
        {
            var g = new GameObject("Goal").transform;
            g.SetParent(Root, false);
            g.localPosition = new Vector3(sgn * Rink.GoalX, 0, 0);
            if (sgn < 0) g.localRotation = Quaternion.Euler(0, 180, 0);
            var red = Mats.Get("#e0322e", 0.6f);
            var white = Mats.Get("#f2f2f2");
            float W = Rink.GoalW, H = Rink.GoalH, D = Rink.GoalD;
            foreach (float z in new[] { -W / 2, W / 2 }) Obj("Post", MeshKit.Cylinder(0.06f, H, 12), red, g, new Vector3(0, H / 2, z), true);
            var bar = Obj("Bar", MeshKit.Cylinder(0.06f, W + 0.12f, 12), red, g, new Vector3(0, H, 0), true);
            bar.transform.localRotation = Quaternion.Euler(90, 0, 0);
            foreach (float z in new[] { -W / 2, W / 2 }) Obj("Base", MeshKit.Box(D, 0.06f, 0.06f), white, g, new Vector3(D / 2, 0.03f, z));
            Obj("Back", MeshKit.Box(0.06f, 0.06f, W), white, g, new Vector3(D, 0.03f, 0));

            var netMat = Mats.Cutout(Tex.Net(), Color.white);
            var mb = new MeshBuilder();
            // верх
            mb.DoubleQuad(new Vector3(0, H, -W / 2), new Vector3(D * 0.6f, H, -W / 2), new Vector3(D * 0.6f, H, W / 2), new Vector3(0, H, W / 2), Vector3.up, new Vector2(2, 10));
            // наклонная задняя сетка
            var nb = Vector3.Cross(new Vector3(D * 0.4f, -H, 0), Vector3.forward).normalized;
            mb.DoubleQuad(new Vector3(D * 0.6f, H, -W / 2), new Vector3(D, 0, -W / 2), new Vector3(D, 0, W / 2), new Vector3(D * 0.6f, H, W / 2), nb, new Vector2(4, 10));
            // боковины
            foreach (float z in new[] { -W / 2, W / 2 })
            {
                var n = new Vector3(0, 0, Mathf.Sign(z));
                mb.DoubleQuad(new Vector3(0, 0, z), new Vector3(D, 0, z), new Vector3(D * 0.6f, H, z), new Vector3(0, H, z), n, new Vector2(4, 4));
            }
            Obj("Net", mb.ToMesh("net"), netMat, g, Vector3.zero, true);
        }

        void BuildStands()
        {
            string[] seat = { "#2b3554", "#34406a", "#2b3554", "#3c4a78", "#2b3554", "#34406a", "#2b3554" };
            var rows = new List<(float off, float y)>();
            for (int i = 0; i < 7; i++)
            {
                float off = 2.8f + i * 1.1f, hgt = 0.6f + i * 0.75f;
                Obj("Stand", MeshKit.RingWall(off, off + 1.1f, 0, hgt, true, false, true), Mats.Get(seat[i], 0.1f), Root, Vector3.zero);
                rows.Add((off + 0.55f, hgt));
            }
            Obj("Wall", MeshKit.RingWall(10.5f, 11.2f, 0, 12, true, false, true), Mats.Get("#20283f"), Root, Vector3.zero);

            // зрители: несколько объединённых мешей, каждый «прыгает» со своей фазой
            string[] shirts = { "#e2483d", "#3f86e8", "#ffd23f", "#2fbf71", "#f28c38", "#ffffff", "#9b59d0", "#222831" };
            var palette = new List<Color>();
            foreach (var s in shirts) palette.Add(Mats.Hex(s));
            foreach (var s in CharacterRig.Skins) palette.Add(Mats.Hex(s));
            var mat = Mats.Textured(Tex.Palette(palette), 0.05f);
            const int groups = 12;
            var builders = new MeshBuilder[groups];
            for (int i = 0; i < groups; i++) builders[i] = new MeshBuilder();
            var rnd = new System.Random(3);
            int n = 0;
            Vector2 PalUV(int i) => new Vector2((i + 0.5f) / palette.Count, 0.5f);
            foreach (var row in rows)
            {
                var pts = MeshKit.Perimeter(row.off, 30);
                float total = MeshKit.Length(pts);
                for (float s = 0.4f; s < total; s += 0.75f)
                {
                    if (rnd.NextDouble() < 0.2) continue;
                    var p = MeshKit.PointAt(pts, s);
                    var rot = Quaternion.LookRotation(new Vector3(-p.Nx, 0, -p.Nz));
                    var mb = builders[n++ % groups];
                    var body = Matrix4x4.TRS(new Vector3(p.X, row.y + 0.28f, p.Z), rot, Vector3.one);
                    var head = Matrix4x4.TRS(new Vector3(p.X, row.y + 0.74f, p.Z), rot, Vector3.one);
                    MeshKit.RoundedBoxData(mb, new Vector3(0.5f, 0.55f, 0.36f), 0.12f, 1, body, PalUV(rnd.Next(shirts.Length)), true);
                    MeshKit.RoundedBoxData(mb, new Vector3(0.38f, 0.36f, 0.36f), 0.12f, 1, head, PalUV(shirts.Length + rnd.Next(CharacterRig.Skins.Length)), true);
                }
            }
            for (int i = 0; i < groups; i++)
            {
                var go = Obj("Crowd", builders[i].ToMesh("crowd"), mat, Root, Vector3.zero, false, false);
                crowdGroups.Add(go.transform);
                crowdPhase.Add(i * 1.7f);
            }

            // прожекторы под потолком
            var lamp = Mats.Emissive("#fff4d6", 2f);
            for (int i = -2; i <= 2; i++)
                foreach (float z in new[] { -12f, 12f })
                    Obj("Lamp", MeshKit.Box(3, 0.3f, 1.2f), lamp, Root, new Vector3(i * 12, 17, z));
        }

        void BuildScoreboard()
        {
            var cube = new GameObject("Jumbotron").transform;
            cube.SetParent(Root, false);
            cube.localPosition = new Vector3(0, 13, 0);
            Obj("Box", MeshKit.RoundedBox(6, 3, 6, 0.25f), Mats.Get("#232a3d"), cube, Vector3.zero);
            scoreTex = new Texture2D(64, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            scorePx = new Color32[64 * 32];
            var m = Mats.TexturedEmissive(scoreTex);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90;
                var face = Obj("Screen", MeshKit.Plane(5.4f, 2.6f), m, cube, Vector3.zero);
                face.transform.localRotation = Quaternion.Euler(0, a, 0);
                face.transform.localPosition = face.transform.localRotation * new Vector3(0, 0, -3.02f);
            }
            DrawScore(0, 0, 0);
        }

        public void DrawScore(int s0, int s1, float time)
        {
            int t = Mathf.Max(0, Mathf.CeilToInt(time));
            string key = $"{s0}:{s1}:{t}";
            if (key == scoreKey) return;
            scoreKey = key;
            var bg = new Color32(11, 15, 26, 255);
            for (int i = 0; i < scorePx.Length; i++) scorePx[i] = bg;
            for (int y = 26; y < 31; y++) for (int x = 2; x < 30; x++) { scorePx[y * 64 + x] = new Color32(226, 72, 61, 255); scorePx[y * 64 + x + 32] = new Color32(63, 134, 232, 255); }
            var yellow = new Color32(255, 210, 63, 255);
            string a = s0.ToString(), b = s1.ToString();
            Tex.DrawText(scorePx, 64, 32, a, 16 - Tex.TextWidth(a, 3) / 2, 9, 3, yellow);
            Tex.DrawText(scorePx, 64, 32, b, 48 - Tex.TextWidth(b, 3) / 2, 9, 3, yellow);
            string clock = $"{t / 60}:{t % 60:00}";
            Tex.DrawText(scorePx, 64, 32, clock, 32 - Tex.TextWidth(clock, 1) / 2, 1, 1, new Color32(127, 224, 255, 255));
            scoreTex.SetPixels32(scorePx);
            scoreTex.Apply();
        }

        public void Update(float dt, float time)
        {
            Excite = Mathf.Max(0, Excite - dt * 0.25f);
            for (int i = 0; i < crowdGroups.Count; i++)
            {
                float speed = 2.2f + (i % 4) * 0.6f + Excite * 4;
                float bob = Mathf.Max(0, Mathf.Sin(time * speed + crowdPhase[i])) * (0.03f + Excite * 0.35f);
                crowdGroups[i].localPosition = new Vector3(0, bob, 0);
            }
        }
    }
}
