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
            string[] required =
            {
                "BottomHudBackground", "StaminaBar", "StaminaFill1", "StaminaFill2", "StaminaFill3",
                "StaminaFill4", "StaminaFill5", "StaminaFillColor1", "StaminaFillColor2",
                "StaminaFillColor3", "StaminaFillColor4", "StaminaFillColor5", "PlayerHpBar",
                "PlayerHpFill", "PlayerHpFillColor", "GenerateButton"
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
            if (hud.generateButton == null) throw new InvalidOperationException("LabHud.generateButton is missing.");

            RectTransform battle = EnsureRect(canvas.transform, "FigmaBattleUi");
            Stretch(battle);
            hud.battleUi = battle.gameObject;

            RectTransform background = EnsureRect(battle, "BottomHudBackground");
            background.gameObject.SetActive(true);
            PlaceGuide(background, 0f, 747f, 402f, 127f);
            SetImage(background, Sprite("BottomHudBackground"), false);
            background.SetAsLastSibling();

            RectTransform stamina = EnsureRect(battle, "FigmaStaminaBar");
            stamina.gameObject.SetActive(true);
            ClearChildrenExcept(stamina);
            PlaceGuide(stamina, 6f, 766f, 159.737f, 99f);
            RemoveImage(stamina);
            stamina.SetAsLastSibling();

            RectTransform staminaFrame = EnsureRect(stamina, "StaminaFrame");
            staminaFrame.gameObject.SetActive(true);
            PlaceLocal(staminaFrame, 0f, 0f, 159.737f, 99f);
            SetImage(staminaFrame, Sprite("StaminaBar"), false);
            staminaFrame.SetAsFirstSibling();

            hud.staminaCells = new Image[5];
            AddStaminaCell(stamina, hud, 0, "StaminaFillColor1");
            AddStaminaCell(stamina, hud, 1, "StaminaFillColor2");
            AddStaminaCell(stamina, hud, 2, "StaminaFillColor3");
            AddStaminaCell(stamina, hud, 3, "StaminaFillColor4");
            AddStaminaCell(stamina, hud, 4, "StaminaFillColor5");

            RectTransform playerHp = EnsureRect(battle, "FigmaPlayerHpBar");
            playerHp.gameObject.SetActive(true);
            ClearChildrenExcept(playerHp, hud.playerHpText == null ? null : hud.playerHpText.transform);
            PlaceGuide(playerHp, 231f, 768f, 163f, 96f);
            RemoveImage(playerHp);
            playerHp.SetAsLastSibling();

            RectTransform hpFrame = EnsureRect(playerHp, "PlayerHpFrame");
            hpFrame.gameObject.SetActive(true);
            PlaceLocal(hpFrame, 0f, 0f, 163f, 96f);
            SetImage(hpFrame, Sprite("PlayerHpBar"), false);
            hpFrame.SetAsFirstSibling();

            RectTransform hpFill = EnsureRect(playerHp, "PlayerHpFill");
            hpFill.gameObject.SetActive(true);
            PlaceLocal(hpFill, 0f, 0f, 163f, 96f);
            RemoveMask(hpFill);
            Image hpImage = SetImage(hpFill, Sprite("PlayerHpFillColor"), false);
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
            if (hud.generateLabel != null) hud.generateLabel.gameObject.SetActive(false);

            RectTransform generate = hud.generateButton.GetComponent<RectTransform>();
            generate.gameObject.SetActive(true);
            generate.SetParent(battle, false);
            generate.name = "FigmaGenerateButton";
            ClearChildrenExcept(generate, hud.generateLabel == null ? null : hud.generateLabel.transform);
            PlaceGuide(generate, 152f, 776f, 98f, 98f);
            Image generateImage = SetImage(generate, Sprite("GenerateButton"), true);
            generateImage.preserveAspect = false;
            hud.generateButton.targetGraphic = generateImage;
            generate.SetAsLastSibling();

            background.SetAsFirstSibling();
            playerHp.SetSiblingIndex(background.GetSiblingIndex() + 1);
            stamina.SetSiblingIndex(playerHp.GetSiblingIndex() + 1);
            generate.SetAsLastSibling();

            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Failed to save " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("C6_LAB_BOTTOM_HUD_DESIGN_APPLIED scene=" + ScenePath);
        }

        private static void AddStaminaCell(RectTransform parent, LabHud hud, int index, string spriteName)
        {
            RectTransform cell = EnsureRect(parent, "StaminaCell" + (index + 1));
            cell.gameObject.SetActive(true);
            PlaceLocal(cell, 0f, 0f, 159.737f, 99f);
            RemoveMask(cell);
            Image image = SetImage(cell, Sprite(spriteName), false);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;
            image.preserveAspect = false;
            hud.staminaCells[index] = image;
            cell.SetAsLastSibling();
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
