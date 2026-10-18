using System;
using UnityEditor;
using UnityEngine;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Runtime coverage masks: one readable linear import, never read back per growth cell.</summary>
    public sealed class LabPaintPatternImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Lab/Resources/GourdPaint/Patterns/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, StringComparison.Ordinal)
                || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
