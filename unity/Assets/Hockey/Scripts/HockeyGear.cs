// Хоккейная экипировка для моделей KayKit: шлем с визором, краги, коньки, номер на спине;
// у вратаря — маска с решёткой, щитки, блин, ловушка и нагрудник.
// Размеры берутся с самой модели (голова/тело в стойке), детали пристёгиваются к костям.
using System.Collections.Generic;
using UnityEngine;

namespace Hockey
{
    public static class HockeyGear
    {
        struct Frame { public Vector3 Fwd, Right, Up; public Quaternion Rot; }

        static Bounds WorldBounds(SkinnedMeshRenderer smr)
        {
            var baked = new Mesh();
            smr.BakeMesh(baked);
            var v = baked.vertices;
            var m = smr.transform.localToWorldMatrix;
            var b = new Bounds(m.MultiplyPoint3x4(v.Length > 0 ? v[0] : Vector3.zero), Vector3.zero);
            foreach (var p in v) b.Encapsulate(m.MultiplyPoint3x4(p));
            Object.Destroy(baked);
            return b;
        }

        static Transform Piece(Mesh mesh, Material mat, Vector3 worldPos, Quaternion worldRot, Transform bone, bool shadow = true, Vector3? scale = null)
        {
            var go = new GameObject("gear");
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            if (scale.HasValue) go.transform.localScale = scale.Value;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(bone, true);
            return go.transform;
        }

        /// Надевает экипировку. Возвращает детали шлема (их можно сбить шайбой).
        public static List<Transform> Dress(Transform model, Dictionary<string, Transform> bones, Dictionary<string, SkinnedMeshRenderer> parts, int team, int number, bool goalie)
        {
            var helmet = new List<Transform>();
            var tc = TeamColors.All[team];
            var fr = new Frame { Fwd = model.forward, Up = Vector3.up, Right = model.right, Rot = model.rotation };
            if (!parts.TryGetValue("Head", out var headR) || !bones.TryGetValue("head", out var head)) return helmet;
            var hb = WorldBounds(headR);
            var hs = hb.size; var hc = hb.center;
            float u = hs.x; // базовый размер — ширина головы

            Material team1 = Mats.Get(tc.Main, 0.75f), dark = Mats.Get(tc.Dark, 0.55f), white = Mats.Get("#f6f6f2", 0.45f);
            Material black = Mats.Get("#25252d", 0.6f), steel = Mats.Get("#dfe8f2", 1f, 0.8f), visorM = Mats.Fade("#8fd8ff", 0.35f, true);

            // ---------------- голова ----------------
            // вылепленный шлем (поле расстояний): рёбра, вентиляция, ушные раковины, подкладка внутри
            var vcol = Mats.Get("#ffffff", 0.85f);
            var helmRot = fr.Rot * Quaternion.Euler(-6, 0, 0);
            var helmScale = new Vector3(hs.x, hs.y, hs.z) * 1.02f;
            if (!goalie)
            {
                helmet.Add(Piece(GearModels.Helmet(Mats.Hex(tc.Main), Mats.Hex(tc.Dark), Mats.Hex("#2b2b33")), vcol, hc, helmRot, head, true, helmScale));
                helmet.Add(Piece(GearModels.Visor(), visorM, hc, helmRot, head, false, helmScale));
                // ремешок под подбородком
                helmet.Add(Piece(MeshKit.Superellipsoid(hs.x * 0.47f, hs.y * 0.03f, hs.z * 0.5f, 0.9f, 0.8f, 24, -0.6f, 0.6f), black, hc - fr.Up * hs.y * 0.36f, fr.Rot * Quaternion.Euler(-20, 0, 0), head));
            }
            else
            {
                // маска вратаря: белый шлем с полосой цвета команды и решётка перед лицом
                Piece(GearModels.Helmet(Mats.Hex("#f6f6f2"), Mats.Hex(tc.Main), Mats.Hex("#2b2b33")), vcol, hc, helmRot, head, true, helmScale);
                float t = 0.012f * u / 0.6f;
                var front = hc + fr.Fwd * hs.z * 0.56f - fr.Up * hs.y * 0.12f;
                var vbar = MeshKit.Superellipsoid(t, hs.y * 0.26f, t, 1, 1, 8);
                var hbar = MeshKit.Superellipsoid(hs.x * 0.42f, t, t, 1, 1, 8);
                for (int i = -3; i <= 3; i++) Piece(vbar, black, front + fr.Right * i * hs.x * 0.11f - fr.Fwd * (i * i) * hs.z * 0.012f, fr.Rot, head);
                for (int i = -1; i <= 1; i++) Piece(hbar, black, front + fr.Up * i * hs.y * 0.14f, fr.Rot, head);
                // подбородник
                Piece(MeshKit.Superellipsoid(hs.x * 0.36f, hs.y * 0.08f, hs.z * 0.2f, 0.6f, 0.6f, 16), white, front - fr.Up * hs.y * 0.26f - fr.Fwd * hs.z * 0.08f, fr.Rot, head);
            }

            // ---------------- номер на спине ----------------
            if (parts.TryGetValue("Body", out var bodyR) && bones.TryGetValue("chest", out var chest))
            {
                var bb = WorldBounds(bodyR);
                var back = bb.center - fr.Fwd * (bb.extents.z + 0.012f) + fr.Up * bb.size.y * 0.12f;
                var num = Mats.Cutout(Tex.Number(number), Color.white);
                Piece(MeshKit.Plane(u * 0.42f, u * 0.42f), num, back, fr.Rot, chest, false);
                if (goalie)
                    Piece(MeshKit.Superellipsoid(bb.extents.x * 1.12f, bb.extents.y * 0.62f, bb.extents.z * 1.15f, 0.5f, 0.6f, 28), team1, bb.center + fr.Up * bb.size.y * 0.12f, fr.Rot, chest);
                else
                    for (int i = 0; i < 2; i++)
                    {
                        float sd = i == 0 ? 1 : -1;
                        Piece(MeshKit.Superellipsoid(u * 0.2f, u * 0.13f, u * 0.22f, 0.6f, 0.6f, 18), team1, bb.center + fr.Right * sd * bb.extents.x * 0.85f + fr.Up * bb.extents.y * 0.75f, fr.Rot, chest);
                    }
            }

            // ---------------- руки ----------------
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? ".l" : ".r";
                if (!bones.TryGetValue("hand" + side, out var hand)) continue;
                bones.TryGetValue("lowerarm" + side, out var lower);
                var hp = hand.position;
                var toElbow = lower != null ? (lower.position - hp).normalized : Vector3.up;
                var rot = Quaternion.LookRotation(fr.Fwd, -toElbow);
                if (goalie && i == 1)
                    Piece(MeshKit.Superellipsoid(u * 0.08f, u * 0.32f, u * 0.24f, 0.25f, 0.3f, 20), white, hp + fr.Fwd * u * 0.06f, rot, hand);   // блин
                else if (goalie)
                    Piece(MeshKit.Superellipsoid(u * 0.26f, u * 0.26f, u * 0.1f, 0.8f, 0.8f, 24), Mats.Get("#a8693a", 0.35f), hp + fr.Fwd * u * 0.05f, fr.Rot, hand); // ловушка
                else
                {
                    // вылепленная крага: раструб с полосой, валики, пальцы, большой палец, кожаная ладонь
                    // крупная крага целиком закрывает кулак модели
                    float gs = u * 0.42f;
                    var glove = GearModels.Glove(Mats.Hex(tc.Main), Mats.Hex(tc.Dark), Mats.Hex("#c9a27a"));
                    Piece(glove, Mats.Get("#ffffff", 0.55f), hp - toElbow * gs * 0.16f, rot, hand, true, new Vector3(i == 0 ? -gs : gs, gs, gs));
                }
            }

            // ---------------- ноги: коньки и щитки вратаря ----------------
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? ".l" : ".r";
                if (!bones.TryGetValue("foot" + side, out var foot)) continue;
                float groundY = model.position.y;
                var fp = foot.position;
                var bootC = new Vector3(fp.x, groundY + u * 0.2f, fp.z) + fr.Fwd * u * 0.07f;
                Piece(MeshKit.Superellipsoid(u * 0.15f, u * 0.14f, u * 0.25f, 0.45f, 0.5f, 22), black, bootC, fr.Rot, foot);
                Piece(MeshKit.Superellipsoid(u * 0.155f, u * 0.035f, u * 0.255f, 0.4f, 0.5f, 18), white, bootC + fr.Up * u * 0.06f, fr.Rot, foot);
                Piece(MeshKit.Superellipsoid(u * 0.035f, u * 0.05f, u * 0.25f, 0.6f, 0.4f, 14), black, new Vector3(bootC.x, groundY + u * 0.085f, bootC.z), fr.Rot, foot);
                Piece(MeshKit.Superellipsoid(u * 0.012f, u * 0.035f, u * 0.29f, 0.3f, 0.25f, 14), steel, new Vector3(bootC.x, groundY + u * 0.035f, bootC.z), fr.Rot, foot);
                if (goalie && bones.TryGetValue("lowerleg" + side, out var shin))
                {
                    var kp = shin.position;
                    float h = Mathf.Max(u * 0.4f, kp.y - groundY + u * 0.08f);
                    var padC = new Vector3(kp.x, groundY + h * 0.55f, kp.z) + fr.Fwd * u * 0.1f;
                    Piece(MeshKit.Superellipsoid(u * 0.21f, h * 0.55f, u * 0.17f, 0.35f, 0.45f, 24), white, padC, fr.Rot, shin);
                    for (int b = -1; b <= 1; b += 2)
                        Piece(MeshKit.Superellipsoid(u * 0.215f, u * 0.03f, u * 0.175f, 0.3f, 0.45f, 16), team1, padC + fr.Up * b * h * 0.22f, fr.Rot, shin);
                }
            }
            return helmet;
        }
    }
}
