using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio.Editor
{
    /// <summary>Figma 3908/4062 presentation migration, called by the existing lobby authoring menu.</summary>
    public static class GameSeatDesignUpgrade
    {
        private const string Art = "Assets/Hapio/Art/UI/Seats/";

        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (string path in Directory.GetFiles(Art, "*.png"))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100; importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            GameObject root = PrefabUtility.LoadPrefabContents(GameLobbySceneUpgrade.PrefabPath);
            try
            {
                var view = root.GetComponent<GameLobbyView>();
                var design = root.GetComponent<GameLobbySeatDesign>() ?? root.AddComponent<GameLobbySeatDesign>();
                view.seatDesign = design;
                design.paper = view.positionPage.transform.Find("RoomPaper").GetComponent<Image>();
                design.storyPaper = Sprite("09a14"); design.teamPaper = Sprite("90e0e"); design.sixPlayerPaper = Sprite("1f1fa");
                design.paper.color = Color.white; design.paper.type = Image.Type.Simple;
                var medium = view.mediumFont;
                var bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
                Style(view.heading, bold, 36, GameLobbySeatColors.Ink);
                Style(view.subheading, medium, 18, GameLobbySeatColors.Muted);
                Style(view.roomName, medium, 25, GameLobbySeatColors.Ink, TextAnchor.MiddleLeft);
                Style(view.joinedCount, medium, 18, GameLobbySeatColors.Brown, TextAnchor.MiddleLeft);
                Style(view.selectedCount, medium, 18, GameLobbySeatColors.Brown, TextAnchor.MiddleRight);
                Place(view.leaveButton.transform, 27, 56, 115, 44);
                view.leaveButton.targetGraphic.color = Color.clear;
                var leave = view.leaveButton.GetComponentInChildren<Text>();
                Style(leave, medium, 22, GameLobbySeatColors.Ink, TextAnchor.MiddleLeft);
                leave.text = "< 방 나가기"; Place(leave.transform, 0, 9, 115, 26);
                view.emptySlots = Sprites("e327d", "c701d", "6a571", "7e76b", "aac1f", "c701d");
                view.filledSlots = Sprites("c7fa7", "a3291", "1fdc3", "e006a", "b2348", "82ba1");
                view.blueTeamEmptySlot = view.emptySlots[0]; view.redTeamEmptySlot = view.emptySlots[1];
                view.blueTeamFilledSlot = view.filledSlots[0]; view.redTeamFilledSlot = view.filledSlots[1];
                view.lines = Sprites("5089e", "3c326", "79dac");
                view.connectionLines.color = Color.white;
                SetSeats(view.seats3, 27, 385, new Vector2(144, 198.5f), new Vector2(54, 49), new Vector2(232, 49));
                SetSeats(view.seats4, 27, 385, new Vector2(143, 196), new Vector2(49, 110), new Vector2(143, 24), new Vector2(235, 110));
                SetSeats(view.seats5, 27, 385, new Vector2(144, 192), new Vector2(58, 125), new Vector2(83, 30), new Vector2(207, 30), new Vector2(229, 125));
                LineAnchor(view.lineLayouts[0], 90.108f, 455.856f, "5089e", 180);
                LineAnchor(view.lineLayouts[1], 87.88f, 428.071f, "3c326", 0);
                LineAnchor(view.lineLayouts[2], 108.266f, 438.187f, "79dac", 180);
                SetSeats(view.seatsOneOnOne, 0, 0, new Vector2(76.5f, 524), new Vector2(263.5f, 524));
                SetSeats(view.seatsTeam, 0, 0, new Vector2(76.5f, 477), new Vector2(76.5f, 584), new Vector2(263.5f, 478), new Vector2(263.5f, 583.5f));
                SetSeats(view.seatsThreeOnThree, 0, 0, new Vector2(76.5f, 459), new Vector2(76.5f, 540), new Vector2(76.5f, 621),
                    new Vector2(263.5f, 458), new Vector2(263.5f, 539), new Vector2(263.5f, 620));
                for (int i = 0; i < view.slotButtons.Length; i++)
                {
                    Style(view.slotLabels[i], medium, 24, GameLobbySeatColors.SeatInk);
                    Style(view.localLabels[i], medium, 20, GameLobbySeatColors.Host);
                    view.slotImages[i].color = Color.white; view.slotImages[i].raycastTarget = false;
                    view.slotLabels[i].raycastTarget = false; view.localLabels[i].raycastTarget = false;
                }
                var mode = view.teamLayout.transform.Find("ModeTitle").GetComponent<Text>();
                Style(mode, bold, 30, GameLobbySeatColors.Brown); mode.text = "< 땅따먹기 >";
                Place(mode.transform, 27, 111, 349, 39);
                foreach (GameObject part in view.teamPairParts) part.SetActive(false);
                foreach (GameObject part in view.teamPairLines) part.SetActive(false);
                view.teamLayout.transform.Find("Versus").gameObject.SetActive(false);
                view.teamTrio.SetActive(false);
                design.teamColumns = new GameObject[3];
                for (int size = 1; size <= 3; size++) design.teamColumns[size - 1] = Columns(view, size, medium, bold);
                view.sealButton.GetComponent<LabRoundedGraphic>().radius = 27;
                view.teamHeadingFontSize = 36; view.teamLocalLabelFontSize = 20; view.teamStartFontSize = 20;
                Style(view.sealButton.GetComponentInChildren<Text>(), bold, 20, GameLobbySeatColors.LightText);
                var colors = view.sealButton.colors; colors.disabledColor = Color.white; view.sealButton.colors = colors;
                design.Present(view, 3, false, false, false, false);
                // Save the default three-player layout without touching any scene or session state.
                for (int i = 0; i < 3; i++)
                {
                    CopyRect(view.seats3[i], (RectTransform)view.slotButtons[i].transform);
                    view.slotImages[i].sprite = view.emptySlots[i];
                    Vector2 bounds = view.emptySlots[i].rect.size / 4;
                    Place(view.slotImages[i].transform, (63 - bounds.x) / 2, (63 - bounds.y) / 2, bounds.x, bounds.y);
                }
                EditorUtility.SetDirty(view); EditorUtility.SetDirty(design);
                PrefabUtility.SaveAsPrefabAsset(root, GameLobbySceneUpgrade.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_SEAT_DESIGN_READY: 3/4/5 story seats and both team columns for 1:1/2:2/3:3 saved; session rules unchanged.");
        }

        private static GameObject Columns(GameLobbyView view, int size, Font medium, Font bold)
        {
            Transform parent = view.teamLayout.transform;
            string name = "FigmaTeamColumns" + size;
            Transform group = parent.Find(name);
            if (group == null) { group = new GameObject(name, typeof(RectTransform)).transform; group.SetParent(parent, false); }
            Place(group, 0, 0, 402, 874);
            float cardTop = size == 3 ? 268 : 283, boxTop = cardTop + 141;
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 51 : 238;
                Color color = side == 0 ? GameLobbySeatColors.Blue : GameLobbySeatColors.Red;
                var box = Child<GameDashedRoundedGraphic>(group, side == 0 ? "BlueBox" : "RedBox");
                Place(box.transform, x, boxTop, 114, size == 3 ? side == 0 ? 306 : 316 : 256);
                box.color = color; box.raycastTarget = false;
                var title = Child<Text>(group, side == 0 ? "BlueTeam" : "RedTeam");
                Style(title, medium, 20, color); title.text = side == 0 ? "청팀" : "홍팀";
                Place(title.transform, x, cardTop + 98, 114, 27);
                for (int line = 0; line < 2; line++)
                {
                    var image = Child<Image>(group, (side == 0 ? "BlueLine" : "RedLine") + line);
                    image.sprite = Sprite("5794c"); image.color = Color.white; image.raycastTarget = false;
                    float top = size == 3 ? cardTop + 80 + 21 + 40 + 49 + (line == 0 ? 50 : 129)
                        : cardTop + 80 + 21 + 40 + (side == 0 ? 19 : 20) + 33 + (side == 0 ? 64 : 63);
                    Place(image.transform, x + 55, top, 4, 42.5f);
                    image.gameObject.SetActive(size > 1 && (size == 3 || line == 0));
                }
            }
            var versus = Child<Text>(group, "Versus");
            Style(versus, bold, 30, GameLobbySeatColors.Versus); versus.text = "VS";
            Place(versus.transform, 178, cardTop + 80 + (size == 1 ? 182 : size == 2 ? 188 : 214), 47, 30);
            group.gameObject.SetActive(false);
            return group.gameObject;
        }

        private static void SetSeats(RectTransform[] anchors, float x, float y, params Vector2[] points)
        { for (int i = 0; i < points.Length; i++) Place(anchors[i], x + points[i].x, y + points[i].y, 63, 63); }
        private static void LineAnchor(RectTransform anchor, float x, float y, string asset, float rotation)
        {
            Vector2 size = Sprite(asset).rect.size / 4;
            Place(anchor, x, y, size.x, size.y); anchor.localEulerAngles = new Vector3(0, 0, rotation);
        }
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png")
            ?? throw new InvalidOperationException("Missing Figma seat sprite: " + name);
        private static Sprite[] Sprites(params string[] names) => Array.ConvertAll(names, Sprite);
        private static T Child<T>(Transform parent, string name) where T : Component
        {
            Transform child = parent.Find(name);
            if (child == null) { child = new GameObject(name, typeof(RectTransform)).transform; child.SetParent(parent, false); }
            return child.GetComponent<T>() ?? child.gameObject.AddComponent<T>();
        }
        private static void Style(Text text, Font font, int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            text.font = font; text.fontSize = size; text.color = color; text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false; text.raycastTarget = false;
        }
        private static void Place(Transform target, float x, float y, float w, float h) => LabLobbyView.Place((RectTransform)target, x, y, w, h);
        private static void CopyRect(RectTransform source, RectTransform destination)
        {
            destination.anchorMin = source.anchorMin; destination.anchorMax = source.anchorMax; destination.pivot = source.pivot;
            destination.sizeDelta = source.sizeDelta; destination.anchoredPosition = source.anchoredPosition;
        }
    }
}
