// Хоккейные позы из настоящего захвата движения (CMU Graphics Lab Motion Capture Database, mocap.cs.cmu.edu,
// свободно для любого использования): стойка с клюшкой — из «подметания пола» (13_23), замах и бросок — из удара
// в гольфе (64_01, отражён под левый хват). Файлы Resources/Anim/*.txt — направления костей в системе персонажа
// (x — к левому боку, y — вверх, z — вперёд) по кадрам; их делает unity/Tools/mocap_export.py.
// Позы накладываются поверх анимаций KayKit: каждая кость поворачивается так, чтобы смотреть как у живого актёра.
using System.Collections.Generic;
using UnityEngine;

namespace Hockey
{
    public class MocapClip
    {
        public int Fps;
        public float[][] Frames;
        public const int Stride = 31;   // 10 векторов по 3 + разнос рук
        static readonly Dictionary<string, MocapClip> cache = new Dictionary<string, MocapClip>();

        public static MocapClip Load(string name)
        {
            if (cache.TryGetValue(name, out var c)) return c;
            var ta = Resources.Load<TextAsset>("Anim/" + name);
            if (ta != null)
            {
                var lines = ta.text.Split('\n');
                var head = lines[0].Split(' ');
                c = new MocapClip { Fps = int.Parse(head[1]) };
                var list = new List<float[]>();
                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Trim().Split(' ');
                    if (parts.Length < Stride) continue;
                    var f = new float[Stride];
                    for (int k = 0; k < Stride; k++) f[k] = float.Parse(parts[k], System.Globalization.CultureInfo.InvariantCulture);
                    list.Add(f);
                }
                c.Frames = list.ToArray();
                if (c.Frames.Length == 0) c = null;
            }
            cache[name] = c;
            return c;
        }

        /// Кадр по доле 0..1 (с плавной интерполяцией между соседними).
        public void Sample(float t01, float[] dst)
        {
            float x = Mathf.Clamp01(t01) * (Frames.Length - 1);
            int a = Mathf.FloorToInt(x), b = Mathf.Min(a + 1, Frames.Length - 1);
            float k = x - a;
            for (int i = 0; i < Stride; i++) dst[i] = Mathf.Lerp(Frames[a][i], Frames[b][i], k);
        }

        /// Зацикленный кадр «туда-обратно» по времени в секундах.
        public void SampleLoop(float time, float[] dst)
        {
            float len = (Frames.Length - 1) / (float)Fps;
            Sample(Mathf.PingPong(time, len) / Mathf.Max(1e-4f, len), dst);
        }
    }
}
