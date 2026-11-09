// Постобработка на камере: свечение, цветокоррекция, виньетка, размытие на скорости.
using UnityEngine;

namespace Hockey
{
    [RequireComponent(typeof(Camera))]
    public class PostFx : MonoBehaviour
    {
        static Material mat;
        static bool tried;
        public float Speed;          // 0..1 — сила размытия на скорости
        public float ExtraBloom;     // всплески свечения (гол, бонус)

        static Material Mat()
        {
            if (mat == null && !tried)
            {
                tried = true;
                var m = Resources.Load<Material>("HockeyPost");
                if (m != null) mat = new Material(m);
                else
                {
                    var sh = Shader.Find("Hockey/Post");
                    if (sh != null && sh.isSupported) mat = new Material(sh);
                }
            }
            return mat;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            var m = Mat();
            if (m == null || Settings.Quality == 0) { Graphics.Blit(src, dst); return; }
            bool high = Settings.Quality >= 2;
            m.SetFloat("_Threshold", 1.4f);
            m.SetFloat("_Knee", 0.2f);
            m.SetFloat("_Bloom", 0.3f + ExtraBloom);
            m.SetFloat("_Exposure", 1.0f);
            m.SetFloat("_Saturation", 1.14f);
            m.SetFloat("_Contrast", 1.06f);
            m.SetFloat("_Vignette", 0.32f);
            m.SetFloat("_Speed", high ? Speed : 0);
            m.SetColor("_Tint", new Color(1.03f, 1.0f, 0.96f));

            // пирамида размытия для свечения
            int w = src.width / 2, h = src.height / 2;
            const int levels = 5;
            var rts = new RenderTexture[levels];
            var cur = RenderTexture.GetTemporary(w, h, 0, src.format);
            Graphics.Blit(src, cur, m, 0);
            rts[0] = cur;
            int n = 1;
            for (; n < levels; n++)
            {
                w /= 2; h /= 2;
                if (w < 4 || h < 4) break;
                var next = RenderTexture.GetTemporary(w, h, 0, src.format);
                Graphics.Blit(cur, next, m, 1);
                rts[n] = next; cur = next;
            }
            for (int i = n - 2; i >= 0; i--)
            {
                Graphics.Blit(cur, rts[i], m, 2);
                cur = rts[i];
            }
            m.SetTexture("_BloomTex", cur);
            Graphics.Blit(src, dst, m, 3);
            for (int i = 0; i < n; i++) RenderTexture.ReleaseTemporary(rts[i]);
        }
    }
}
