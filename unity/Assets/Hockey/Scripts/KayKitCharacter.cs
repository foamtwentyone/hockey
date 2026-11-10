// Персонажи на моделях KayKit «Adventurers» (CC0, Kay Lousberg, kaylousberg.com).
// Модель грузится из Resources, форма перекрашивается в цвет команды, оружие и плащи прячутся,
// в правую руку вкладывается клюшка. Анимации смешиваются через Playables:
// нижний слой — всё тело (стойка, бег, прыжок, нырок, падение, радость),
// верхний слой — руки с клюшкой (держать, замах, бросок).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Hockey
{
    public class KayKitCharacter
    {
        public Transform Model, HandR, Head, StickBlade, Stick;
        Transform armL0, armL1, armL2, armR0, armR1, armR2;   // плечо, предплечье, кисть обеих рук — для хвата клюшки
        Vector3 restL2, restR2;
        // кости и клипы захвата движения (MocapPose.cs)
        Transform bHips, bSpine, bChest, bHead, bWristL, bWristR, bLegL, bLegR;
        MocapClip moStance, moWind, moShot;
        readonly float[] fS = new float[MocapClip.Stride], fW = new float[MocapClip.Stride], fSh = new float[MocapClip.Stride], fMix = new float[MocapClip.Stride];
        float wWind, wShot, leftSign = 1, lastDt = 1 / 60f;   // исходное положение кистей (рука «тянется» только на кадр)
        float gripW;
        readonly List<Transform> hands = new List<Transform>();
        class GloveRig { public Transform T, Hand, Lower, Fist, Sleeve, Stripe; public float Along, FistSize; public Vector3 BackLocal; public bool Left; }

        /// Рукав хоккейного свитера: цилиндр цвета команды от локтя до раструба краги, с белой полосой.
        void PlaceSleeve(GloveRig g, Vector3 cuff)
        {
            if (g.Sleeve == null) return;
            var elbow = g.Lower.position;
            var d = cuff - elbow; float L = d.magnitude;
            if (L < 1e-4f) return;
            float r = g.FistSize * 0.26f;
            var a = elbow - d / L * r * 0.8f;           // чуть заходит за локоть, чтобы не было щели
            d = cuff - a; L = d.magnitude;
            float ps = Mathf.Max(1e-4f, g.Sleeve.parent.lossyScale.x);
            var rot = Quaternion.FromToRotation(Vector3.up, d);
            g.Sleeve.SetPositionAndRotation(a + d * 0.5f, rot);
            g.Sleeve.localScale = new Vector3(2 * r, L, 2 * r) / ps;
            g.Stripe.SetPositionAndRotation(a + d * 0.74f, rot);
            g.Stripe.localScale = new Vector3(2.12f * r, L * 0.1f, 2.12f * r) / ps;
        }
        // текущий хват: точки на древке для правой (верхней) и левой (нижней) руки и направление древка к крюку
        Vector3 gripTop, gripLow, gripDir; float gripNow;
        readonly List<GloveRig> gloves = new List<GloveRig>();

        void PlaceGloves()
        {
            foreach (var g in gloves)
            {
                var fdir = g.Hand.position - g.Lower.position;
                if (fdir.sqrMagnitude < 1e-8f) continue;
                fdir.Normalize();
                var back = Vector3.ProjectOnPlane(g.Hand.rotation * g.BackLocal, fdir);
                if (back.sqrMagnitude < 1e-6f) back = Vector3.ProjectOnPlane(Model.forward, fdir);
                var pos = g.Hand.position + fdir * g.Along;
                var rot = Quaternion.LookRotation(back.normalized, fdir);
                bool fist = g.Fist != null && gripNow > 0.5f;
                if (g.Fist != null && g.Fist.gameObject.activeSelf != fist) g.Fist.gameObject.SetActive(fist);
                if (g.T.gameObject.activeSelf == fist) g.T.gameObject.SetActive(!fist);
                if (fist)
                {
                    // кулак обхватывает древко: центр хвата — в точке на клюшке, древко по оси X кулака,
                    // раструб смотрит к локтю
                    var grip = g.Left ? gripLow : gripTop;
                    // раструб смотрит к плечу (а не к локтю, который IK может увести вперёд — кулак выворачивался)
                    var shoulder = g.Lower.parent != null ? g.Lower.parent.position : g.Lower.position;
                    var up = Vector3.ProjectOnPlane(grip - shoulder, gripDir);
                    if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, gripDir);
                    up.Normalize();
                    var x = gripDir;
                    g.Fist.SetPositionAndRotation(grip, Quaternion.LookRotation(Vector3.Cross(x, up), up));
                    PlaceSleeve(g, grip - up * g.FistSize * 0.45f);
                    continue;
                }
                if (gripNow > 0.01f)
                {
                    // кулак обхватывает древко: центр краги — на древке, пальцы — по руке (от локтя)
                    var grip = g.Left ? gripLow : gripTop;
                    var up = Vector3.ProjectOnPlane(grip - g.Lower.position, gripDir);
                    if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, gripDir);
                    up.Normalize();
                    // тыльная сторона краги — наружу-вперёд (как на фото хоккеиста), ладонь — к древку и телу;
                    // открытая ладонь к зрителю выглядела как «машущая» рука
                    var outward = g.Left ? -Model.right : Model.right;
                    var bk = Vector3.ProjectOnPlane(Model.forward + outward * 0.45f + Vector3.up * 0.25f, up);
                    if (bk.sqrMagnitude < 1e-6f) bk = Vector3.ProjectOnPlane(Model.forward, up);
                    var gr = Quaternion.LookRotation(bk.normalized, up);
                    pos = Vector3.Lerp(pos, grip, gripNow);
                    rot = Quaternion.Slerp(rot, gr, gripNow);
                }
                g.T.SetPositionAndRotation(pos, rot);
                PlaceSleeve(g, g.Hand.position);
            }
        }
        float GloveAlong(bool left)
        {
            // у кулака кисть заходит в раструб: запястье — на полпути от центра хвата к краю раструба
            foreach (var g in gloves) if (g.Left == left) return g.Fist != null ? g.FistSize * 0.5f : Mathf.Max(0, g.Along);
            return 0;
        }
        const float HandShrink = 0.62f;
        const float StickTop = 0.36f, GripL = 0.32f;
        public List<Transform> HelmetParts = new List<Transform>();
        Spring headKick;
        float stickLen;
        public bool Goalie;
        PlayableGraph graph;
        AnimationMixerPlayable baseMix, upperMix;
        AnimationLayerMixerPlayable layers;
        readonly List<AnimationClipPlayable> baseClips = new List<AnimationClipPlayable>();
        readonly List<AnimationClipPlayable> upperClips = new List<AnimationClipPlayable>();
        float[] baseW, upperW;
        float upperLayerW = 1;

        // индексы клипов нижнего слоя
        const int Idle = 0, Run = 1, Air = 2, Dive = 3, Fall = 4, StandUp = 5, Cheer = 6, Save = 7, Hit = 8;
        static readonly string[] BaseNames = { "2H_Melee_Idle", "Running_A", "Jump_Idle", "Dodge_Forward", "Death_A", "Lie_StandUp", "Cheer", "Block_Hit", "Hit_A" };
        static readonly string[] GoalieBase = { "Blocking", "Running_Strafe_Right", "Jump_Idle", "Dodge_Forward", "Death_A", "Lie_StandUp", "Cheer", "Dodge_Left", "Hit_A" };
        // верхний слой
        const int Hold = 0, Windup = 1, Swing = 2, PassAnim = 3, PunchAnim = 4;
        static readonly string[] UpperNames = { "2H_Melee_Idle", "2H_Melee_Attack_Chop", "2H_Melee_Attack_Slice", "Throw", "Unarmed_Melee_Attack_Punch_A" };
        public static readonly string[] TapeColors = { "#ffffff", "#25252d", "#ffd23f", "#ff7ad9", "#5cf28a", "#4fe3ff" };
        float punchT = 99; bool wasPunch;

        float runPhase, cheerT, fallT, standT = 99, diveT, swingT = 99, saveT = 99, hitT = 99;
        bool wasStun, wasDive, wasSwing, wasSave;

        static readonly Dictionary<string, AnimationClip[]> clipCache = new Dictionary<string, AnimationClip[]>();
        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
        static readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
        public static bool Available => Load("Rogue") != null;

        static GameObject Load(string name)
        {
            if (!prefabCache.TryGetValue(name, out var go))
            {
                go = Resources.Load<GameObject>("KayKit/" + name);
                prefabCache[name] = go;
            }
            return go;
        }

        static AnimationClip FindClip(string model, string name)
        {
            if (!clipCache.TryGetValue(model, out var clips))
            {
                clips = Resources.LoadAll<AnimationClip>("KayKit/" + model);
                clipCache[model] = clips;
            }
            foreach (var c in clips)
            {
                if (c == null || c.name.StartsWith("__preview__")) continue;
                if (c.name == name || c.name.EndsWith("|" + name)) return c;
            }
            return null;
        }

        // ---------- перекраска палитры: форма в цвет команды, кожа — в выбранный тон ----------
        static Material TeamMaterial(int team, int skin)
        {
            string key = "rogue" + team + "|" + skin;
            if (matCache.TryGetValue(key, out var m)) return m;
            var src = Resources.Load<Texture2D>("KayKit/rogue_texture");
            Texture2D tex = src;
            if (src != null && src.isReadable)
            {
                tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = src.GetPixels();
                int w = src.width, h = src.height;
                float targetHue = team == 0 ? 0.0f : 0.6f;
                Color skinBase = px[(h - 8) * w + 8];
                Color skinTarget = Mats.Hex(CharacterRig.Skins[Mathf.Abs(skin) % CharacterRig.Skins.Length]);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var c = px[y * w + x];
                        // кожа: левая верхняя ячейка атласа и две правые нижние
                        bool skinCell = (x < w / 8 && y >= h * 3 / 4) || (x >= w * 3 / 4 && y < h / 4);
                        if (skinCell && skin > 0)
                        {
                            float k = c.grayscale / Mathf.Max(0.05f, skinBase.grayscale);
                            var nc = skinTarget * k; nc.a = c.a;
                            px[y * w + x] = nc;
                            continue;
                        }
                        if (y < h / 4) continue; // нижний ряд — общие цвета деталей
                        Color.RGBToHSV(c, out float hh, out float s, out float v);
                        bool skipCell = x >= w / 4 && x < w * 3 / 8 && y >= h / 4 && y < h / 2;
                        if (skipCell || !(hh > 0.27f && hh < 0.52f && s > 0.2f)) continue;
                        float nv = Mathf.Clamp01(v * (team == 0 ? 1.08f : 1.05f) + 0.04f);
                        var tc = Color.HSVToRGB(targetHue + (team == 0 ? 0.005f : 0), Mathf.Clamp01(s * 1.05f), nv);
                        tc.a = c.a;
                        px[y * w + x] = tc;
                    }
                tex.SetPixels(px);
                tex.Apply(false);
            }
            m = Mats.ToonModel(tex);
            matCache[key] = m;
            return m;
        }

        static readonly Dictionary<Mesh, Mesh> thinCache = new Dictionary<Mesh, Mesh>();

        /// Сжимает вершины предплечья и кисти к оси кости (ось кости — Y в пространстве кости).
        static void ThinForearm(SkinnedMeshRenderer smr)
        {
            var src = smr.sharedMesh;
            if (src == null) return;
            if (thinCache.TryGetValue(src, out var done)) { smr.sharedMesh = done; return; }
            if (!src.isReadable) return;
            var m = Object.Instantiate(src);
            var v = m.vertices; var bw = m.boneWeights; var bp = m.bindposes; var bones = smr.bones;
            if (bw.Length != v.Length || bones == null) return;
            for (int i = 0; i < v.Length; i++)
            {
                var w = bw[i];
                int b = w.boneIndex0; float best = w.weight0;
                if (w.weight1 > best) { b = w.boneIndex1; best = w.weight1; }
                if (w.weight2 > best) { b = w.boneIndex2; best = w.weight2; }
                if (w.weight3 > best) { b = w.boneIndex3; }
                if (b < 0 || b >= bones.Length || bones[b] == null) continue;
                string n = bones[b].name;
                float f = n.StartsWith("lowerarm") ? 0.7f : n.StartsWith("hand") || n.StartsWith("handslot") ? 0.65f : 1;
                if (f >= 1) continue;
                var lp = bp[b].MultiplyPoint3x4(v[i]);
                lp.x *= f; lp.z *= f;
                v[i] = bp[b].inverse.MultiplyPoint3x4(lp);
            }
            m.vertices = v;
            m.RecalculateBounds();
            thinCache[src] = m;
            smr.sharedMesh = m;
        }

        /// Создаёт персонажа KayKit или возвращает null, если моделей нет.
        public static KayKitCharacter Create(int team, int skin, int tape, int number, bool goalie, Transform parent)
        {
            const string model = "Rogue";
            var prefab = Load(model);
            if (prefab == null) return null;
            var k = new KayKitCharacter { Goalie = goalie };
            var go = Object.Instantiate(prefab, parent, false);
            go.name = model;
            k.Model = go.transform;

            var mat = TeamMaterial(team, skin);
            var parts = new Dictionary<string, SkinnedMeshRenderer>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                bool body = n.EndsWith("ArmLeft") || n.EndsWith("ArmRight") || n.EndsWith("Body") || n.EndsWith("_Head") || n.EndsWith("LegLeft") || n.EndsWith("LegRight");
                if (!body) { r.enabled = false; r.gameObject.SetActive(false); continue; }
                if (r is SkinnedMeshRenderer sk) parts[n.Substring(n.IndexOf('_') + 1)] = sk;
                r.sharedMaterial = Mats.Outlined(mat, 1f);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            // предплечья и кисти модели тоньше, чтобы не торчали из краг (у вратаря ловушка и блин крупные — не трогаем)
            if (!goalie)
                foreach (var key in new[] { "ArmLeft", "ArmRight" })
                    if (parts.TryGetValue(key, out var armSmr)) ThinForearm(armSmr);
            var bones = new Dictionary<string, Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;
                if (t.name == "handslot.r") k.HandR = t;
                else if (t.name == "upperarm.l") k.armL0 = t;
                else if (t.name == "lowerarm.l") k.armL1 = t;
                else if (t.name == "hand.l") k.armL2 = t;
                else if (t.name == "upperarm.r") k.armR0 = t;
                else if (t.name == "lowerarm.r") k.armR1 = t;
                else if (t.name == "hand.r") k.armR2 = t;
                else if (t.name == "hips") k.bHips = t;
                else if (t.name == "spine") k.bSpine = t;
                else if (t.name == "chest") k.bChest = t;
                else if (t.name == "wrist.l") k.bWristL = t;
                else if (t.name == "wrist.r") k.bWristR = t;
                else if (t.name == "upperleg.l") k.bLegL = t;
                else if (t.name == "upperleg.r") k.bLegR = t;
                else if (t.name == "head") k.Head = t;
            }

            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            k.BuildGraph(animator, model, goalie);

            // автоподгон роста: макушка примерно на 1.75 м
            k.graph.Evaluate(0);
            if (k.Head != null)
            {
                float headY = go.transform.InverseTransformPoint(k.Head.position).y;
                if (headY > 0.01f) go.transform.localScale = Vector3.one * (1.12f / headY);
            }

            k.graph.Evaluate(0);
            k.HelmetParts = HockeyGear.Dress(go.transform, bones, parts, team, number, goalie);
            if (k.HandR != null) k.BuildStick(team, Mats.Hex(TapeColors[Mathf.Abs(tape) % TapeColors.Length]));
            // краги — продолжение предплечья: каждый кадр ставим их на ось предплечья у запястья
            // (а не жёстко на кисть, которую анимация может вывернуть — тогда крага «отлетала» от руки)
            foreach (var side in new[] { ".l", ".r" })
                if (bones.TryGetValue("hand" + side, out var hb0) && bones.TryGetValue("lowerarm" + side, out var lb0))
                {
                    var fdir = (hb0.position - lb0.position).normalized;
                    var list = new List<Transform>();
                    foreach (Transform ch in hb0) if (ch.name.StartsWith("gear_glove")) list.Add(ch);
                    foreach (var ch in list)
                    {
                        k.gloves.Add(new GloveRig
                        {
                            T = ch, Hand = hb0, Lower = lb0,
                            Along = Vector3.Dot(ch.position - hb0.position, fdir),
                            BackLocal = Quaternion.Inverse(hb0.rotation) * (ch.rotation * Vector3.forward),
                            Left = side == ".l",
                        });
                        ch.SetParent(go.transform, true);
                        // кулак той же величины: длина кулака ≈ 0.72 длины раскрытой краги
                        float len = 0.2f;
                        foreach (var rr in ch.GetComponentsInChildren<Renderer>(true)) len = Mathf.Max(len, rr.bounds.size.magnitude * 0.62f);
                        var gr = k.gloves[k.gloves.Count - 1];
                        gr.FistSize = len * 0.72f;
                        gr.Fist = HockeyGear.Fist(team, go.transform, gr.FistSize, side == ".r");
                        // рукав свитера поверх тонкого предплечья модели
                        Transform Cyl(string nm, string hex)
                        {
                            var o = new GameObject(nm).transform;
                            o.SetParent(go.transform, false);
                            o.gameObject.AddComponent<MeshFilter>().sharedMesh = MeshKit.Cylinder(0.5f, 1f, 16);
                            var mr = o.gameObject.AddComponent<MeshRenderer>();
                            mr.sharedMaterial = Mats.Outlined(Mats.Get(hex, 0.12f), 0.8f);
                            return o;
                        }
                        gr.Sleeve = Cyl("sleeve" + side, TeamColors.All[Mathf.Clamp(team, 0, 1)].Main);
                        gr.Stripe = Cyl("sleeve_stripe" + side, "#f4f4f0");
                    }
                }
            // кулак модели меньше — чтобы не протыкал крагу; всё надетое на кисть компенсируем, оно остаётся прежнего размера
            foreach (var hn in new[] { "hand.l", "hand.r" })
                if (bones.TryGetValue(hn, out var hb))
                {
                    k.hands.Add(hb);
                    foreach (Transform ch in hb) { ch.localPosition /= HandShrink; ch.localScale /= HandShrink; }
                }
            if (!goalie)
            {
                k.moStance = MocapClip.Load("stance"); k.moWind = MocapClip.Load("windup"); k.moShot = MocapClip.Load("shot");
                if (k.bLegL != null && k.bLegR != null)
                    k.leftSign = Mathf.Sign(k.Model.InverseTransformDirection(k.bLegL.position - k.bLegR.position).x);
            }
            if (k.armL2 != null) k.restL2 = k.armL2.localPosition;
            if (k.armR2 != null) k.restR2 = k.armR2.localPosition;
            go.AddComponent<KayKitGraphOwner>().Graph = k.graph;
            return k;
        }

        void BuildStick(int team, Color tapeColor)
        {
            var stick = new GameObject("HockeyStick").transform;
            stick.SetParent(HandR, false);
            // компенсируем масштаб модели, чтобы клюшка была в метрах
            var s = HandR.lossyScale;
            stick.localScale = new Vector3(1 / Mathf.Max(1e-4f, s.x), 1 / Mathf.Max(1e-4f, s.y), 1 / Mathf.Max(1e-4f, s.z));
            float len = Goalie ? 1.1f : 1.4f;
            Stick = stick; stickLen = len;
            void P(Mesh m, Material mt, Vector3 pos)
            {
                var p = new GameObject("p");
                p.transform.SetParent(stick, false);
                p.transform.localPosition = pos;
                p.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = p.AddComponent<MeshRenderer>(); mr.sharedMaterial = mt;
            }
            var carbon = Mats.Get("#2b2f3d", 0.85f);
            var stripe = Mats.Get(TeamColors.All[team].Main, 0.8f);
            var tape = Mats.Get("#" + ColorUtility.ToHtmlStringRGB(tapeColor), 0.25f);
            float shaft = len - 0.25f + StickTop;
            // рукоять: карбон с полосками цвета команды, сверху — набалдашник из обмотки
            float sw = Goalie ? 0.036f : 0.027f, sd = Goalie ? 0.028f : 0.021f;
            P(MeshKit.Superellipsoid(sw, shaft / 2, sd, 0.25f, 0.35f, 20), carbon, new Vector3(0, StickTop - shaft / 2, 0));
            if (Goalie)
            {
                // широкая «лопата» вратарской клюшки над крюком
                P(MeshKit.Superellipsoid(0.065f, 0.24f, 0.03f, 0.25f, 0.3f, 20), carbon, new Vector3(0, -len + 0.25f + 0.26f, 0.02f));
                P(MeshKit.Superellipsoid(0.067f, 0.035f, 0.032f, 0.25f, 0.3f, 16), stripe, new Vector3(0, -len + 0.25f + 0.38f, 0.02f));
            }
            P(MeshKit.Superellipsoid(0.03f, 0.05f, 0.024f, 0.25f, 0.35f, 16), stripe, new Vector3(0, -0.15f, 0));
            P(MeshKit.Superellipsoid(0.03f, 0.03f, 0.024f, 0.25f, 0.35f, 16), stripe, new Vector3(0, -0.3f, 0));
            P(MeshKit.Superellipsoid(0.034f, 0.08f, 0.028f, 0.5f, 0.5f, 16), tape, new Vector3(0, StickTop - 0.03f, 0));
            // изогнутый крюк с обмоткой
            float bl = Goalie ? 0.56f : 0.37f;
            var blade = new GameObject("blade").transform;
            blade.SetParent(stick, false);
            blade.localPosition = new Vector3(0, -len + 0.25f, 0);
            StickBlade = blade;
            var bladeMesh = MeshKit.Blade(bl, Goalie ? 0.14f : 0.085f, Goalie ? 0.03f : 0.022f, Goalie ? 0.02f : 0.045f);
            P(bladeMesh, carbon, blade.localPosition + new Vector3(0, -0.02f, -0.04f));
            P(MeshKit.Superellipsoid(0.016f, (Goalie ? 0.13f : 0.085f) * 0.56f, bl * 0.25f, 0.3f, 0.4f, 16), tape, blade.localPosition + new Vector3(0.006f, -0.02f, bl * 0.42f));
        }

        void BuildGraph(Animator animator, string model, bool goalie)
        {
            graph = PlayableGraph.Create("KayKit-" + model);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "anim", animator);
            var names = goalie ? GoalieBase : BaseNames;
            baseMix = AnimationMixerPlayable.Create(graph, names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                var clip = FindClip(model, names[i]) ?? FindClip(model, BaseNames[i]) ?? FindClip(model, "Idle");
                var cp = AnimationClipPlayable.Create(graph, clip);
                cp.SetApplyFootIK(false);
                cp.SetSpeed(0);
                graph.Connect(cp, 0, baseMix, i);
                baseClips.Add(cp);
            }
            upperMix = AnimationMixerPlayable.Create(graph, UpperNames.Length);
            for (int i = 0; i < UpperNames.Length; i++)
            {
                var clip = FindClip(model, UpperNames[i]) ?? FindClip(model, "Idle");
                var cp = AnimationClipPlayable.Create(graph, clip);
                cp.SetApplyFootIK(false);
                cp.SetSpeed(0);
                graph.Connect(cp, 0, upperMix, i);
                upperClips.Add(cp);
            }
            layers = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(baseMix, 0, layers, 0);
            graph.Connect(upperMix, 0, layers, 1);
            layers.SetInputWeight(0, 1);
            layers.SetInputWeight(1, 1);
            layers.SetLayerMaskFromAvatarMask(1, UpperMask(animator.transform));
            output.SetSourcePlayable(layers);
            baseW = new float[names.Length]; upperW = new float[UpperNames.Length];
            baseW[Idle] = 1; upperW[Hold] = 1;
            Apply();
        }

        /// Маска верхнего слоя: грудь, руки и голова.
        static AvatarMask UpperMask(Transform root)
        {
            var mask = new AvatarMask();
            var all = root.GetComponentsInChildren<Transform>(true);
            mask.transformCount = all.Length;
            for (int i = 0; i < all.Length; i++)
            {
                string path = PathOf(all[i], root);
                mask.SetTransformPath(i, path);
                mask.SetTransformActive(i, path.Contains("/chest") || path.EndsWith("chest") || path.Contains("handIK") || path.Contains("elbowIK"));
            }
            return mask;
        }

        static string PathOf(Transform t, Transform root)
        {
            if (t == root) return "";
            string p = t.name;
            for (var c = t.parent; c != null && c != root; c = c.parent) p = c.name + "/" + p;
            return p;
        }

        static float Len(AnimationClipPlayable p) { var c = p.GetAnimationClip(); return c != null ? Mathf.Max(0.05f, c.length) : 1; }

        void SetTime(List<AnimationClipPlayable> list, int i, float t, bool loop)
        {
            float len = Len(list[i]);
            list[i].SetTime(loop ? Mathf.Repeat(t, len) : Mathf.Clamp(t, 0, len - 0.001f));
        }

        void Apply()
        {
            float sb = 0, su = 0;
            foreach (var w in baseW) sb += w;
            foreach (var w in upperW) su += w;
            for (int i = 0; i < baseW.Length; i++) baseMix.SetInputWeight(i, sb > 0 ? baseW[i] / sb : (i == 0 ? 1 : 0));
            for (int i = 0; i < upperW.Length; i++) upperMix.SetInputWeight(i, su > 0 ? upperW[i] / su : (i == 0 ? 1 : 0));
            layers.SetInputWeight(1, upperLayerW);
        }

        float[] targetB, targetU;

        /// Обновление анимации по состоянию игрока.
        public void Update(PlayerView s, bool celebrate, bool dance, bool hasPuck, float lateralVel, float dt)
        {
            targetB ??= new float[baseW.Length];
            targetU ??= new float[upperW.Length];
            System.Array.Clear(targetB, 0, targetB.Length);
            System.Array.Clear(targetU, 0, targetU.Length);
            bool air = s.Y > 0.05f;
            bool swing = s.Swing > 0;

            // события
            if (s.Stun && !wasStun) fallT = 0;
            if (!s.Stun && wasStun) standT = 0;
            if (s.Dive && !wasDive) diveT = 0;
            if (swing && !wasSwing) swingT = 0;
            if (s.Punch && !wasPunch) punchT = 0;
            wasPunch = s.Punch; punchT += dt;
            if (s.Save > 0.3f && !wasSave) saveT = 0;
            if (s.Stagger && hitT > 0.5f) hitT = 0;
            wasStun = s.Stun; wasDive = s.Dive; wasSwing = swing; wasSave = s.Save > 0.3f;
            fallT += dt; standT += dt; diveT += dt; swingT += dt; saveT += dt; hitT += dt; cheerT += dt;

            float upper = 1;
            if (s.Stagger && !s.Stun)
            {
                targetB[Hit] = 1;
                SetTime(baseClips, Hit, hitT * 1.4f, false);
                upper = 0.3f;
            }
            else if (s.Stun)
            {
                targetB[fallT < 0.25f ? Hit : Fall] = 1;
                SetTime(baseClips, Hit, fallT * 1.5f, false);
                SetTime(baseClips, Fall, fallT * 1.4f, false);
                upper = 0;
            }
            else if (standT < 0.6f)
            {
                targetB[StandUp] = 1;
                SetTime(baseClips, StandUp, Len(baseClips[StandUp]) * 0.45f + standT * 1.8f, false);
                upper = 0;
            }
            else if (s.Dive)
            {
                targetB[Dive] = 1;
                SetTime(baseClips, Dive, diveT * Len(baseClips[Dive]) / 0.8f, false);
                upper = 0;
            }
            else if (air)
            {
                targetB[Air] = 1;
                SetTime(baseClips, Air, cheerT, true);
                upper = 0.6f;
            }
            else if (celebrate || dance)
            {
                targetB[Cheer] = 1;
                SetTime(baseClips, Cheer, cheerT, true);
                upper = 0;
            }
            else if (Goalie && saveT < 0.6f)
            {
                targetB[Save] = 1;
                SetTime(baseClips, Save, saveT * 1.6f, false);
                upper = 0;
            }
            else
            {
                // стойка ↔ бег по скорости; на коньках бег чуть «плывёт» — темп ниже, шаг шире
                float run = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.6f, 4.5f, s.Spd));
                if (Goalie) run = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.5f, 3f, Mathf.Abs(lateralVel)));
                targetB[Idle] = 1 - run;
                targetB[Run] = run;
                runPhase += dt * Mathf.Lerp(0.6f, 1.05f, Mathf.Clamp01(s.Spd / 10f)) * (Goalie ? 1 : 0.85f);
                SetTime(baseClips, Run, runPhase * Len(baseClips[Run]), true);
                SetTime(baseClips, Idle, cheerT, true);
                upper = Goalie ? 0.0f : 1;
            }

            // руки с клюшкой: держим, замах при зарядке, удар при броске/пасе, кулак при толчке
            if (punchT < 0.4f)
            {
                targetU[PunchAnim] = 1;
                upper = 1;
                SetTime(upperClips, PunchAnim, punchT * 1.6f, false);
            }
            else if (s.Charge > 0.02f)
            {
                targetU[Windup] = 1;
                SetTime(upperClips, Windup, Len(upperClips[Windup]) * Mathf.Lerp(0.1f, 0.4f, s.Charge), false);
            }
            else if (swingT < 0.45f)
            {
                targetU[Swing] = 1;
                SetTime(upperClips, Swing, Len(upperClips[Swing]) * 0.3f + swingT * 1.6f, false);
            }
            else
            {
                targetU[Hold] = 1;
                SetTime(upperClips, Hold, cheerT, true);
            }

            // плавное смешивание
            float k = 1 - Mathf.Exp(-dt * 14);
            for (int i = 0; i < baseW.Length; i++) baseW[i] += (targetB[i] - baseW[i]) * k;
            float ku = 1 - Mathf.Exp(-dt * (s.Charge > 0 || swingT < 0.45f ? 30 : 14));
            for (int i = 0; i < upperW.Length; i++) upperW[i] += (targetU[i] - upperW[i]) * ku;
            upperLayerW += (upper - upperLayerW) * k;
            Apply();
            graph.Evaluate(0);
            foreach (var hb in hands) hb.localScale = Vector3.one * HandShrink;
            if (armL2 != null) armL2.localPosition = restL2;   // сброс «растяжки» руки прошлого кадра
            if (armR2 != null) armR2.localPosition = restR2;
            if (Stick != null && Stick.parent != null)
            {   // клюшка — в метрах независимо от масштаба кисти
                var ls = Stick.parent.lossyScale;
                Stick.localScale = new Vector3(1 / Mathf.Max(1e-4f, ls.x), 1 / Mathf.Max(1e-4f, ls.y), 1 / Mathf.Max(1e-4f, ls.z));
            }
            // откинутая ударом голова — пружиной поверх анимации
            headKick.Step(0, 2.2f, 0.25f, dt);
            if (Head != null && Mathf.Abs(headKick.X) > 0.001f)
                Head.rotation = Quaternion.AngleAxis(-headKick.X * Mathf.Rad2Deg, Model.right) * Head.rotation;
            AimStick(s);
            // левая рука держит клюшку за верх рукояти (правая — ниже), и при замахе, и при броске
            bool grip = !Goalie && upper > 0.5f && punchT >= 0.4f && !s.Brawl && !s.Stun;
            gripW += ((grip ? 1 : 0) - gripW) * (1 - Mathf.Exp(-dt * 12));
            gripNow = 0;
            lastDt = dt;
            if (gripW > 0.01f && Stick != null) { TwoHandGrip(s, gripW); gripNow = gripW; }
            PlaceGloves();
        }

        /// Хват двумя руками: верх рукояти — в левой руке перед животом, правая ниже по древку;
        /// клюшка качается вокруг верхней руки (замах, бросок), обе руки тянутся к своим точкам IK.
        void TwoHandGrip(PlayerView s, float w)
        {
            if (moStance != null && moWind != null && moShot != null && bHips != null && bSpine != null && bChest != null && Head != null
                && bWristL != null && bWristR != null && bLegL != null && bLegR != null && armL0 != null && armR0 != null && armL1 != null && armR1 != null && armL2 != null)
            { MocapGrip(s, w); return; }
            if (armL0 == null || armL1 == null || armL2 == null || armR0 == null || armR1 == null || armR2 == null) return;
            var fwd = Model.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 1e-4f) return;
            fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            float reach = (armL1.position - armL0.position).magnitude + (armL2.position - armL1.position).magnitude;
            var chest = (armL0.position + armR0.position) * 0.5f;
            // как у настоящего хоккеиста: верхняя (левая) рука с набалдашником у бедра, нижняя (правая) — далеко
            // по древку на почти выпрямленной руке, клюшка длинная и пологая, крюк далеко впереди на льду
            // левый хват (крюк слева): правая рука сверху у правого бедра, левая — снизу, далеко по древку
            var top = chest + Vector3.down * reach * 0.72f + fwd * reach * 0.38f + right * reach * 0.36f;   // у правого бедра
            float g = Stick.lossyScale.y;
            var dir = StickDir(top, (stickLen - 0.25f + GripL) * g, s, fwd, right, out var bladeFwd);
            var rot = Quaternion.LookRotation(bladeFwd, -dir);
            var origin = top + dir * GripL * g;   // локальный ноль древка
            float reachL = (armL1.position - armL0.position).magnitude + (armL2.position - armL1.position).magnitude;
            float best = 0.2f;
            for (float t = 0.5f; t >= 0.2f; t -= 0.02f)
                if ((top + dir * t * g - armL0.position).magnitude <= reachL * 1.4f) { best = t; break; }
            var lowGrip = top + dir * best * g;   // руки мультяшно тянутся, поэтому хват далеко по древку
            // кисть — у манжеты краги: чуть ближе к плечу, чем точка хвата
            float alongR = GloveAlong(false), alongL = GloveAlong(true);
            Ik(armR0, armR1, armR2, top - (top - armR0.position).normalized * alongR, w, right);
            Ik(armL0, armL1, armL2, lowGrip - (lowGrip - armL0.position).normalized * alongL, w, -right);
            gripTop = top; gripLow = lowGrip; gripDir = dir;
            Stick.SetPositionAndRotation(Vector3.Lerp(Stick.position, origin, w), Quaternion.Slerp(Stick.rotation, rot, w));
        }

        /// Поза из захвата движения: стойка (подметание), замах и бросок (удар в гольфе) — корпус, плечи, руки;
        /// клюшка: верх в правой руке, в стойке крюк на льду впереди слева, в замахе и броске — как клюшка у актёра.
        void MocapGrip(PlayerView s, float w)
        {
            float dt = lastDt;
            bool charging = s.Charge > 0.02f, shooting = !charging && swingT < 0.45f;
            wWind += ((charging ? 1 : 0) - wWind) * (1 - Mathf.Exp(-dt * 18));
            wShot += ((shooting ? 1 : 0) - wShot) * (1 - Mathf.Exp(-dt * 22));
            float ws = Mathf.Max(0, 1 - wWind - wShot);
            moStance.SampleLoop(Time.time * 0.8f, fS);
            moWind.Sample(s.Charge, fW);
            moShot.Sample(swingT / 0.45f, fSh);
            for (int i = 0; i < MocapClip.Stride; i++) fMix[i] = fS[i] * ws + fW[i] * wWind + fSh[i] * wShot;
            float act = Mathf.Clamp01(wWind + wShot);

            // корпус и руки
            if (act > 0.01f)
            {
                Aim(bHips, bSpine, MV(0), w * act);
                Twist(bHips, bSpine, bLegL, bLegR, MV(1), w * act);
            }
            Aim(bSpine, bChest, MV(2), w);
            Aim(bChest, Head, MV(3), w * 0.85f);
            Twist(bChest, Head, armL0, armR0, MV(4), w);
            // руки из записи — только в замахе и броске; в стойке их ставим прямо на клюшку (IK ниже)
            Aim(armL0, armL1, MV(5), w * act); Aim(armL1, bWristL, MV(6), w * act);
            Aim(armR0, armR1, MV(7), w * act); Aim(armR1, bWristR, MV(8), w * act);

            // клюшка
            var fwd = Model.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 1e-4f) return;
            fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            float g = Stick.lossyScale.y;
            // стойка как у хоккеиста: верхняя (правая) рука с концом клюшки у правого бедра, нижняя (левая) —
            // далеко по древку, клюшка длинная и пологая, крюк на льду впереди слева
            float reach = (armR1.position - armR0.position).magnitude + (bWristR.position - armR1.position).magnitude;
            var chest = (armL0.position + armR0.position) * 0.5f;
            var topStance = chest + Vector3.down * reach * 0.72f + fwd * reach * 0.4f + right * reach * 0.34f;
            var top = Vector3.Lerp(topStance, HandR.position, act);
            var iceDir = IceDir(top, (stickLen - 0.25f + GripL) * g, fwd, right);
            var dir = Vector3.Slerp(iceDir, MV(9), act).normalized;
            var bladeFwd = Vector3.ProjectOnPlane(right * 0.9f + fwd * 0.35f, dir);
            if (bladeFwd.sqrMagnitude < 1e-4f) bladeFwd = Vector3.ProjectOnPlane(fwd, dir);
            float armLen = (armL1.position - armL0.position).magnitude + (bWristL.position - armL1.position).magnitude;
            float sep = Mathf.Lerp(0.62f, Mathf.Clamp(fMix[30] * armLen, 0.16f, 0.45f), act);   // в стойке руки широко по древку
            var lowGrip = top + dir * sep;
            float alongL = GloveAlong(true), alongR = GloveAlong(false);
            if (act < 0.99f) Ik(armR0, armR1, armR2, top - (top - armR0.position).normalized * alongR, w * (1 - act), right);
            Ik(armL0, armL1, armL2, lowGrip - (lowGrip - armL0.position).normalized * alongL, w, -right);
            gripTop = top; gripLow = lowGrip; gripDir = dir;
            var origin = top + dir * GripL * g;
            Stick.SetPositionAndRotation(Vector3.Lerp(Stick.position, origin, w), Quaternion.Slerp(Stick.rotation, Quaternion.LookRotation(bladeFwd.normalized, -dir), w));
        }

        /// Направление из данных клипа (система персонажа) в мировое.
        Vector3 MV(int j)
        {
            var v = new Vector3(fMix[j * 3] * leftSign, fMix[j * 3 + 1], fMix[j * 3 + 2]);
            if (v.sqrMagnitude < 1e-8f) v = Vector3.up;
            return Model.TransformDirection(v.normalized);
        }

        static void Aim(Transform b, Transform c, Vector3 target, float w)
        {
            if (w <= 0.001f) return;
            var cur = c.position - b.position;
            if (cur.sqrMagnitude < 1e-10f) return;
            b.rotation = Quaternion.Slerp(b.rotation, Quaternion.FromToRotation(cur, target) * b.rotation, w);
        }

        /// Поворот кости b вокруг её оси (b→axisEnd), чтобы линия sideA−sideB смотрела как target.
        static void Twist(Transform b, Transform axisEnd, Transform sideA, Transform sideB, Vector3 target, float w)
        {
            if (w <= 0.001f) return;
            var ax = (axisEnd.position - b.position).normalized;
            var cur = Vector3.ProjectOnPlane(sideA.position - sideB.position, ax);
            var tg = Vector3.ProjectOnPlane(target, ax);
            if (cur.sqrMagnitude < 1e-8f || tg.sqrMagnitude < 1e-8f) return;
            b.rotation = Quaternion.Slerp(b.rotation, Quaternion.FromToRotation(cur, tg) * b.rotation, w);
        }

        /// Древко из точки pivot к крюку на льду впереди слева (без замаха).
        Vector3 IceDir(Vector3 pivot, float L, Vector3 fwd, Vector3 right)
        {
            float g = Stick.lossyScale.y;
            var target = Model.position + (fwd * 1.2f - right * 0.55f) * g;
            float drop = pivot.y - (Model.position.y + 0.04f);
            var hor = target - pivot; hor.y = 0;
            if (drop >= L || hor.sqrMagnitude < 1e-4f) return Vector3.down;
            return (hor.normalized * Mathf.Sqrt(L * L - drop * drop) + Vector3.down * drop) / L;
        }

        /// Направление древка из точки pivot к крюку на льду (с замахом и проводкой), длина L.
        Vector3 StickDir(Vector3 pivot, float L, PlayerView s, Vector3 fwd, Vector3 right, out Vector3 bladeFwd)
        {
            float g = Stick.lossyScale.y;
            var target = Model.position + (fwd * (Goalie ? 0.75f : 1.2f) + right * (Goalie ? 0.05f : -0.55f)) * g;   // клюшка слева: левый хват, как у большинства хоккеистов
            float drop = pivot.y - (Model.position.y + 0.04f);
            var hor = target - pivot; hor.y = 0;
            Vector3 dir;
            if (drop >= L || hor.sqrMagnitude < 1e-4f) dir = Vector3.down;
            else dir = (hor.normalized * Mathf.Sqrt(L * L - drop * drop) + Vector3.down * drop) / L;
            var wind = (-fwd * 0.45f + Vector3.up * 0.85f + right * (Goalie ? 0.3f : -0.3f)).normalized;
            var follow = (fwd * 0.9f + Vector3.up * 0.1f + right * (Goalie ? -0.4f : 0.4f)).normalized;
            dir = Vector3.Slerp(dir, wind, Mathf.Clamp01(s.Charge) * 0.9f);
            if (swingT < 0.35f) dir = Vector3.Slerp(dir, follow, Mathf.Sin(swingT / 0.35f * Mathf.PI));
            // крюк лежит поперёк: носок смотрит влево-вперёд, лицевая сторона — вперёд, к шайбе
            bladeFwd = Vector3.ProjectOnPlane(right * 0.9f + fwd * 0.35f, dir);
            if (bladeFwd.sqrMagnitude < 1e-4f) bladeFwd = Vector3.ProjectOnPlane(fwd, dir);
            bladeFwd.Normalize();
            return dir;
        }

        /// Двухзвенный IK: кисть c — к target; локоть в плоскости текущей позы (или вниз-наружу).
        static void Ik(Transform t0, Transform t1, Transform t2, Vector3 target, float w, Vector3 outward)
        {
            Vector3 a = t0.position, b = t1.position, c = t2.position;
            target = Vector3.Lerp(c, target, w);
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
            var at = target - a;
            float d = Mathf.Clamp(at.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, (l1 + l2) * 0.999f);
            var dir = at.normalized;
            var pole = Vector3.ProjectOnPlane(Vector3.down * 0.6f + outward * 0.8f, dir);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(b - a, dir);
            pole.Normalize();
            float x = (l1 * l1 - l2 * l2 + d * d) / (2 * d);
            float h = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - x * x));
            var elbow = a + dir * x + pole * h;
            t0.rotation = Quaternion.FromToRotation(b - a, elbow - a) * t0.rotation;
            var c2 = t2.position; var b2 = t1.position;
            t1.rotation = Quaternion.FromToRotation(c2 - b2, a + dir * d - b2) * t1.rotation;
            // мультяшно «тянущаяся» рука: если не достаём — удлиняем предплечье (до +50%)
            float need = at.magnitude - (l1 + l2) * 0.999f;
            if (need > 0)
            {
                var fd = (t2.position - t1.position).normalized;
                t2.position += fd * Mathf.Min(need, (l1 + l2) * 0.5f);
            }
        }

        /// Удар по голове: голова резко откидывается назад и качается.
        public void KickHead(float strength) { headKick.V += strength; }

        public void SetHelmet(bool on)
        {
            foreach (var t in HelmetParts) if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        /// После анимации наводим клюшку из руки на точку на льду справа-спереди (замах — вверх-назад, бросок — вперёд).
        void AimStick(PlayerView s)
        {
            if (Stick == null || Model == null) return;
            var fwd = Model.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 1e-4f) return;
            fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            float g = Stick.lossyScale.y;
            float L = (stickLen - 0.25f) * g;
            var h = Stick.position;
            var target = Model.position + (fwd * (Goalie ? 0.75f : 1.2f) + right * (Goalie ? 0.05f : -0.55f)) * g;   // клюшка слева: левый хват, как у большинства хоккеистов
            float drop = h.y - (Model.position.y + 0.04f);
            var hor = target - h; hor.y = 0;
            Vector3 dir;
            if (drop >= L || hor.sqrMagnitude < 1e-4f) dir = Vector3.down;
            else dir = (hor.normalized * Mathf.Sqrt(L * L - drop * drop) + Vector3.down * drop) / L;
            var wind = (-fwd * 0.45f + Vector3.up * 0.85f + right * (Goalie ? 0.3f : -0.3f)).normalized;
            var follow = (fwd * 0.9f + Vector3.up * 0.1f + right * (Goalie ? -0.4f : 0.4f)).normalized;
            dir = Vector3.Slerp(dir, wind, Mathf.Clamp01(s.Charge) * 0.9f);
            if (swingT < 0.35f) dir = Vector3.Slerp(dir, follow, Mathf.Sin(swingT / 0.35f * Mathf.PI));
            var bladeFwd = Vector3.ProjectOnPlane(Goalie ? fwd : right * 0.9f + fwd * 0.35f, dir);
            if (bladeFwd.sqrMagnitude < 1e-4f) bladeFwd = Vector3.ProjectOnPlane(fwd, dir);
            Stick.rotation = Quaternion.LookRotation(bladeFwd.normalized, -dir);
        }
    }
}
