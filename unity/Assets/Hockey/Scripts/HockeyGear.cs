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

        // ---------------- внешние модели (Resources/Gear/*.fbx) ----------------
        static readonly Dictionary<string, GameObject> extCache = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> extMats = new Dictionary<string, Material>();

        static readonly Dictionary<string, Mesh> bytesCache = new Dictionary<string, Mesh>();

        /// Компактная сетка из Resources/Gear/<name>_mesh.bytes (подготовлена конвертером):
        /// позиции, нормали и категория цвета (0 — основной цвет, 1 — тёмная подкладка, 2 — визор).
        static Mesh LoadBytesMesh(string name, Color tint, int team = -1)
        {
            string key = name + ColorUtility.ToHtmlStringRGB(tint) + team;
            if (bytesCache.TryGetValue(key, out var cached)) return cached;
            Mesh mesh = null;
            var ta = Resources.Load<TextAsset>("Gear/" + name + "_mesh");
            if (ta != null)
            {
                using var br = new System.IO.BinaryReader(new System.IO.MemoryStream(ta.bytes));
                var magic = new string(br.ReadChars(4));
                if (magic == "PKM2")
                {
                    // цвета из текстуры; белое умножается на tint, красное у синей команды становится синим
                    int vc = br.ReadInt32(), tc = br.ReadInt32();
                    var V = new Vector3[vc]; var N = new Vector3[vc]; var C = new Color[vc];
                    Color.RGBToHSV(Mats.Hex(TeamColors.All[1].Main), out float blueH, out _, out _);
                    for (int i = 0; i < vc; i++)
                    {
                        V[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        N[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        var c = new Color32(br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());
                        Color cc = c;
                        Color.RGBToHSV(cc, out float h, out float sat, out float val);
                        bool red = (h < 0.05f || h > 0.93f) && sat > 0.35f && val > 0.25f;
                        if (red && team == 1) cc = Color.HSVToRGB(blueH, sat, val);
                        else if (!red && sat < 0.2f && val > 0.6f) cc *= tint;   // светлые части — в цвет команды
                        C[i] = cc;
                    }
                    var T = new int[tc * 3];
                    for (int i = 0; i < T.Length; i++) T[i] = br.ReadInt32();
                    mesh = new Mesh { name = name };
                    if (vc > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = V; mesh.normals = N; mesh.colors = C; mesh.triangles = T;
                    mesh.RecalculateBounds();
                }
                else if (magic == "PKM1")
                {
                    int vc = br.ReadInt32(), tc = br.ReadInt32();
                    var V = new Vector3[vc]; var N = new Vector3[vc]; var C = new Color[vc];
                    Color dark = Mats.Hex("#2b2b33"), visor = Mats.Hex("#9fd8ff");
                    for (int i = 0; i < vc; i++)
                    {
                        V[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        N[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        byte cat = br.ReadByte();
                        C[i] = cat == 1 ? dark : cat == 2 ? visor : tint;
                    }
                    var T = new int[tc * 3];
                    for (int i = 0; i < T.Length; i++) T[i] = br.ReadInt32();
                    mesh = new Mesh { name = name };
                    if (vc > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = V; mesh.normals = N; mesh.colors = C; mesh.triangles = T;
                    mesh.RecalculateBounds();
                }
            }
            bytesCache[key] = mesh;
            return mesh;
        }

        static bool HasExt(string name) => LoadExt(name) != null || Resources.Load<TextAsset>("Gear/" + name + "_mesh") != null;

        static GameObject LoadExt(string name)
        {
            if (!extCache.TryGetValue(name, out var go)) { go = Resources.Load<GameObject>("Gear/" + name); extCache[name] = go; }
            return go;
        }

        /// Ставит внешнюю модель name (если она есть) на место вылепленной: та же точка, поворот и габарит.
        /// Иначе — вылепленную. tint — цвет команды (умножается на текстуру модели).
        static Transform Gear(string name, Mesh sculpt, Material mat, Vector3 pos, Quaternion rot, Transform bone, Vector3 scale, Color tint, bool shadow = true, int team = -1)
        {
            var bm = LoadBytesMesh(name, tint, team);
            if (bm != null)
            {
                // сетка из конвертера: подгоняем габарит под вылепленную модель
                var absS = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                var wantS = Vector3.Scale(sculpt.bounds.size, absS);
                var bb = bm.bounds;
                var sb = sculpt.bounds;
                bool headGear = name == "helmet" || name == "goalie_mask";
                bool legGear = name == "pad";
                float kk;
                Vector3 anchorSculpt, anchorModel;
                if (headGear)
                {
                    // по ширине головы с запасом, макушка к макушке
                    kk = wantS.x * (name == "helmet" ? 0.98f : 1.06f) / bb.size.x;
                    anchorSculpt = new Vector3(sb.center.x, sb.max.y, sb.center.z);
                    anchorModel = new Vector3(bb.center.x, bb.max.y, bb.center.z);
                }
                else if (legGear)
                {
                    // по высоте ноги, низ на льду
                    kk = wantS.y / bb.size.y;
                    anchorSculpt = new Vector3(sb.center.x, sb.min.y, sb.center.z);
                    anchorModel = new Vector3(bb.center.x, bb.min.y, bb.center.z);
                }
                else
                {
                    kk = Mathf.Min(wantS.x / bb.size.x, Mathf.Min(wantS.y / bb.size.y, wantS.z / bb.size.z));
                    anchorSculpt = sb.center; anchorModel = bb.center;
                }
                var hold = new GameObject("gear_" + name + "_fit").transform;
                hold.SetPositionAndRotation(pos + rot * Vector3.Scale(anchorSculpt, scale), rot);
                var g = new GameObject("gear_" + name);
                g.transform.SetParent(hold, false);
                g.transform.localScale = new Vector3(Mathf.Sign(scale.x), 1, 1) * kk;
                g.transform.localPosition = -Vector3.Scale(anchorModel, g.transform.localScale) + (headGear ? Vector3.up * wantS.y * 0.04f : Vector3.zero);
                g.AddComponent<MeshFilter>().sharedMesh = bm;
                var mr = g.AddComponent<MeshRenderer>();
                // у моделей из Meshy треугольники не сварены — обводка просвечивает сквозь щели, поэтому без неё
                bool welded = name == "helmet";
                mr.sharedMaterial = welded ? Mats.Outlined(Mats.Get("#ffffff", 0.15f), 0.8f) : Mats.Get("#ffffff", 0.15f);
                mr.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                hold.SetParent(bone, true);
                return hold;
            }
            var prefab = LoadExt(name);
            if (prefab == null) return Piece(sculpt, mat, pos, rot, bone, shadow, scale);
            var go = Object.Instantiate(prefab);
            go.name = "gear_" + name;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            var em = ExtMaterial(name, tint, team);
            var b = new Bounds(); bool first = true;
            foreach (var r in rends)
            {
                var mf = r.GetComponent<MeshFilter>();
                var m = mf != null ? mf.sharedMesh : (r as SkinnedMeshRenderer)?.sharedMesh;
                if (m == null) continue;
                var toRoot = go.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                var mb0 = m.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = mb0.center + Vector3.Scale(mb0.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var p = toRoot.MultiplyPoint3x4(corner);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) mats[k] = em;
                r.sharedMaterials = mats;
                r.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var holder = FitHolder(name, sculpt, pos, rot, scale, b, out var lscale, out var lpos);
            go.transform.SetParent(holder, false);
            go.transform.localScale = lscale;
            go.transform.localPosition = lpos;
            holder.SetParent(bone, true);
            return holder;
        }

        /// Подгонка внешней модели (габарит b) под место вылепленной: голова — по ширине макушкой к макушке, щиток — по высоте от льда.
        static Transform FitHolder(string name, Mesh sculpt, Vector3 pos, Quaternion rot, Vector3 scale, Bounds bb, out Vector3 lscale, out Vector3 lpos)
        {
            var absS = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var wantS = Vector3.Scale(sculpt.bounds.size, absS);
            var sb = sculpt.bounds;
            bool headGear = name == "helmet" || name == "goalie_mask";
            bool legGear = name == "pad";
            float kk; Vector3 anchorSculpt, anchorModel;
            if (headGear) { kk = wantS.x * (name == "helmet" ? 0.98f : 1.06f) / bb.size.x; anchorSculpt = new Vector3(sb.center.x, sb.max.y, sb.center.z); anchorModel = new Vector3(bb.center.x, bb.max.y, bb.center.z); }
            else if (legGear) { kk = wantS.y / bb.size.y; anchorSculpt = new Vector3(sb.center.x, sb.min.y, sb.center.z); anchorModel = new Vector3(bb.center.x, bb.min.y, bb.center.z); }
            else if (name == "glove")
            {
                // ладонь краги (≈62% высоты от раструба) — в центр кулака; ширина — как у вылепленной краги
                kk = wantS.x * 1.05f / bb.size.x;
                anchorSculpt = new Vector3(0, 0.05f, 0);
                anchorModel = new Vector3(bb.center.x, bb.min.y + bb.size.y * 0.62f, bb.center.z);
            }
            else { kk = Mathf.Min(wantS.x / bb.size.x, Mathf.Min(wantS.y / bb.size.y, wantS.z / bb.size.z)); anchorSculpt = sb.center; anchorModel = bb.center; }
            var hold = new GameObject("gear_" + name + "_fit").transform;
            hold.SetPositionAndRotation(pos + rot * Vector3.Scale(anchorSculpt, scale), rot);
            lscale = new Vector3(Mathf.Sign(scale.x), 1, 1) * kk;
            lpos = -Vector3.Scale(anchorModel, lscale) + (headGear ? Vector3.up * wantS.y * 0.04f : Vector3.zero);
            return hold;
        }

        /// Материал внешней модели: её текстура (Resources/Gear/<name>.png), у синей команды красное перекрашено в синий.
        static Material ExtMaterial(string name, Color tint, int team)
        {
            string key = name + ColorUtility.ToHtmlStringRGB(tint) + team;
            if (extMats.TryGetValue(key, out var em)) return em;
            var tex = Resources.Load<Texture2D>("Gear/" + name);
            Texture2D use = tex;
            bool tinted = tint.r < 0.99f || tint.g < 0.99f || tint.b < 0.99f;
            if (tex != null && tex.isReadable && (team == 1 || tinted))
            {
                Color.RGBToHSV(Mats.Hex(TeamColors.All[1].Main), out float blueH, out _, out _);
                var px = tex.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    Color.RGBToHSV(px[i], out float h, out float s, out float v);
                    if (team == 1 && (h < 0.05f || h > 0.93f) && s > 0.35f && v > 0.25f) px[i] = Color.HSVToRGB(blueH, s, v);
                    else if (tinted && s < 0.12f && v > 0.7f) px[i] *= tint;   // в цвет команды — только белые части
                }
                use = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
                use.SetPixels(px); use.Apply(true);
            }
            em = Mats.ToonModel(use, 0.15f);
            em.color = use != tex || tex == null || !tex.isReadable ? Color.white : tint;
            if (tex != null && !tex.isReadable) em.color = tint;
            extMats[key] = em;
            return em;
        }

        static Transform Piece(Mesh mesh, Material mat, Vector3 worldPos, Quaternion worldRot, Transform bone, bool shadow = true, Vector3? scale = null)
        {
            var go = new GameObject("gear");
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            if (scale.HasValue) go.transform.localScale = scale.Value;
            go.AddComponent<MeshFilter>().sharedMesh = GearModels.Sculpted.Contains(mesh) ? mesh : MeshKit.Faceted(mesh);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = shadow ? Mats.Outlined(mat, 0.8f) : mat;
            // мелкие детали (лезвия, ремешки, полоски) тень не отбрасывают — меньше работы для видеокарты
            var sz = Vector3.Scale(mesh.bounds.size, scale ?? Vector3.one);
            bool tiny = Mathf.Max(Mathf.Abs(sz.x), Mathf.Max(Mathf.Abs(sz.y), Mathf.Abs(sz.z))) < 0.08f;
            mr.shadowCastingMode = shadow && !tiny ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
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
                bool extHelmet = HasExt("helmet");
                helmet.Add(Gear("helmet", GearModels.Helmet(Mats.Hex(tc.Main), Mats.Hex(tc.Dark), Mats.Hex("#2b2b33")), vcol, hc, helmRot, head, helmScale, Mats.Hex(tc.Main)));
                // прозрачный визор: генераторы не умеют стекло, поэтому он всегда наш
                helmet.Add(Piece(GearModels.Visor(), visorM, hc + (extHelmet ? fr.Fwd * hs.z * 0.02f - fr.Up * hs.y * 0.13f : Vector3.zero), helmRot, head, false, extHelmet ? helmScale * 0.98f : helmScale));
                // ремешок под подбородком
                helmet.Add(Piece(MeshKit.Superellipsoid(hs.x * 0.47f, hs.y * 0.03f, hs.z * 0.5f, 0.9f, 0.8f, 24, -0.6f, 0.6f), black, hc - fr.Up * hs.y * 0.36f, fr.Rot * Quaternion.Euler(-20, 0, 0), head));
            }
            else
            {
                // маска вратаря: цельная модель с закрытыми щеками и решёткой, вплавленной в рамку
                Gear("goalie_mask", GearModels.GoalieMask(Mats.Hex("#e6e3dc"), Mats.Hex(tc.Main)), vcol, hc, fr.Rot * Quaternion.Euler(-3, 0, 0), head, helmScale, Color.white, true, team);
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
                    if (i == 0) Gear("catcher", GearModels.Catcher(Mats.Hex("#e6e3dc"), Mats.Hex(tc.Main)), Mats.Get("#ffffff", 0.1f), fist + fr.Fwd * fr0 * 0.3f, face, hand, new Vector3(-gsz, gsz, gsz), Color.white, true, team);
                    else Gear("blocker", GearModels.Blocker(Mats.Hex("#e6e3dc"), Mats.Hex(tc.Main)), Mats.Get("#ffffff", 0.1f), fist + fr.Fwd * fr0 * 0.2f, face, hand, new Vector3(gsz, gsz, gsz), Color.white, true, team);
                }
                else
                {
                    // ладонная часть краги (y≈0.05, ширина ≈0.78 в её единицах) охватывает кулак с запасом
                    float gs = Mathf.Max(u * 0.28f, fr0 * 2 * 1.2f / 0.78f);
                    var glove = GearModels.Glove(Mats.Hex(tc.Main), Mats.Hex(tc.Dark), Mats.Hex("#c9a27a"));
                    Gear("glove", glove, Mats.Get("#ffffff", 0.1f), fist - fingerDir * gs * 0.05f, rot, hand, new Vector3(i == 0 ? -gs : gs, gs, gs), Mats.Hex(tc.Main));
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
                    Gear("pad", GearModels.LegPad(Mats.Hex("#e6e3dc"), Mats.Hex(tc.Main)), Mats.Get("#ffffff", 0.1f), padC, fr.Rot, shin,
                        new Vector3((i == 0 ? -1 : 1) * w / 0.7f, h / 1.15f, w / 0.7f * 0.8f), Color.white, true, team);
                }
            }
            return helmet;
        }
    }
}
