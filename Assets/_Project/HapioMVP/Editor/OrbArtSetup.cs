using C6.Prototype.Presentation;
using UnityEditor;
using UnityEngine;

namespace C6.Editor
{
    /// <summary>
    /// #4 orb artwork helpers.
    /// 1) New textures under Art/Orbs import as centered sprites (512 max, mipmaps, alpha transparency).
    /// 2) C6 > Orb Art > Use Placeholder Fire Art creates/updates an OrbArtSet and links it to ScreenLayoutConfig.
    /// </summary>
    public sealed class OrbArtSetup : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/_Project/HapioMVP/Art/Orbs";
        public const string PlaceholderFolder = ArtFolder + "/Placeholder";
        public const string ArtSetPath = ArtFolder + "/OrbArtSet_Placeholder.asset";
        public const string ConfigPath = "Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder + "/")) return;
            var importer = (TextureImporter)assetImporter;
            // Configure new files, and files first imported as Default before this script compiled.
            // Once a file is a Sprite, later manual Inspector changes are kept.
            if (!importer.importSettingsMissing && importer.textureType == TextureImporterType.Sprite) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        [MenuItem("C6/Orb Art/Use Placeholder Fire Art")]
        public static void UsePlaceholderFireArt()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Orb Art", "Stop Play mode first.", "OK");
                return;
            }
            var yin = Load("fire_yin");
            var yang = Load("fire_yang");
            var combined = Load("fire_comb") ?? Load("comb_fire_fire");
            if (yin == null || yang == null || combined == null)
            {
                EditorUtility.DisplayDialog("Orb Art",
                    "Sprites not found in " + PlaceholderFolder + ".\nNeeded: fire_yin, fire_yang, fire_comb (imported as Sprite).", "OK");
                return;
            }
            var set = AssetDatabase.LoadAssetAtPath<OrbArtSet>(ArtSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<OrbArtSet>();
                AssetDatabase.CreateAsset(set, ArtSetPath);
            }
            var so = new SerializedObject(set);
            so.FindProperty("yinSprite").objectReferenceValue = yin;
            so.FindProperty("yangSprite").objectReferenceValue = yang;
            so.FindProperty("combinedSprite").objectReferenceValue = combined;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);

            var config = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
            if (config != null)
            {
                var configSo = new SerializedObject(config);
                configSo.FindProperty("orbArt").objectReferenceValue = set;
                configSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();
            Selection.activeObject = set;
            Debug.Log("[OrbArt] Placeholder fire art linked: yin=" + yin.name + ", yang=" + yang.name + ", combined=" + combined.name + "" +
                      (config != null ? " -> ScreenLayoutConfig.orbArt" : " (ScreenLayoutConfig not found)"));
        }

        [MenuItem("C6/Orb Art/Clear Orb Art (use generated circles)")]
        public static void ClearOrbArt()
        {
            var config = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
            if (config == null) return;
            var configSo = new SerializedObject(config);
            configSo.FindProperty("orbArt").objectReferenceValue = null;
            configSo.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[OrbArt] ScreenLayoutConfig.orbArt cleared.");
        }

        private static Sprite Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderFolder + "/" + name + ".png");
    }
}
