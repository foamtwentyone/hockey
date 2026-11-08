// Синтезированные звуки — без аудиофайлов.
using System.Collections.Generic;
using UnityEngine;

namespace Hockey
{
    public class GameAudio
    {
        const int Rate = 44100;
        readonly AudioSource[] pool;
        int next;
        readonly AudioSource crowd;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        float crowdTarget = 0.12f, crowdHold;
        public bool Muted;
        static readonly System.Random rnd = new System.Random(1);

        public GameAudio(GameObject host)
        {
            pool = new AudioSource[12];
            for (int i = 0; i < pool.Length; i++) { pool[i] = host.AddComponent<AudioSource>(); pool[i].playOnAwake = false; pool[i].spatialBlend = 0; }
            clips["shot"] = Mix(Noise(0.12f, 0.35f, 0.9f), Tone(180, 0.08f, 0.4f, Wave.Triangle, -80));
            clips["pass"] = Noise(0.07f, 0.4f, 0.8f);
            clips["receive"] = Noise(0.05f, 0.5f, 0.4f);
            clips["swish"] = Noise(0.15f, 0.15f, 0.25f);
            clips["whoosh"] = Noise(0.2f, 0.08f, 0.3f);
            clips["board"] = Noise(0.18f, 0.03f, 1f);
            clips["boardPuck"] = Noise(0.15f, 0.06f, 0.9f);
            clips["post"] = Mix(Tone(1650, 0.5f, 0.35f, Wave.Sine, 0), Tone(2470, 0.4f, 0.15f, Wave.Sine, 0));
            clips["hit"] = Mix(Noise(0.25f, 0.02f, 1f), Tone(90, 0.2f, 0.6f, Wave.Sine, -40));
            clips["body"] = Noise(0.1f, 0.05f, 0.5f);
            clips["save"] = Noise(0.1f, 0.12f, 0.6f);
            clips["steal"] = Noise(0.06f, 0.35f, 0.4f);
            clips["whistle"] = Mix(Tone(2900, 0.35f, 0.25f, Wave.Sine, 120), Tone(2950, 0.35f, 0.08f, Wave.Square, 100));
            clips["horn"] = Mix(Tone(196, 1.6f, 0.3f, Wave.Saw, 0), Tone(247, 1.6f, 0.22f, Wave.Saw, 0));
            clips["goal"] = Mix(Mix(Tone(220, 2.2f, 0.25f, Wave.Saw, 0), Tone(277, 2.2f, 0.2f, Wave.Saw, 0)), Tone(330, 2.2f, 0.2f, Wave.Saw, 0));

            crowd = host.AddComponent<AudioSource>();
            crowd.clip = CrowdLoop();
            crowd.loop = true; crowd.volume = 0.12f; crowd.spatialBlend = 0;
            crowd.Play();
        }

        enum Wave { Sine, Square, Saw, Triangle }

        static AudioClip Make(float[] data)
        {
            var c = AudioClip.Create("sfx", data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// Шум с огибающей; brightness 0..1 — простой фильтр (0 — глухо, 1 — звонко).
        static AudioClip Noise(float dur, float brightness, float vol)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0, a = Mathf.Clamp01(brightness);
            for (int i = 0; i < n; i++)
            {
                float w = (float)rnd.NextDouble() * 2 - 1;
                lp += (w - lp) * a;
                float env = Mathf.Exp(-i / (float)n * 5f);
                d[i] = lp * env * vol * (a < 0.2f ? 2.5f : 1);
            }
            return Make(d);
        }

        static AudioClip Tone(float freq, float dur, float vol, Wave wave, float slide)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = freq + slide * t;
                ph += f / Rate;
                float p = (float)(ph - System.Math.Floor(ph));
                float s = wave switch
                {
                    Wave.Sine => Mathf.Sin(p * Mathf.PI * 2),
                    Wave.Square => p < 0.5f ? 1 : -1,
                    Wave.Saw => p * 2 - 1,
                    _ => 1 - 4 * Mathf.Abs(p - 0.5f),
                };
                float env = Mathf.Min(1, i / (Rate * 0.01f)) * Mathf.Exp(-t * 3);
                d[i] = s * env * vol;
            }
            return Make(d);
        }

        static AudioClip Mix(AudioClip a, AudioClip b)
        {
            var da = new float[a.samples]; a.GetData(da, 0);
            var db = new float[b.samples]; b.GetData(db, 0);
            var o = new float[Mathf.Max(da.Length, db.Length)];
            for (int i = 0; i < o.Length; i++) o[i] = (i < da.Length ? da[i] : 0) + (i < db.Length ? db[i] : 0);
            return Make(o);
        }

        static AudioClip CrowdLoop()
        {
            int n = Rate * 3;
            var d = new float[n];
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float w = (float)rnd.NextDouble() * 2 - 1;
                lp += (w - lp) * 0.12f;
                lp2 += (lp - lp2) * 0.3f;
                d[i] = (lp - lp2) * 2.2f;
            }
            // плавная склейка концов
            int fade = Rate / 10;
            for (int i = 0; i < fade; i++) { float t = i / (float)fade; d[i] = d[i] * t + d[n - fade + i] * (1 - t); }
            System.Array.Resize(ref d, n - fade);
            return Make(d);
        }

        void Play(string key, float vol = 1, float pitch = 1)
        {
            if (Muted || !clips.TryGetValue(key, out var c)) return;
            var s = pool[next++ % pool.Length];
            s.pitch = pitch; s.volume = vol;
            s.PlayOneShot(c);
        }

        void Cheer(float level, float hold) { crowdTarget = Mathf.Max(crowdTarget, level); crowdHold = Mathf.Max(crowdHold, hold); }

        public void Update(float dt)
        {
            crowdHold -= dt;
            if (crowdHold <= 0) crowdTarget = Mathf.MoveTowards(crowdTarget, 0.12f, dt * 0.1f);
            crowd.volume = Muted ? 0 : Mathf.MoveTowards(crowd.volume, crowdTarget, dt * 1.5f);
        }

        public void Event(GameEvent e)
        {
            float pr = 0.92f + (float)rnd.NextDouble() * 0.16f;
            switch (e.Type)
            {
                case EvType.Shot: Play("shot", 0.6f + e.Value * 0.4f, pr); break;
                case EvType.Pass: Play("pass", 0.5f, pr); break;
                case EvType.Receive: Play("receive", 0.4f, pr); break;
                case EvType.Swish: Play("swish", 0.4f, pr); break;
                case EvType.Whoosh: Play("whoosh", 0.5f, pr); break;
                case EvType.Board: Play("board", Mathf.Min(1, e.Value / 12), pr); break;
                case EvType.BoardPuck: Play("boardPuck", Mathf.Min(1, e.Value / 15), pr); break;
                case EvType.Post: Play("post", 0.8f); Cheer(0.3f, 0.6f); break;
                case EvType.Hit: Play("hit", 1, pr); Cheer(0.35f, 0.8f); break;
                case EvType.Body: Play("body", 0.6f, pr); break;
                case EvType.Save: Play("save", 0.8f, pr); Cheer(0.3f, 0.6f); break;
                case EvType.Deflect: Play("receive", 0.6f, 1.3f); break;
                case EvType.Steal: Play("steal", 0.5f, pr); break;
                case EvType.Whistle: Play("whistle", 0.7f); break;
                case EvType.Goal: Play("goal", 0.9f); Cheer(0.9f, 3.5f); break;
                case EvType.Horn: case EvType.End: case EvType.Overtime: Play("horn", 0.8f); break;
            }
        }
    }
}
