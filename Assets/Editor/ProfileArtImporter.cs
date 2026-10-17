using UnityEditor;

namespace PlantBreeding.EditorTools
{
    /// <summary>
    /// Аватари й рамки профілю (Assets/Resources/Profile, малюнки з папки «Аватариірамки», PNG 512×512)
    /// імпортуються як спрайти для UI. Mip-мапи — бо на екрані вони малі (40–170 px), без них зменшення «рветься».
    /// </summary>
    public class ProfileArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Profile/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
