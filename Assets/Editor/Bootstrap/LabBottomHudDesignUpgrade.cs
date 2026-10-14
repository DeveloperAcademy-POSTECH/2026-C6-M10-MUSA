using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace C6Lab.Editor
{
    /// <summary>Applies the Figma bottom HUD while keeping the saved Lab gameplay references.</summary>
    public static class LabBottomHudDesignUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const string ArtPath = "Assets/Lab/Art/BottomHud/";
        private const float GuideWidth = 402f;
        private const float GuideHeight = 874f;

        [MenuItem("C6 Lab/Apply Bottom HUD Design")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying the bottom HUD.");
            BuildFill("StaminaBar", "StaminaContinuousFill", new Color32(255, 255, 255, 255));
            BuildFill("PlayerHpBar", "PlayerHpContinuousFill", new Color32(255, 255, 255, 255));
            string[] required =
            {
                "BottomHudBackground", "StaminaBar", "StaminaContinuousFill",
                "PlayerHpBar", "PlayerHpContinuousFill"
            };
            foreach (string name in required)
            {
                string path = ArtPath + name + ".png";
                if (!File.Exists(path)) throw new FileNotFoundException("Missing bottom HUD PNG: " + name, path);
                PrepareSprite(path);
            }
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Saved Lab Scene is missing.", ScenePath);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject canvas = GameObject.Find("LabCanvas");
            if (canvas == null) throw new InvalidOperationException("LabCanvas is missing.");
            LabHud hud = canvas.GetComponent<LabHud>();
            if (hud == null) throw new InvalidOperationException("LabCanvas is missing LabHud.");

            RectTransform battle = EnsureRect(canvas.transform, "FigmaBattleUi");
            Stretch(battle);
            hud.battleUi = battle.gameObject;

            RectTransform background = EnsureRect(battle, "BottomHudBackground");
            background.gameObject.SetActive(true);
            PlaceGuide(background, 0f, 797f, 403.511f, 77.29f);
            SetImage(background, Sprite("BottomHudBackground"), false);
            background.SetAsLastSibling();

            // Cover the paper showing through the PNG's translucent bottom edge.
            RectTransform bottomEdge = EnsureRect(background, "BottomEdgeBacking");
            bottomEdge.gameObject.SetActive(true);
            bottomEdge.anchorMin = Vector2.zero;
            bottomEdge.anchorMax = new Vector2(1f, 0f);
            bottomEdge.pivot = new Vector2(.5f, 0f);
            bottomEdge.offsetMin = Vector2.zero;
            bottomEdge.offsetMax = new Vector2(0f, 3f);
            Image edgeImage = SetImage(bottomEdge, null, false);
            var backgroundTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                ImageConversion.LoadImage(backgroundTexture, File.ReadAllBytes(ArtPath + "BottomHudBackground.png"));
                Color edgeColor = backgroundTexture.GetPixel(backgroundTexture.width / 2, backgroundTexture.height / 3);
                edgeColor.a = 1f;
                edgeImage.color = edgeColor;
            }
            finally { UnityEngine.Object.DestroyImmediate(backgroundTexture); }

            RectTransform stamina = EnsureRect(battle, "FigmaStaminaBar");
            stamina.gameObject.SetActive(true);
            ClearChildrenExcept(stamina);
            PlaceGuide(stamina, 4f, 809f, 193.952f, 60.251f);
            RemoveImage(stamina);
            stamina.SetAsLastSibling();

            RectTransform staminaFrame = EnsureRect(stamina, "StaminaFrame");
            staminaFrame.gameObject.SetActive(false);
            Stretch(staminaFrame);
            SetImage(staminaFrame, Sprite("StaminaBar"), false);
            staminaFrame.SetAsFirstSibling();

            hud.staminaCells = Array.Empty<Image>();
            RectTransform staminaFill = EnsureRect(stamina, "StaminaContinuousFill");
            Stretch(staminaFill);
            LabShapeFillImage staminaImage = staminaFill.gameObject.AddComponent<LabShapeFillImage>();
            staminaImage.sprite = Sprite("StaminaContinuousFill");
            staminaImage.filledColor = new Color32(0x46, 0x78, 0xB1, 255);
            staminaImage.raycastTarget = false;
            staminaImage.type = Image.Type.Filled;
            staminaImage.fillMethod = Image.FillMethod.Horizontal;
            staminaImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            staminaImage.fillAmount = 1f;
            hud.staminaFill = staminaImage;

            RectTransform playerHp = EnsureRect(battle, "FigmaPlayerHpBar");
            playerHp.gameObject.SetActive(true);
            ClearChildrenExcept(playerHp, hud.playerHpText == null ? null : hud.playerHpText.transform);
            PlaceGuide(playerHp, 202f, 810f, 194f, 59f);
            RemoveImage(playerHp);
            playerHp.SetAsLastSibling();

            RectTransform hpFrame = EnsureRect(playerHp, "PlayerHpFrame");
            hpFrame.gameObject.SetActive(false);
            Stretch(hpFrame);
            SetImage(hpFrame, Sprite("PlayerHpBar"), false);
            hpFrame.SetAsFirstSibling();

            RectTransform hpFill = EnsureRect(playerHp, "PlayerHpFill");
            hpFill.gameObject.SetActive(true);
            Stretch(hpFill);
            RemoveMask(hpFill);
            LabShapeFillImage hpImage = hpFill.gameObject.AddComponent<LabShapeFillImage>();
            hpImage.sprite = Sprite("PlayerHpContinuousFill");
            hpImage.filledColor = new Color32(0xCB, 0x4B, 0x45, 255);
            hpImage.raycastTarget = false;
            hpImage.type = Image.Type.Filled;
            hpImage.fillMethod = Image.FillMethod.Horizontal;
            hpImage.fillOrigin = (int)Image.OriginHorizontal.Right;
            hpImage.fillAmount = 1f;
            hpImage.preserveAspect = false;
            hud.playerHpFill = hpImage;
            hpFill.SetAsLastSibling();

            if (hud.playerHpText != null)
            {
                hud.playerHpText.transform.SetParent(playerHp, false);
                hud.playerHpText.gameObject.SetActive(false);
            }
            if (hud.staminaText != null) hud.staminaText.gameObject.SetActive(false);
            Transform oldButton = battle.Find("FigmaGenerateButton");
            if (oldButton != null) oldButton.gameObject.SetActive(false);

            background.SetAsFirstSibling();
            playerHp.SetSiblingIndex(background.GetSiblingIndex() + 1);
            stamina.SetSiblingIndex(playerHp.GetSiblingIndex() + 1);

            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Failed to save " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("C6_LAB_BOTTOM_HUD_DESIGN_APPLIED scene=" + ScenePath);
        }

        private static void BuildFill(string sourceName, string outputName, Color32 color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(ArtPath + sourceName + ".png")))
                    throw new IOException("Cannot decode " + sourceName);
                Color32[] pixels = texture.GetPixels32();
                // The original alpha defines both the curved edge and the gaps between cells.
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(color.r, color.g, color.b, pixels[i].a);
                texture.SetPixels32(pixels);
                texture.Apply();
                string path = ArtPath + outputName + ".png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static void PrepareSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Cannot import PNG: " + path);
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                dirty = true;
            }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f))
            {
                importer.spritePixelsPerUnit = 100f;
                dirty = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }
            if (importer.maxTextureSize < 4096)
            {
                importer.maxTextureSize = 4096;
                dirty = true;
            }
            if (dirty) importer.SaveAndReimport();
        }

        private static Sprite Sprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + name + ".png");
            if (sprite == null) throw new InvalidOperationException(name + " is not imported as a Sprite.");
            return sprite;
        }

        private static RectTransform EnsureRect(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found != null)
            {
                RectTransform existing = found.GetComponent<RectTransform>();
                if (existing == null) throw new InvalidOperationException(name + " exists without RectTransform.");
                return existing;
            }
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static Image SetImage(RectTransform rect, Sprite sprite, bool raycast)
        {
            Image image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = raycast;
            return image;
        }

        private static void RemoveImage(RectTransform rect)
        {
            Image image = rect.GetComponent<Image>();
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }

        private static void RemoveMask(RectTransform rect)
        {
            Mask mask = rect.GetComponent<Mask>();
            if (mask != null) UnityEngine.Object.DestroyImmediate(mask);
        }


        private static void ClearChildrenExcept(RectTransform parent, Transform keep = null)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (keep != null && child == keep) continue;
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void PlaceGuide(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(x / GuideWidth, 1f - (y + height) / GuideHeight);
            rect.anchorMax = new Vector2((x + width) / GuideWidth, 1f - y / GuideHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, .5f);
        }

        private static void PlaceLocal(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
