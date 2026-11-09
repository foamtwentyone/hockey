// Настройки импорта моделей KayKit (CC0, Kay Lousberg): Generic-риг с анимациями,
// без импорта материалов (материалы ставит код), читаемая текстура-палитра без мип-карт —
// её перекрашивают в цвета команд при запуске.
using UnityEditor;
using UnityEngine;

namespace Hockey.EditorTools
{
    public class KayKitImport : AssetPostprocessor
    {
        bool IsKayKit => assetPath.Replace('\\', '/').Contains("/Resources/KayKit/") || assetPath.Replace('\\', '/').Contains("/Resources/Gear/");

        void OnPreprocessModel()
        {
            if (!IsKayKit) return;
            var mi = (ModelImporter)assetImporter;
            if (assetPath.Replace('\\', '/').Contains("/Resources/Gear/")) { mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false; mi.isReadable = true; }
            else { mi.animationType = ModelImporterAnimationType.Generic; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; }
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        void OnPreprocessTexture()
        {
            if (!IsKayKit) return;
            var ti = (TextureImporter)assetImporter;
            ti.isReadable = true;
            bool gear = assetPath.Replace('\\', '/').Contains("/Resources/Gear/");
            ti.mipmapEnabled = gear;
            if (gear) { ti.anisoLevel = 8; ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter; }
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.filterMode = gear ? FilterMode.Trilinear : FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.sRGBTexture = true;
        }
    }
}
