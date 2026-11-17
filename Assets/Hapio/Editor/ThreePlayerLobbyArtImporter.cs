using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>Import only the original #802 Figma PNGs; never regenerate lobby or Scene objects.</summary>
    public sealed class ThreePlayerLobbyArtImporter : AssetPostprocessor
    {
        public const string Root = "Assets/Hapio/Resources/Hapio/UI/ThreePlayerLobby/";

        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(Root, StringComparison.Ordinal) && assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                Configure((TextureImporter)assetImporter);
        }

        private static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        [MenuItem("Hapio/UI/Import Three Player Lobby Art")]
        public static void ImportArt()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before importing lobby artwork.");
            foreach (string path in Directory.GetFiles(Root, "*.png"))
                AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("HAPIO_THREE_PLAYER_LOBBY_ART_READY: original Figma sprites imported; no lobby or Scene rebuild.");
        }
    }
}
