// Сетка ворот: ромбическое плетение с узелками и «живая» физика — сетка прогибается, когда в неё влетает шайба,
// и пружинисто колышется обратно. Панели — сетки вершин, края закреплены на раме.
using System.Collections.Generic;
using UnityEngine;

namespace Hockey
{
    public class GoalNet
    {
        readonly Transform tr;
        readonly Mesh mesh;
        readonly Vector3[] rest, normal, verts;   // verts: лицевые + обратные копии
        readonly Vector3[] disp, vel;
        readonly bool[] pinned;
        readonly int[][] nb;
        readonly int n;
        readonly float W, H, D;
        float energy;
        Vector3 lastPuck; bool hasLast;

        public GoalNet(Transform goal, Material mat, float w, float h, float d)
        {
            W = w; H = h; D = d;
            var pos = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>();
            var pin = new List<bool>(); var tris = new List<int>();
            var neigh = new List<List<int>>();
            void Panel(Vector3 a, Vector3 b, Vector3 c, Vector3 dd, Vector3 outN, int nu, int nv)
            {
                int start = pos.Count;
                float lu = (b - a).magnitude, lv = (dd - a).magnitude;
                for (int j = 0; j <= nv; j++)
                    for (int i = 0; i <= nu; i++)
                    {
                        float u = i / (float)nu, v = j / (float)nv;
                        var p = Vector3.Lerp(Vector3.Lerp(a, b, u), Vector3.Lerp(dd, c, u), v);
                        pos.Add(p); nrm.Add(outN.normalized);
                        uv.Add(new Vector2(u * lu / 0.26f, v * lv / 0.26f));
                        pin.Add(i == 0 || j == 0 || i == nu || j == nv);
                        neigh.Add(new List<int>());
                    }
                for (int j = 0; j <= nv; j++)
                    for (int i = 0; i <= nu; i++)
                    {
                        int k = start + j * (nu + 1) + i;
                        if (i > 0) neigh[k].Add(k - 1);
                        if (i < nu) neigh[k].Add(k + 1);
                        if (j > 0) neigh[k].Add(k - (nu + 1));
                        if (j < nv) neigh[k].Add(k + (nu + 1));
                        if (i < nu && j < nv)
                        {
                            int k1 = k + 1, k2 = k + nu + 1, k3 = k2 + 1;
                            tris.Add(k); tris.Add(k2); tris.Add(k1);
                            tris.Add(k1); tris.Add(k2); tris.Add(k3);
                        }
                    }
            }
            Panel(new Vector3(0, H, -W / 2), new Vector3(D * 0.6f, H, -W / 2), new Vector3(D * 0.6f, H, W / 2), new Vector3(0, H, W / 2), Vector3.up, 4, 14);
            Panel(new Vector3(D * 0.6f, H, -W / 2), new Vector3(D, 0, -W / 2), new Vector3(D, 0, W / 2), new Vector3(D * 0.6f, H, W / 2), new Vector3(H, D * 0.4f, 0), 7, 14);
            foreach (float z in new[] { -W / 2, W / 2 })
                Panel(new Vector3(0, 0, z), new Vector3(D, 0, z), new Vector3(D * 0.6f, H, z), new Vector3(0, H, z), new Vector3(0, 0, Mathf.Sign(z)), 6, 7);

            n = pos.Count;
            rest = pos.ToArray(); normal = nrm.ToArray(); pinned = pin.ToArray();
            nb = new int[n][];
            for (int i = 0; i < n; i++) nb[i] = neigh[i].ToArray();
            disp = new Vector3[n]; vel = new Vector3[n];
            // двусторонняя сетка: вторая копия вершин с обратными нормалями и обратным обходом
            verts = new Vector3[n * 2];
            var allN = new Vector3[n * 2]; var allUv = new Vector2[n * 2];
            for (int i = 0; i < n; i++) { verts[i] = verts[i + n] = rest[i]; allN[i] = normal[i]; allN[i + n] = -normal[i]; allUv[i] = allUv[i + n] = uv[i]; }
            var all = new List<int>(tris);
            for (int t = 0; t < tris.Count; t += 3) { all.Add(tris[t] + n); all.Add(tris[t + 2] + n); all.Add(tris[t + 1] + n); }
            mesh = new Mesh { name = "goal-net" };
            mesh.MarkDynamic();
            mesh.vertices = verts; mesh.normals = allN; mesh.uv = allUv;
            mesh.SetTriangles(all, 0);
            mesh.RecalculateBounds();
            mesh.bounds = new Bounds(new Vector3(D / 2, H / 2, 0), new Vector3(D + 1.2f, H + 1.2f, W + 1.2f));

            var go = new GameObject("Net");
            go.transform.SetParent(goal, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            tr = goal;
        }

        /// Шайба (мировые координаты): если она в воротах и давит на сетку — сетка прогибается.
        public void Update(Vector3 puckWorld, float dt)
        {
            if (dt <= 0) return;
            var p = tr.InverseTransformPoint(puckWorld);
            var pv = hasLast ? (p - lastPuck) / dt : Vector3.zero;
            if (hasLast && pv.magnitude > 60) pv = Vector3.zero;   // телепорт (вбрасывание)
            lastPuck = p; hasLast = true;

            bool inside = p.x > -0.1f && p.x < D + 0.25f && Mathf.Abs(p.z) < W / 2 + 0.25f && p.y < H + 0.25f;
            if (inside)
            {
                for (int i = 0; i < n; i++)
                {
                    if (pinned[i]) continue;
                    var cur = rest[i] + disp[i];
                    float r = (cur - p).magnitude;
                    if (r > 0.55f) continue;
                    float fall = 1 - r / 0.55f; fall *= fall;
                    float push = Vector3.Dot(pv, normal[i]);
                    // шайба продавливает сетку: толкаем узлы наружу пропорционально скорости и близости
                    if (push > 0) vel[i] += normal[i] * push * fall * 0.55f;
                    float pen = Vector3.Dot(p - cur, normal[i]) + 0.1f;
                    if (pen > 0) disp[i] += normal[i] * pen * fall * 0.6f;
                    energy = 1;
                }
            }
            if (energy < 0.002f) return;

            // пружины к раме + натяжение между соседями + затухание
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / (1f / 90)), 1, 4);
            float h = dt / steps;
            float e = 0;
            for (int s = 0; s < steps; s++)
            {
                for (int i = 0; i < n; i++)
                {
                    if (pinned[i]) { disp[i] = vel[i] = Vector3.zero; continue; }
                    var avg = Vector3.zero;
                    foreach (int j in nb[i]) avg += disp[j];
                    avg /= nb[i].Length;
                    var acc = -disp[i] * 45f + (avg - disp[i]) * 260f - vel[i] * 5.5f;
                    vel[i] += acc * h;
                }
                for (int i = 0; i < n; i++)
                {
                    disp[i] += vel[i] * h;
                    if (disp[i].sqrMagnitude > 0.16f) disp[i] = disp[i].normalized * 0.4f;
                }
            }
            for (int i = 0; i < n; i++)
            {
                e += disp[i].sqrMagnitude + vel[i].sqrMagnitude * 0.01f;
                verts[i] = verts[i + n] = rest[i] + disp[i];
            }
            energy = e;
            mesh.vertices = verts;
        }

        /// Плетение сетки: ромбы из шнура с объёмом (светлая середина, тень по краю) и узелки на пересечениях.
        /// Мип-уровни рисуются отдельно с той же толщиной в пикселях — издали сетка не исчезает.
        public static Texture2D Texture()
        {
            const int S = 256;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            int levels = t.mipmapCount;
            for (int m = 0; m < levels; m++)
            {
                int s = Mathf.Max(1, S >> m);
                var px = new Color[s * s];
                float width = Mathf.Max(0.9f, 5.5f / Mathf.Pow(2, m * 0.75f));   // полутолщина шнура в пикселях этого уровня
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                        // две диагонали, по 2 ромба на плитку
                        float d1 = Mathf.Abs(Mathf.Repeat((u + v) * 2 + 0.5f, 1) - 0.5f) / 2 / 1.4142f * s;
                        float d2 = Mathf.Abs(Mathf.Repeat((u - v) * 2 + 0.5f, 1) - 0.5f) / 2 / 1.4142f * s;
                        float d = Mathf.Min(d1, d2);
                        float knot = Mathf.Sqrt(d1 * d1 + d2 * d2);
                        float a = Mathf.Clamp01(width - d + 0.5f);
                        a = Mathf.Max(a, Mathf.Clamp01(width * 1.7f - knot + 0.5f));
                        float shade = Mathf.Clamp01(1 - Mathf.Min(d, knot * 0.6f) / (width + 0.01f));
                        var c = Color.Lerp(new Color(0.72f, 0.75f, 0.82f), new Color(1f, 1f, 1f), 0.35f + 0.65f * shade);
                        c.a = a;
                        px[y * s + x] = c;
                    }
                t.SetPixels(px, m);
            }
            t.Apply(false);
            return t;
        }
    }
}
