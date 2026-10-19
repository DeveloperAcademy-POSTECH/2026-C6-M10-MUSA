using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab.Editor
{
    /// <summary>Separates the supplied frame and cell alpha so live stamina can tint the cells.</summary>
    public static class LabStaminaBarUpgrade
    {
        private const string Art = "Assets/Lab/Art/UI/";

        [MenuItem("C6 Lab/Apply New Stamina Bar")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying the stamina bar.");
            Rect cellArea = BuildLayers();
            var scene = EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity");
            var hud = GameObject.Find("LabCanvas").GetComponent<LabHud>();
            // The new bottom row contains only the player badge and stamina.
            hud.battleUi.transform.Find("FigmaPlayerHpBar").gameObject.SetActive(false);
            hud.battleUi.transform.Find("BottomHudBackground").gameObject.SetActive(false);
            var bar = (RectTransform)hud.battleUi.transform.Find("FigmaStaminaBar");
            // 446×88 source at half size; centred on the Figma badge's y=832.5 row.
            bar.anchorMin = bar.anchorMax = new Vector2(229f / 402f, 1f - 832.5f / 874f);
            bar.pivot = new Vector2(.5f, .5f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(223f, 44f);
            bar.gameObject.SetActive(true);
            var fill = hud.staminaFill as LabShapeFillImage;
            if (fill == null) throw new InvalidOperationException("The saved stamina bar needs its existing shape fill.");
            fill.rectTransform.anchorMin = cellArea.min;
            fill.rectTransform.anchorMax = cellArea.max;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "StaminaBarFillMask.png");
            fill.color = Color.white;
            fill.filledColor = new Color32(0x2F, 0x62, 0xA2, 255);
            fill.emptyColor = new Color32(0xC1, 0xD2, 0xE0, 255);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            fill.gameObject.SetActive(true);
            fill.SetVerticesDirty();
            var frame = bar.Find("StaminaFrame").GetComponent<Image>();
            Stretch(frame.rectTransform);
            frame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "StaminaBarFrame.png");
            frame.color = Color.white;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = true;
            frame.raycastTarget = false;
            frame.gameObject.SetActive(true);
            frame.transform.SetAsLastSibling();
            EditorUtility.SetDirty(fill);
            EditorUtility.SetDirty(frame);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the stamina bar.");
            AssetDatabase.SaveAssets();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Rect BuildLayers()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(Art + "StaminaBarConcept.png")))
                    throw new IOException("Cannot decode the supplied stamina PNG.");
                Color32[] original = texture.GetPixels32();
                var frame = new Color32[original.Length];
                var cells = new Color32[original.Length];
                int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
                for (int i = 0; i < original.Length; i++)
                {
                    Color32 pixel = original[i];
                    // Both blue cell colours have B > R; the beige frame has R > B.
                    // Retain each edge pixel's original alpha, including antialiasing.
                    if (pixel.a > 0 && pixel.b > pixel.r)
                        {
                        cells[i] = new Color32(255, 255, 255, pixel.a);
                        int x = i % texture.width, y = i / texture.width;
                        minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                    }
                    else frame[i] = pixel;
                }
                SaveLayer(texture, frame, "StaminaBarFrame");
                if (maxX < minX) throw new IOException("The supplied stamina PNG has no blue cells.");
                texture.SetPixels32(cells);
                texture.Apply();
                int width = maxX - minX + 1, height = maxY - minY + 1;
                var cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    cropped.SetPixels(texture.GetPixels(minX, minY, width, height));
                    cropped.Apply();
                    SaveLayer(cropped, cropped.GetPixels32(), "StaminaBarFillMask");
                }
                finally { UnityEngine.Object.DestroyImmediate(cropped); }
                // Exclude the empty space around the cells from the resource fraction.
                return new Rect(minX / (float)texture.width, minY / (float)texture.height,
                    width / (float)texture.width, height / (float)texture.height);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static void SaveLayer(Texture2D texture, Color32[] pixels, string name)
        {
            texture.SetPixels32(pixels);
            texture.Apply();
            string path = Art + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
