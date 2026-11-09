// Планарные отражения на льду: перед отрисовкой льда для каждой камеры рендерим зеркальную копию сцены.
using UnityEngine;

namespace Hockey
{
    public class IceReflection : MonoBehaviour
    {
        Camera reflCam;
        RenderTexture rt;
        static bool rendering;
        Material mat;
        public int Layer = 4;   // слой льда — его в отражении не рисуем

        void Start()
        {
            mat = GetComponent<Renderer>().sharedMaterial;
            gameObject.layer = Layer;
        }

        void OnWillRenderObject()
        {
            var cam = Camera.current;
            if (cam == null || rendering || cam == reflCam || mat == null || !mat.HasProperty("_ReflTex")) return;
            if (Settings.Quality < 2) { mat.SetFloat("_Refl", 0.12f); mat.SetTexture("_ReflTex", Texture2D.blackTexture); return; }
            // отражение обновляем через кадр — на льду разница незаметна
            if (Time.frameCount % 2 == 1 && rt != null && rt.width == Mathf.Max(64, (int)cam.pixelWidth / 3)) { mat.SetTexture("_ReflTex", rt); return; }
            rendering = true;
            int w = Mathf.Max(64, (int)cam.pixelWidth / 3), h = Mathf.Max(64, (int)cam.pixelHeight / 3);
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { rt.Release(); Destroy(rt); }
                rt = new RenderTexture(w, h, 16) { name = "IceRefl", hideFlags = HideFlags.DontSave };
            }
            if (reflCam == null)
            {
                var go = new GameObject("IceReflCam") { hideFlags = HideFlags.HideAndDontSave };
                reflCam = go.AddComponent<Camera>();
                reflCam.enabled = false;
            }
            reflCam.CopyFrom(cam);
            reflCam.rect = new Rect(0, 0, 1, 1);
            reflCam.cullingMask = cam.cullingMask & ~(1 << Layer) & ~(1 << Arena.CrowdLayer);
            reflCam.renderingPath = RenderingPath.Forward;
            reflCam.allowMSAA = false; reflCam.allowHDR = false;
            reflCam.targetTexture = rt;
            reflCam.clearFlags = CameraClearFlags.SolidColor;
            reflCam.backgroundColor = Mats.Hex("#2a2f6b");

            // отражение относительно плоскости y = высота льда
            float y = transform.position.y;
            var refl = Matrix4x4.identity;
            refl.m11 = -1; refl.m13 = 2 * y;
            reflCam.worldToCameraMatrix = cam.worldToCameraMatrix * refl;
            // наклонная плоскость отсечения — не рисуем то, что под льдом
            var cpos = reflCam.worldToCameraMatrix.MultiplyPoint(new Vector3(0, y, 0));
            var cnorm = reflCam.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
            var clip = new Vector4(cnorm.x, cnorm.y, cnorm.z, -Vector3.Dot(cpos, cnorm) + 0.02f);
            reflCam.projectionMatrix = cam.CalculateObliqueMatrix(clip);
            GL.invertCulling = true;
            var pfx = reflCam.GetComponent<PostFx>();
            if (pfx != null) pfx.enabled = false;
            reflCam.Render();
            GL.invertCulling = false;
            mat.SetTexture("_ReflTex", rt);
            mat.SetFloat("_Refl", 0.25f);
            rendering = false;
        }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (reflCam != null) Destroy(reflCam.gameObject);
        }
    }
}
