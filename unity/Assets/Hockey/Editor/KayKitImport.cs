// Настройки импорта моделей KayKit (CC0, Kay Lousberg): Generic-риг с анимациями,
// без импорта материалов (материалы ставит код), читаемая текстура-палитра без мип-карт —
// её перекрашивают в цвета команд при запуске.
using UnityEditor;
using UnityEngine;

namespace Hockey.EditorTools
{
    public class KayKitImport : AssetPostprocessor
    {
        bool IsKayKit => assetPath.Replace('\\', '/').Contains("/Resources/KayKit/");

        void OnPreprocessModel()
        {
            if (!IsKayKit) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
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
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.sRGBTexture = true;
        }
    }
}
