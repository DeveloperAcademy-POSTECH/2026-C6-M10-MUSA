using UnityEditor;
using UnityEngine;

namespace C6Lab.Experiments.Editor
{
    // These three source masks are CPU-readable study inputs, not ordinary game textures.
    public sealed class PaintStyleMaskImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Lab/Art/GourdPaint/Experiments/StyleComparison/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.Ordinal)
                || !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
