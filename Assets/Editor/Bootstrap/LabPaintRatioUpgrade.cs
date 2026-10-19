using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab.Editor
{
    public static class LabPaintRatioUpgrade
    {
        private const string Art = "Assets/Lab/Art/UI/";
        [MenuItem("C6 Lab/Apply New Paint Ratio Bar")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying the paint ratio bar.");
            BuildLayers();
            var scene = EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity");
            var hud = GameObject.Find("LabCanvas").GetComponent<C6Lab.LabHud>();
            var bar = (RectTransform)hud.battleUi.transform.Find("FigmaYokaiHpBar");
            bar.anchorMin = bar.anchorMax = new Vector2(192f / 402f, 1f - 118.5f / 874f);
            bar.pivot = new Vector2(.5f, .5f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(232.5f, 29.5f);
            var paper = bar.GetComponent<Image>();
            paper.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "PaintRatioEmpty.png");
            paper.color = Color.white;
            paper.type = Image.Type.Simple;
            paper.preserveAspect = true;
            paper.raycastTarget = false;
            bar.Find("TitleMask").gameObject.SetActive(false);
            bar.Find("TrackMask").gameObject.SetActive(false);
            var fill = hud.yokaiHpFill;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "PaintRatioFillMask.png");
            fill.color = new Color32(0xAF, 0x47, 0x38, 255);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.preserveAspect = true;
            fill.raycastTarget = false;
            var value = hud.hpText;
            value.rectTransform.anchorMin = value.rectTransform.anchorMax = new Vector2(0, 1);
            value.rectTransform.pivot = new Vector2(0, 1);
            value.rectTransform.anchoredPosition = new Vector2(239.25f, -.25f);
            value.rectTransform.sizeDelta = new Vector2(66, 28);
            value.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
            value.fontSize = 21;
            value.fontStyle = FontStyle.Normal;
            value.resizeTextForBestFit = false;
            value.color = Color.black;
            value.alignment = TextAnchor.MiddleLeft;
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            value.verticalOverflow = VerticalWrapMode.Overflow;
            value.raycastTarget = false;
            value.text = "0.0%";
            hud.compactPaintProgress = true;
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(value);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void BuildLayers()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(Art + "PaintRatioConcept.png")))
                    throw new IOException("Cannot decode the supplied paint ratio PNG.");
                var pixels = source.GetPixels32();
                // The source also contains a baked black "7.9%" on transparency.
                // Keep only the largest connected alpha component: the actual paper bar.
                var visited = new bool[pixels.Length];
                var queue = new System.Collections.Generic.List<int>();
                int minX = 0, minY = 0, maxX = -1, maxY = -1, largest = 0;
                for (int seed = 0; seed < pixels.Length; seed++)
                {
                    if (visited[seed] || pixels[seed].a == 0) continue;
                    queue.Clear(); queue.Add(seed); visited[seed] = true;
                    int left = source.width, bottom = source.height, right = -1, top = -1;
                    for (int at = 0; at < queue.Count; at++)
                    {
                        int index = queue[at], x = index % source.width, y = index / source.width;
                        left = Math.Min(left, x); bottom = Math.Min(bottom, y);
                        right = Math.Max(right, x); top = Math.Max(top, y);
                        for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= source.width || ny < 0 || ny >= source.height) continue;
                            int next = ny * source.width + nx;
                            if (visited[next] || pixels[next].a == 0) continue;
                            visited[next] = true; queue.Add(next);
                        }
                    }
                    if (queue.Count <= largest) continue;
                    largest = queue.Count; minX = left; minY = bottom; maxX = right; maxY = top;
                }
                if (largest == 0) throw new IOException("The supplied paint ratio PNG is empty.");
                int width = maxX - minX + 1, height = maxY - minY + 1;
                var empty = new Color32[width * height];
                var mask = new Color32[empty.Length];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Color32 pixel = pixels[(y + minY) * source.width + x + minX];
                    bool red = x < 80 && pixel.r > pixel.g * 1.5f && pixel.r > pixel.b * 1.8f;
                    Color32 paper = pixel;
                    if (red)
                    {
                        // Continue the adjacent paper texture beneath the baked example fill.
                        paper = pixels[(y + minY) * source.width + minX + Math.Min(x + 72, width - 1)];
                        paper.a = (byte)(paper.a * pixel.a / 255);
                    }
                    empty[y * width + x] = paper;
                    bool ink = pixel.r < 20 && pixel.g < 20 && pixel.b < 20;
                    // The paper is translucent, while the supplied painted portion is opaque.
                    byte alpha = ink ? (byte)0 : red ? pixel.a : (byte)Math.Min(255, pixel.a * 2);
                    mask[y * width + x] = new Color32(255, 255, 255, alpha);
                }
                SaveLayer(width, height, empty, "PaintRatioEmpty");
                SaveLayer(width, height, mask, "PaintRatioFillMask");
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        private static void SaveLayer(int width, int height, Color32[] pixels, string name)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels); texture.Apply();
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
