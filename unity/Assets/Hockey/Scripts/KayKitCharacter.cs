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
        public Transform Model, HandR, Head, StickBlade;
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
        const int Hold = 0, Windup = 1, Swing = 2, PassAnim = 3;
        static readonly string[] UpperNames = { "2H_Melee_Idle", "2H_Melee_Attack_Chop", "2H_Melee_Attack_Slice", "Throw" };

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

        // ---------- перекраска палитры в цвет команды ----------
        static Material TeamMaterial(string texName, int team, bool knight)
        {
            string key = texName + team;
            if (matCache.TryGetValue(key, out var m)) return m;
            var src = Resources.Load<Texture2D>("KayKit/" + texName);
            Texture2D tex = src;
            if (src != null && src.isReadable)
            {
                tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = src.GetPixels();
                int w = src.width, h = src.height;
                float targetHue = team == 0 ? 0.0f : 0.6f;
                for (int y = 0; y < h; y++)
                {
                    // нижний ряд палитры — общие цвета мелких деталей, не трогаем
                    if (y < h / 4) continue;
                    for (int x = 0; x < w; x++)
                    {
                        var c = px[y * w + x];
                        Color.RGBToHSV(c, out float hh, out float s, out float v);
                        bool recolor;
                        if (knight) recolor = team == 1 && (hh < 0.04f || hh > 0.94f) && s > 0.45f && v > 0.25f;
                        else
                        {
                            // зелёная одежда разбойника; ячейку с жёлто-зелёным градиентом пропускаем
                            bool skipCell = x >= w / 4 && x < w * 3 / 8 && y >= h / 4 && y < h / 2;
                            recolor = !skipCell && hh > 0.27f && hh < 0.52f && s > 0.2f;
                        }
                        if (!recolor) continue;
                        // лёгкая коррекция яркости, чтобы красный и синий были сочными
                        float nv = Mathf.Clamp01(v * (team == 0 ? 1.08f : 1.05f) + 0.04f);
                        var nc = Color.HSVToRGB(targetHue + (team == 0 ? 0.005f : 0), Mathf.Clamp01(s * 1.05f), nv);
                        nc.a = c.a;
                        px[y * w + x] = nc;
                    }
                }
                tex.SetPixels(px);
                tex.Apply(false);
            }
            m = Mats.ToonModel(tex);
            matCache[key] = m;
            return m;
        }

        /// Создаёт персонажа KayKit или возвращает null, если моделей нет.
        public static KayKitCharacter Create(int team, int variant, bool goalie, Transform parent, Material stickWood, Material stickTape)
        {
            string model = goalie ? "Knight" : (variant % 2 == 0 ? "Rogue" : "RogueHooded");
            var prefab = Load(model);
            if (prefab == null) return null;
            var k = new KayKitCharacter { Goalie = goalie };
            var go = Object.Instantiate(prefab, parent, false);
            go.name = model;
            k.Model = go.transform;

            var mat = TeamMaterial(goalie ? "knight_texture" : "rogue_texture", team, goalie);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                bool body = n.EndsWith("ArmLeft") || n.EndsWith("ArmRight") || n.EndsWith("Body") || n.Contains("_Head") || n.EndsWith("LegLeft") || n.EndsWith("LegRight") || n.EndsWith("Helmet");
                if (!body) { r.enabled = false; r.gameObject.SetActive(false); continue; }
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "handslot.r") k.HandR = t;
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

            if (k.HandR != null) k.BuildStick(stickWood, stickTape);
            go.AddComponent<KayKitGraphOwner>().Graph = k.graph;
            return k;
        }

        void BuildStick(Material wood, Material tape)
        {
            var stick = new GameObject("HockeyStick").transform;
            stick.SetParent(HandR, false);
            // компенсируем масштаб модели, чтобы клюшка была в метрах
            var s = HandR.lossyScale;
            stick.localScale = new Vector3(1 / Mathf.Max(1e-4f, s.x), 1 / Mathf.Max(1e-4f, s.y), 1 / Mathf.Max(1e-4f, s.z));
            float len = Goalie ? 1.1f : 1.25f;
            void P(Mesh m, Material mt, Vector3 pos, Vector3 scale)
            {
                var p = new GameObject("p");
                p.transform.SetParent(stick, false);
                p.transform.localPosition = pos;
                p.transform.localScale = scale;
                p.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = p.AddComponent<MeshRenderer>(); mr.sharedMaterial = mt;
            }
            // рукоять смотрит вдоль оси слота оружия, крюк — внизу
            P(MeshKit.RoundedBox(0.055f, len, 0.045f, 0.018f, 2, 0.05f), wood, new Vector3(0, -len / 2 + 0.22f, 0), Vector3.one);
            P(MeshKit.RoundedBox(0.062f, 0.16f, 0.052f, 0.02f, 2, 0), tape, new Vector3(0, 0.2f, 0), Vector3.one);
            float bl = Goalie ? 0.5f : 0.36f;
            var blade = new GameObject("blade").transform;
            blade.SetParent(stick, false);
            blade.localPosition = new Vector3(0, -len + 0.25f, 0);
            StickBlade = blade;
            P(MeshKit.RoundedBox(0.04f, Goalie ? 0.13f : 0.09f, bl, 0.02f, 2, 0), tape, new Vector3(0, -0.03f, bl / 2 - 0.04f) + blade.localPosition, Vector3.one);
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
            if (s.Save > 0.3f && !wasSave) saveT = 0;
            wasStun = s.Stun; wasDive = s.Dive; wasSwing = swing; wasSave = s.Save > 0.3f;
            fallT += dt; standT += dt; diveT += dt; swingT += dt; saveT += dt; hitT += dt; cheerT += dt;

            float upper = 1;
            if (s.Stun)
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

            // руки с клюшкой: держим, замах при зарядке, удар при броске/пасе
            if (s.Charge > 0.02f)
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
        }
    }
}
