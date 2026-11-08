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

        readonly ParticleSystem confetti, chips, sparks;
        readonly Transform root;
        readonly List<GameObject>[] itemPool = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };

        public Fx(Transform parent)
        {
            root = new GameObject("Fx").transform;
            root.SetParent(parent, false);
            confetti = Make("Confetti", 1500, 0.6f, true);
            chips = Make("IceChips", 800, 1.4f, false);
            sparks = Make("Sparks", 600, -0.1f, true);
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
