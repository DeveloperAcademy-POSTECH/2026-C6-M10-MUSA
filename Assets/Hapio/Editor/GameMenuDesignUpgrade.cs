using System;
using C6Lab;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio.Editor
{
    /// <summary>Patches the shared menu only; preserves scene bindings, title animation and seat layouts.</summary>
    public static class GameMenuDesignUpgrade
    {
        private const string Art = "Assets/Hapio/Art/UI/Menu/";
        private static readonly Color Ink = LabLobbyView.Html("312B23"), Muted = LabLobbyView.Html("6E6F61"),
            Brown = LabLobbyView.Html("51473B"), Ivory = LabLobbyView.Html("FAF6F1");

        // Called by the existing Hapio > Upgrade 2v2 Lobby UI authoring entry point.
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before changing the saved menu.");
            Import(Art + "BrowserPaper.png", false);
            Import(Art + "BrowserEmptyPaper.png", true);
            GameObject root = PrefabUtility.LoadPrefabContents(GameLobbySceneUpgrade.PrefabPath);
            try
            {
                var view = root.GetComponent<GameLobbyView>();
                Font bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
                view.browserBackground = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "BrowserPaper.png");
                // Only the start button changes; Animated Title keeps its clouds, flag, leaves and logo.
                Place(view.enterButton.transform, 54.5f, 755, 293, 56);
                Button(view.enterButton, "게임 시작", bold, 20);
                Transform page = view.browserPage.transform;
                if (page.GetComponent<RectMask2D>() == null) page.gameObject.AddComponent<RectMask2D>();
                Text(page.Find("Heading"), "혼자선 수습 불가오.\n함께할 동지를 모으시오.", bold, 30, Ink,
                    new Rect(63, 109, 276, 76)).lineSpacing = 1.1259259f;
                Text(page.Find("Wifi"), "같은 와이-파이에 연결해야\n입장할 수 있소.", view.mediumFont, 18, Muted,
                    new Rect(63, 199, 276, 48)).lineSpacing = 1.1851852f;
                Text(page.Find("People"), "동지 수", view.mediumFont, 20, Ink, new Rect(27, 313, 65, 38), TextAnchor.MiddleLeft);
                // All five chips now fit; retain the authored content and buttons/listeners.
                var chips = view.countButtons[0].GetComponentInParent<ScrollRect>(true);
                Place(chips.transform, 99, 313, 276, 38);
                chips.horizontal = chips.vertical = false;
                chips.inertia = false;
                LabLobbyView.Place(chips.content, 0, 0, 276, 38);
                for (int i = 0; i < view.countButtons.Length; i++)
                {
                    Place(view.countButtons[i].transform, 57 * i, 0, 48, 38);
                    Button(view.countButtons[i], (i + 2) + "명", view.mediumFont, 18);
                    Corners(view.countButtons[i].targetGraphic, 16);
                    view.countButtons[i].targetGraphic.color = new Color(232f/255f, 210f/255f, 185f/255f, .7f);
                    view.countButtons[i].GetComponentInChildren<Text>().color = Ink;
                }
                Place(view.createButton.transform, 27, 373, 348, 45);
                Button(view.createButton, "새 방 만들기 +", bold, 20);
                Text(page.Find("OpenRooms"), "열린 방", view.mediumFont, 22, Ink, new Rect(27, 450, 150, 26), TextAnchor.MiddleLeft);
                Place(view.refreshButton.transform, 280, 442, 95, 44);
                Label(view.refreshButton, "다시 찾기", view.mediumFont, 18, Ink);
                Place(view.refreshButton.GetComponentInChildren<Text>().transform, 28, 0, 67, 44);
                Glyph(view.refreshButton.transform, "RefreshGlyph", "↻", new Rect(0, 0, 28, 44), 24);
                Place(view.cancelButton.transform, 252, 442, 123, 44);
                // Keep Direct IP reachable without inserting an extra row into the authored content.
                Place(view.directConnectionButton.transform, 242, 831, 133, 32);
                Label(view.directConnectionButton, "직접 연결", view.mediumFont, 16, Muted);
                view.directConnectionButton.targetGraphic.color = Color.clear;
                Place(view.emptyRooms.transform, 27, 491, 348, 335);
                Transform paper = view.emptyRooms.transform.Find("Paper");
                Place(paper, -2.32168f, -2.95752f, 353.086f, 340.958f);
                var emptyImage = paper.GetComponent<Image>();
                emptyImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "BrowserEmptyPaper.png");
                emptyImage.type = Image.Type.Simple; emptyImage.color = Color.white;
                Text(view.emptyRooms.transform.Find("EmptyTitle"), "아직 모인 동지가 없소..", view.mediumFont, 24, Ink,
                    new Rect(36, 124, 276, 34));
                Text(view.emptyRooms.transform.Find("Hint"), "방을 직접 만들어 보시오.", view.mediumFont, 18, Ink,
                    new Rect(36, 167, 276, 26));
                var rooms = view.roomContent.GetComponentInParent<ScrollRect>(true);
                Place(rooms.transform, 27, 491, 348, 335);
                var group = view.roomContent.GetComponent<VerticalLayoutGroup>();
                group.spacing = 15; group.padding = new RectOffset();
                group.childControlHeight = true; group.childForceExpandHeight = false;
                group.childControlWidth = true; group.childForceExpandWidth = true;
                var row = view.roomTemplate;
                Place(row.transform, 0, 0, 348, 84);
                var layout = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = layout.preferredHeight = 84;
                Button(row, null, view.mediumFont, 20);
                Corners(row.targetGraphic, 23.514f);
                Text(row.transform.Find("Name"), "수장고 1", view.mediumFont, 24, Ink, new Rect(24, 13, 225, 30), TextAnchor.MiddleLeft);
                Text(row.transform.Find("Count"), "1 / 3명 모였소", view.mediumFont, 16, Muted, new Rect(24, 47, 230, 23), TextAnchor.MiddleLeft);
                Text(row.transform.Find("Action"), "입장 →", view.mediumFont, 20, Ink, new Rect(270, 24, 62, 36));
                Glyph(row.transform, "Arrow", "→", new Rect(310, 27, 22, 30), 16);
                Cloud(page, "Browser Cloud Left", new Rect(-250, 167, 337, 112));
                Cloud(page, "Browser Cloud Right", new Rect(286, 31, 235, 78));

                view.modeCard = (RectTransform)view.modePanel.transform.Find("Card");
                // Figma's 50% black overlay blends in sRGB; compensate for Unity's linear UI framebuffer.
                view.modeDismissButton.targetGraphic.color = new Color(0, 0, 0,
                    QualitySettings.activeColorSpace == ColorSpace.Linear ? 1f - Mathf.GammaToLinearSpace(.5f) : .5f);
                Place(view.modeCard, 28, 287, 345, 292);
                var card = Rounded(view.modeCard.GetComponent<Graphic>());
                card.radius = 30;
                card.color = LabLobbyView.Html("EDDBC7");
                Text(view.modeCard.Find("Title"), "어떤 걸로 동지를 부르겠소?", bold, 24, Ink, new Rect(21, 40, 303, 37));
                Text(view.modeCard.Find("Hint"), "하고 싶은 유형을 선택하시오.", view.mediumFont, 18, Muted, new Rect(21, 78, 303, 28));
                Place(view.basicModeButton.transform, 21, 132, 303, 48);
                Place(view.teamModeButton.transform, 21, 193, 303, 48);
                Button(view.basicModeButton, "기본 모드", bold, 20);
                Button(view.teamModeButton, "땅따먹기", bold, 20);
                foreach (var reason in new[] { view.basicModeReason, view.teamModeReason })
                {
                    Text(reason.transform, reason.text, view.mediumFont, 16, Muted, new Rect(16, 253, 313, 25));
                    reason.gameObject.SetActive(false);
                }
                Place(view.teamSoloButton.transform, 111, 600, 180, 34);
                view.modePanel.SetActive(false);
                EditorUtility.SetDirty(view);
                PrefabUtility.SaveAsPrefabAsset(root, GameLobbySceneUpgrade.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_MENU_DESIGN_READY: shared title button, browser and count-dependent mode card saved.");
        }

        private static void Import(string path, bool sprite)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = sprite;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void Cloud(Transform parent, string name, Rect rect)
        {
            Transform target = parent.Find(name);
            if (target == null) { target = new GameObject(name, typeof(RectTransform), typeof(Image)).transform; target.SetParent(parent, false); }
            Place(target, rect.x, rect.y, rect.width, rect.height);
            var image = target.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hapio/Art/Story/Title/5e97a.png");
            image.raycastTarget = false;
            target.SetAsFirstSibling();
        }

        private static void Button(Button button, string label, Font font, int size)
        {
            button.targetGraphic = Rounded(button.targetGraphic);
            Corners(button.targetGraphic, 27);
            button.targetGraphic.color = Brown;
            var colors = button.colors; colors.disabledColor = Color.white; button.colors = colors;
            if (label != null) Label(button, label, font, size, Ivory);
        }

        private static LabRoundedGraphic Rounded(Graphic source)
        {
            if (source is LabRoundedGraphic rounded) return rounded;
            var owner = source.gameObject;
            bool raycast = source.raycastTarget;
            UnityEngine.Object.DestroyImmediate(source);
            var graphic = owner.AddComponent<LabRoundedGraphic>(); graphic.raycastTarget = raycast;
            return graphic;
        }

        // Shilla has no arrow glyphs. Use Unity's bundled font for symbols, keeping Korean in Shilla.
        private static void Glyph(Transform parent, string name, string value, Rect rect, int size)
        {
            Transform target = parent.Find(name);
            if (target == null) { target = new GameObject(name, typeof(RectTransform), typeof(Text)).transform; target.SetParent(parent, false); }
            Text(target, value, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), size, Ink, rect).raycastTarget = false;
        }

        private static void Corners(Graphic graphic, float radius)
        {
            if (graphic is LabRoundedGraphic rounded) { rounded.radius = radius; rounded.SetAllDirty(); }
            else if (graphic is Image image) image.pixelsPerUnitMultiplier = 45f / radius;
        }

        private static void Label(Button button, string value, Font font, int size, Color color)
        {
            var label = button.GetComponentInChildren<Text>(true);
            var rect = (RectTransform)button.transform;
            Text(label.transform, value, font, size, color, new Rect(0, 0, rect.rect.width, rect.rect.height));
        }

        private static Text Text(Transform target, string value, Font font, int size, Color color, Rect rect,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Place(target, rect.x, rect.y, rect.width, rect.height);
            var text = target.GetComponent<Text>();
            text.text = value; text.font = font; text.fontSize = size; text.color = color;
            text.alignment = alignment; text.lineSpacing = 1; text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Place(Transform target, float x, float y, float width, float height)
            => LabLobbyView.Place((RectTransform)target, x, y, width, height);
    }
}
