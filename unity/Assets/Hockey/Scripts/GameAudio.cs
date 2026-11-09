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
        readonly AudioSource crowd, music;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        float crowdTarget = 0.12f, crowdHold;
        public bool Muted;
        static readonly System.Random rnd = new System.Random(1);

        public GameAudio(GameObject host)
        {
            pool = new AudioSource[12];
            for (int i = 0; i < pool.Length; i++) { pool[i] = host.AddComponent<AudioSource>(); pool[i].playOnAwake = false; pool[i].spatialBlend = 0; }
            // мягкие «мультяшные» звуки в духе Fall Guys
            clips["shot"] = Mix(Bonk(520, 0.12f, 0.5f), Noise(0.08f, 0.3f, 0.35f));
            clips["pass"] = Glide(560, 760, 0.09f, 0.3f, Wave.Sine, 0, 0);
            clips["receive"] = Glide(420, 360, 0.06f, 0.22f, Wave.Sine, 0, 0);
            clips["swish"] = Noise(0.15f, 0.12f, 0.22f);
            clips["whoosh"] = Mix(Noise(0.18f, 0.1f, 0.25f), Glide(260, 420, 0.15f, 0.2f, Wave.Sine, 0, 0));
            clips["board"] = Plop(140, 0.16f, 0.6f);
            clips["boardPuck"] = Plop(260, 0.1f, 0.45f);
            clips["post"] = Mix(Glide(1650, 1640, 0.6f, 0.3f, Wave.Sine, 6, 8), Glide(2470, 2460, 0.4f, 0.12f, Wave.Sine, 0, 0));
            clips["hit"] = Mix(Squeak(0.22f, 0.32f), Bonk(300, 0.2f, 0.6f));
            clips["body"] = Plop(200, 0.12f, 0.35f);
            clips["save"] = Boing(220, 0.35f, 0.45f);
            clips["steal"] = Glide(400, 1200, 0.16f, 0.25f, Wave.Sine, 0, 0);
            clips["whistle"] = Glide(1800, 2600, 0.45f, 0.22f, Wave.Sine, 14, 120);
            clips["horn"] = Kazoo(new[] { 262f, 196f }, 0.45f, 0.3f);
            clips["goal"] = Mix(Kazoo(new[] { 392f, 523f, 659f, 784f, 659f, 784f }, 0.17f, 0.32f), Yay(2.4f, 0.35f));
            clips["jump"] = Boing(330, 0.28f, 0.42f, true);
            clips["dive"] = Mix(Glide(1500, 600, 0.45f, 0.22f, Wave.Sine, 5, 25), Noise(0.3f, 0.1f, 0.2f));
            clips["land"] = Plop(170, 0.14f, 0.55f);
            clips["stop"] = Noise(0.3f, 0.55f, 0.25f);
            clips["powerup"] = Arp(new[] { 784f, 988f, 1175f, 1568f }, 0.06f, 0.22f);
            clips["freeze"] = Mix(Arp(new[] { 2093f, 1760f, 1568f, 1319f }, 0.05f, 0.18f), Noise(0.5f, 0.9f, 0.12f));
            clips["mega"] = Mix(Glide(160, 60, 0.45f, 0.6f, Wave.Sine, 0, 0), Noise(0.4f, 0.08f, 0.45f));
            clips["spawn"] = Bubble(0.12f, 0.25f);
            clips["click"] = Bubble(0.08f, 0.35f);
            clips["hover"] = Glide(900, 1300, 0.045f, 0.12f, Wave.Sine, 0, 0);
            clips["punch"] = Mix(Noise(0.07f, 0.25f, 0.6f), Mix(Plop(220, 0.08f, 0.5f), Squeak(0.1f, 0.12f)));
            clips["brawl"] = Brawl(2.3f);
            clips["brawlEnd"] = Mix(Bonk(240, 0.25f, 0.7f), Glide(1300, 300, 0.6f, 0.25f, Wave.Sine, 6, 30));

            music = host.AddComponent<AudioSource>();
            music.clip = MusicLoop();
            music.loop = true; music.volume = 0; music.spatialBlend = 0;
            music.Play();

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

        /// Тон с плавным скольжением частоты и вибрато (основа «мультяшных» звуков).
        static AudioClip Glide(float f0, float f1, float dur, float vol, Wave wave, float vibHz, float vibDepth)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t * t * (3 - 2 * t)) + Mathf.Sin(i / (float)Rate * vibHz * Mathf.PI * 2) * vibDepth;
                ph += f / Rate;
                float p = (float)(ph - System.Math.Floor(ph));
                float sg = wave == Wave.Sine ? Mathf.Sin(p * Mathf.PI * 2) : wave == Wave.Triangle ? 1 - 4 * Mathf.Abs(p - 0.5f) : wave == Wave.Square ? (p < 0.5f ? 1 : -1) : p * 2 - 1;
                float env = Mathf.Min(1, i / (Rate * 0.006f)) * Mathf.Pow(1 - t, 1.6f);
                d[i] = sg * env * vol;
            }
            return Make(d);
        }

        /// «Боинг»: пружинка с качающимся тоном.
        static AudioClip Boing(float f, float dur, float vol, bool up = false)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n, ts = i / (float)Rate;
                float fr = f * (up ? Mathf.Lerp(0.8f, 1.9f, Mathf.Sqrt(t)) : Mathf.Lerp(1.6f, 0.9f, t)) * (1 + Mathf.Sin(ts * 38) * 0.12f * (1 - t));
                ph += fr / Rate;
                d[i] = Mathf.Sin((float)(ph * Mathf.PI * 2)) * Mathf.Exp(-t * 3.5f) * Mathf.Min(1, i / (Rate * 0.004f)) * vol;
            }
            return Make(d);
        }

        /// «Плюх»: мягкий глухой удар.
        static AudioClip Plop(float f, float dur, float vol)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                ph += f * Mathf.Lerp(1.8f, 0.6f, Mathf.Sqrt(t)) / Rate;
                d[i] = Mathf.Sin((float)(ph * Mathf.PI * 2)) * Mathf.Exp(-t * 6) * vol + ((float)rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t * 30) * vol * 0.15f;
            }
            return Make(d);
        }

        /// «Бонк»: деревянная коробочка.
        static AudioClip Bonk(float f, float dur, float vol)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, k = i / (float)n;
                d[i] = (Mathf.Sin(t * f * Mathf.PI * 2) + 0.5f * Mathf.Sin(t * f * 2.76f * Mathf.PI * 2)) * Mathf.Exp(-k * 9) * vol * 0.7f;
            }
            return Make(d);
        }

        /// Резиновая пищалка: два быстрых «пи-пи» с изгибом высоты.
        static AudioClip Squeak(float dur, float vol)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float bend = Mathf.Sin(t * Mathf.PI * 2) * 0.5f + 0.5f;
                ph += Mathf.Lerp(1300, 2300, bend) / Rate;
                float p = (float)(ph - System.Math.Floor(ph));
                float sg = Mathf.Sin(p * Mathf.PI * 2) * 0.7f + (1 - 4 * Mathf.Abs(p - 0.5f)) * 0.3f;
                d[i] = sg * Mathf.Sin(t * Mathf.PI) * vol;
            }
            return Make(d);
        }

        /// Пузырик для интерфейса.
        static AudioClip Bubble(float dur, float vol)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                ph += Mathf.Lerp(500, 1400, t * t) / Rate;
                d[i] = Mathf.Sin((float)(ph * Mathf.PI * 2)) * Mathf.Sin(t * Mathf.PI) * vol;
            }
            return Make(d);
        }

        /// «Казу»: пила с вибрато через мягкий фильтр — смешная дудочка для фанфар.
        static AudioClip Kazoo(float[] notes, float step, float vol)
        {
            int per = (int)(step * Rate);
            int n = per * notes.Length + Rate / 3;
            var d = new float[n];
            double ph = 0; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                int k = Mathf.Min(notes.Length - 1, i / per);
                float ts = i / (float)Rate;
                float f = notes[k] * (1 + Mathf.Sin(ts * 32) * 0.015f);
                ph += f / Rate;
                float p = (float)(ph - System.Math.Floor(ph));
                float saw = p * 2 - 1;
                lp += (saw - lp) * 0.18f;
                float local = (i % per) / (float)per;
                float env = (k == notes.Length - 1 ? Mathf.Clamp01(1 - (i - k * per) / (float)(per + Rate / 3)) : 1) * Mathf.Min(1, (i % per) / (Rate * 0.01f)) * (0.85f + 0.15f * Mathf.Sin(local * Mathf.PI));
                d[i] = lp * env * vol;
            }
            return Make(d);
        }

        /// Радостное «Ура!» трибун: шум с формантой, нарастающий и спадающий.
        static AudioClip Yay(float dur, float vol)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float w = (float)rnd.NextDouble() * 2 - 1;
                lp += (w - lp) * 0.25f; lp2 += (lp - lp2) * 0.25f;
                float env = Mathf.Min(1, t * 6) * Mathf.Pow(1 - t, 1.2f);
                d[i] = (lp - lp2) * 3 * env * vol;
            }
            return Make(d);
        }

        /// Звук потасовки: серия «пап», «бонк» и писков.
        static AudioClip Brawl(float dur)
        {
            int n = (int)(dur * Rate);
            var d = new float[n];
            var r = new System.Random(5);
            float t = 0;
            while (t < dur - 0.15f)
            {
                int s0 = (int)(t * Rate);
                int kind = r.Next(3);
                int len = (int)(Rate * (kind == 2 ? 0.12f : 0.07f));
                float f = 150 + r.Next(250);
                for (int i = 0; i < len && s0 + i < n; i++)
                {
                    float k = i / (float)len, ts = i / (float)Rate;
                    float v = kind == 0 ? ((float)r.NextDouble() * 2 - 1) * Mathf.Exp(-k * 8) * 0.5f
                        : kind == 1 ? Mathf.Sin(ts * f * Mathf.PI * 2 * (1.6f - k)) * Mathf.Exp(-k * 6) * 0.5f
                        : Mathf.Sin(ts * Mathf.Lerp(1400, 2200, Mathf.Sin(k * Mathf.PI)) * Mathf.PI * 2) * Mathf.Sin(k * Mathf.PI) * 0.18f;
                    d[s0 + i] += v;
                }
                t += 0.06f + (float)r.NextDouble() * 0.1f;
            }
            return Make(d);
        }

        static AudioClip Arp(float[] notes, float step, float vol)
        {
            int n = (int)((notes.Length * step + 0.25f) * Rate);
            var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(k * step * Rate), len = (int)(0.25f * Rate);
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1, i / (Rate * 0.005f)) * Mathf.Exp(-t * 12);
                    float ph = t * notes[k];
                    d[start + i] += ((ph - Mathf.Floor(ph)) < 0.5f ? 1 : -1) * 0.5f * env * vol + Mathf.Sin(ph * Mathf.PI * 2) * env * vol * 0.5f;
                }
            }
            return Make(d);
        }

        /// Весёлая фоновая петля: бас, аккорды и мелодия (4 такта, 120 BPM).
        static AudioClip MusicLoop()
        {
            const float bpm = 124; float beat = 60 / bpm;
            int bars = 4; float dur = bars * 4 * beat;
            int n = (int)(dur * Rate);
            var d = new float[n];
            float[][] chords = { new[] { 261.6f, 329.6f, 392f }, new[] { 196f, 246.9f, 293.7f }, new[] { 220f, 261.6f, 329.6f }, new[] { 174.6f, 220f, 261.6f } };
            float[] scale = { 523.3f, 587.3f, 659.3f, 784f, 880f, 1046.5f };
            int[] melody = { 0, 2, 3, 2, 4, 3, 2, -1, 1, 3, 4, 3, 5, 4, 3, -1, 2, 3, 4, 2, 3, 1, 0, -1, 3, 2, 1, 2, 0, 0, -1, -1 };
            void Note(float freq, float start, float len, float vol, int wave)
            {
                int s0 = (int)(start * Rate), cnt = (int)(len * Rate);
                for (int i = 0; i < cnt && s0 + i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1, i / (Rate * 0.004f)) * Mathf.Exp(-t * (wave == 2 ? 3 : 7));
                    float ph = t * freq; ph -= Mathf.Floor(ph);
                    float sgl = wave == 0 ? (ph < 0.5f ? 1 : -1) * 0.6f : wave == 1 ? 1 - 4 * Mathf.Abs(ph - 0.5f) : Mathf.Sin(ph * Mathf.PI * 2);
                    d[s0 + i] += sgl * env * vol;
                }
            }
            for (int b = 0; b < bars; b++)
            {
                var ch = chords[b];
                for (int q = 0; q < 4; q++)
                {
                    float t0 = (b * 4 + q) * beat;
                    Note(ch[0] / 2, t0, beat * 0.9f, 0.22f, 1);                       // бас
                    Note(ch[q % 3], t0 + beat * 0.5f, beat * 0.4f, 0.05f, 0);         // «чик» аккорда
                    Note(ch[(q + 1) % 3], t0 + beat * 0.5f, beat * 0.4f, 0.04f, 0);
                    // хай-хэт
                    int hs = (int)((t0 + beat * 0.5f) * Rate);
                    for (int i = 0; i < Rate * 0.03f && hs + i < n; i++) d[hs + i] += ((float)rnd.NextDouble() * 2 - 1) * 0.05f * Mathf.Exp(-i / (Rate * 0.008f));
                    // «бочка»
                    int ks = (int)(t0 * Rate);
                    for (int i = 0; i < Rate * 0.12f && ks + i < n; i++) { float t = i / (float)Rate; d[ks + i] += Mathf.Sin(t * Mathf.PI * 2 * (90 - t * 300)) * 0.25f * Mathf.Exp(-t * 25); }
                }
                for (int e = 0; e < 8; e++)
                {
                    int m = melody[(b * 8 + e) % melody.Length];
                    if (m >= 0) Note(scale[m], (b * 4 + e * 0.5f) * beat, beat * 0.45f, 0.09f, 2);
                }
            }
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i], -1, 1) * 0.8f;
            return Make(d);
        }

        public void SetMusic(bool on) { musicOn = on; }
        bool musicOn = true;

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
            s.pitch = pitch; s.volume = vol * Settings.Sfx;
            s.PlayOneShot(c);
        }

        void Cheer(float level, float hold) { crowdTarget = Mathf.Max(crowdTarget, level); crowdHold = Mathf.Max(crowdHold, hold); }

        public void Update(float dt)
        {
            crowdHold -= dt;
            if (crowdHold <= 0) crowdTarget = Mathf.MoveTowards(crowdTarget, 0.12f, dt * 0.1f);
            crowd.volume = Muted ? 0 : Mathf.MoveTowards(crowd.volume, crowdTarget * Settings.Crowd, dt * 1.5f);
            music.volume = Muted ? 0 : Mathf.MoveTowards(music.volume, musicOn ? Settings.Music * 0.35f : Settings.Music * 0.12f, dt * 0.5f);
            AudioListener.volume = Settings.Master;
        }

        public void Ui(bool click) => Play(click ? "click" : "hover", click ? 0.6f : 0.4f);

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
                case EvType.Jump: Play("jump", 0.6f, pr); break;
                case EvType.Punch: Play("punch", 0.8f, pr); Cheer(0.2f, 0.4f); break;
                case EvType.BrawlStart: Play("brawl", 0.8f); Cheer(0.45f, 2.2f); break;
                case EvType.BrawlEnd: Play("brawlEnd", 0.9f); Cheer(0.5f, 1f); break;
                case EvType.Dive: Play("dive", 0.6f, pr); break;
                case EvType.Land: Play("land", Mathf.Min(1, e.Value / 8), pr); break;
                case EvType.Stop: Play("stop", Mathf.Min(0.6f, e.Value / 14), pr); break;
                case EvType.PowerUp: Play("powerup", 0.8f); Cheer(0.25f, 0.5f); break;
                case EvType.Freeze: Play("freeze", 0.9f); break;
                case EvType.MegaShot: Play("mega", 1f); Cheer(0.35f, 0.6f); break;
                case EvType.ItemSpawn: Play("spawn", 0.5f); break;
            }
        }
    }
}
