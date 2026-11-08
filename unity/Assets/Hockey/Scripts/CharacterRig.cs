// Мультяшные «чиби»-хоккеисты из скруглённых кубиков: большая голова, короткое тело, процедурная анимация.
using UnityEngine;

namespace Hockey
{
    public class TeamColors
    {
        public string Main, Dark, Accent, Pants, Glove;
        public static readonly TeamColors[] All =
        {
            new TeamColors { Main = "#e2483d", Dark = "#9e2b25", Accent = "#ffffff", Pants = "#2b2f45", Glove = "#7a2420" },
            new TeamColors { Main = "#3f86e8", Dark = "#22549c", Accent = "#ffffff", Pants = "#22283d", Glove = "#1f3f78" },
        };
    }

    public class CharacterRig
    {
        public static readonly string[] Skins = { "#f3c9a3", "#e9b48c", "#cf956b", "#94603f", "#f7d9bd" };
        public static readonly string[] Hairs = { "#6b4226", "#2e1f16", "#c98a3c", "#e8c35c", "#8e4a26", "#1d1d22" };

        public Transform Root, Tilt, Hips, Torso, Head, StickPivot;
        public Transform[] Legs = new Transform[2], Arms = new Transform[2];
        public bool Goalie;
        public int Team;
        float phase, fall, celebrate, baseHipY, sx;
        // текущие углы (градусы удобнее держать в радианах и конвертировать при записи)
        Vector3 legL, legR, armL, armR, torsoRot, stickRot;
        float hipsY, hipsYaw;

        static Transform Group(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static Transform Part(Mesh mesh, Material mat, Vector3 pos, Transform parent, bool shadow = true)
        {
            var go = new GameObject("part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        static Mesh RB(float w, float h, float d, float r) => MeshKit.RoundedBox(w, h, d, r);

        /// Лицом смотрит в +Z.
        public static CharacterRig Build(int team, int skin, int hair, int number, bool goalie, Transform parent)
        {
            var tc = TeamColors.All[team];
            var c = new CharacterRig { Goalie = goalie, Team = team };
            Material main = Mats.Get(tc.Main), dark = Mats.Get(tc.Dark), white = Mats.Get("#f4f4f2"), pants = Mats.Get(tc.Pants), glove = Mats.Get(tc.Glove);
            Material skinM = Mats.Get(Skins[skin % Skins.Length], 0.15f), hairM = Mats.Get(Hairs[hair % Hairs.Length], 0.2f), black = Mats.Get("#1b1b20", 0.6f);
            Material blade = Mats.Get("#cfd8e3", 0.8f, 0.6f), boot = Mats.Get("#26262c", 0.4f), wood = Mats.Get("#d9a066", 0.3f), tape = Mats.Get("#202024");
            Material cheek = Mats.Fade("#ff8f8f", 0.45f), eyeHi = Mats.Emissive("#ffffff", 0.4f), cage = Mats.Get("#3a3a40", 0.6f, 0.5f), brown = Mats.Get("#8a5a32");

            c.Root = Group("Player", parent, Vector3.zero);
            c.Tilt = Group("tilt", c.Root, Vector3.zero);
            c.Hips = Group("hips", c.Tilt, new Vector3(0, goalie ? 0.5f : 0.55f, 0));
            c.baseHipY = c.Hips.localPosition.y; c.hipsY = c.baseHipY;

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1 : -1;
                var leg = Group("leg", c.Hips, new Vector3(side * 0.13f, 0, 0));
                if (goalie)
                {
                    Part(RB(0.3f, 0.52f, 0.32f, 0.08f), white, new Vector3(0, -0.24f, 0.06f), leg);
                    Part(RB(0.31f, 0.08f, 0.33f, 0.03f), main, new Vector3(0, -0.12f, 0.06f), leg);
                    Part(RB(0.31f, 0.08f, 0.33f, 0.03f), main, new Vector3(0, -0.32f, 0.06f), leg);
                }
                else
                {
                    Part(RB(0.18f, 0.3f, 0.19f, 0.05f), main, new Vector3(0, -0.2f, 0), leg);
                    Part(RB(0.185f, 0.06f, 0.195f, 0.02f), white, new Vector3(0, -0.2f, 0), leg);
                }
                Part(RB(0.2f, 0.15f, 0.34f, 0.05f), boot, new Vector3(0, -0.43f, 0.04f), leg);
                Part(MeshKit.Box(0.035f, 0.05f, 0.38f), blade, new Vector3(0, -0.52f, 0.04f), leg);
                c.Legs[i] = leg;
            }

            Part(RB(goalie ? 0.58f : 0.5f, 0.26f, 0.38f, 0.08f), pants, Vector3.zero, c.Hips);
            Part(RB(goalie ? 0.585f : 0.505f, 0.05f, 0.385f, 0.02f), main, new Vector3(0, -0.08f, 0), c.Hips);

            c.Torso = Group("torso", c.Hips, new Vector3(0, 0.1f, 0));
            float bw = goalie ? 0.72f : 0.6f, bd = goalie ? 0.48f : 0.4f;
            Part(RB(bw, 0.46f, bd, 0.1f), main, new Vector3(0, 0.22f, 0), c.Torso);
            Part(RB(bw + 0.01f, 0.07f, bd + 0.01f, 0.03f), white, new Vector3(0, 0.1f, 0), c.Torso);
            Part(RB(bw + 0.01f, 0.04f, bd + 0.01f, 0.02f), dark, new Vector3(0, 0.05f, 0), c.Torso);
            // номер на спине и на груди
            var numMat = Mats.Cutout(Tex.Number(number), Color.white);
            Part(MeshKit.Plane(0.34f, 0.34f), numMat, new Vector3(0, 0.27f, -bd / 2 - 0.006f), c.Torso, false);
            var chest = Part(MeshKit.Plane(0.16f, 0.16f), numMat, new Vector3(-0.13f, 0.3f, bd / 2 + 0.006f), c.Torso, false);
            chest.localRotation = Quaternion.Euler(0, 180, 0);

            // голова
            c.Head = Group("head", c.Torso, new Vector3(0, 0.44f, 0));
            Part(RB(0.66f, 0.6f, 0.6f, 0.16f), skinM, new Vector3(0, 0.3f, 0), c.Head);
            var face = Group("face", c.Head, Vector3.zero);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? 1 : -1;
                Part(RB(0.075f, 0.12f, 0.04f, 0.03f), black, new Vector3(s * 0.14f, 0.26f, 0.3f), face, false);
                Part(MeshKit.Box(0.025f, 0.03f, 0.01f), eyeHi, new Vector3(s * 0.14f - 0.015f, 0.29f, 0.322f), face, false);
                Part(RB(0.13f, 0.035f, 0.03f, 0.012f), hairM, new Vector3(s * 0.15f, 0.36f, 0.3f), face, false);
                var ch = Part(MeshKit.Cylinder(0.05f, 0.004f, 14), cheek, new Vector3(s * 0.22f, 0.17f, 0.302f), face, false);
                ch.localRotation = Quaternion.Euler(90, 0, 0);
            }
            Part(RB(0.07f, 0.06f, 0.05f, 0.02f), skinM, new Vector3(0, 0.2f, 0.31f), face, false);
            Part(RB(0.1f, 0.025f, 0.02f, 0.01f), Mats.Get("#7a3b2e"), new Vector3(0, 0.12f, 0.3f), face, false);
            // волосы из-под шлема
            Part(RB(0.6f, 0.2f, 0.12f, 0.05f), hairM, new Vector3(0, 0.32f, -0.27f), c.Head);
            for (int i = 0; i < 2; i++) Part(RB(0.08f, 0.16f, 0.14f, 0.03f), hairM, new Vector3((i == 0 ? 1 : -1) * 0.32f, 0.36f, 0.05f), c.Head);
            Part(RB(0.46f, 0.07f, 0.06f, 0.025f), hairM, new Vector3(0.04f, 0.415f, 0.29f), c.Head);

            if (goalie)
            {
                Part(RB(0.72f, 0.66f, 0.66f, 0.2f), white, new Vector3(0, 0.32f, -0.02f), c.Head);
                Part(RB(0.2f, 0.67f, 0.67f, 0.06f), main, new Vector3(0, 0.33f, -0.02f), c.Head);
                var bars = Group("cage", c.Head, new Vector3(0, 0.25f, 0.335f));
                for (int i = -2; i <= 2; i++) Part(MeshKit.Box(0.018f, 0.36f, 0.018f), cage, new Vector3(i * 0.07f, 0, 0), bars, false);
                for (int i = -1; i <= 1; i++) Part(MeshKit.Box(0.34f, 0.018f, 0.018f), cage, new Vector3(0, i * 0.1f, 0), bars, false);
                face.localPosition = new Vector3(0, 0, 0.03f);
            }
            else
            {
                Part(RB(0.72f, 0.26f, 0.68f, 0.12f), main, new Vector3(0, 0.56f, -0.03f), c.Head);
                Part(RB(0.74f, 0.04f, 0.7f, 0.02f), dark, new Vector3(0, 0.45f, -0.03f), c.Head);
                for (int i = 0; i < 2; i++) Part(RB(0.08f, 0.2f, 0.22f, 0.04f), main, new Vector3((i == 0 ? 1 : -1) * 0.35f, 0.32f, 0), c.Head);
            }

            // руки
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1 : -1;
                var arm = Group("arm", c.Torso, new Vector3(side * (bw / 2 + 0.06f), 0.38f, 0));
                Part(RB(0.17f, 0.32f, 0.17f, 0.06f), main, new Vector3(0, -0.14f, 0), arm);
                Part(RB(0.175f, 0.05f, 0.175f, 0.02f), white, new Vector3(0, -0.2f, 0), arm);
                if (goalie && i == 0) Part(RB(0.3f, 0.3f, 0.14f, 0.08f), brown, new Vector3(0, -0.34f, 0.02f), arm);
                else if (goalie) Part(RB(0.14f, 0.34f, 0.28f, 0.05f), white, new Vector3(0, -0.34f, 0.03f), arm);
                else Part(RB(0.2f, 0.18f, 0.2f, 0.06f), glove, new Vector3(0, -0.34f, 0), arm);
                c.Arms[i] = arm;
            }

            // клюшка: шарнир у рук, крюк лежит на льду справа-спереди
            var handle = new Vector3(0.12f, 0.2f, 0.28f);
            c.StickPivot = Group("stick", c.Torso, handle);
            float torsoWorldY = c.Hips.localPosition.y + c.Torso.localPosition.y;
            var bladeTarget = new Vector3(-0.3f, 0.03f - torsoWorldY, 0.95f);
            var dir = bladeTarget - handle;
            float len = dir.magnitude;
            var orient = Group("orient", c.StickPivot, Vector3.zero);
            orient.localRotation = Quaternion.FromToRotation(Vector3.down, dir.normalized);
            Part(MeshKit.Box(0.045f, len + 0.25f, 0.045f), wood, new Vector3(0, -len / 2 + 0.12f, 0), orient);
            Part(MeshKit.Box(0.05f, 0.14f, 0.05f), tape, new Vector3(0, 0.18f, 0), orient);
            var bladeHolder = Group("blade", orient, new Vector3(0, -len, 0));
            bladeHolder.localRotation = Quaternion.Inverse(orient.localRotation);
            float bl = goalie ? 0.5f : 0.36f;
            Part(MeshKit.Box(bl, goalie ? 0.12f : 0.08f, 0.035f), tape, new Vector3(-bl / 2 + 0.05f, 0.02f, 0.02f), bladeHolder);

            c.phase = Random.value * 6;
            return c;
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
        static Quaternion Rad(Vector3 r) => Quaternion.Euler(r * Mathf.Rad2Deg);

        /// Процедурная анимация по состоянию игрока.
        public void Animate(PlayerView s, bool hasPuck, bool celebrating, float dt, float time)
        {
            float k = 1 - Mathf.Exp(-dt * 12);
            float spd = s.Spd;
            phase += dt * (2.2f + spd * 0.85f);
            fall = Lerp(fall, s.Stun ? 1 : 0, 1 - Mathf.Exp(-dt * (s.Stun ? 9 : 4)));
            celebrate = Lerp(celebrate, celebrating ? 1 : 0, k);
            float amp = Mathf.Min(1, spd / 7);
            float sinp = Mathf.Sin(phase);

            if (Goalie)
            {
                float sv = Mathf.Min(1, s.Save * 3);
                hipsY = Lerp(hipsY, baseHipY - 0.08f - sv * 0.2f, k);
                legL = Lerp(legL, new Vector3(-0.25f, 0, 0.25f + sv * 1.1f), k);
                legR = Lerp(legR, new Vector3(-0.25f, 0, -0.25f - sv * 1.1f), k);
                torsoRot = Lerp(torsoRot, new Vector3(0.35f, 0, 0), k);
                armL = Lerp(armL, new Vector3(-0.9f - sv * 0.8f, 0, 0.5f + sv * 0.6f), k);
                armR = Lerp(armR, new Vector3(-0.9f, 0, -0.2f), k);
                stickRot = Lerp(stickRot, new Vector3(-0.1f + sv * 0.2f, 0.5f, 0), k);
                Apply(0);
                return;
            }

            float back = s.Back ? -1 : 1;
            float stride = amp * 0.55f;
            legL = Lerp(legL, new Vector3(sinp * stride * 0.6f * back - 0.15f, 0, Mathf.Max(0, sinp) * stride * 0.7f + 0.04f), k);
            legR = Lerp(legR, new Vector3(-sinp * stride * 0.6f * back - 0.15f, 0, -Mathf.Max(0, -sinp) * stride * 0.7f - 0.04f), k);
            hipsY = Lerp(hipsY, baseHipY - 0.03f - amp * 0.04f + Mathf.Abs(sinp) * 0.025f * amp + celebrate * Mathf.Max(0, Mathf.Sin(time * 9)) * 0.25f, k);
            hipsYaw = Lerp(hipsYaw, sinp * 0.12f * amp, k);

            float lean = 0.18f + amp * 0.22f + s.Charge * 0.15f;
            torsoRot = Lerp(torsoRot, new Vector3(lean - celebrate * 0.3f, -s.Charge * 0.5f + (s.Swing > 0 ? 0.4f : 0) - sinp * 0.08f * amp, 0), k);

            armL = Lerp(armL, new Vector3(Lerp(-1.05f, -2.9f, celebrate), 0, Lerp(-0.45f, 0.2f, celebrate)), k);
            armR = Lerp(armR, new Vector3(Lerp(-0.85f, -2.9f, celebrate), 0, Lerp(0.1f, -0.2f, celebrate)), k);

            float tx = 0, ty = 0, tz = 0;
            if (s.Charge > 0) { tx = s.Charge * 1.3f; ty = -s.Charge * 0.6f; tz = -s.Charge * 0.3f; }
            if (s.Swing > 0) { tx = -0.55f; ty = 0.5f; }
            if (hasPuck && s.Charge <= 0 && s.Swing <= 0) ty = Mathf.Sin(time * 9) * 0.15f;
            if (celebrate > 0.5f) tx = -1.6f;
            float kk = s.Swing > 0 ? 1 - Mathf.Exp(-dt * 40) : k;
            sx = Lerp(sx, tx, kk);
            stickRot = new Vector3(sx - torsoRot.x * 0.9f, Lerp(stickRot.y, ty, kk), Lerp(stickRot.z, tz, kk));
            Apply(fall);
        }

        void Apply(float fallAmt)
        {
            Legs[0].localRotation = Rad(legL); Legs[1].localRotation = Rad(legR);
            Arms[0].localRotation = Rad(armL); Arms[1].localRotation = Rad(armR);
            Torso.localRotation = Rad(torsoRot);
            StickPivot.localRotation = Rad(stickRot);
            Hips.localPosition = new Vector3(0, hipsY, 0);
            Hips.localRotation = Quaternion.Euler(0, hipsYaw * Mathf.Rad2Deg, 0);
            // падение после силового приёма
            Tilt.localRotation = Quaternion.Euler(-fallAmt * 1.45f * Mathf.Rad2Deg, 0, 0);
            Tilt.localPosition = new Vector3(0, fallAmt * 0.25f, -fallAmt * 0.35f);
        }
    }
}
