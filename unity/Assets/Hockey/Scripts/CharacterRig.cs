// «Шайбятки»: мягкие игрушечные хоккеисты (большая голова, круглые ручки, толстые коньки)
// и пружинная анимация в духе Fall Guys — наклоны по инерции, сжатие-растяжение, болтающаяся голова,
// кувырки после силовых приёмов, нырок «рыбкой», танцы.
using UnityEngine;

namespace Hockey
{
    public class TeamColors
    {
        public string Main, Dark, Light, Pants, Glove, Helmet;
        public static readonly TeamColors[] All =
        {
            new TeamColors { Main = "#ef5a4c", Dark = "#b8352c", Light = "#ffd7cf", Pants = "#2d3352", Glove = "#8a2a24", Helmet = "#ef5a4c" },
            new TeamColors { Main = "#4a8ff0", Dark = "#2a5fb8", Light = "#d6e7ff", Pants = "#262d4a", Glove = "#23447e", Helmet = "#4a8ff0" },
        };
    }

    /// Пружина второго порядка: плавно догоняет цель с перелётом (overshoot).
    public struct Spring
    {
        public float X, V;
        public void Step(float target, float freq, float damp, float dt)
        {
            float w = freq * Mathf.PI * 2;
            int n = dt > 0.017f ? 2 : 1;
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                float a = w * w * (target - X) - 2 * damp * w * V;
                V += a * h;
                X += V * h;
            }
        }
    }

    public class CharacterRig
    {
        public static readonly string[] Skins = { "#f6cba5", "#eab38b", "#cf946a", "#9a6343", "#fad9bd" };
        public static readonly string[] Hairs = { "#7a4a2a", "#3a271b", "#d6943f", "#f0c95e", "#a8502c", "#26232b" };

        public Transform Root, Scale, Tilt, Hips, Torso, Head, StickPivot, Ice;
        public Transform[] Legs = new Transform[2], Arms = new Transform[2];
        public Transform MegaGlow;
        public KayKitCharacter KK;
        public Transform Dizzy, Blob;
        static Material blobMat;
        public TrailRenderer[] Trails;   // настоящая модель KayKit (если есть), иначе — процедурная
        public bool Goalie;
        public int Team;

        // состояние анимации
        float phase, baseHipY, time;
        Spring leanF, leanS, squash, headP, headR, headY, tumble, diveS, giant, celebrate, charge, swing;
        Vector3 prevPos, vel, acc; float prevYaw, yawRate; bool init;
        bool wasStun, wasAir; float tumbleSpin, tumbleSide;
        Vector3 legL, legR, armL, armR, torsoRot, stickRot;
        float hipsY;

        static Transform Group(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static Transform Part(Mesh mesh, Material mat, Vector3 pos, Transform parent, bool shadow = true, Vector3? scale = null, Vector3? euler = null)
        {
            var go = new GameObject("part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            if (scale.HasValue) go.transform.localScale = scale.Value;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            return go.transform;
        }

        static Mesh RB(float w, float h, float d, float r, float shade = 0.22f) => MeshKit.RoundedBox(w, h, d, r, 3, shade);

        static Texture2D fabric, wood;

        /// Лицом смотрит в +Z.
        public static CharacterRig Build(int team, int skin, int hair, int number, bool goalie, Transform parent)
        {
            fabric ??= TexArt.Fabric();
            wood ??= TexArt.Wood();
            var tc = TeamColors.All[team];
            var c = new CharacterRig { Goalie = goalie, Team = team };
            Material jersey = Mats.Toon(fabric, tc.Main, 0.1f), jerseyDark = Mats.Toon(fabric, tc.Dark, 0.1f), trim = Mats.Toon(fabric, "#fdfdf8", 0.1f);
            Material pants = Mats.Toon(fabric, tc.Pants, 0.08f), glove = Mats.Get(tc.Glove, 0.35f), helmet = Mats.Get(tc.Helmet, 0.9f), helmetDark = Mats.Get(tc.Dark, 0.6f);
            Material skinM = Mats.Get(Skins[skin % Skins.Length], 0.12f), hairM = Mats.Get(Hairs[hair % Hairs.Length], 0.3f);
            Material eye = Mats.Get("#1d1b26", 0.9f), eyeHi = Mats.Emissive("#ffffff", 0.6f), mouth = Mats.Get("#6e2c2a", 0.2f), cheek = Mats.Get("#ff9a96", 0.1f);
            Material boot = Mats.Get("#2b2b35", 0.5f), bootTrim = Mats.Get("#fdfdf8", 0.3f), blade = Mats.Get("#dfe8f2", 1f, 0.8f);
            Material woodM = Mats.Toon(wood, "#ffffff", 0.2f), tape = Mats.Get("#23232b", 0.3f), white = Mats.Get("#f7f7f2", 0.4f), cage = Mats.Get("#3d3d46", 0.8f, 0.6f);
            Material pad = Mats.Get("#f7f7f2", 0.35f), catcher = Mats.Get("#9a6337", 0.3f);

            c.Root = Group("Player", parent, Vector3.zero);
            c.Scale = Group("scale", c.Root, Vector3.zero);
            c.Tilt = Group("tilt", c.Scale, Vector3.zero);

            // основной путь: модели KayKit «Adventurers» в цветах команды
            var kk = KayKitCharacter.Create(team, skin, hair, number, goalie, c.Tilt);
            if (kk != null)
            {
                c.KK = kk;
                var ice0 = Part(RB(1.0f, 1.9f, 1.0f, 0.2f, 0), Mats.Fade("#bfefff", 0.42f, true), new Vector3(0, 0.95f, 0), c.Root, false);
                ice0.gameObject.SetActive(false);
                c.Ice = ice0;
                var glowParent = kk.StickBlade != null ? kk.StickBlade : c.Tilt;
                var glow0 = Part(MeshKit.Sphere(0.16f, 12, 0), Mats.Fade("#ff6a2e", 0.55f, true), Vector3.zero, glowParent, false, new Vector3(1.2f, 0.9f, 2.6f));
                glow0.gameObject.SetActive(false);
                c.MegaGlow = glow0;
                // мягкая контактная тень под ногами (прижимает к льду, сжимается в прыжке)
                if (blobMat == null)
                {
                    blobMat = new Material(Mats.Fade("#151a40", 0.55f)) { mainTexture = TexArt.Soft(64) };
                    blobMat.renderQueue = 2990;
                }
                c.Blob = Part(MeshKit.Plane(1.5f, 1.5f), blobMat, new Vector3(0, 0.008f, 0), c.Root, false, null, new Vector3(90, 0, 0));
                // звёздочки над головой у оглушённого
                c.Dizzy = Group("dizzy", c.Tilt, new Vector3(0, 2.0f, 0));
                for (int i = 0; i < 3; i++)
                {
                    var st = Part(MeshKit.Star(0.11f), Mats.Emissive("#ffd23f", 0.9f), Vector3.zero, c.Dizzy, false);
                    float a = i * Mathf.PI * 2 / 3;
                    st.localPosition = new Vector3(Mathf.Cos(a) * 0.38f, 0, Mathf.Sin(a) * 0.38f);
                }
                c.Dizzy.gameObject.SetActive(false);
                // следы коньков на льду
                c.Trails = new TrailRenderer[2];
                for (int i = 0; i < 2; i++)
                {
                    var tgo = new GameObject("skateTrail");
                    tgo.transform.SetParent(c.Root, false);
                    tgo.transform.localPosition = new Vector3(i == 0 ? 0.13f : -0.13f, 0.012f, 0);
                    tgo.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    var tr = tgo.AddComponent<TrailRenderer>();
                    tr.alignment = LineAlignment.TransformZ;
                    tr.time = 1.4f; tr.minVertexDistance = 0.15f; tr.widthMultiplier = 0.07f;
                    tr.widthCurve = new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(1, 1));
                    tr.sharedMaterial = Mats.Fade("#ffffff", 0.45f);
                    tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    tr.receiveShadows = false;
                    tr.emitting = false;
                    c.Trails[i] = tr;
                }
                c.phase = Random.value * 6;
                c.squash.X = 1; c.giant.X = 1;
                return c;
            }

            c.Hips = Group("hips", c.Tilt, new Vector3(0, goalie ? 0.5f : 0.54f, 0));
            c.baseHipY = c.Hips.localPosition.y; c.hipsY = c.baseHipY;

            // ноги и коньки
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1 : -1;
                var leg = Group("leg", c.Hips, new Vector3(side * 0.13f, -0.02f, 0));
                if (goalie)
                {
                    Part(RB(0.32f, 0.5f, 0.34f, 0.12f), pad, new Vector3(0, -0.24f, 0.07f), leg);
                    Part(RB(0.33f, 0.07f, 0.35f, 0.03f), jersey, new Vector3(0, -0.1f, 0.07f), leg);
                    Part(RB(0.33f, 0.07f, 0.35f, 0.03f), jersey, new Vector3(0, -0.3f, 0.07f), leg);
                }
                else
                {
                    Part(MeshKit.Capsule(0.105f, 0.34f), jersey, new Vector3(0, -0.2f, 0), leg);
                    Part(RB(0.22f, 0.05f, 0.22f, 0.025f), trim, new Vector3(0, -0.24f, 0), leg);
                }
                Part(RB(0.24f, 0.2f, 0.4f, 0.09f), boot, new Vector3(0, -0.41f, 0.05f), leg);
                Part(RB(0.245f, 0.05f, 0.405f, 0.02f), bootTrim, new Vector3(0, -0.34f, 0.05f), leg);
                Part(RB(0.04f, 0.06f, 0.46f, 0.02f, 0), blade, new Vector3(0, -0.53f, 0.05f), leg);
                c.Legs[i] = leg;
            }

            // шорты
            Part(RB(goalie ? 0.6f : 0.52f, 0.3f, 0.4f, 0.13f), pants, new Vector3(0, 0, 0), c.Hips);
            Part(RB(goalie ? 0.605f : 0.525f, 0.05f, 0.405f, 0.02f), jersey, new Vector3(0, -0.09f, 0), c.Hips);

            // торс
            c.Torso = Group("torso", c.Hips, new Vector3(0, 0.1f, 0));
            float bw = goalie ? 0.74f : 0.6f, bd = goalie ? 0.5f : 0.42f;
            Part(RB(bw, 0.5f, bd, 0.19f), jersey, new Vector3(0, 0.22f, 0), c.Torso);
            Part(RB(bw + 0.08f, 0.17f, bd + 0.04f, 0.08f), jersey, new Vector3(0, 0.4f, 0), c.Torso);          // плечевые щитки
            Part(RB(bw + 0.012f, 0.07f, bd + 0.012f, 0.03f), trim, new Vector3(0, 0.1f, 0), c.Torso);
            Part(RB(bw + 0.012f, 0.035f, bd + 0.012f, 0.015f), jerseyDark, new Vector3(0, 0.05f, 0), c.Torso);
            Part(RB(0.26f, 0.06f, 0.08f, 0.03f), trim, new Vector3(0, 0.47f, bd / 2 - 0.01f), c.Torso);        // ворот
            var numMat = Mats.Cutout(Tex.Number(number), Color.white);
            Part(MeshKit.Plane(0.34f, 0.34f), numMat, new Vector3(0, 0.25f, -bd / 2 - 0.008f), c.Torso, false);
            Part(MeshKit.Plane(0.15f, 0.15f), numMat, new Vector3(-0.14f, 0.3f, bd / 2 + 0.008f), c.Torso, false, null, new Vector3(0, 180, 0));

            // голова (большая!)
            c.Head = Group("head", c.Torso, new Vector3(0, 0.48f, 0));
            Part(RB(0.74f, 0.66f, 0.66f, 0.26f, 0.12f), skinM, new Vector3(0, 0.33f, 0), c.Head);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? 1 : -1;
                Part(RB(0.08f, 0.14f, 0.12f, 0.04f), skinM, new Vector3(s * 0.38f, 0.3f, 0.02f), c.Head);           // уши
            }
            var face = Group("face", c.Head, Vector3.zero);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? 1 : -1;
                Part(MeshKit.Capsule(0.045f, 0.13f), eye, new Vector3(s * 0.14f, 0.3f, 0.315f), face, false, new Vector3(1, 1, 0.5f));
                Part(MeshKit.Sphere(0.018f, 8, 0), eyeHi, new Vector3(s * 0.14f + 0.014f, 0.33f, 0.335f), face, false);
                if (goalie) continue; // у вратаря лицо закрыто маской — видны только глаза
                Part(RB(0.13f, 0.035f, 0.03f, 0.015f, 0), hairM, new Vector3(s * 0.15f, 0.42f, 0.32f), face, false, null, new Vector3(0, 0, -s * 8));
                Part(MeshKit.Sphere(0.055f, 10, 0), cheek, new Vector3(s * 0.235f, 0.19f, 0.3f), face, false, new Vector3(1, 0.7f, 0.35f));
            }
            if (!goalie)
            {
                Part(MeshKit.Sphere(0.045f, 10, 0.1f), skinM, new Vector3(0, 0.23f, 0.335f), face, false, new Vector3(1.1f, 0.85f, 0.8f));
                Part(RB(0.11f, 0.03f, 0.02f, 0.012f, 0), mouth, new Vector3(0, 0.135f, 0.325f), face, false);
            }
            // волосы
            Part(RB(0.64f, 0.22f, 0.14f, 0.06f), hairM, new Vector3(0, 0.36f, -0.3f), c.Head);
            for (int i = 0; i < 2; i++) Part(RB(0.08f, 0.18f, 0.16f, 0.035f), hairM, new Vector3((i == 0 ? 1 : -1) * 0.355f, 0.4f, 0.1f), c.Head);
            Part(RB(0.34f, 0.08f, 0.07f, 0.03f), hairM, new Vector3(0.1f, 0.5f, 0.31f), c.Head, true, null, new Vector3(0, 0, 6));
            Part(RB(0.16f, 0.07f, 0.07f, 0.03f), hairM, new Vector3(-0.17f, 0.49f, 0.31f), c.Head, true, null, new Vector3(0, 0, -12));

            if (goalie)
            {
                // маска: белый шлем с тонкой полосой цвета команды и решёткой; глаза видны в окошке
                Part(RB(0.8f, 0.72f, 0.66f, 0.28f, 0.1f), white, new Vector3(0, 0.36f, -0.06f), c.Head);
                Part(RB(0.12f, 0.73f, 0.67f, 0.05f, 0.1f), jersey, new Vector3(0, 0.37f, -0.06f), c.Head);
                Part(RB(0.62f, 0.12f, 0.08f, 0.04f, 0.05f), white, new Vector3(0, 0.46f, 0.29f), c.Head);
                var bars = Group("cage", c.Head, new Vector3(0, 0.25f, 0.35f));
                for (int i = -2; i <= 2; i++) Part(RB(0.02f, 0.36f, 0.02f, 0.008f, 0), cage, new Vector3(i * 0.075f, 0, 0), bars, false);
                for (int i = -1; i <= 1; i++) Part(RB(0.38f, 0.02f, 0.02f, 0.008f, 0), cage, new Vector3(0, i * 0.1f, 0), bars, false);
            }
            else
            {
                // шлем плотно сидит на голове: купол, гребень и наушники
                Part(RB(0.78f, 0.3f, 0.7f, 0.15f, 0.08f), helmet, new Vector3(0, 0.6f, -0.03f), c.Head);
                Part(RB(0.1f, 0.06f, 0.6f, 0.03f, 0), helmetDark, new Vector3(0, 0.76f, -0.05f), c.Head);
                Part(RB(0.79f, 0.03f, 0.71f, 0.012f, 0), helmetDark, new Vector3(0, 0.46f, -0.03f), c.Head);
                for (int i = 0; i < 2; i++) Part(RB(0.07f, 0.18f, 0.2f, 0.035f), helmet, new Vector3((i == 0 ? 1 : -1) * 0.38f, 0.38f, 0.0f), c.Head);
            }

            // руки: плечо-капсула, налокотник, круглая перчатка
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1 : -1;
                var arm = Group("arm", c.Torso, new Vector3(side * (bw / 2 + 0.07f), 0.38f, 0));
                Part(MeshKit.Capsule(0.095f, 0.34f), jersey, new Vector3(0, -0.13f, 0), arm);
                Part(RB(0.2f, 0.06f, 0.2f, 0.03f), trim, new Vector3(0, -0.2f, 0), arm);
                if (goalie && i == 0) Part(MeshKit.Sphere(0.18f, 14), catcher, new Vector3(0, -0.36f, 0.03f), arm, true, new Vector3(1, 1, 0.55f));
                else if (goalie) Part(RB(0.14f, 0.36f, 0.3f, 0.06f), pad, new Vector3(0, -0.36f, 0.03f), arm);
                else Part(MeshKit.Sphere(0.115f, 14), glove, new Vector3(0, -0.34f, 0.01f), arm, true, new Vector3(1, 0.95f, 1.1f));
                c.Arms[i] = arm;
            }

            // клюшка
            var handle = new Vector3(-0.12f, 0.2f, 0.3f);
            c.StickPivot = Group("stick", c.Torso, handle);
            float torsoWorldY = c.Hips.localPosition.y + c.Torso.localPosition.y;
            var bladeTarget = new Vector3(0.3f, 0.03f - torsoWorldY, 0.97f);
            var dir = bladeTarget - handle;
            float len = dir.magnitude;
            var orient = Group("orient", c.StickPivot, Vector3.zero);
            orient.localRotation = Quaternion.FromToRotation(Vector3.down, dir.normalized);
            Part(RB(0.05f, len + 0.25f, 0.05f, 0.015f, 0.05f), woodM, new Vector3(0, -len / 2 + 0.12f, 0), orient);
            Part(RB(0.058f, 0.16f, 0.058f, 0.02f, 0), tape, new Vector3(0, 0.18f, 0), orient);
            var bladeHolder = Group("blade", orient, new Vector3(0, -len, 0));
            bladeHolder.localRotation = Quaternion.Inverse(orient.localRotation);
            float bl = goalie ? 0.52f : 0.38f;
            Part(RB(bl, goalie ? 0.13f : 0.09f, 0.04f, 0.02f, 0), tape, new Vector3(-bl / 2 + 0.05f, 0.02f, 0.02f), bladeHolder);
            var glow = Part(MeshKit.Sphere(0.16f, 12, 0), Mats.Fade("#ff6a2e", 0.55f, true), new Vector3(-bl / 2 + 0.05f, 0.05f, 0.02f), bladeHolder, false, new Vector3(2.4f, 0.9f, 1));
            glow.gameObject.SetActive(false);
            c.MegaGlow = glow;

            // ледяная глыба для «заморозки»
            var ice = Part(RB(1.0f, 1.8f, 1.0f, 0.2f, 0), Mats.Fade("#bfefff", 0.42f, true), new Vector3(0, 0.9f, 0), c.Root, false);
            ice.gameObject.SetActive(false);
            c.Ice = ice;

            c.phase = Random.value * 6;
            c.squash.X = 1; c.giant.X = 1;
            return c;
        }

        static Quaternion Rad(Vector3 r) => Quaternion.Euler(r * Mathf.Rad2Deg);
        static float L(float a, float b, float t) => a + (b - a) * t;
        static Vector3 L(Vector3 a, Vector3 b, float t) => a + (b - a) * t;

        /// Удар по пружине «сплющивания» (приземление, толчок, бросок).
        public void Kick(float squashVel) => squash.V += squashVel;

        public struct AnimCtx
        {
            public bool HasPuck, Celebrate, Dance;
            public Vector3 LookAt;
            public float Dt, Time;
        }

        public void Animate(PlayerView s, AnimCtx ctx)
        {
            float dt = Mathf.Max(1e-4f, ctx.Dt);
            time = ctx.Time;
            var pos = Root.position;
            float yaw = s.H;
            if (!init) { prevPos = pos; prevYaw = yaw; init = true; }
            var v = (pos - prevPos) / dt; v.y = 0;
            var newVel = Vector3.Lerp(vel, v, 1 - Mathf.Exp(-dt * 12));
            acc = Vector3.Lerp(acc, (newVel - vel) / dt, 1 - Mathf.Exp(-dt * 8));
            vel = newVel;
            yawRate = Mathf.Lerp(yawRate, Sim.Wrap(yaw - prevYaw) / dt, 1 - Mathf.Exp(-dt * 10));
            prevPos = pos; prevYaw = yaw;

            var fwd = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
            var right = new Vector3(fwd.z, 0, -fwd.x);
            float spd = vel.magnitude;
            float aF = Vector3.Dot(acc, fwd);
            bool air = s.Y > 0.03f;
            bool frozen = s.Has(Buff.Frozen);
            float k = 1 - Mathf.Exp(-dt * 12);

            Ice.gameObject.SetActive(frozen);
            MegaGlow.gameObject.SetActive(s.Has(Buff.Mega));
            if (frozen) { Root.localPosition = new Vector3(Root.localPosition.x, s.Y, Root.localPosition.z); return; }

            // события: падение, приземление
            if (s.Stun && !wasStun) { tumbleSpin = (Random.value - 0.5f) * 5; tumbleSide = Random.value < 0.5f ? -1 : 1; squash.V -= 4; }
            if (!s.Stun && wasStun) squash.V += 3;
            if (!air && wasAir) squash.V -= 4.5f;
            if (air && !wasAir) squash.V += 3;
            wasStun = s.Stun; wasAir = air;

            // пружины
            leanF.Step(Mathf.Clamp(aF * 0.03f + spd * 0.03f, -0.4f, 0.55f), 2.2f, 0.45f, dt);
            leanS.Step(Mathf.Clamp(-yawRate * spd * 0.03f, -0.5f, 0.5f), 2.0f, 0.4f, dt);
            squash.Step(1, 3.2f, 0.28f, dt);
            tumble.Step(s.Stun ? 1 : 0, s.Stun ? 2.2f : 1.6f, 0.35f, dt);
            diveS.Step(s.Dive ? 1 : 0, 3.5f, 0.55f, dt);
            giant.Step(s.Has(Buff.Giant) ? 1.45f : 1, 2.0f, 0.3f, dt);
            celebrate.Step(ctx.Celebrate || ctx.Dance ? 1 : 0, 1.5f, 0.6f, dt);
            charge.Step(s.Charge, 4f, 0.7f, dt);
            swing.Step(s.Swing > 0 ? 1 : 0, 6f, 0.4f, dt);

            // голова болтается с запаздыванием и смотрит на шайбу
            var toLook = ctx.LookAt - pos; toLook.y = 0;
            float lookYaw = toLook.sqrMagnitude > 0.01f ? Mathf.Clamp(Sim.Wrap(Mathf.Atan2(toLook.x, toLook.z) - yaw), -0.9f, 0.9f) : 0;
            headP.Step(-leanF.X * 0.7f + (air ? -0.2f : 0), 2.6f, 0.22f, dt);
            headR.Step(-leanS.X * 0.8f, 2.4f, 0.2f, dt);
            headY.Step(ctx.Dance ? Mathf.Sin(time * 3) * 0.5f : lookYaw, 2.0f, 0.5f, dt);

            phase += dt * (2.0f + spd * 0.8f);
            float sinp = Mathf.Sin(phase);
            float amp = Mathf.Min(1, spd / 7);

            // масштаб: великан + сжатие/растяжение (объём сохраняется)
            float sq = Mathf.Clamp(squash.X, 0.6f, 1.45f);
            float breathe = 1 + Mathf.Sin(time * 2.4f + phase * 0.1f) * 0.012f;
            float g = giant.X;
            float inv = 1 / Mathf.Sqrt(sq);
            Scale.localScale = new Vector3(g * inv, g * sq * breathe, g * inv);

            if (KK != null)
            {
                bool brawl = s.Brawl;
                float bs = Mathf.Lerp(1, 0.45f, Mathf.Clamp01(s.Y / 2f)) * giant.X * (Goalie ? 1.15f : 1);
                Blob.localScale = new Vector3(bs, bs, bs);
                Blob.localPosition = new Vector3(0, 0.008f - s.Y, 0);
                Blob.gameObject.SetActive(!brawl);
                if (Scale.gameObject.activeSelf == brawl) Scale.gameObject.SetActive(!brawl); // в драке видно только облако
                bool dizzy = (s.Stun || s.Stagger) && !brawl;
                Dizzy.gameObject.SetActive(dizzy);
                if (dizzy) Dizzy.localRotation = Quaternion.Euler(0, time * 240, 0);
                bool trail = !air && spd > 1.5f && !brawl && !s.Stun;
                foreach (var t in Trails) t.emitting = trail;
                if (brawl) return;
                // у модели KayKit падение и нырок — свои анимации; пружины добавляют наклоны по инерции
                Tilt.localRotation = Quaternion.Euler(leanF.X * 0.45f * Mathf.Rad2Deg, 0, -leanS.X * 0.7f * Mathf.Rad2Deg);
                Tilt.localPosition = Vector3.zero;
                KK.Update(s, ctx.Celebrate, ctx.Dance, ctx.HasPuck, Vector3.Dot(vel, right), dt);
                return;
            }

            // наклон всего тела: инерция, кувырок, нырок
            float pitch = leanF.X * 0.6f + diveS.X * 1.35f - tumble.X * 1.5f;
            float roll = -leanS.X * 0.7f + tumble.X * tumbleSide * 0.35f;
            Tilt.localRotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, tumble.X * tumbleSpin * 25, roll * Mathf.Rad2Deg);
            Tilt.localPosition = new Vector3(0, tumble.X * 0.25f + diveS.X * 0.32f, -tumble.X * 0.3f);

            if (Goalie)
            {
                float sv = Mathf.Min(1, s.Save * 3);
                hipsY = L(hipsY, baseHipY - 0.1f - sv * 0.2f, k);
                legL = L(legL, new Vector3(-0.3f, 0, 0.28f + sv * 1.15f), k);
                legR = L(legR, new Vector3(-0.3f, 0, -0.28f - sv * 1.15f), k);
                torsoRot = L(torsoRot, new Vector3(0.38f, Mathf.Sin(time * 1.3f) * 0.05f, 0), k);
                armL = L(armL, new Vector3(-0.9f - sv * 0.9f, 0, 0.55f + sv * 0.7f), k);
                armR = L(armR, new Vector3(-0.9f, 0, -0.25f), k);
                stickRot = L(stickRot, new Vector3(-0.15f + sv * 0.2f, 0.5f, 0), k);
                Apply();
                return;
            }

            float cel = celebrate.X;
            float dive = diveS.X;
            float airK = air ? 1 : 0;

            // ноги: боковой толчок конька, в воздухе — поджаты, в нырке — назад
            float stride = amp * 0.55f * (1 - airK) * (1 - dive);
            float tuck = airK * 0.9f;
            legL = L(legL, new Vector3(sinp * stride * 0.6f - 0.15f - tuck * 0.6f + dive * 0.5f, 0, Mathf.Max(0, sinp) * stride * 0.75f + 0.05f + tuck * 0.15f), k);
            legR = L(legR, new Vector3(-sinp * stride * 0.6f - 0.15f + tuck * 0.3f + dive * 0.5f, 0, -Mathf.Max(0, -sinp) * stride * 0.75f - 0.05f - tuck * 0.15f), k);
            float hop = (ctx.Dance ? Mathf.Abs(Mathf.Sin(time * 5)) * 0.18f : 0);
            hipsY = L(hipsY, baseHipY - 0.03f - amp * 0.05f + Mathf.Abs(sinp) * 0.03f * amp + hop, k);

            // торс: замах, поворот, танец
            float danceTwist = ctx.Dance ? Mathf.Sin(time * 5) * 0.35f : 0;
            torsoRot = L(torsoRot, new Vector3(0.12f + amp * 0.15f + charge.X * 0.15f - cel * 0.25f,
                -charge.X * 0.6f + swing.X * 0.5f - sinp * 0.1f * amp + danceTwist, Mathf.Sin(time * 5) * 0.12f * cel), k);

            // руки
            float flail = airK * Mathf.Sin(time * 22) * 0.5f;
            float wave = Mathf.Sin(time * 9) * 0.5f;
            Vector3 holdL = new Vector3(-1.05f, 0, -0.45f), holdR = new Vector3(-0.85f, 0, 0.1f);
            Vector3 upL = new Vector3(-2.9f + wave * 0.3f, 0, 0.35f + wave * 0.3f), upR = new Vector3(-2.9f - wave * 0.3f, 0, -0.35f - wave * 0.3f);
            Vector3 superL = new Vector3(-2.7f, 0, -0.15f), superR = new Vector3(-2.7f, 0, 0.15f);
            var tL = L(L(holdL, upL, cel), superL, dive) + new Vector3(flail, 0, airK * 0.6f);
            var tR = L(L(holdR, upR, cel), superR, dive) + new Vector3(-flail, 0, -airK * 0.6f);
            armL = L(armL, tL, k);
            armR = L(armR, tR, k);

            // клюшка: замах, бросок, дриблинг, поднята при празднике
            float sx = charge.X * 1.3f - swing.X * 0.7f - cel * 1.6f - dive * 0.6f;
            float sy = -charge.X * 0.6f + swing.X * 0.6f + (ctx.HasPuck && s.Charge <= 0 ? Mathf.Sin(time * 9) * 0.15f : 0);
            stickRot = new Vector3(sx - torsoRot.x * 0.9f, sy, -charge.X * 0.3f);
            Apply();
        }

        void Apply()
        {
            Legs[0].localRotation = Rad(legL); Legs[1].localRotation = Rad(legR);
            Arms[0].localRotation = Rad(armL); Arms[1].localRotation = Rad(armR);
            Torso.localRotation = Rad(torsoRot);
            StickPivot.localRotation = Rad(stickRot);
            Hips.localPosition = new Vector3(0, hipsY, 0);
            Head.localRotation = Rad(new Vector3(headP.X, headY.X, headR.X));
        }
    }
}
