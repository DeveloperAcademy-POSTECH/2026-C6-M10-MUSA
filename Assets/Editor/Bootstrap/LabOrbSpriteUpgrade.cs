using System;
using UnityEditor;
using UnityEngine;

namespace C6Lab.Editor
{
    public static class LabOrbSpriteUpgrade
    {
        [MenuItem("C6 Lab/Apply Yin Yang And Element Orb Sprites")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying orb art.");
            var appearance = AssetDatabase.LoadAssetAtPath<LabOrbAppearance>(
                "Assets/Lab/Art/Orbs/LabOrbAppearance.asset");
            var serialized = new SerializedObject(appearance);
            foreach (string name in new[] { "Yin", "Yang", "Fire", "Water", "Wood", "Metal", "Earth" })
            {
                string path = "Assets/Lab/Art/Orbs/Sprites/" + name + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePivot = new Vector2(.5f, .5f);
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                serialized.FindProperty(char.ToLowerInvariant(name[0]) + name.Substring(1) + "Sprite")
                    .objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}
