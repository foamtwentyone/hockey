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

        static List<Vector3> BakedVerts(SkinnedMeshRenderer smr)
        {
            var baked = new Mesh();
            smr.BakeMesh(baked);
            var m = smr.transform.localToWorldMatrix;
            var res = new List<Vector3>();
            foreach (var v in baked.vertices) res.Add(m.MultiplyPoint3x4(v));
            Object.Destroy(baked);
            return res;
        }

        /// Центр и радиус кулака по вершинам руки рядом с костью кисти.
        static bool Fist(SkinnedMeshRenderer arm, Vector3 wrist, Vector3 fingerDir, float u, out Vector3 center, out float radius)
        {
            center = wrist; radius = u * 0.15f;
            if (arm == null) return false;
            var sum = Vector3.zero; int n = 0;
            var pts = new List<Vector3>();
            foreach (var v in BakedVerts(arm))
            {
                var d = v - wrist;
                if (d.magnitude < u * 0.35f && Vector3.Dot(d, fingerDir) > -u * 0.02f) { sum += v; n++; pts.Add(v); }
            }
            if (n < 4) return false;
            center = sum / n;
            float r = 0;
            foreach (var v in pts) r = Mathf.Max(r, (v - center).magnitude);
            radius = r;
            return true;
        }

        static Transform Piece(Mesh mesh, Material mat, Vector3 worldPos, Quaternion worldRot, Transform bone, bool shadow = true, Vector3? scale = null)
        {
            var go = new GameObject("gear");
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            if (scale.HasValue) go.transform.localScale = scale.Value;
            go.AddComponent<MeshFilter>().sharedMesh = MeshKit.Faceted(mesh);
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

            Material team1 = Mats.Get(tc.Main, 0.12f), dark = Mats.Get(tc.Dark, 0.1f), white = Mats.Get("#f6f6f2", 0.1f);
            Material black = Mats.Get("#2c2c36", 0.1f), steel = Mats.Get("#dfe8f2", 0.35f), visorM = Mats.Fade("#8fd8ff", 0.35f, true);

            // ---------------- голова ----------------
            // вылепленный шлем (поле расстояний): рёбра, вентиляция, ушные раковины, подкладка внутри
            var vcol = Mats.Get("#ffffff", 0.1f);
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
                // маска вратаря: цельная модель с закрытыми щеками и решёткой, вплавленной в рамку
                Piece(GearModels.GoalieMask(Mats.Hex("#f6f6f2"), Mats.Hex(tc.Main)), vcol, hc, fr.Rot * Quaternion.Euler(-3, 0, 0), head, true, helmScale);
            }

            // ---------------- номер на спине ----------------
            if (parts.TryGetValue("Body", out var bodyR) && bones.TryGetValue("chest", out var chest))
            {
                var bb = WorldBounds(bodyR);
                var back = bb.center - fr.Fwd * (bb.extents.z + 0.012f) + fr.Up * bb.size.y * 0.12f;
                var num = Mats.Cutout(Tex.Number(number), Color.white);
                Piece(MeshKit.Plane(u * 0.42f, u * 0.42f), num, back, fr.Rot, chest, false);
                if (!goalie)
                    for (int i = 0; i < 2; i++)
                    {
                        float sd = i == 0 ? 1 : -1;
                        Piece(MeshKit.Superellipsoid(u * 0.2f, u * 0.13f, u * 0.22f, 0.6f, 0.6f, 18), team1, bb.center + fr.Right * sd * bb.extents.x * 0.85f + fr.Up * bb.extents.y * 0.75f, fr.Rot, chest);
                    }
            }

            // ---------------- руки: краги надеваются на кулак модели ----------------
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? ".l" : ".r";
                if (!bones.TryGetValue("hand" + side, out var hand)) continue;
                bones.TryGetValue("lowerarm" + side, out var lower);
                parts.TryGetValue(i == 0 ? "ArmLeft" : "ArmRight", out var armR);
                var wrist = hand.position;
                var fingerDir = lower != null ? (wrist - lower.position).normalized : Vector3.down;
                Fist(armR, wrist, fingerDir, u, out var fist, out float fr0);
                var fwdP = Vector3.ProjectOnPlane(fr.Fwd, fingerDir);
                if (fwdP.sqrMagnitude < 1e-4f) fwdP = Vector3.ProjectOnPlane(fr.Right, fingerDir);
                var rot = Quaternion.LookRotation(fwdP.normalized, fingerDir);
                if (goalie)
                {
                    // ловушка — на левой, блин — на правой; лицевой стороной к шайбе
                    var face = Quaternion.LookRotation(fr.Fwd, Vector3.ProjectOnPlane(fingerDir, fr.Fwd).normalized);
                    float gsz = Mathf.Max(u * 0.55f, fr0 * 2 * 2.8f);
                    if (i == 0) Piece(GearModels.Catcher(Mats.Hex("#f6f6f2"), Mats.Hex(tc.Main)), Mats.Get("#ffffff", 0.1f), fist + fr.Fwd * fr0 * 0.3f, face, hand, true, new Vector3(-gsz, gsz, gsz));
                    else Piece(GearModels.Blocker(Mats.Hex("#f6f6f2"), Mats.Hex(tc.Main)), Mats.Get("#ffffff", 0.1f), fist + fr.Fwd * fr0 * 0.2f, face, hand, true, new Vector3(gsz, gsz, gsz));
                }
                else
                {
                    // ладонная часть краги (y≈0.05, ширина ≈0.78 в её единицах) охватывает кулак с запасом
                    float gs = Mathf.Max(u * 0.28f, fr0 * 2 * 1.2f / 0.78f);
                    var glove = GearModels.Glove(Mats.Hex(tc.Main), Mats.Hex(tc.Dark), Mats.Hex("#c9a27a"));
                    Piece(glove, Mats.Get("#ffffff", 0.1f), fist - fingerDir * gs * 0.05f, rot, hand, true, new Vector3(i == 0 ? -gs : gs, gs, gs));
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
                    // щиток: от льда до колена и чуть выше, по ширине ноги
                    float kneeY = shin.position.y + u * 0.08f;
                    var lb = new Bounds(new Vector3(fp.x, groundY, fp.z), Vector3.zero);
                    if (parts.TryGetValue(i == 0 ? "LegLeft" : "LegRight", out var legR))
                        foreach (var v in BakedVerts(legR)) if (v.y < kneeY) lb.Encapsulate(v);
                    float h = Mathf.Max(u * 0.45f, kneeY - groundY + u * 0.12f);
                    float w = Mathf.Max(lb.size.x, lb.size.z) * 1.35f;
                    var padC = new Vector3(lb.center.x, groundY + h * 0.5f, lb.center.z) + fr.Fwd * w * 0.25f;
                    Piece(GearModels.LegPad(Mats.Hex("#f6f6f2"), Mats.Hex(tc.Main)), Mats.Get("#ffffff", 0.1f), padC, fr.Rot, shin, true,
                        new Vector3((i == 0 ? -1 : 1) * w / 0.7f, h / 1.15f, w / 0.7f * 0.8f));
                }
            }
            return helmet;
        }
    }
}
