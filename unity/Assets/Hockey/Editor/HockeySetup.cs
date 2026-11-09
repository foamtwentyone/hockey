// Первоначальная настройка проекта при открытии в редакторе:
// создаёт сцену с игрой, добавляет её в Build Settings и готовит базовые материалы,
// чтобы шейдеры гарантированно попали в сборку.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hockey.EditorTools
{
    [InitializeOnLoad]
    public static class HockeySetup
    {
        const string ScenePath = "Assets/Hockey/Hockey.unity";
        const string ResDir = "Assets/Hockey/Resources";

        static HockeySetup()
        {
            EditorApplication.delayCall += Run;
        }

        [MenuItem("Hockey/Настроить проект")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureMaterials();
            EnsureScene();
            PlayerSettings.runInBackground = true;
            // одно цветовое пространство у всех (картинка настроена под Gamma)
            if (PlayerSettings.colorSpace != ColorSpace.Gamma) PlayerSettings.colorSpace = ColorSpace.Gamma;
            if (PlayerSettings.productName == "unity" || PlayerSettings.productName == "Дворовый хоккей 3D" || PlayerSettings.productName == "Шайбятки" || PlayerSettings.productName == "Pucklings" || string.IsNullOrEmpty(PlayerSettings.productName))
                PlayerSettings.productName = "Pucko";
        }

        static void EnsureMaterials()
        {
            if (!Directory.Exists(ResDir)) Directory.CreateDirectory(ResDir);
            var toon = Shader.Find("Hockey/Toon");
            if (toon != null) Create("HockeyToon", toon, m => { });
            var post = Shader.Find("Hockey/Post");
            if (post != null) Create("HockeyPost", post, m => { });
            var ice = Shader.Find("Hockey/Ice");
            if (ice != null) Create("HockeyIce", ice, m => { });
            var std = Shader.Find("Standard");
            if (std == null) return;
            Create("HockeyOpaque", std, m => { });
            Create("HockeyCutout", std, m =>
            {
                m.SetFloat("_Mode", 1);
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.SetInt("_SrcBlend", (int)BlendMode.One); m.SetInt("_DstBlend", (int)BlendMode.Zero); m.SetInt("_ZWrite", 1);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            });
            Create("HockeyFade", std, m =>
            {
                m.SetFloat("_Mode", 2);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_ALPHABLEND_ON");
                m.renderQueue = (int)RenderQueue.Transparent;
            });
            // варианты с подсветкой (_EMISSION) — для табло, ламп и колец
            Create("HockeyOpaqueEmissive", std, m => { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white); });
            Create("HockeyFadeEmissive", std, m =>
            {
                m.SetFloat("_Mode", 2);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_ALPHABLEND_ON"); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white);
                m.renderQueue = (int)RenderQueue.Transparent;
            });
            AssetDatabase.SaveAssets();
        }

        static void Create(string name, Shader shader, System.Action<Material> setup)
        {
            string path = ResDir + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var m = new Material(shader);
            setup(m);
            AssetDatabase.CreateAsset(m, path);
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Hockey").AddComponent<HockeyGame>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            var list = EditorBuildSettings.scenes;
            bool has = false;
            foreach (var s in list) if (s.path == ScenePath) has = true;
            if (!has)
            {
                var nl = new EditorBuildSettingsScene[list.Length + 1];
                nl[0] = new EditorBuildSettingsScene(ScenePath, true);
                for (int i = 0; i < list.Length; i++) nl[i + 1] = list[i];
                EditorBuildSettings.scenes = nl;
            }
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
