using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace C6Lab.EditorBootstrap
{
    public static class LabFigmaBattleUiBuilder
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const string AssetRoot = "Assets/Lab/Art/FigmaBattleUI";
        private static readonly Vector2 DesignSize = new Vector2(402f, 874f);

        public static void ApplyToLabScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject canvasObject = GameObject.Find("LabCanvas");
            if (canvasObject == null) throw new InvalidOperationException("LabCanvas not found.");
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = DesignSize;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
            }
            RectTransform old = canvasObject.transform.Find("FigmaBattleUI") as RectTransform;
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            RectTransform root = CreateRect("FigmaBattleUI", canvasObject.transform, Vector2.zero, Vector2.one);
            root.SetAsFirstSibling();
            var rootImage = root.gameObject.AddComponent<Image>();
            rootImage.color = new Color32(222, 226, 213, 255);
            rootImage.raycastTarget = false;
            ColorBlock("FigmaBattleBackground", root, new Rect(0f, 263f, 402f, 611f), new Color32(236, 216, 187, 255));
            AddImage(root, "MountainBack", "mountain", new Rect(99f, 9f, 98f, 51f));
            AddImage(root, "MountainLeft", "mountain", new Rect(20f, 191f, 164f, 90f));
            AddImage(root, "MountainRight", "mountain", new Rect(234f, 183f, 164f, 90f));
            AddImage(root, "Clouds", "clouds", new Rect(-51f, 11f, 361f, 243f));
            AddImage(root, "TimerFrame", "timer", new Rect(14f, 21f, 127f, 44f));
            AddImage(root, "PauseButtonArt", "pause_button", new Rect(351f, 21f, 39f, 40f));
            AddImage(root, "Gumiho2D", "gumiho", new Rect(69f, 148f, 271f, 257.02f));
            var monsterBack = AddImage(root, "MonsterHpFrame", "monster_hp_empty", new Rect(75f, 79f, 268f, 69f));
            var monsterFill = ColorBlock("MonsterHpFill", root, new Rect(127f, 111f, 174f, 20f), new Color32(223, 66, 59, 255));
            ConfigureFill(monsterFill);
            monsterFill.transform.SetSiblingIndex(monsterBack.transform.GetSiblingIndex() + 1);
            AddImage(root, "OrbBoardArt", "orb_board", new Rect(-23f, 420f, 449f, 412f));
            AddImage(root, "PlayerBack", "player_back", new Rect(12f, 625f, 155f, 164.69f));
            var playerBack = AddImage(root, "PlayerHpFrame", "player_hp_full", new Rect(115f, 716f, 284f, 70f));
            var playerFill = ColorBlock("PlayerHpFill", root, new Rect(202f, 748f, 146f, 20f), new Color32(223, 66, 59, 255));
            ConfigureFill(playerFill);
            playerFill.transform.SetSiblingIndex(playerBack.transform.GetSiblingIndex() + 1);
            var staminaBack = AddImage(root, "StaminaFrame", "stamina_full", new Rect(92f, 805f, 221f, 79f));
            var staminaFill = ColorBlock("StaminaFill", root, new Rect(126f, 833f, 62f, 21f), new Color32(45, 135, 228, 255));
            ConfigureFill(staminaFill);
            staminaFill.transform.SetSiblingIndex(staminaBack.transform.GetSiblingIndex() + 1);
            AddImage(root, "OrbFire", "orb_fire", new Rect(34f, 448f, 42f, 42f));
            AddImage(root, "OrbWood", "orb_wood", new Rect(326f, 448f, 42f, 42f));
            AddImage(root, "OrbWater", "orb_water", new Rect(113f, 454f, 42f, 42f));
            AddImage(root, "OrbMetal", "orb_metal", new Rect(247f, 454f, 42f, 42f));
            AddImage(root, "OrbEarth", "orb_earth", new Rect(180f, 454f, 42f, 42f));
            LabHud hud = canvasObject.GetComponent<LabHud>();
            if (hud == null) throw new InvalidOperationException("LabHud not found.");
            hud.monsterHpFill = monsterFill;
            hud.playerHpFill = playerFill;
            hud.staminaFill = staminaFill;
            PositionText(hud.hpText, new Rect(112f, 93f, 76f, 20f), 12, TextAnchor.MiddleCenter);
            PositionText(hud.timeText, new Rect(62f, 35f, 46f, 20f), 14, TextAnchor.MiddleCenter);
            PositionText(hud.staminaText, new Rect(124f, 812f, 140f, 20f), 12, TextAnchor.MiddleCenter);
            PositionText(hud.orbsText, new Rect(200f, 474f, 88f, 20f), 12, TextAnchor.MiddleCenter);
            PositionText(hud.seatText, new Rect(130f, 720f, 40f, 20f), 12, TextAnchor.MiddleCenter);
            if (hud.generateButton != null)
            {
                SetTopLeft(hud.generateButton.GetComponent<RectTransform>(), new Rect(111f, 704f, 180.47f, 140f));
                var image = hud.generateButton.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = Sprite("generate_button");
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                    image.color = Color.white;
                    image.raycastTarget = true;
                }
                hud.generateButton.transform.SetAsLastSibling();
                if (hud.generateLabel != null) hud.generateLabel.text = string.Empty;
            }
            HideLegacyBattleChrome(canvasObject.transform);
            HideTargetRenderers();
            EnsureEventSystem();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("C6_LAB_FIGMA_UI_APPLIED");
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image AddImage(Transform parent, string name, string spriteName, Rect designRect)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f));
            SetTopLeft(rect, designRect);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Sprite(spriteName);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        private static Image ColorBlock(string name, Transform parent, Rect designRect, Color color)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f));
            SetTopLeft(rect, designRect);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void SetTopLeft(RectTransform rect, Rect designRect)
        {
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(designRect.x, -designRect.y);
            rect.sizeDelta = new Vector2(designRect.width, designRect.height);
        }

        private static void ConfigureFill(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;
        }

        private static Sprite Sprite(string name)
        {
            string path = $"{AssetRoot}/{name}.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new FileNotFoundException("Missing Figma sprite", path);
            return sprite;
        }

        private static void PositionText(Text text, Rect designRect, int size, TextAnchor alignment)
        {
            if (text == null) return;
            SetTopLeft(text.GetComponent<RectTransform>(), designRect);
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color32(74, 56, 45, 255);
            text.raycastTarget = false;
            text.transform.SetAsLastSibling();
        }

        private static void HideLegacyBattleChrome(Transform canvasRoot)
        {
            foreach (string name in new[] { "BattleHeader", "BoardControls", "BoardAreaTint" })
            {
                Transform target = canvasRoot.Find(name);
                if (target == null) continue;
                foreach (Graphic graphic in target.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic is Image image) image.color = new Color(1f, 1f, 1f, 0f);
                    graphic.raycastTarget = false;
                }
            }
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = go.GetComponent<EventSystem>();
            }
            foreach (InputSystemUIInputModule module in eventSystem.GetComponents<InputSystemUIInputModule>())
                module.enabled = false;

            StandaloneInputModule standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone == null) standalone = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            standalone.enabled = true;
        }

        private static void HideTargetRenderers()
        {
            GameObject target = GameObject.Find("Cylinder Target");
            if (target == null) return;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }
    }
}
