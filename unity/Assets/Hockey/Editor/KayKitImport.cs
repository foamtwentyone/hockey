// Настройки импорта моделей KayKit (CC0, Kay Lousberg): Generic-риг с анимациями,
// без импорта материалов (материалы ставит код), читаемая текстура-палитра без мип-карт —
// её перекрашивают в цвета команд при запуске.
using UnityEditor;
using UnityEngine;

namespace Hockey.EditorTools
{
    public class KayKitImport : AssetPostprocessor
    {
        // версия настроек: при изменении Unity переимпортирует модели (руки персонажа должны быть читаемыми)
        public override uint GetVersion() => 2;

        bool IsKayKit => assetPath.Replace('\\', '/').Contains("/Resources/KayKit/") || assetPath.Replace('\\', '/').Contains("/Resources/Gear/");

        void OnPreprocessModel()
        {
            if (!IsKayKit) return;
            var mi = (ModelImporter)assetImporter;
            if (assetPath.Replace('\\', '/').Contains("/Resources/Gear/")) { mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false; mi.isReadable = true; }
            else { mi.animationType = ModelImporterAnimationType.Generic; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; mi.isReadable = true; }
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        void OnPreprocessTexture()
        {
            if (assetPath.Replace('\\', '/').Contains("/Resources/UI/"))
            {
                // картинки интерфейса (логотип): без сжатия и без растяжения до степени двойки, гладкие мип-карты
                var ui = (TextureImporter)assetImporter;
                ui.textureType = TextureImporterType.Default;
                ui.npotScale = TextureImporterNPOTScale.None;
                ui.textureCompression = TextureImporterCompression.Uncompressed;
                ui.mipmapEnabled = true; ui.filterMode = FilterMode.Trilinear;
                ui.alphaIsTransparency = true; ui.wrapMode = TextureWrapMode.Clamp;
                return;
            }
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
