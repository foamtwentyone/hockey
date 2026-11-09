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
        public float Excite;
        Texture2D scoreTex;
        Color32[] scorePx;
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
            RenderSettings.ambientSkyColor = Mats.Hex("#cfdcff") * 0.9f;
            RenderSettings.ambientEquatorColor = Mats.Hex("#b7b2d6") * 0.75f;
            RenderSettings.ambientGroundColor = Mats.Hex("#8a86b0") * 0.6f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Mats.Hex("#2a2f6b");
            RenderSettings.fogStartDistance = 60; RenderSettings.fogEndDistance = 140;
            RenderSettings.skybox = null;
            QualitySettings.shadowDistance = 45;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowCascade4Split = new Vector3(0.07f, 0.2f, 0.47f);
            QualitySettings.shadowProjection = ShadowProjection.StableFit;

            Sun = new GameObject("Sun").AddComponent<Light>();
            Sun.transform.SetParent(Root, false);
            Sun.type = LightType.Directional;
            Sun.color = Mats.Hex("#fff3e2");
            Sun.intensity = 1.05f;
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
                Obj("Stand", MeshKit.RingWall(off, off + 1.1f, 0, hgt, true, false, true), Mats.Get(seat[i], 0.1f), Root, Vector3.zero);
                rows.Add((off + 0.55f, hgt));
            }
            Obj("Wall", MeshKit.RingWall(10.5f, 11.2f, 0, 9, true, false, true), Mats.Get("#2c2f5e"), Root, Vector3.zero);
            // светящаяся полоса над трибунами
            Obj("Ribbon", MeshKit.RingWall(10.45f, 10.5f, 6.4f, 7.0f, true, false, false), Mats.Emissive("#5cf2ff", 0.9f), Root, Vector3.zero);

            // зрители: объединённые меши, каждая группа «прыгает» со своей фазой
            string[] shirts = { "#ef5a4c", "#4a8ff0", "#ffd23f", "#2fd17f", "#ff8a3d", "#ffffff", "#9b6bff", "#ff7ad9", "#2a2a3a" };
            var palette = new List<Color>();
            foreach (var s in shirts) palette.Add(Mats.Hex(s));
            foreach (var s in CharacterRig.Skins) palette.Add(Mats.Hex(s));
            var mat = Mats.Toon(Tex.Palette(palette), "#ffffff", 0.05f);
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
                    if (rnd.NextDouble() < 0.18) continue;
                    var p = MeshKit.PointAt(pts, s);
                    var rot = Quaternion.LookRotation(new Vector3(-p.Nx, 0, -p.Nz));
                    var mb = builders[n++ % groups];
                    int shirt = rnd.Next(shirts.Length);
                    var body = Matrix4x4.TRS(new Vector3(p.X, row.y + 0.3f, p.Z), rot, Vector3.one);
                    var head = Matrix4x4.TRS(new Vector3(p.X, row.y + 0.8f, p.Z), rot, Vector3.one);
                    MeshKit.RoundedBoxData(mb, new Vector3(0.48f, 0.56f, 0.36f), 0.16f, 1, body, PalUV(shirt), true);
                    MeshKit.RoundedBoxData(mb, new Vector3(0.42f, 0.4f, 0.38f), 0.16f, 1, head, PalUV(shirts.Length + rnd.Next(CharacterRig.Skins.Length)), true);
                    if (rnd.NextDouble() < 0.3)
                    {
                        // поднятые руки
                        var arm = Matrix4x4.TRS(new Vector3(p.X, row.y + 0.9f, p.Z) + rot * new Vector3(0.3f, 0, 0), rot * Quaternion.Euler(0, 0, -15), Vector3.one);
                        MeshKit.RoundedBoxData(mb, new Vector3(0.12f, 0.45f, 0.12f), 0.05f, 1, arm, PalUV(shirt), true);
                    }
                }
            }
            for (int i = 0; i < groups; i++)
            {
                var go = Obj("Crowd", builders[i].ToMesh("crowd"), mat, Root, Vector3.zero, false, true);
                crowdGroups.Add(go.transform);
                crowdPhase.Add(i * 1.7f);
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
            scoreTex = new Texture2D(64, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            scorePx = new Color32[64 * 32];
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
            probe.resolution = 256;
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
            var bg = new Color32(16, 18, 44, 255);
            for (int i = 0; i < scorePx.Length; i++) scorePx[i] = bg;
            for (int y = 26; y < 31; y++) for (int x = 2; x < 30; x++) { scorePx[y * 64 + x] = new Color32(239, 90, 76, 255); scorePx[y * 64 + x + 32] = new Color32(74, 143, 240, 255); }
            var yellow = new Color32(255, 210, 63, 255);
            string a = s0.ToString(), b = s1.ToString();
            Tex.DrawText(scorePx, 64, 32, a, 16 - Tex.TextWidth(a, 3) / 2, 9, 3, yellow);
            Tex.DrawText(scorePx, 64, 32, b, 48 - Tex.TextWidth(b, 3) / 2, 9, 3, yellow);
            string clock = $"{t / 60}:{t % 60:00}";
            Tex.DrawText(scorePx, 64, 32, clock, 32 - Tex.TextWidth(clock, 1) / 2, 1, 1, new Color32(92, 242, 255, 255));
            scoreTex.SetPixels32(scorePx);
            scoreTex.Apply();
        }

        public void SetQuality(int level)
        {
            QualitySettings.shadows = level == 0 ? ShadowQuality.Disable : ShadowQuality.All;
            QualitySettings.shadowResolution = level >= 2 ? ShadowResolution.VeryHigh : ShadowResolution.Medium;
            QualitySettings.shadowCascades = level >= 2 ? 4 : level == 1 ? 2 : 1;
            QualitySettings.shadowDistance = level >= 2 ? 45 : 35;
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
            for (int i = 0; i < crowdGroups.Count; i++)
            {
                float speed = 2.2f + (i % 4) * 0.6f + Excite * 4;
                float bob = Mathf.Max(0, Mathf.Sin(time * speed + crowdPhase[i])) * (0.03f + Excite * 0.35f);
                crowdGroups[i].localPosition = new Vector3(0, bob, 0);
            }
            for (int i = 0; i < beams.Count; i++)
            {
                beams[i].localRotation = Quaternion.Euler(0, Mathf.Sin(time * 0.4f + i) * 8, 0) * beamBase[i];
            }
        }
    }
}
