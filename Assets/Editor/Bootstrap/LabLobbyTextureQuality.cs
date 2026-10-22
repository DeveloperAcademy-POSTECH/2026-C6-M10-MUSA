using System;
using UnityEditor;
using UnityEngine;

namespace C6Lab.Editor
{
    /// <summary>Keep the supplied lobby art lossless on the Editor and iPhone alike.</summary>
    public sealed class LabLobbyTextureQuality : AssetPostprocessor
    {
        public const string ArtPath = "Assets/Lab/Art/Lobby/";

        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(ArtPath, StringComparison.Ordinal))
                Configure((TextureImporter)assetImporter);
        }

        public static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            foreach (string platform in new[] { "Standalone", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = 4096;
                settings.format = TextureImporterFormat.RGBA32;
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                settings.crunchedCompression = false;
                importer.SetPlatformTextureSettings(settings);
            }
        }

        [MenuItem("C6 Lab/Apply Lossless Lobby Texture Quality")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before reimporting lobby textures.");
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtPath.TrimEnd('/') }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (importer == null) continue;
                Configure(importer);
                importer.SaveAndReimport();
                count++;
            }
            Debug.Log("C6_LOBBY_LOSSLESS_TEXTURES count=" + count);
        }
    }
}
