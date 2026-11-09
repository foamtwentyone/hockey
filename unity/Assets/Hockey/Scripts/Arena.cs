// Арена: блестящий лёд с разметкой и отражениями, борта с рекламой, стекло, ворота,
// трибуны с прыгающими зрителями, табло-куб, небо-купол и лучи прожекторов.
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
        readonly List<float> crowdSector = new List<float>();
        float waveT = -10, nextWave = 12;
        /// Запустить «волну» на трибунах.
        public void Wave(float time) { waveT = time; }
        public float Excite;
        public const int CrowdLayer = 9;
        Texture2D scoreTex;
        Color[] scorePx;
        string scoreKey;
        ReflectionProbe probe;
        bool probeRendered;
        readonly List<Transform> beams = new List<Transform>();
        readonly List<Quaternion> beamBase = new List<Quaternion>();
        public Light Sun;

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
            BuildSky();
            BuildRink();
            BuildGoal(1);
            BuildGoal(-1);
            BuildStands();
            BuildScoreboard();
            BuildProbe();
        }

        void BuildLights()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            SetBrightness(Settings.Brightness);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Mats.Hex("#2a2f6b");
            RenderSettings.fogStartDistance = 60; RenderSettings.fogEndDistance = 140;
            RenderSettings.skybox = null;
            QualitySettings.shadowDistance = 38;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowCascade4Split = new Vector3(0.07f, 0.2f, 0.47f);
            QualitySettings.shadowProjection = ShadowProjection.StableFit;

            Sun = new GameObject("Sun").AddComponent<Light>();
            Sun.transform.SetParent(Root, false);
            Sun.type = LightType.Directional;
            Sun.color = Mats.Hex("#fff3e2");
            Sun.intensity = 0.95f * Settings.Brightness;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.72f;
            Sun.shadowBias = 0.02f; Sun.shadowNormalBias = 0.15f;
            Sun.shadowResolution = LightShadowResolution.VeryHigh;
            Sun.transform.rotation = Quaternion.LookRotation(new Vector3(10, -30, -14));
        }

        void BuildSky()
        {
            var sky = Obj("Sky", MeshKit.Flip(MeshKit.Sphere(110, 24, 0)), Mats.Unlit(TexArt.SkyGradient()), Root, new Vector3(0, -10, 0), false, false);
            sky.transform.localScale = new Vector3(1, 0.6f, 1);
        }

        void BuildRink()
        {
            var iceTex = TexArt.Ice();
            Material iceMat = null;
            var iceBase = Resources.Load<Material>("HockeyIce");
            if (iceBase != null) iceMat = new Material(iceBase);
            else { var sh = Shader.Find("Hockey/Ice"); if (sh != null && sh.isSupported) iceMat = new Material(sh); }
            if (iceMat != null) iceMat.mainTexture = iceTex;
            else iceMat = Mats.Standard(iceTex, 0.78f);
            var ice = Obj("Ice", MeshKit.Ice(), iceMat, Root, Vector3.zero);
            ice.GetComponent<MeshRenderer>().receiveShadows = true;
            if (iceMat.HasProperty("_ReflTex")) ice.AddComponent<IceReflection>();

            var boardsMat = Mats.Toon(TexArt.Boards(), "#ffffff", 0.25f);
            Obj("Boards", MeshKit.RingWall(0, 0.25f, 0, Rink.BoardH, true, false, false, TexArt.BoardsMeters), boardsMat, Root, Vector3.zero, true);
            Obj("BoardsOut", MeshKit.RingWall(0, 0.25f, 0, Rink.BoardH, false, true, false), Mats.Get("#e9ecf5", 0.2f), Root, Vector3.zero);
            Obj("Cap", MeshKit.RingWall(-0.05f, 0.32f, Rink.BoardH, Rink.BoardH + 0.1f), Mats.Get("#3b5bd6", 0.6f), Root, Vector3.zero, true);
            Obj("Glass", MeshKit.RingWall(0.08f, 0.12f, Rink.BoardH + 0.1f, Rink.BoardH + 2.2f), Mats.Fade("#d8f0ff", 0.1f), Root, Vector3.zero, false, false);
            Obj("GlassTop", MeshKit.RingWall(0.05f, 0.15f, Rink.BoardH + 2.2f, Rink.BoardH + 2.27f), Mats.Get("#aab8d8", 0.5f), Root, Vector3.zero);
            Obj("Floor", MeshKit.RingWall(0.25f, 3f, -0.01f, 0.01f, false, false, true), Mats.Get("#40466e"), Root, Vector3.zero);
            // стойки стекла
            var pts = MeshKit.Perimeter(0.1f, 30);
            float total = MeshKit.Length(pts);
            var post = Mats.Get("#aab8d8", 0.5f);
            for (float s = 0; s < total; s += 3f)
            {
                var p = MeshKit.PointAt(pts, s);
                var go = Obj("Post", MeshKit.Box(0.06f, 2.1f, 0.08f), post, Root, new Vector3(p.X, Rink.BoardH + 1.15f, p.Z));
                go.transform.rotation = Quaternion.LookRotation(new Vector3(p.Nx, 0, p.Nz));
            }
        }

        void BuildGoal(int sgn)
        {
            var g = new GameObject("Goal").transform;
            g.SetParent(Root, false);
            g.localPosition = new Vector3(sgn * Rink.GoalX, 0, 0);
            if (sgn < 0) g.localRotation = Quaternion.Euler(0, 180, 0);
            var red = Mats.Get("#ff3b30", 0.9f);
            var white = Mats.Get("#f2f2f2");
            float W = Rink.GoalW, H = Rink.GoalH, D = Rink.GoalD;
            foreach (float z in new[] { -W / 2, W / 2 }) Obj("Post", MeshKit.Cylinder(0.07f, H, 14), red, g, new Vector3(0, H / 2, z), true);
            var bar = Obj("Bar", MeshKit.Cylinder(0.07f, W + 0.14f, 14), red, g, new Vector3(0, H, 0), true);
            bar.transform.localRotation = Quaternion.Euler(90, 0, 0);
            foreach (float z in new[] { -W / 2, W / 2 }) Obj("Base", MeshKit.Box(D, 0.07f, 0.07f), white, g, new Vector3(D / 2, 0.035f, z));
            Obj("Back", MeshKit.Box(0.07f, 0.07f, W), white, g, new Vector3(D, 0.035f, 0));

            var netMat = Mats.Cutout(Tex.Net(), Color.white);
            var mb = new MeshBuilder();
            mb.DoubleQuad(new Vector3(0, H, -W / 2), new Vector3(D * 0.6f, H, -W / 2), new Vector3(D * 0.6f, H, W / 2), new Vector3(0, H, W / 2), Vector3.up, new Vector2(2, 10));
            var nb = Vector3.Cross(new Vector3(D * 0.4f, -H, 0), Vector3.forward).normalized;
            mb.DoubleQuad(new Vector3(D * 0.6f, H, -W / 2), new Vector3(D, 0, -W / 2), new Vector3(D, 0, W / 2), new Vector3(D * 0.6f, H, W / 2), nb, new Vector2(4, 10));
            foreach (float z in new[] { -W / 2, W / 2 })
            {
                var n = new Vector3(0, 0, Mathf.Sign(z));
                mb.DoubleQuad(new Vector3(0, 0, z), new Vector3(D, 0, z), new Vector3(D * 0.6f, H, z), new Vector3(0, H, z), n, new Vector2(4, 4));
            }
            Obj("Net", mb.ToMesh("net"), netMat, g, Vector3.zero, true);
        }

        void BuildStands()
        {
            string[] seat = { "#3a3f7a", "#4a4f92", "#3a3f7a", "#565caa", "#3a3f7a", "#4a4f92", "#3a3f7a" };
            var rows = new List<(float off, float y)>();
            for (int i = 0; i < 7; i++)
            {
                float off = 2.8f + i * 1.1f, hgt = 0.6f + i * 0.75f;
                Obj("Stand", MeshKit.RingWall(off, off + 1.1f, 0, hgt, true, false, true), Mats.Get(seat[i], 0.1f), Root, Vector3.zero, false, false).layer = CrowdLayer;
                rows.Add((off + 0.55f, hgt));
            }
            Obj("Wall", MeshKit.RingWall(10.5f, 11.2f, 0, 9, true, false, true), Mats.Get("#2c2f5e"), Root, Vector3.zero);
            // светящаяся полоса над трибунами
            Obj("Ribbon", MeshKit.RingWall(10.45f, 10.5f, 6.4f, 7.0f, true, false, false), Mats.Emissive("#5cf2ff", 0.9f), Root, Vector3.zero);

            // зрители: мини-болельщики в шапках и шарфах, с флагами и плакатами.
            // Группы — сектора по кругу арены (для «волны»), в каждом секторе две подгруппы с разной фазой.
            var palette = new List<Color>();
            var palIdx = new Dictionary<string, int>();
            int Pal(string hex) { if (!palIdx.TryGetValue(hex, out int i)) { i = palette.Count; palIdx[hex] = i; palette.Add(Mats.Hex(hex)); } return i; }
            string[] shirts = { "#ef5a4c", "#4a8ff0", "#ffd23f", "#2fd17f", "#ff8a3d", "#ffffff", "#9b6bff", "#ff7ad9", "#2a2a3a" };
            string[] hairs = { "#7a4a2a", "#3a271b", "#d6943f", "#f0c95e", "#a8502c", "#26232b" };
            foreach (var h in shirts) Pal(h);
            foreach (var h in hairs) Pal(h);
            foreach (var h in CharacterRig.Skins) Pal(h);
            Pal("#1d1b26"); Pal("#ffffff"); Pal("#8a5a32"); Pal("#ef5a4c"); Pal("#4a8ff0");
            var crowdTex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var crowdBuilders = new Dictionary<int, MeshBuilder>();
            var rnd = new System.Random(3);
            const int sectors = 24;
            MeshBuilder B(int g) { if (!crowdBuilders.TryGetValue(g, out var mb)) { mb = new MeshBuilder(); crowdBuilders[g] = mb; } return mb; }
            Vector2 UV(int i) => new Vector2((i + 0.5f) / 64f, 0.5f);
            void Box(MeshBuilder mb, Vector3 size, float r, Vector3 pos, Quaternion rot, int col) =>
                MeshKit.RoundedBoxData(mb, size, r, 1, Matrix4x4.TRS(pos, rot, Vector3.one), UV(col), true, 0.15f);
            foreach (var row in rows)
            {
                var pts = MeshKit.Perimeter(row.off, 30);
                float total = MeshKit.Length(pts);
                for (float s = 0.4f; s < total; s += 0.75f)
                {
                    if (rnd.NextDouble() < 0.15) continue;
                    var p = MeshKit.PointAt(pts, s);
                    var rot = Quaternion.LookRotation(new Vector3(-p.Nx, 0, -p.Nz));
                    float ang = Mathf.Atan2(p.Z, p.X);
                    int sector = Mathf.Clamp((int)((ang + Mathf.PI) / (Mathf.PI * 2) * sectors), 0, sectors - 1);
                    var mb = B(sector * 2 + rnd.Next(2));
                    // болельщики красных — на «красной» половине, синих — на другой
                    bool redFan = p.X < 0 ? rnd.NextDouble() < 0.75 : rnd.NextDouble() < 0.25;
                    string team = redFan ? "#ef5a4c" : "#4a8ff0";
                    int shirt = rnd.NextDouble() < 0.6 ? Pal(team) : Pal(shirts[rnd.Next(shirts.Length)]);
                    int skin = Pal(CharacterRig.Skins[rnd.Next(CharacterRig.Skins.Length)]);
                    var basePos = new Vector3(p.X, row.y, p.Z);
                    Vector3 L(float x, float y, float z) => basePos + rot * new Vector3(x, y, z);
                    float sc = 0.9f + (float)rnd.NextDouble() * 0.2f;
                    Box(mb, new Vector3(0.46f, 0.5f, 0.34f) * sc, 0.16f, L(0, 0.27f * sc, 0), rot, shirt);                // тело
                    Box(mb, new Vector3(0.5f, 0.46f, 0.44f) * sc, 0.2f, L(0, 0.75f * sc, 0), rot, skin);                  // голова
                    int eye = Pal("#1d1b26");
                    for (int e = -1; e <= 1; e += 2) Box(mb, new Vector3(0.06f, 0.1f, 0.03f), 0.02f, L(e * 0.1f * sc, 0.76f * sc, 0.22f * sc), rot, eye);
                    double hatRoll = rnd.NextDouble();
                    if (hatRoll < 0.4)
                    {
                        // шапка-бини в цвет команды с белым помпоном
                        Box(mb, new Vector3(0.53f, 0.24f, 0.47f) * sc, 0.12f, L(0, 0.98f * sc, -0.01f), rot, Pal(team));
                        Box(mb, new Vector3(0.54f, 0.07f, 0.48f) * sc, 0.03f, L(0, 0.88f * sc, -0.01f), rot, Pal("#ffffff"));
                        Box(mb, new Vector3(0.14f, 0.14f, 0.14f) * sc, 0.07f, L(0, 1.13f * sc, -0.01f), rot, Pal("#ffffff"));
                    }
                    else if (hatRoll < 0.55)
                        Box(mb, new Vector3(0.52f, 0.12f, 0.46f) * sc, 0.06f, L(0, 0.97f * sc, 0), rot, Pal(shirts[rnd.Next(shirts.Length)])); // кепка
                    else
                        Box(mb, new Vector3(0.52f, 0.16f, 0.46f) * sc, 0.08f, L(0, 0.96f * sc, -0.02f), rot, Pal(hairs[rnd.Next(hairs.Length)])); // волосы
                    if (rnd.NextDouble() < 0.35)
                        Box(mb, new Vector3(0.48f, 0.1f, 0.38f) * sc, 0.05f, L(0, 0.52f * sc, 0.01f), rot, Pal(team));           // шарф
                    double hands = rnd.NextDouble();
                    if (hands < 0.07)
                    {
                        // флаг команды
                        Box(mb, new Vector3(0.04f, 1.3f, 0.04f), 0.015f, L(0.28f, 1.1f, 0.05f), rot, Pal("#8a5a32"));
                        Box(mb, new Vector3(0.7f, 0.45f, 0.03f), 0.02f, L(0.65f, 1.5f, 0.05f), rot, Pal(team));
                        Box(mb, new Vector3(0.7f, 0.08f, 0.035f), 0.01f, L(0.65f, 1.5f, 0.05f), rot, Pal("#ffffff"));
                    }
                    else if (hands < 0.12)
                    {
                        // плакат над головой
                        Box(mb, new Vector3(0.75f, 0.42f, 0.04f), 0.03f, L(0, 1.45f, 0.05f), rot, Pal("#ffffff"));
                        Box(mb, new Vector3(0.6f, 0.1f, 0.045f), 0.02f, L(0, 1.5f, 0.05f), rot, Pal(team));
                        Box(mb, new Vector3(0.4f, 0.06f, 0.045f), 0.02f, L(0, 1.36f, 0.05f), rot, Pal(team));
                        for (int a = -1; a <= 1; a += 2) Box(mb, new Vector3(0.1f, 0.4f, 0.1f), 0.04f, L(a * 0.25f, 1.05f, 0.03f), rot, shirt);
                    }
                    else if (hands < 0.4)
                        for (int a = -1; a <= 1; a += 2)
                            Box(mb, new Vector3(0.11f, 0.42f, 0.11f), 0.045f, L(a * 0.29f * sc, 0.95f * sc, 0.02f), rot * Quaternion.Euler(0, 0, -a * 18), shirt); // руки вверх
                }
            }
            var px = new Color[64];
            for (int i = 0; i < 64; i++) px[i] = i < palette.Count ? palette[i] : Color.white;
            crowdTex.SetPixels(px); crowdTex.Apply();
            var mat = Mats.Toon(crowdTex, "#ffffff", 0.05f);
            foreach (var kv in crowdBuilders)
            {
                var go = Obj("Crowd", kv.Value.ToMesh("crowd"), mat, Root, Vector3.zero, false, false);
                go.layer = CrowdLayer;
                crowdGroups.Add(go.transform);
                int sector = kv.Key / 2;
                crowdPhase.Add(sector * (Mathf.PI * 2 / sectors) + (kv.Key % 2) * 2.1f);
                crowdSector.Add(sector * (Mathf.PI * 2 / sectors));
            }

            // прожекторы и лучи света
            var lamp = Mats.Emissive("#fff4d6", 1.4f);
            var beamMat = Mats.Fade("#fff1c4", 0.035f, true);
            for (int i = -2; i <= 2; i++)
                foreach (float z in new[] { -13f, 13f })
                {
                    Obj("Lamp", MeshKit.RoundedBox(2.2f, 0.4f, 1.0f, 0.15f), lamp, Root, new Vector3(i * 12, 18, z));
                    var b = Obj("Beam", MeshKit.Cone(3.2f, 17, 16), beamMat, Root, new Vector3(i * 12 * 0.9f, 9.5f, z * 0.7f));
                    b.transform.localRotation = Quaternion.Euler(180 + (z > 0 ? -12 : 12), 0, -i * 3);
                    beams.Add(b.transform);
                    beamBase.Add(b.transform.localRotation);
                }
        }

        void BuildScoreboard()
        {
            var cube = new GameObject("Jumbotron").transform;
            cube.SetParent(Root, false);
            cube.localPosition = new Vector3(0, 14, 0);
            Obj("Box", MeshKit.RoundedBox(6.4f, 3.2f, 6.4f, 0.4f), Mats.Get("#2b2f55", 0.5f), cube, Vector3.zero);
            Obj("Top", MeshKit.RoundedBox(4f, 0.6f, 4f, 0.25f), Mats.Get("#3b5bd6", 0.5f), cube, new Vector3(0, 1.9f, 0));
            Obj("Glow", MeshKit.RoundedBox(6.5f, 0.12f, 6.5f, 0.05f, 1, 0), Mats.Emissive("#5cf2ff", 1f), cube, new Vector3(0, -1.65f, 0));
            scoreTex = new Texture2D(512, 256, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            scorePx = new Color[512 * 256];
            var m = Mats.TexturedEmissive(scoreTex);
            for (int i = 0; i < 4; i++)
            {
                var face = Obj("Screen", MeshKit.Plane(5.6f, 2.8f), m, cube, Vector3.zero);
                face.transform.localRotation = Quaternion.Euler(0, i * 90, 0);
                face.transform.localPosition = face.transform.localRotation * new Vector3(0, 0, -3.22f);
            }
            DrawScore(0, 0, 0);
        }

        void BuildProbe()
        {
            var go = new GameObject("IceReflections");
            go.transform.SetParent(Root, false);
            go.transform.localPosition = new Vector3(0, 1.5f, 0);
            probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(80, 40, 60);
            probe.resolution = 128;
            probe.boxProjection = false;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = Mats.Hex("#2a2f6b");
            probe.intensity = 0.8f;
        }

        public void DrawScore(int s0, int s1, float time)
        {
            int t = Mathf.Max(0, Mathf.CeilToInt(time));
            string key = $"{s0}:{s1}:{t}";
            if (key == scoreKey) return;
            scoreKey = key;
            const int W = 512, H = 256;
            var bg = new Color32(16, 18, 44, 255);
            for (int i = 0; i < scorePx.Length; i++) scorePx[i] = (Color)bg;
            for (int y = 200; y < 236; y++) for (int x = 16; x < 240; x++) { scorePx[y * W + x] = Mats.Hex("#ef5a4c"); scorePx[y * W + x + 256] = Mats.Hex("#4a8ff0"); }
            var yellow = Mats.Hex("#ffd23f");
            Tex.DrawSmooth(scorePx, W, H, s0.ToString(), 128, 110, 120, yellow, 0.22f);
            Tex.DrawSmooth(scorePx, W, H, s1.ToString(), 384, 110, 120, yellow, 0.22f);
            Tex.DrawSmooth(scorePx, W, H, $"{t / 60}:{t % 60:00}", 256, 28, 34, Mats.Hex("#5cf2ff"), 0.2f);
            scoreTex.SetPixels(scorePx);
            scoreTex.Apply(true);
        }

        public void SetBrightness(float b)
        {
            RenderSettings.ambientSkyColor = Mats.Hex("#cfdcff") * 0.8f * b;
            RenderSettings.ambientEquatorColor = Mats.Hex("#b7b2d6") * 0.68f * b;
            RenderSettings.ambientGroundColor = Mats.Hex("#8a86b0") * 0.55f * b;
            if (Sun != null) Sun.intensity = 0.95f * b;
        }

        public void SetQuality(int level)
        {
            QualitySettings.shadows = level == 0 ? ShadowQuality.Disable : ShadowQuality.All;
            QualitySettings.shadowResolution = level >= 2 ? ShadowResolution.VeryHigh : ShadowResolution.Medium;
            QualitySettings.shadowCascades = level >= 2 ? 2 : 1;
            QualitySettings.anisotropicFiltering = level == 0 ? AnisotropicFiltering.Disable : AnisotropicFiltering.ForceEnable;
            QualitySettings.lodBias = level == 0 ? 0.6f : 1f;
            QualitySettings.shadowDistance = level >= 2 ? 38 : 26;
            Sun.shadowResolution = level >= 2 ? LightShadowResolution.VeryHigh : LightShadowResolution.Medium;
            QualitySettings.antiAliasing = level == 0 ? 0 : level == 1 ? 2 : 4;
            Sun.shadows = level == 0 ? LightShadows.None : LightShadows.Soft;
            probe.enabled = level > 0;
            foreach (var b in beams) b.gameObject.SetActive(level > 0);
            for (int i = 0; i < crowdGroups.Count; i++) crowdGroups[i].gameObject.SetActive(level > 0 || i % 2 == 0);
        }

        public void Update(float dt, float time)
        {
            if (!probeRendered && probe.enabled) { probe.RenderProbe(); probeRendered = true; }
            Excite = Mathf.Max(0, Excite - dt * 0.25f);
            if (time > nextWave) { waveT = time; nextWave = time + 25 + Mathf.Repeat(time * 7.3f, 15); }
            if (Excite > 0.95f && time - waveT > 6) waveT = time;
            for (int i = 0; i < crowdGroups.Count; i++)
            {
                float speed = 2.2f + (i % 4) * 0.6f + Excite * 4;
                float bob = Mathf.Max(0, Mathf.Sin(time * speed + crowdPhase[i])) * (0.03f + Excite * 0.35f);
                // «волна»: гребень бежит по кругу арены за ~5 секунд
                float wt = time - waveT;
                if (wt >= 0 && wt < 6)
                {
                    float front = wt / 5f * Mathf.PI * 2 - Mathf.PI;
                    float d = Mathf.Abs(Mathf.DeltaAngle(front * Mathf.Rad2Deg, (crowdSector[i] - Mathf.PI) * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    bob += Mathf.Max(0, 1 - d / 0.5f) * 0.6f;
                }
                crowdGroups[i].localPosition = new Vector3(0, bob, 0);
            }
            for (int i = 0; i < beams.Count; i++)
            {
                beams[i].localRotation = Quaternion.Euler(0, Mathf.Sin(time * 0.4f + i) * 8, 0) * beamBase[i];
            }
        }
    }
}
