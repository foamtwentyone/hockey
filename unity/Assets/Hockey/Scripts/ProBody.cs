// Собственная модель полевого игрока: тело из гладких сегментов на своём скелете и полностью свои позы и анимации
// (стойка с клюшкой, катание, замах, бросок, пас, прыжок, нырок, падение, радость, удар кулаком, посадка на скамью).
// Сегменты рук и ног каждый кадр натягиваются между суставами, поэтому локти и колени всегда гнутся правильно,
// плечи соединены с корпусом, а кулаки сидят точно на клюшке. Шлем и краги — прежние модели экипировки.
using System.Collections.Generic;
using UnityEngine;

namespace Hockey
{
    public class ProBody
    {
        public Transform Model, Head, Stick, StickBlade;
        public List<Transform> HelmetParts = new List<Transform>();
        public bool Sit;

        // размеры (метры)
        const float HipW = 0.11f, Thigh = 0.2f, Shin = 0.2f, Ankle = 0.08f, Pelvis = 0.2f, Chest = 0.36f, ShW = 0.21f;
        const float UArm = 0.24f, FArm = 0.22f, HeadUp = 0.27f, StickL = 1.12f, FistS = 0.2f;

        struct Pose
        {
            public Vector3 Hip, FootL, FootR, Top, Blade, HandL;
            public float Pitch, Twist, Roll, Sep, FreeL, HeadPitch;
            public static Pose Lerp(Pose a, Pose b, float t)
            {
                t = Mathf.Clamp01(t);
                return new Pose
                {
                    Hip = Vector3.Lerp(a.Hip, b.Hip, t), FootL = Vector3.Lerp(a.FootL, b.FootL, t), FootR = Vector3.Lerp(a.FootR, b.FootR, t),
                    Top = Vector3.Lerp(a.Top, b.Top, t), Blade = Vector3.Lerp(a.Blade, b.Blade, t), HandL = Vector3.Lerp(a.HandL, b.HandL, t),
                    Pitch = Mathf.Lerp(a.Pitch, b.Pitch, t), Twist = Mathf.Lerp(a.Twist, b.Twist, t), Roll = Mathf.Lerp(a.Roll, b.Roll, t),
                    Sep = Mathf.Lerp(a.Sep, b.Sep, t), FreeL = Mathf.Lerp(a.FreeL, b.FreeL, t), HeadPitch = Mathf.Lerp(a.HeadPitch, b.HeadPitch, t),
                };
            }
        }

        // ключевые позы (x — вправо, y — вверх, z — вперёд; клюшка слева — левый хват, правая рука сверху)
        static Pose Stance => new Pose { Hip = new Vector3(0, 0.4f, -0.03f), Pitch = 0.5f, FootL = new Vector3(-0.15f, Ankle, 0.02f), FootR = new Vector3(0.15f, Ankle, -0.04f),
            Top = new Vector3(0.17f, 0.6f, 0.42f), Blade = new Vector3(-0.5f, 0.03f, 1.05f), Sep = 0.27f };
        static Pose Windup => new Pose { Hip = new Vector3(-0.02f, 0.39f, -0.04f), Pitch = 0.42f, Twist = -0.75f, Roll = -0.06f, FootL = new Vector3(-0.17f, Ankle, 0.05f), FootR = new Vector3(0.15f, Ankle, -0.08f),
            Top = new Vector3(-0.02f, 0.72f, 0.4f), Blade = new Vector3(-0.85f, 1.05f, -0.45f), Sep = 0.3f, HeadPitch = 0.1f };
        static Pose Contact => new Pose { Hip = new Vector3(-0.03f, 0.37f, 0.02f), Pitch = 0.62f, Twist = -0.1f, FootL = new Vector3(-0.18f, Ankle, 0.1f), FootR = new Vector3(0.15f, Ankle, -0.1f),
            Top = new Vector3(0.18f, 0.6f, 0.48f), Blade = new Vector3(-0.42f, 0.03f, 0.85f), Sep = 0.28f };
        static Pose Follow => new Pose { Hip = new Vector3(-0.02f, 0.39f, 0.07f), Pitch = 0.5f, Twist = 0.6f, Roll = 0.05f, FootL = new Vector3(-0.18f, Ankle, 0.14f), FootR = new Vector3(0.14f, Ankle + 0.03f, -0.16f),
            Top = new Vector3(0.15f, 0.68f, 0.42f), Blade = new Vector3(-0.05f, 0.55f, 1.35f), Sep = 0.3f, HeadPitch = -0.1f };

        // части тела
        Transform torso, hem, hemStripe, pants, headT, shoulderL, shoulderR, numberT, crestT;
        Transform uaL, faL, elL, stL, uaR, faR, elR, stR, thL, shL, knL, s1L, s2L, btL, blL, thR, shR, knR, s1R, s2R, btR, blR, fistL, fistR;
        int team;
        float phase, swingT = 9, punchT = 9, heldCharge, relCharge, wWind, wShot, wCel, wSad, wAir, wDive, wFall, wSit, wPunch, celT, headKick, headKickV, time;
        bool relPass, wasSwing, wasPunch;

        Transform Make(Mesh m, Material mat, bool outline = true, bool shadow = true)
        {
            var go = new GameObject("b");
            go.transform.SetParent(Model, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = outline ? Mats.Outlined(mat, 0.8f) : mat;
            mr.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static void Seg(Transform t, Vector3 a, Vector3 b, float r)
        {
            var d = b - a; float l = d.magnitude;
            if (l < 1e-5f) { d = Vector3.up; l = 1e-5f; }
            t.localPosition = (a + b) * 0.5f;
            t.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            t.localScale = new Vector3(2 * r, l, 2 * r);
        }
        static void Ring(Transform t, Vector3 a, Vector3 b, float k, float h, float r)
        {
            var d = (b - a).normalized; var p = Vector3.Lerp(a, b, k);
            Seg(t, p - d * h, p + d * h, r);
        }
        static void Ball(Transform t, Vector3 p, float r) { t.localPosition = p; t.localScale = Vector3.one * (2 * r); }

        /// Двухзвенная конечность: возвращает сустав, конец подтягивается в пределы досягаемости.
        static Vector3 Ik(Vector3 a, ref Vector3 end, float l1, float l2, Vector3 pole)
        {
            var at = end - a; float d = Mathf.Clamp(at.magnitude, Mathf.Abs(l1 - l2) + 1e-3f, (l1 + l2) * 0.985f);
            var dir = at.sqrMagnitude > 1e-8f ? at.normalized : Vector3.down;
            end = a + dir * d;
            var p = Vector3.ProjectOnPlane(pole, dir);
            if (p.sqrMagnitude < 1e-6f) p = Vector3.ProjectOnPlane(Vector3.forward, dir);
            p.Normalize();
            float x = (l1 * l1 - l2 * l2 + d * d) / (2 * d), h = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - x * x));
            return a + dir * x + p * h;
        }

        public static ProBody Create(int team, int skin, int tape, int number, Transform parent)
        {
            var b = new ProBody { team = team };
            b.Model = new GameObject("ProBody").transform;
            b.Model.SetParent(parent, false);
            b.Model.localScale = Vector3.one * 1.18f;   // рост как у прежней модели
            var tc = TeamColors.All[Mathf.Clamp(team, 0, 1)];
            Material jersey = Mats.Get(tc.Main, 0.12f), dark = Mats.Get(tc.Dark, 0.12f), white = Mats.Get("#f6f6f2", 0.1f), pantsM = Mats.Get(tc.Pants, 0.1f);
            Material skinM = Mats.Get(CharacterRig.Skins[Mathf.Abs(skin) % CharacterRig.Skins.Length], 0.12f), black = Mats.Get("#23232b", 0.35f), steel = Mats.Get("#dfe8f2", 0.6f), hair = Mats.Get("#7a4a2a", 0.2f);
            Mesh cyl = MeshKit.Cylinder(0.5f, 1f, 18), sph = MeshKit.Sphere(0.5f, 18, 0), box = MeshKit.Box(1, 1, 1), sup = MeshKit.Superellipsoid(0.5f, 0.5f, 0.5f, 0.6f, 0.75f, 26);

            b.pants = b.Make(sup, pantsM);
            b.torso = b.Make(sup, jersey);
            b.hem = b.Make(cyl, jersey); b.hemStripe = b.Make(cyl, white, false, false);
            b.shoulderL = b.Make(sph, jersey); b.shoulderR = b.Make(sph, jersey);
            b.uaL = b.Make(cyl, jersey); b.faL = b.Make(cyl, jersey); b.elL = b.Make(sph, jersey); b.stL = b.Make(cyl, white, false, false);
            b.uaR = b.Make(cyl, jersey); b.faR = b.Make(cyl, jersey); b.elR = b.Make(sph, jersey); b.stR = b.Make(cyl, white, false, false);
            b.thL = b.Make(cyl, pantsM); b.shL = b.Make(cyl, jersey); b.knL = b.Make(sph, pantsM); b.s1L = b.Make(cyl, white, false, false); b.s2L = b.Make(cyl, dark, false, false);
            b.thR = b.Make(cyl, pantsM); b.shR = b.Make(cyl, jersey); b.knR = b.Make(sph, pantsM); b.s1R = b.Make(cyl, white, false, false); b.s2R = b.Make(cyl, dark, false, false);
            b.btL = b.Make(MeshKit.RoundedBox(0.11f, 0.11f, 0.23f, 0.03f), black); b.btR = b.Make(MeshKit.RoundedBox(0.11f, 0.11f, 0.23f, 0.03f), black);
            b.blL = b.Make(box, steel, false, false); b.blR = b.Make(box, steel, false, false);
            foreach (var bt in new[] { b.btL, b.btR })
            {   // белый носок ботинка и шнуровка
                var toe = new GameObject("toe").transform; toe.SetParent(bt, false); toe.localPosition = new Vector3(0, -0.02f, 0.1f);
                toe.gameObject.AddComponent<MeshFilter>().sharedMesh = MeshKit.RoundedBox(0.112f, 0.07f, 0.06f, 0.02f);
                toe.gameObject.AddComponent<MeshRenderer>().sharedMaterial = white;
            }

            // голова: лицо в духе прежней модели — большие глаза, нос, улыбка, румянец; волосы под шлемом
            b.headT = b.Make(sph, skinM);
            b.Head = b.headT;
            void Face(Mesh m, Material mat, Vector3 pos, Vector3 scale)
            {
                var f = new GameObject("f").transform; f.SetParent(b.headT, false); f.localPosition = pos; f.localScale = scale;
                f.gameObject.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = f.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var eye = Mats.Get("#1d1b26", 0.9f);
            foreach (float sx in new[] { -1f, 1f })
            {
                Face(sph, eye, new Vector3(sx * 0.19f, 0.0f, 0.44f), new Vector3(0.13f, 0.2f, 0.08f));
                Face(sph, Mats.Emissive("#ffffff", 0.6f), new Vector3(sx * 0.19f + 0.025f, 0.05f, 0.475f), new Vector3(0.04f, 0.05f, 0.03f));
                Face(sph, Mats.Get("#ff9a96", 0.1f), new Vector3(sx * 0.3f, -0.16f, 0.36f), new Vector3(0.12f, 0.07f, 0.05f));
                Face(sph, hair, new Vector3(sx * 0.19f, 0.2f, 0.42f), new Vector3(0.17f, 0.04f, 0.06f));   // брови
            }
            Face(sph, skinM, new Vector3(0, -0.1f, 0.5f), new Vector3(0.13f, 0.11f, 0.12f));                 // нос
            Face(sph, Mats.Get("#6e2c2a", 0.2f), new Vector3(0, -0.27f, 0.42f), new Vector3(0.2f, 0.05f, 0.08f));   // улыбка
            Face(sph, hair, new Vector3(0, 0.08f, -0.06f), new Vector3(1.05f, 0.95f, 0.98f));                // волосы (видны по бокам из-под шлема)

            // номер и эмблема
            b.numberT = b.Make(MeshKit.Plane(0.3f, 0.3f), Mats.Cutout(Tex.Number(number), Color.white), false, false);
            b.crestT = b.Make(MeshKit.Plane(0.2f, 0.24f), Mats.Cutout(HockeyGear.CrestTexture(), Color.white), false, false);

            // клюшка: древко от набалдашника (y=+0.1) до крюка (y=−StickL), ноль — в верхнем кулаке
            b.Stick = new GameObject("Stick").transform; b.Stick.SetParent(b.Model, false);
            void P(Mesh m, Material mt, Vector3 pos)
            {
                var p = new GameObject("p"); p.transform.SetParent(b.Stick, false); p.transform.localPosition = pos;
                p.AddComponent<MeshFilter>().sharedMesh = m; p.AddComponent<MeshRenderer>().sharedMaterial = mt;
            }
            var carbon = Mats.Get("#2b2f3d", 0.85f); var stripe = Mats.Get(tc.Main, 0.8f);
            var tapeM = Mats.Get(KayKitCharacter.TapeColors[Mathf.Abs(tape) % KayKitCharacter.TapeColors.Length], 0.25f);
            float shaft = StickL + 0.1f;
            P(MeshKit.Superellipsoid(0.027f, shaft / 2, 0.021f, 0.25f, 0.35f, 20), carbon, new Vector3(0, 0.1f - shaft / 2, 0));
            P(MeshKit.Superellipsoid(0.034f, 0.05f, 0.028f, 0.5f, 0.5f, 16), tapeM, new Vector3(0, 0.07f, 0));
            P(MeshKit.Superellipsoid(0.03f, 0.04f, 0.024f, 0.25f, 0.35f, 16), stripe, new Vector3(0, -StickL * 0.55f, 0));
            b.StickBlade = new GameObject("blade").transform; b.StickBlade.SetParent(b.Stick, false); b.StickBlade.localPosition = new Vector3(0, -StickL, 0);
            const float bl = 0.37f;
            P(MeshKit.Blade(bl, 0.085f, 0.022f, 0.045f), carbon, new Vector3(0, -StickL - 0.02f, -0.04f));
            P(MeshKit.Superellipsoid(0.016f, 0.085f * 0.56f, bl * 0.25f, 0.3f, 0.4f, 16), tapeM, new Vector3(0.006f, -StickL - 0.02f, bl * 0.42f));

            // краги-кулаки и шлем — модели экипировки
            b.fistL = HockeyGear.Fist(team, b.Model, FistS, false); b.fistR = HockeyGear.Fist(team, b.Model, FistS, true);
            if (b.fistL != null) b.fistL.gameObject.SetActive(true);
            if (b.fistR != null) b.fistR.gameObject.SetActive(true);
            b.time = Random.value * 10;
            b.Solve(Stance, 0);
            b.HelmetParts = HockeyGear.HeadGear(b.headT, b.headT.position, new Vector3(0.52f, 0.5f, 0.54f), b.Model.rotation, team);
            return b;
        }

        public void SetHelmet(bool on) { foreach (var t in HelmetParts) if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on); }
        public void KickHead(float strength) { headKickV += strength; }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        static Pose ShotPose(float u)
        {
            if (u < 0.38f) return Pose.Lerp(Windup, Contact, Ease(u / 0.38f));
            if (u < 0.7f) return Pose.Lerp(Contact, Follow, Ease((u - 0.38f) / 0.32f));
            return Pose.Lerp(Follow, Stance, Ease((u - 0.7f) / 0.3f));
        }

        public void Update(PlayerView s, bool celebrate, bool dance, bool sad, bool hasPuck, float dt)
        {
            time += dt;
            bool swing = s.Swing > 0;
            if (s.Charge > 0.02f) heldCharge = s.Charge;
            if (swing && !wasSwing) { swingT = 0; relPass = s.Swing < 0.27f; relCharge = relPass ? 0 : heldCharge; heldCharge = 0; }
            if (s.Punch && !wasPunch) punchT = 0;
            wasSwing = swing; wasPunch = s.Punch; swingT += dt; punchT += dt;
            float spd = s.Spd;
            phase += dt * (2.2f + Mathf.Min(spd, 11) * 0.75f);

            // стойка: катание — толчки коньками в стороны-назад, корпус качается навстречу; клюшка ведёт шайбу
            var p = Stance;
            float amp = Mathf.Clamp01(spd / 6f), sp = Mathf.Sin(phase), pushL = Mathf.Max(0, sp), pushR = Mathf.Max(0, -sp);
            p.FootL += new Vector3(-0.12f * pushL, 0.05f * Mathf.Max(0, -Mathf.Cos(phase)) * pushR, -0.2f * pushL + 0.06f * pushR) * amp;
            p.FootR += new Vector3(0.12f * pushR, 0.05f * Mathf.Max(0, Mathf.Cos(phase)) * pushL, -0.2f * pushR + 0.06f * pushL) * amp;
            p.Hip += new Vector3(-sp * 0.035f * amp, -0.02f * amp + Mathf.Abs(sp) * 0.015f * amp + Mathf.Sin(time * 2.1f) * 0.006f, 0);
            p.Roll = sp * 0.07f * amp; p.Twist = -sp * 0.08f * amp;
            p.Pitch += Mathf.Min(spd, 11) * 0.012f;
            float dr = hasPuck ? Mathf.Sin(time * 7) * 0.1f : Mathf.Sin(time * 1.7f) * 0.03f;   // ведение шайбы
            p.Blade += new Vector3(dr, 0, Mathf.Abs(dr) * 0.2f);
            p.Top += new Vector3(-dr * 0.2f, 0, 0);

            float shotDur = relPass ? 0.24f : Mathf.Lerp(0.26f, 0.52f, relCharge), shotFrom = relPass ? 0.3f : Mathf.Lerp(0.26f, 0f, relCharge);
            bool charging = s.Charge > 0.02f, shooting = !charging && swingT < shotDur;
            float K(float rate) => 1 - Mathf.Exp(-dt * rate);
            wWind += ((charging ? 1 : 0) - wWind) * K(18); wShot += ((shooting ? 1 : 0) - wShot) * K(24);
            bool air = s.Y > 0.05f && !s.Dive;
            wCel += ((celebrate || dance ? 1 : 0) - wCel) * K(8); wSad += ((sad ? 1 : 0) - wSad) * K(5);
            wAir += ((air ? 1 : 0) - wAir) * K(14); wDive += ((s.Dive ? 1 : 0) - wDive) * K(12);
            wFall += ((s.Stun ? 1 : 0) - wFall) * K(s.Stun ? 10 : 5); wSit += ((Sit ? 1 : 0) - wSit) * K(20);
            wPunch += ((punchT < 0.3f ? 1 : 0) - wPunch) * K(25);

            if (wWind > 0.001f) p = Pose.Lerp(p, Pose.Lerp(Stance, Windup, Ease(s.Charge * 1.15f)), wWind);
            if (wShot > 0.001f) p = Pose.Lerp(p, ShotPose(shotFrom + (1 - shotFrom) * Mathf.Clamp01(swingT / shotDur)), wShot);
            if (wSad > 0.001f)
            {
                var q = p; q.Pitch = 0.75f; q.HeadPitch = 0.45f; q.Hip.y -= 0.03f; q.Roll = Mathf.Sin(time * 1.3f) * 0.05f;
                p = Pose.Lerp(p, q, wSad);
            }
            if (wAir > 0.001f)
            {
                var q = p; q.FootL.y += 0.14f; q.FootR.y += 0.1f; q.FootL.z += 0.08f; q.Pitch = 0.3f; q.Top.y += 0.12f; q.Blade.y += 0.35f;
                p = Pose.Lerp(p, q, wAir);
            }
            if (wCel > 0.001f)
            {   // радость: клюшка в поднятой правой руке, левая машет, прыжки
                celT += dt;
                float hop = Mathf.Abs(Mathf.Sin(celT * 6.5f)) * 0.12f, wave = Mathf.Sin(celT * 9);
                var q = new Pose { Hip = new Vector3(0, 0.43f + hop, 0), Pitch = -0.05f, Roll = wave * 0.06f, FootL = new Vector3(-0.14f, Ankle + hop * 0.9f, 0), FootR = new Vector3(0.14f, Ankle + hop * 0.6f, 0),
                    Top = new Vector3(0.36f, 1.42f + hop, 0.08f), Blade = new Vector3(0.5f + wave * 0.15f, 2.6f, 0.1f), Sep = 0.3f, FreeL = 1, HandL = new Vector3(-0.38f - wave * 0.06f, 1.42f + hop, 0.08f), HeadPitch = -0.2f };
                p = Pose.Lerp(p, q, wCel);
            }
            else celT = 0;
            if (wPunch > 0.001f)
            {   // удар левой: клюшка остаётся в правой руке
                var q = p; q.FreeL = 1; q.Twist = 0.5f; q.HandL = new Vector3(-0.02f, 0.95f, 0.62f); q.Top = new Vector3(0.3f, 0.7f, 0.2f); q.Blade = new Vector3(0.45f, 0.03f, 0.9f);
                p = Pose.Lerp(p, q, wPunch);
            }
            if (wDive > 0.001f)
            {
                var q = new Pose { Hip = new Vector3(0, 0.42f, 0), Pitch = 0.1f, FootL = new Vector3(-0.1f, 0.05f, -0.05f), FootR = new Vector3(0.1f, 0.05f, -0.05f),
                    Top = new Vector3(0.14f, 1.25f, 0.2f), Blade = new Vector3(-0.1f, 2.3f, 0.3f), Sep = 0.26f };
                p = Pose.Lerp(p, q, wDive);
            }
            if (wFall > 0.001f)
            {   // лежит на спине: руки и ноги в стороны, клюшка рядом
                var q = new Pose { Hip = new Vector3(0, 0.43f, 0), Pitch = -0.05f, FootL = new Vector3(-0.22f, 0.06f, 0.1f), FootR = new Vector3(0.24f, 0.06f, 0.06f),
                    Top = new Vector3(0.55f, 1.0f, 0.1f), Blade = new Vector3(0.75f, 0.2f, 0.4f), Sep = 0.3f, FreeL = 1, HandL = new Vector3(-0.58f, 1.05f, 0.1f), HeadPitch = -0.15f };
                p = Pose.Lerp(p, q, wFall);
            }
            if (wSit > 0.001f)
            {   // сидит на скамье: бёдра горизонтально, руки на коленях
                var q = new Pose { Hip = new Vector3(0, 0.33f, -0.3f), Pitch = 0.1f + Mathf.Sin(time * 1.6f) * 0.015f, FootL = new Vector3(-0.13f, 0.13f + Mathf.Sin(time * 1.9f) * 0.02f, -0.1f), FootR = new Vector3(0.13f, 0.13f - Mathf.Sin(time * 1.9f) * 0.02f, -0.1f),
                    Top = new Vector3(0.15f, 0.4f, -0.08f), Blade = new Vector3(0.15f, -1f, -0.06f), Sep = 0.3f, FreeL = 1, HandL = new Vector3(-0.15f, 0.4f, -0.08f), HeadPitch = Mathf.Sin(time * 0.7f) * 0.06f };
                p = Pose.Lerp(p, q, wSit);
            }
            bool showStick = wSit < 0.5f;
            if (Stick.gameObject.activeSelf != showStick) Stick.gameObject.SetActive(showStick);

            // всё тело: нырок — вперёд «рыбкой», падение — на спину
            Model.localRotation = Quaternion.Euler(78 * wDive - 84 * wFall, 0, 0);
            Model.localPosition = new Vector3(0, 0.22f * wDive + 0.2f * wFall, -0.35f * wFall + 0.1f * wDive);

            // голова, откинутая ударом шайбы, — пружиной
            float w = 2.2f * Mathf.PI * 2;
            headKickV += (-w * w * headKick - 2 * 0.25f * w * headKickV) * dt; headKick += headKickV * dt;
            Solve(p, -headKick);
        }

        void Solve(Pose p, float headExtra)
        {
            var qp = Quaternion.Euler(p.Pitch * 0.35f * Mathf.Rad2Deg, p.Twist * 0.3f * Mathf.Rad2Deg, p.Roll * Mathf.Rad2Deg);
            var qc = Quaternion.Euler(p.Pitch * Mathf.Rad2Deg, p.Twist * Mathf.Rad2Deg, p.Roll * 0.5f * Mathf.Rad2Deg);
            var hip = p.Hip;
            var waist = hip + qp * new Vector3(0, Pelvis, 0);
            var chestTop = waist + qc * new Vector3(0, Chest, 0);
            Vector3 right = qc * Vector3.right, fwd = qc * Vector3.forward;

            // корпус
            var qt = Quaternion.Slerp(qp, qc, 0.65f);
            var mid = (hip + chestTop) * 0.5f;
            torso.localPosition = mid + qt * new Vector3(0, 0.03f, 0); torso.localRotation = qt;
            torso.localScale = new Vector3(0.5f, (chestTop - hip).magnitude + 0.2f, 0.38f);
            pants.localPosition = hip + qp * new Vector3(0, -0.06f, 0); pants.localRotation = qp; pants.localScale = new Vector3(0.47f, 0.3f, 0.37f);
            hem.localPosition = hip + qp * new Vector3(0, 0.06f, 0); hem.localRotation = qp; hem.localScale = new Vector3(0.515f, 0.13f, 0.395f);
            hemStripe.localPosition = hip + qp * new Vector3(0, 0.02f, 0); hemStripe.localRotation = qp; hemStripe.localScale = new Vector3(0.522f, 0.035f, 0.402f);
            numberT.localPosition = mid + qt * new Vector3(0, 0.1f, -0.197f); numberT.localRotation = qt;
            crestT.localPosition = mid + qt * new Vector3(0, 0.12f, 0.197f); crestT.localRotation = qt * Quaternion.Euler(0, 180, 0);

            // голова держится ровнее корпуса
            var qh = Quaternion.Euler((p.Pitch * 0.2f + p.HeadPitch + headExtra) * Mathf.Rad2Deg, p.Twist * 0.4f * Mathf.Rad2Deg, 0);
            headT.localPosition = chestTop + qc * new Vector3(0, 0.05f, 0.03f) + qh * new Vector3(0, HeadUp - 0.05f, 0.02f);
            headT.localRotation = qh; headT.localScale = Vector3.one * 0.51f;

            // ноги
            Leg(hip + qp * new Vector3(-HipW, -0.02f, 0), p.FootL, -1, thL, shL, knL, s1L, s2L, btL, blL);
            Leg(hip + qp * new Vector3(HipW, -0.02f, 0), p.FootR, 1, thR, shR, knR, s1R, s2R, btR, blR);

            // клюшка и руки: правая — сверху, левая — ниже по древку (или свободна)
            Vector3 shR0 = chestTop + qc * new Vector3(ShW, -0.03f, 0), shL0 = chestTop + qc * new Vector3(-ShW, -0.03f, 0);
            Ball(shoulderR, shR0, 0.115f); Ball(shoulderL, shL0, 0.115f);
            var top = p.Top;
            var wr = top; var elbowR = Ik(shR0, ref wr, UArm, FArm + 0.04f, right * 1f + Vector3.down * 0.45f - fwd * 0.25f);
            top = wr;
            var dir = (p.Blade - top).sqrMagnitude > 1e-6f ? (p.Blade - top).normalized : Vector3.down;
            if (p.Blade.y < 0.2f && dir.y < -0.05f)
            {   // крюк на льду: длина клюшки фиксирована, пересчитываем наклон
                float drop = top.y - 0.035f; var hor = p.Blade - top; hor.y = 0;
                if (drop < StickL && hor.sqrMagnitude > 1e-6f) dir = (hor.normalized * Mathf.Sqrt(StickL * StickL - drop * drop) + Vector3.down * drop) / StickL;
            }
            var low = top + dir * p.Sep;
            for (int i = 0; i < 10 && p.Sep - i * 0.02f > 0.16f && (low - shL0).magnitude > (UArm + FArm) * 0.93f; i++) low = top + dir * (p.Sep - (i + 1) * 0.02f);
            var hl = Vector3.Lerp(low, p.HandL, p.FreeL);
            var elbowL = Ik(shL0, ref hl, UArm, FArm + 0.04f, -right * 1f + Vector3.down * 0.45f - fwd * 0.25f);
            Arm(shR0, elbowR, top, uaR, faR, elR, stR); Arm(shL0, elbowL, hl, uaL, faL, elL, stL);

            var bladeFwd = Vector3.ProjectOnPlane(Vector3.right * 0.9f + Vector3.forward * 0.35f, dir);
            if (bladeFwd.sqrMagnitude < 1e-4f) bladeFwd = Vector3.ProjectOnPlane(Vector3.forward, dir);
            Stick.localPosition = top; Stick.localRotation = Quaternion.LookRotation(bladeFwd.normalized, -dir);
            Fist(fistR, top, elbowR, dir, false, 1);
            Fist(fistL, hl, elbowL, dir, true, 1 - p.FreeL);
        }

        void Leg(Vector3 hipJ, Vector3 foot, float side, Transform th, Transform sh, Transform kn, Transform s1, Transform s2, Transform boot, Transform blade)
        {
            var knee = Ik(hipJ, ref foot, Thigh, Shin, Vector3.forward + Vector3.right * side * 0.35f);
            Seg(th, hipJ, knee, 0.095f); Seg(sh, knee, foot, 0.078f); Ball(kn, knee, 0.092f);
            Ring(s1, knee, foot, 0.45f, 0.025f, 0.081f); Ring(s2, knee, foot, 0.68f, 0.012f, 0.081f);
            var yaw = Quaternion.Euler(0, side * 14, 0);
            boot.localPosition = foot + yaw * new Vector3(0, -0.015f, 0.035f); boot.localRotation = yaw; boot.localScale = Vector3.one;
            blade.localPosition = foot + yaw * new Vector3(0, -0.068f, 0.03f); blade.localRotation = yaw; blade.localScale = new Vector3(0.014f, 0.03f, 0.27f);
        }

        static void Arm(Vector3 sh, Vector3 el, Vector3 hand, Transform ua, Transform fa, Transform elb, Transform stripe)
        {
            Seg(ua, sh, el, 0.08f); Seg(fa, el, hand, 0.07f); Ball(elb, el, 0.078f);
            Ring(stripe, el, hand, 0.55f, 0.022f, 0.073f);
        }

        /// Кулак: на клюшке — обхватывает древко, раструб к локтю; свободный — продолжение предплечья.
        void Fist(Transform f, Vector3 grip, Vector3 elbow, Vector3 stickDir, bool left, float onStick)
        {
            if (f == null) return;
            var fore = (grip - elbow).sqrMagnitude > 1e-8f ? (grip - elbow).normalized : Vector3.down;
            var up = Vector3.ProjectOnPlane(fore, stickDir);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, stickDir);
            up = Vector3.Slerp(up.normalized, fore, Mathf.Lerp(1, 0.5f, onStick)).normalized;
            var x = Vector3.ProjectOnPlane(left ? -stickDir : stickDir, up);
            if (x.sqrMagnitude < 1e-6f) x = Vector3.ProjectOnPlane(Vector3.right, up);
            f.localPosition = grip + fore * (0.03f * (1 - onStick));
            f.localRotation = Quaternion.LookRotation(Vector3.Cross(x.normalized, up), up);
        }
    }
}
