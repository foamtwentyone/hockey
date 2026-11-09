// Эффекты: конфетти, ледяная крошка из-под коньков, искры бонусов, снежинки заморозки,
// а также парящие бонусы на льду (режим «Аркада»).
using System.Collections.Generic;
using UnityEngine;

namespace Hockey
{
    public class Fx
    {
        public static readonly string[] ItemColors = { "#36d6ff", "#ff6a2e", "#a06bff", "#ff3b6b", "#a8f0ff" };
        public static readonly string[] ItemNames = { "Турбо", "Мега-бросок", "Великан", "Магнит", "Заморозка" };
        public static readonly string[] ItemHints = { "быстрее катаешься", "следующий бросок — ракета", "большой и непробиваемый", "шайба липнет к клюшке", "соперники вмёрзли в лёд" };

        readonly ParticleSystem confetti, chips, sparks, puffs, streaks;
        public Camera FacingCam;   // к ней поворачиваются «ПОВ!»-звёзды
        class Burst { public Transform T; public float Age, Life, Size; public bool Ring; public Material Mat; public Color Col; }
        readonly List<Burst> bursts = new List<Burst>();
        readonly List<(float t, Vector3 pos, Color col)> fireworks = new List<(float, Vector3, Color)>();
        readonly Dictionary<int, Transform> clouds = new Dictionary<int, Transform>();
        float time;
        class Flyer { public Transform T; public Vector3 V, W; public float Age, Floor; }
        readonly List<Flyer> flyers = new List<Flyer>();

        /// Слетевший шлем: копия деталей летит, кувыркается и отскакивает от льда.
        public void FlyingHelmet(List<Transform> parts, Vector3 vel)
        {
            if (parts == null || parts.Count == 0) return;
            var center = Vector3.zero; int n = 0;
            foreach (var p in parts) if (p != null) { center += p.position; n++; }
            if (n == 0) return;
            center /= n;
            var root = new GameObject("FlyingHelmet").transform;
            root.SetParent(this.root, false);
            root.position = center;
            float minY = 0;
            var pieces = new List<Transform>();
            foreach (var p in parts) if (p != null) foreach (var mfc in p.GetComponentsInChildren<MeshFilter>()) pieces.Add(mfc.transform);
            foreach (var p in pieces)
            {
                var mf = p.GetComponent<MeshFilter>(); var mr = p.GetComponent<MeshRenderer>();
                if (mf == null || mr == null) continue;
                var c = new GameObject("piece");
                c.transform.SetPositionAndRotation(p.position, p.rotation);
                c.transform.localScale = p.lossyScale;
                c.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = c.AddComponent<MeshRenderer>(); r.sharedMaterial = mr.sharedMaterial;
                c.transform.SetParent(root, true);
                minY = Mathf.Min(minY, (p.position - center).y);
            }
            flyers.Add(new Flyer { T = root, V = vel, W = new Vector3(Random.Range(-12f, -6f), Random.Range(-6f, 6f), Random.Range(-8f, 8f)), Floor = -minY + 0.05f });
        }
        readonly Transform root;
        readonly List<GameObject>[] itemPool = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };

        public Fx(Transform parent)
        {
            root = new GameObject("Fx").transform;
            root.SetParent(parent, false);
            confetti = Make("Confetti", 1500, 0.6f, true);
            chips = Make("IceChips", 800, 1.4f, false);
            sparks = Make("Sparks", 600, -0.1f, true);
            puffs = Make("Puffs", 500, -0.05f, false);
            var pr = puffs.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = MeshKit.Sphere(0.5f, 12, 0.25f);
            var psz = puffs.sizeOverLifetime;
            psz.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1, 0)));
            streaks = Make("Streaks", 300, 0, false);
            var sr = streaks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.velocityScale = 0.08f; sr.lengthScale = 1.5f;
            sr.sharedMaterial = Mats.Fade("#ffffff", 0.5f, true);
        }

        // ---------------- новые эффекты ----------------
        /// Мультяшные клубы пыли/снега (объёмные шарики).
        public void Puff(Vector3 pos, int n, Color c, float spread = 0.6f, float speed = 1.5f, float size = 0.5f)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = pos + Random.insideUnitSphere * spread;
                var d = Random.insideUnitSphere; d.y = Mathf.Abs(d.y) * 0.6f + 0.2f;
                ep.velocity = d * speed;
                ep.startColor = Color.Lerp(c, Color.white, Random.value * 0.3f);
                ep.startSize = size * Random.Range(0.6f, 1.3f);
                ep.startLifetime = Random.Range(0.45f, 0.9f);
                puffs.Emit(ep, 1);
            }
        }

        /// Линии ветра вокруг быстрого игрока.
        public void Streak(Vector3 pos, Vector3 vel)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = pos + Random.insideUnitSphere * 0.9f + Vector3.up * 0.8f,
                velocity = -vel * 0.6f + Vector3.up * 0.1f,
                startColor = new Color(1, 1, 1, 0.8f),
                startSize = 0.05f,
                startLifetime = 0.35f,
            };
            streaks.Emit(ep, 1);
        }

        static readonly Dictionary<string, Material> burstMats = new Dictionary<string, Material>();
        Material BurstMat(Color c, bool ring)
        {
            string key = ColorUtility.ToHtmlStringRGB(c) + ring;
            if (!burstMats.TryGetValue(key, out var m))
            {
                m = new Material(Mats.Fade("#" + ColorUtility.ToHtmlStringRGB(c), 0.9f, true));
                burstMats[key] = m;
            }
            return m;
        }

        Burst Spawn(Mesh mesh, Color c, Vector3 pos, float size, float life, bool ring)
        {
            var go = new GameObject(ring ? "Shockwave" : "Pow");
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(BurstMat(c, ring));
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var b = new Burst { T = go.transform, Life = life, Size = size, Ring = ring, Mat = mat, Col = c };
            bursts.Add(b);
            return b;
        }

        /// Ударная волна по льду.
        public void Shockwave(Vector3 pos, Color c, float size = 2.5f) => Spawn(MeshKit.FlatRing(0.8f, 1f, 48), c, new Vector3(pos.x, 0.03f, pos.z), size, 0.45f, true);

        /// Комикс-вспышка «ПОВ!»: жёлтая звезда с белой серединой.
        public void Pow(Vector3 pos, float size = 0.9f)
        {
            Spawn(MeshKit.Star(1f, 0.5f, 8), Mats.Hex("#ffd23f"), pos, size, 0.35f, false);
            Spawn(MeshKit.Star(0.6f, 0.55f, 8), Color.white, pos + Vector3.up * 0.01f, size, 0.3f, false);
        }

        public void Fireworks(Vector3 center, int count)
        {
            for (int i = 0; i < count; i++)
                fireworks.Add((time + 0.25f + i * 0.28f, center + new Vector3(Random.Range(-12f, 12f), Random.Range(7f, 12f), Random.Range(-8f, 8f)), ConfettiColors[Random.Range(0, ConfettiColors.Length)]));
        }

        /// Облако драки над дерущимися: пыль, мелькающие кулаки и звёзды.
        public void BrawlCloud(int id, Vector3 center, bool active, float dt)
        {
            if (!active) { if (clouds.TryGetValue(id, out var old)) { Object.Destroy(old.gameObject); clouds.Remove(id); } return; }
            if (!clouds.TryGetValue(id, out var cloud))
            {
                cloud = new GameObject("BrawlCloud").transform;
                cloud.SetParent(root, false);
                var mat = Mats.Get("#f3f1ec", 0.05f);
                for (int i = 0; i < 9; i++)
                {
                    var p = new GameObject("puff");
                    p.transform.SetParent(cloud, false);
                    p.AddComponent<MeshFilter>().sharedMesh = MeshKit.Sphere(0.5f, 14, 0.3f);
                    p.AddComponent<MeshRenderer>().sharedMaterial = mat;
                }
                var fist = Mats.Get("#ef5a4c", 0.3f); var fist2 = Mats.Get("#4a8ff0", 0.3f);
                for (int i = 0; i < 4; i++)
                {
                    var f = new GameObject("fist");
                    f.transform.SetParent(cloud, false);
                    f.AddComponent<MeshFilter>().sharedMesh = MeshKit.Sphere(0.16f, 10, 0.1f);
                    f.AddComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? fist : fist2;
                }
                clouds[id] = cloud;
            }
            cloud.position = center + Vector3.up * 0.9f;
            for (int i = 0; i < cloud.childCount; i++)
            {
                var c = cloud.GetChild(i);
                float a = time * (i < 9 ? 6 : 13) + i * 1.9f;
                if (i < 9)
                {
                    c.localPosition = new Vector3(Mathf.Sin(a) * 0.55f, Mathf.Sin(a * 1.7f) * 0.35f, Mathf.Cos(a * 1.3f) * 0.55f);
                    float sc = 1.1f + Mathf.Sin(a * 2.3f) * 0.25f;
                    c.localScale = new Vector3(sc, sc, sc);
                }
                else
                {
                    float pop = Mathf.Max(0, Mathf.Sin(a));
                    c.localPosition = new Vector3(Mathf.Sin(a * 0.7f + i) * (0.6f + pop * 0.6f), Mathf.Cos(a * 0.9f) * 0.5f, Mathf.Cos(a * 0.7f + i) * (0.6f + pop * 0.6f));
                    c.gameObject.SetActive(pop > 0.2f);
                }
            }
            if (Random.value < dt * 5) Pow(center + Vector3.up * 1f + Random.insideUnitSphere * 0.8f, 0.55f);
            if (Random.value < dt * 10) Puff(center + Vector3.up * 0.3f, 1, Color.white, 0.6f, 2f, 0.4f);
        }

        public void Update(float dt)
        {
            time += dt;
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var b = bursts[i];
                b.Age += dt;
                float k = b.Age / b.Life;
                if (k >= 1) { Object.Destroy(b.T.gameObject); Object.Destroy(b.Mat); bursts.RemoveAt(i); continue; }
                float sc = b.Ring ? Mathf.Lerp(0.3f, 1, Mathf.Sqrt(k)) * b.Size : b.Size * (k < 0.3f ? Mathf.Lerp(0.2f, 1.15f, k / 0.3f) : Mathf.Lerp(1.15f, 0.9f, (k - 0.3f) / 0.7f));
                b.T.localScale = new Vector3(sc, sc, sc);
                if (b.Ring) b.T.rotation = Quaternion.identity;
                else if (FacingCam != null) b.T.rotation = Quaternion.LookRotation(b.T.position - FacingCam.transform.position) * Quaternion.Euler(0, 0, b.Age * 200);
                var c = b.Col; c.a = Mathf.Clamp01((1 - k) * 1.6f) * (b.Ring ? 0.7f : 1);
                b.Mat.color = c;
            }
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                f.Age += dt;
                if (f.Age > 9 || f.T == null) { if (f.T != null) Object.Destroy(f.T.gameObject); flyers.RemoveAt(i); continue; }
                f.V += Vector3.down * 14 * dt;
                var pos = f.T.position + f.V * dt;
                if (pos.y < f.Floor)
                {
                    pos.y = f.Floor;
                    if (f.V.y < -1.5f) { f.V.y = -f.V.y * 0.45f; f.W *= 0.6f; Puff(pos, 2, Color.white, 0.2f, 1f, 0.25f); }
                    else f.V.y = 0;
                    f.V.x *= 1 - dt * 1.5f; f.V.z *= 1 - dt * 1.5f;
                    f.W *= 1 - dt * 2.5f;
                }
                // не вылетать за борта
                if (Rink.Clamp(pos.x, pos.z, 0.3f, out var hit)) { pos.x = hit.X; pos.z = hit.Z; var nrm = new Vector3(hit.Nx, 0, hit.Nz); f.V -= 1.6f * Vector3.Dot(f.V, nrm) * nrm; }
                f.T.position = pos;
                f.T.rotation = Quaternion.Euler(f.W * dt * Mathf.Rad2Deg) * f.T.rotation;
                if (f.Age > 8) f.T.localScale = Vector3.one * (9 - f.Age);
            }
            for (int i = fireworks.Count - 1; i >= 0; i--)
            {
                if (fireworks[i].t > time) continue;
                var f = fireworks[i];
                fireworks.RemoveAt(i);
                Sparks(f.pos, f.col, 70, 7);
                Sparks(f.pos, Color.white, 20, 4);
            }
        }

        ParticleSystem Make(string name, int max, float gravity, bool spin)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.75f, 0.9f), new Keyframe(1, 0)));
            if (spin)
            {
                var rol = ps.rotationOverLifetime; rol.enabled = true;
                rol.z = new ParticleSystem.MinMaxCurve(-8, 8);
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            var mat = Mats.Get("#ffffff", 0.1f);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static readonly Color[] ConfettiColors =
        {
            Mats.Hex("#ff5a4c"), Mats.Hex("#4a8ff0"), Mats.Hex("#ffd23f"), Mats.Hex("#2fd17f"), Mats.Hex("#ff7ad9"), Mats.Hex("#ffffff"), Mats.Hex("#9b6bff"),
        };

        public void Confetti(Vector3 pos, int n, Color? tint = null)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.8f;
                var dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) * 2 + 0.6f;
                ep.velocity = dir.normalized * Random.Range(5f, 12f);
                var c = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
                if (tint.HasValue && Random.value < 0.5f) c = tint.Value;
                ep.startColor = c;
                ep.startSize = Random.Range(0.12f, 0.24f);
                ep.startLifetime = Random.Range(2.5f, 4.5f);
                ep.rotation = Random.Range(0, 360);
                confetti.Emit(ep, 1);
            }
        }

        /// Дождь конфетти с потолка над точкой.
        public void ConfettiRain(Vector3 center, int n)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = center + new Vector3(Random.Range(-14f, 14f), Random.Range(8f, 13f), Random.Range(-10f, 10f));
                ep.velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(-2f, 0f), Random.Range(-1f, 1f));
                ep.startColor = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
                ep.startSize = Random.Range(0.14f, 0.26f);
                ep.startLifetime = Random.Range(4f, 6f);
                ep.rotation = Random.Range(0, 360);
                confetti.Emit(ep, 1);
            }
        }

        public void Chips(Vector3 pos, Vector3 dir, int n, float speed = 4)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.15f;
                var d = dir + Random.insideUnitSphere * 0.6f; d.y = Mathf.Abs(d.y) + 0.5f;
                ep.velocity = d.normalized * speed * Random.Range(0.5f, 1.2f);
                float w = Random.Range(0.85f, 1f);
                ep.startColor = new Color(w * 0.92f, w * 0.97f, w, 1);
                ep.startSize = Random.Range(0.05f, 0.12f);
                ep.startLifetime = Random.Range(0.35f, 0.8f);
                chips.Emit(ep, 1);
            }
        }

        public void Sparks(Vector3 pos, Color c, int n, float speed = 4)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.3f;
                ep.velocity = Random.onUnitSphere * speed * Random.Range(0.5f, 1.2f) + Vector3.up * 2;
                ep.startColor = Random.value < 0.3f ? Color.white : c;
                ep.startSize = Random.Range(0.08f, 0.2f);
                ep.startLifetime = Random.Range(0.5f, 1.1f);
                ep.rotation = Random.Range(0, 360);
                sparks.Emit(ep, 1);
            }
        }

        // ---------------- бонусы на льду ----------------
        GameObject BuildItem(int type)
        {
            var go = new GameObject("Item" + type);
            go.transform.SetParent(root, false);
            var col = ItemColors[type];
            var glow = Mats.Emissive(col, 0.55f);
            var white = Mats.Emissive("#ffffff", 0.4f);
            var spin = new GameObject("spin").transform;
            spin.SetParent(go.transform, false);
            void P(Mesh m, Material mat, Vector3 pos, Vector3 euler, Vector3 scale)
            {
                var p = new GameObject("p");
                p.transform.SetParent(spin, false);
                p.transform.localPosition = pos; p.transform.localRotation = Quaternion.Euler(euler); p.transform.localScale = scale;
                p.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = p.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            var box = MeshKit.RoundedBox(1, 1, 1, 0.2f, 2, 0.1f);
            switch (type)
            {
                case 0: // молния
                    P(box, glow, new Vector3(0.08f, 0.22f, 0), new Vector3(0, 0, -25), new Vector3(0.16f, 0.4f, 0.14f));
                    P(box, glow, new Vector3(0, 0, 0), new Vector3(0, 0, 65), new Vector3(0.12f, 0.3f, 0.14f));
                    P(box, glow, new Vector3(-0.08f, -0.22f, 0), new Vector3(0, 0, -25), new Vector3(0.16f, 0.4f, 0.14f));
                    break;
                case 1: // огненная шайба
                    P(MeshKit.Cylinder(0.26f, 0.12f, 18), Mats.Get("#1d1d24", 0.6f), Vector3.zero, new Vector3(90, 0, 0), Vector3.one);
                    P(MeshKit.Sphere(0.2f, 12), glow, new Vector3(0, 0.12f, -0.1f), Vector3.zero, new Vector3(1, 1.6f, 1));
                    P(MeshKit.Sphere(0.12f, 10), Mats.Emissive("#ffd23f", 0.8f), new Vector3(0, 0.1f, -0.05f), Vector3.zero, new Vector3(1, 1.4f, 1));
                    break;
                case 2: // стрелка вверх
                    P(box, glow, new Vector3(0, -0.12f, 0), Vector3.zero, new Vector3(0.18f, 0.36f, 0.18f));
                    P(MeshKit.Cone(0.3f, 0.32f, 4), glow, new Vector3(0, 0.2f, 0), new Vector3(180, 45, 0), Vector3.one);
                    break;
                case 3: // магнит
                    P(box, glow, new Vector3(-0.17f, 0.02f, 0), Vector3.zero, new Vector3(0.14f, 0.42f, 0.16f));
                    P(box, glow, new Vector3(0.17f, 0.02f, 0), Vector3.zero, new Vector3(0.14f, 0.42f, 0.16f));
                    P(box, glow, new Vector3(0, -0.2f, 0), Vector3.zero, new Vector3(0.48f, 0.14f, 0.16f));
                    P(box, white, new Vector3(-0.17f, 0.24f, 0), Vector3.zero, new Vector3(0.15f, 0.1f, 0.17f));
                    P(box, white, new Vector3(0.17f, 0.24f, 0), Vector3.zero, new Vector3(0.15f, 0.1f, 0.17f));
                    break;
                default: // снежинка
                    for (int i = 0; i < 3; i++) P(box, glow, Vector3.zero, new Vector3(0, 0, i * 60), new Vector3(0.1f, 0.6f, 0.1f));
                    P(MeshKit.Sphere(0.1f, 10), white, Vector3.zero, Vector3.zero, Vector3.one);
                    break;
            }
            // пузырь и кольцо на льду
            var bubble = new GameObject("bubble");
            bubble.transform.SetParent(spin, false);
            bubble.AddComponent<MeshFilter>().sharedMesh = MeshKit.Sphere(0.48f, 18, 0);
            var bmr = bubble.AddComponent<MeshRenderer>();
            bmr.sharedMaterial = Mats.Fade(col, 0.22f, true);
            bmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var ring = new GameObject("ring");
            ring.transform.SetParent(go.transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = MeshKit.FlatRing(0.55f, 0.75f);
            var rmr = ring.AddComponent<MeshRenderer>();
            rmr.sharedMaterial = Mats.Fade(col, 0.6f, true);
            rmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public void UpdateItems(List<ItemView> items, float time)
        {
            var used = new int[5];
            foreach (var it in items)
            {
                int t = Mathf.Clamp(it.Type, 0, 4);
                var pool = itemPool[t];
                if (used[t] >= pool.Count) pool.Add(BuildItem(t));
                var go = pool[used[t]++];
                go.SetActive(true);
                go.transform.localPosition = new Vector3(it.X, 0, it.Z);
                var spin = go.transform.GetChild(0);
                spin.localPosition = new Vector3(0, 0.9f + Mathf.Sin(time * 3 + it.X) * 0.15f, 0);
                spin.localRotation = Quaternion.Euler(0, time * 120 + it.Z * 10, Mathf.Sin(time * 2) * 10);
                go.transform.GetChild(1).localPosition = new Vector3(0, 0.02f, 0);
                float s = 1 + Mathf.Sin(time * 6) * 0.06f;
                go.transform.GetChild(1).localScale = new Vector3(s, 1, s);
            }
            for (int t = 0; t < 5; t++)
                for (int i = used[t]; i < itemPool[t].Count; i++) itemPool[t][i].SetActive(false);
        }
    }
}
