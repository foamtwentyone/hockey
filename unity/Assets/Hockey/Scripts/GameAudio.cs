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

            clips["jump"] = Tone(320, 0.22f, 0.35f, Wave.Sine, 420);
            clips["dive"] = Mix(Noise(0.3f, 0.12f, 0.5f), Tone(500, 0.25f, 0.2f, Wave.Triangle, -300));
            clips["land"] = Mix(Noise(0.12f, 0.03f, 0.8f), Tone(110, 0.12f, 0.4f, Wave.Sine, -50));
            clips["stop"] = Noise(0.35f, 0.7f, 0.35f);
            clips["powerup"] = Arp(new[] { 523f, 659f, 784f, 1047f }, 0.07f, 0.25f);
            clips["freeze"] = Mix(Arp(new[] { 1568f, 1319f, 1175f, 1047f }, 0.06f, 0.2f), Noise(0.5f, 0.9f, 0.15f));
            clips["mega"] = Mix(Tone(70, 0.5f, 0.7f, Wave.Sine, -30), Noise(0.4f, 0.1f, 0.6f));
            clips["spawn"] = Mix(Tone(880, 0.25f, 0.15f, Wave.Sine, 0), Tone(1320, 0.3f, 0.1f, Wave.Sine, 0));
            clips["click"] = Tone(660, 0.05f, 0.25f, Wave.Triangle, 200);
            clips["hover"] = Tone(990, 0.03f, 0.08f, Wave.Sine, 0);

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
