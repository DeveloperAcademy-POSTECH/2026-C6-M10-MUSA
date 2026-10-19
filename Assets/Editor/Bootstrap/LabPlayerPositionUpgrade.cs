using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace C6Lab.Editor
{
    public static class LabPlayerPositionUpgrade
    {
        private const string Art = "Assets/Lab/Art/UI/";
        [MenuItem("C6 Lab/Apply New Player Position Map")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying the player position map.");
            BuildIcons();
            var scene = EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity");
            var hud = GameObject.Find("LabCanvas").GetComponent<LabHud>();
            var controller = UnityEngine.Object.FindAnyObjectByType<LabSceneController>();
            var map = controller.GetComponent<LabDrumMinimap>();
            if (map == null) map = controller.gameObject.AddComponent<LabDrumMinimap>();
            map.playerSprites = (Sprite[])hud.playerCircleSprites.Clone();
            map.drumIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "PlayerPositionDrumIcon.png");
            map.soloDrumSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "PlayerPositionSoloIcon.png");
            // The existing default Minimap Size is 50%; keep Config and DEV semantics intact.
            map.size = new Vector2(151f, 142f);
            map.gapAboveBadge = 7f;
            map.insetFromBadgeRight = 11.5f;
            map.lineWidth = 5f;
            map.lineColor = new Color32(89, 60, 30, 255);
            EditorUtility.SetDirty(map);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void BuildIcons()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(Art + "PlayerPositionSoloConcept.png")))
                    throw new IOException("Cannot decode the supplied solo position PNG.");
                // Remove only the source's transparent margins for the exact solo token.
                Save("PlayerPositionSoloIcon", 71, 70, texture.GetPixels(41, 37, 71, 70));
                Color[] pixels = texture.GetPixels(50, 45, 53, 53);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    // Isolate the painted taegeuk so a P2..P5 token retains its own ring colour.
                    bool ring = pixel.r > 180 && pixel.g > 90 && pixel.b > 80 && pixel.b > pixel.r * .48f;
                    if (ring) pixels[i] = Color.clear;
                }
                Save("PlayerPositionDrumIcon", 53, 53, pixels);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static void Save(string name, int width, int height, Color[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(pixels); texture.Apply();
                string path = Art + name + ".png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
