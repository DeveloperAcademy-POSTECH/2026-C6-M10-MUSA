using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>
    /// Incrementally places the Figma UI Guide on the saved Lab Canvas. Existing HUD objects and
    /// references are moved, never replaced. Safe to re-run after a designer adjusts other objects.
    /// </summary>
    public static class LabFigmaUiUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const string ArtPath = "Assets/Lab/Art/UI/";
        private const float GuideWidth = 402f;
        private const float GuideHeight = 874f;
        private static readonly Color Ink = new Color32(67, 48, 37, 255);
        private static readonly Color Cream = new Color32(242, 216, 181, 255);
        private static readonly Color Dark = new Color32(43, 35, 39, 255);

        [MenuItem("C6 Lab/Apply Figma UI Guide")]
        public static void Apply()
        {
            string[] required = { "BoardPlate", "PauseButton", "TimeFrame", "YokaiHpFrame",
                "StaminaBar", "StaminaBall", "StaminaSocket", "GenerateButton", "NeighborBadge", "SolidFill" };
            foreach (string name in required)
                if (!File.Exists(ArtPath + name + ".png"))
                    throw new FileNotFoundException("Missing transparent Figma UI sprite: " + name, ArtPath + name + ".png");
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Saved Lab Scene is missing.", ScenePath);

            foreach (string name in required)
                if (name != "BoardPlate") PrepareSprite(ArtPath + name + ".png");
            PrepareBoardTexture(ArtPath + "BoardPlate.png");
            Sprite pauseArt = RequireSprite("PauseButton");
            Sprite timeArt = RequireSprite("TimeFrame");
            Sprite healthFrame = RequireSprite("YokaiHpFrame");
            Sprite solidFill = RequireSprite("SolidFill");
            Sprite staminaFrame = RequireSprite("StaminaBar");
            Sprite staminaBall = RequireSprite("StaminaBall");
            Sprite emptySocket = RequireSprite("StaminaSocket");
            Sprite neighborArt = RequireSprite("NeighborBadge");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject canvas = GameObject.Find("LabCanvas");
            if (canvas == null || canvas.GetComponent<LabHud>() == null)
                throw new InvalidOperationException("The saved Lab Canvas/HUD is missing.");
            LabHud hud = canvas.GetComponent<LabHud>();
            Require(hud.network, "LabNetwork");
            Require(hud.hpText, "YokaiHpText");
            Require(hud.playerHpText, "PlayerHpText");
            Require(hud.timeText, "BattleTimeText");

            RectTransform battle = EnsureRect(canvas.transform, "FigmaBattleUi");
            Stretch(battle);
            hud.battleUi = battle.gameObject;

            RectTransform stamina = EnsureRect(battle, "FigmaStaminaBar");
            PlaceGuide(stamina, 98f, 758f, 221f, 79f);
            SetImage(stamina, staminaFrame, false);
            // In the guide, the first blue ball is a fixed marker. The other five holes show
            // five 20-point portions of the runtime stamina value (which can be DEV tuned).
            float[] slots = { 32f, 59.5f, 86.7f, 114f, 141.5f, 169f };
            for (int i = 0; i < slots.Length; i++)
            {
                RectTransform socket = EnsureRect(stamina, "StaminaSocket" + (i + 1));
                // Cover the full baked blue circle, including its antialiased edge.
                PlaceLocal(socket, slots[i] - 2f, 26f, 27f, 27f);
                SetImage(socket, emptySocket, false);
            }
            RectTransform marker = EnsureRect(stamina, "StaminaMarker");
            PlaceLocal(marker, slots[0], 28f, 23f, 23f);
            SetImage(marker, staminaBall, false);
            hud.staminaCells = new UnityEngine.UI.Image[5];
            for (int i = 1; i < slots.Length; i++)
            {
                RectTransform cell = EnsureRect(stamina, "StaminaCell" + i);
                PlaceLocal(cell, slots[i], 28f, 23f, 23f);
                UnityEngine.UI.Image image = SetImage(cell, staminaBall, false);
                image.type = UnityEngine.UI.Image.Type.Filled;
                image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                image.fillOrigin = 0;
                hud.staminaCells[i - 1] = image;
            }

            RectTransform yokai = EnsureRect(battle, "FigmaYokaiHpBar");
            PlaceGuide(yokai, 71f, 87f, 282f, 72f);
            ConfigureHealthBar(yokai, healthFrame, solidFill, hud.hpText,
                "YOKAI HP", new Color32(226, 61, 59, 255), out hud.yokaiHpFill);

            RectTransform player = EnsureRect(battle, "FigmaPlayerHpBar");
            PlaceGuide(player, 101f, 794f, 282f, 72f);
            ConfigureHealthBar(player, healthFrame, solidFill, hud.playerHpText,
                "MY HP", new Color32(219, 73, 83, 255), out hud.playerHpFill);

            RectTransform time = EnsureRect(battle, "FigmaTimeFrame");
            PlaceGuide(time, 16f, 15f, 127f, 44f);
            SetImage(time, timeArt, false);
            // The Figma instance has a baked 03:00. Mask only its digit slot so the clock can
            // display the Host's actual remaining time without painting over its hourglass.
            RectTransform clockMask = EnsureRect(time, "ClockValueMask");
            PlaceLocal(clockMask, 48f, 10f, 62f, 25f);
            UnityEngine.UI.Image maskImage = SetImage(clockMask, null, false);
            maskImage.color = Cream;
            PlaceText(hud.timeText, time, 48f, 8f, 66f, 28f, 16, Ink);
            hud.timeText.fontStyle = FontStyle.Bold;

            RectTransform pause = EnsureRect(battle, "FigmaPauseButton");
            PlaceGuide(pause, 332f, 9f, 53f, 55f);
            hud.pauseButton = SetButton(pause, pauseArt);

            RectTransform left = EnsureRect(battle, "LeftNeighborBadge");
            PlaceGuide(left, -21f, 398f, 85f, 85f);
            SetImage(left, neighborArt, false);
            hud.leftNeighborBadge = left.gameObject;
            hud.leftNeighborText = EnsureText(left, "PNumber", "P—", 22, Ink);
            PlaceLocal(hud.leftNeighborText.rectTransform, 20f, 18f, 45f, 40f);

            RectTransform right = EnsureRect(battle, "RightNeighborBadge");
            PlaceGuide(right, 347f, 398f, 85f, 85f);
            SetImage(right, neighborArt, false);
            hud.rightNeighborBadge = right.gameObject;
            hud.rightNeighborText = EnsureText(right, "PNumber", "P—", 22, Ink);
            PlaceLocal(hud.rightNeighborText.rectTransform, 20f, 18f, 45f, 40f);

            // Orbs are generated by tapping the drum on the board; the battle UI has no button.
            // The guide layers the player HP frame behind the overlapping stamina ornament.
            // This order also keeps their runtime values readable.
            player.SetAsFirstSibling();
            stamina.SetAsLastSibling();

            // Keep all saved references, including lobby-only diagnostics, while removing their
            // previous opaque blocks from the battle composition.
            SetInactive(canvas.transform.Find("BattleHeader"));
            SetInactive(canvas.transform.Find("BoardControls"));
            SetInactive(hud.sessionControls == null ? null : hud.sessionControls.transform);
            SetInactive(hud.statusText == null ? null : hud.statusText.transform);
            SetInactive(canvas.transform.Find("DeveloperModeButton"));
            if (hud.noticeText != null)
                PlaceGuide(hud.noticeText.rectTransform, 14f, 372f, 374f, 30f);

            ConfigurePauseMenu(canvas.transform, hud);
            ConfigureResultControls(canvas.transform, hud);
            ConfigurePlate();
            battle.gameObject.SetActive(true); // visible for Scene editing; LabHud hides it outside battle.

            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Failed to save the Figma Lab UI scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("C6_LAB_FIGMA_UI_APPLIED scene=" + ScenePath);
        }

        private static void ConfigureHealthBar(RectTransform root, Sprite frame, Sprite solidFill,
            UnityEngine.UI.Text value, string title, Color fillColor, out UnityEngine.UI.Image fill)
        {
            SetImage(root, frame, false);
            // Figma artwork has a static red fill. Hide the full interior before drawing a
            // runtime-driven fill; otherwise the old red remains after HP decreases.
            RectTransform trackMask = EnsureRect(root, "TrackMask");
            PlaceLocal(trackMask, 32f, 31f, 230f, 20f);
            SetImage(trackMask, null, false).color = new Color32(214, 199, 179, 255);
            RectTransform gauge = EnsureRect(root, "DynamicFill");
            PlaceLocal(gauge, 32f, 31f, 220f, 20f);
            fill = SetImage(gauge, solidFill, false);
            fill.preserveAspect = false;
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.color = fillColor;

            RectTransform titleMask = EnsureRect(root, "TitleMask");
            PlaceLocal(titleMask, 33f, 8f, 83f, 23f);
            SetImage(titleMask, null, false).color = Cream;
            UnityEngine.UI.Text titleText = EnsureText(titleMask, "Title", title, 13, Ink);
            Stretch(titleText.rectTransform);
            titleText.fontStyle = FontStyle.Bold;

            PlaceText(value, root, 78f, 29f, 150f, 24f, 13, Ink);
            value.fontStyle = FontStyle.Bold;
        }

        private static void ConfigurePauseMenu(Transform canvas, LabHud hud)
        {
            RectTransform overlay = EnsureRect(canvas, "FigmaPauseMenu");
            Stretch(overlay);
            SetImage(overlay, null, true).color = new Color(0f, 0f, 0f, .72f);
            overlay.SetAsLastSibling();
            hud.pauseMenu = overlay.gameObject;

            RectTransform panel = EnsureRect(overlay, "PausePanel");
            PlaceGuide(panel, 34f, 253f, 334f, 365f);
            SetImage(panel, null, true).color = new Color32(221, 187, 144, 255);
            RectTransform inner = EnsureRect(panel, "Inner");
            PlaceLocal(inner, 7f, 7f, 320f, 351f);
            SetImage(inner, null, false).color = new Color32(248, 233, 205, 255);

            hud.pauseStatusText = EnsureText(panel, "PauseStatus", "PAUSED", 24, Dark);
            PlaceLocal(hud.pauseStatusText.rectTransform, 28f, 29f, 278f, 42f);
            hud.pauseStatusText.fontStyle = FontStyle.Bold;

            hud.resumeButton = PauseAction(panel, "ResumeButton", "CONTINUE", 29f, 107f);
            hud.returnLobbyButton = PauseAction(panel, "ReturnLobbyButton", "RETURN ALL TO LOBBY", 29f, 177f);
            hud.developerSettingsButton = PauseAction(panel, "DeveloperSettingsButton", "DEV SETTINGS / RESTART", 29f, 247f);
            hud.developerMode = canvas.GetComponent<LabDeveloperMode>();
            overlay.gameObject.SetActive(false);
        }

        private static void ConfigureResultControls(Transform canvas, LabHud hud)
        {
            // The old lobby block occupies y126–262 and overlaps the Figma yokai HP frame.
            // Keep its original layout for the lobby, and expose only the result actions here.
            RectTransform panel = EnsureRect(canvas, "FigmaResultControls");
            PlaceGuide(panel, 54f, 505f, 294f, 166f);
            SetImage(panel, null, true).color = new Color32(221, 187, 144, 250);
            RectTransform inner = EnsureRect(panel, "Inner");
            PlaceLocal(inner, 6f, 6f, 282f, 154f);
            SetImage(inner, null, false).color = new Color32(248, 233, 205, 255);

            hud.resultControls = panel.gameObject;
            hud.resultStatusText = EnsureText(panel, "ResultStatus", "ROUND COMPLETE", 19, Dark);
            PlaceLocal(hud.resultStatusText.rectTransform, 12f, 11f, 270f, 39f);
            hud.resultStatusText.fontStyle = FontStyle.Bold;
            hud.resultRestartButton = PauseAction(panel, "RestartButton", "RESTART", 9f, 53f);
            hud.resultReturnLobbyButton = PauseAction(panel, "ReturnAllButton", "RETURN ALL TO LOBBY", 9f, 109f);
            hud.resultLeaveButton = PauseAction(panel, "LeaveRoomButton", "LEAVE ROOM", 9f, 109f);
            panel.SetAsLastSibling();
            panel.gameObject.SetActive(false);
        }

        private static UnityEngine.UI.Button PauseAction(Transform parent, string name, string text,
            float x, float y)
        {
            RectTransform rect = EnsureRect(parent, name);
            PlaceLocal(rect, x, y, 276f, 52f);
            UnityEngine.UI.Image image = SetImage(rect, null, true);
            image.color = new Color32(144, 86, 63, 255);
            UnityEngine.UI.Button button = SetButton(rect, null);
            button.targetGraphic = image;
            UnityEngine.UI.Text label = EnsureText(rect, "Label", text, 16, Color.white);
            Stretch(label.rectTransform);
            label.fontStyle = FontStyle.Bold;
            return button;
        }

        private static void ConfigurePlate()
        {
            GameObject plate = GameObject.Find("OrbBoardPlate");
            if (plate == null || plate.GetComponent<MeshRenderer>() == null)
                throw new InvalidOperationException("Existing world-space OrbBoardPlate is missing.");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + "BoardPlate.png");
            if (texture == null) throw new InvalidOperationException("Imported BoardPlate texture is missing.");
            const string materialPath = ArtPath + "FigmaBoardPlate.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
                if (shader == null) throw new InvalidOperationException("No transparent unlit shader is available for the plate.");
                material = new Material(shader) { name = "FigmaBoardPlate" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.mainTexture = texture;
            material.color = Color.white;
            EditorUtility.SetDirty(material);
            plate.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void PrepareSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Cannot import Figma PNG: " + path);
            if (importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single && !importer.mipmapEnabled
                && importer.alphaIsTransparency && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.maxTextureSize >= 4096) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }

        private static void PrepareBoardTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Cannot import Figma board plate: " + path);
            if (importer.textureType == TextureImporterType.Default && importer.alphaIsTransparency
                && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed)
                return;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static Sprite RequireSprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + name + ".png");
            if (sprite == null) throw new InvalidOperationException("Figma PNG is not a Sprite: " + name);
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

        private static UnityEngine.UI.Image SetImage(RectTransform rect, Sprite sprite, bool raycast)
        {
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (image == null) image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = sprite != null;
            image.raycastTarget = raycast;
            return image;
        }

        private static UnityEngine.UI.Button SetButton(RectTransform rect, Sprite sprite)
        {
            UnityEngine.UI.Image image = rect.GetComponent<UnityEngine.UI.Image>();
            if (image == null || sprite != null) image = SetImage(rect, sprite, true);
            image.raycastTarget = true;
            UnityEngine.UI.Button button = rect.GetComponent<UnityEngine.UI.Button>();
            if (button == null) button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            return button;
        }

        private static UnityEngine.UI.Text EnsureText(Transform parent, string name, string content,
            int size, Color color)
        {
            RectTransform rect = EnsureRect(parent, name);
            UnityEngine.UI.Text text = rect.GetComponent<UnityEngine.UI.Text>();
            if (text == null) text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.text = content;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static void PlaceText(UnityEngine.UI.Text text, Transform parent,
            float x, float y, float width, float height, int size, Color color)
        {
            text.transform.SetParent(parent, false);
            PlaceLocal(text.rectTransform, x, y, width, height);
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
        }

        private static void PlaceGuide(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(x / GuideWidth, 1f - (y + height) / GuideHeight);
            rect.anchorMax = new Vector2((x + width) / GuideWidth, 1f - y / GuideHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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

        private static void SetInactive(Transform target)
        {
            if (target != null) target.gameObject.SetActive(false);
        }

        private static void Require(UnityEngine.Object value, string name)
        {
            if (value == null) throw new InvalidOperationException("Saved Scene reference is missing: " + name);
        }
    }
}
