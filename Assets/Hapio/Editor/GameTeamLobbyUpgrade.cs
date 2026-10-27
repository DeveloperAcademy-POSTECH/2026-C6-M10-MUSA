using System;
using System.Linq;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Editor
{
    /// <summary>
    /// 2:2 청홍 땅따먹기 (#281): adds the authored mode popup and the team seat screen to the shared lobby prefab
    /// through Unity, at the Figma positions of the 402x874 design. Existing objects and their edits are kept;
    /// running it again only fills in what is missing. Home/Lobby receive the new objects as prefab instances.
    /// </summary>
    public static class GameTeamLobbyUpgrade
    {
        private const string ArtFolder = "Assets/Hapio/Art/UI/Team/";
        private static readonly Color Ink = LabLobbyView.Html("3F3D38"), Dark = LabLobbyView.Html("3F3D38"),
            Light = LabLobbyView.Html("F3EBDD"), Blue = LabLobbyView.Html("2F62A2"), Red = LabLobbyView.Html("D85836"),
            Line = LabLobbyView.Html("545454");
        private static readonly string[] Shifted = { "Heading", "Hint", "RoomPaper", "RoomName", "Joined", "Selected", "Seal" };
        // The Figma 2:2 screen keeps the story card but adds its mode title above, so these all sit 41 lower.
        private const float ShiftDown = 41f;

        [MenuItem("Hapio/Upgrade 2v2 Lobby UI")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before upgrading saved UI.");
            ImportSprites();
            GameObject root = PrefabUtility.LoadPrefabContents(GameLobbySceneUpgrade.PrefabPath);
            try
            {
                GameLobbyView view = root.GetComponent<GameLobbyView>();
                if (view == null) throw new InvalidOperationException("The shared lobby prefab has no GameLobbyView.");
                BuildModePanel(view);
                BuildTeamScreen(view);
                if (view.teamLobbyDesignRevision < 2) ApplyFigma303(view);
                if (view.teamLobbyDesignRevision < 3) ApplyInk303(view);
                EditorUtility.SetDirty(view);
                PrefabUtility.SaveAsPrefabAsset(root, GameLobbySceneUpgrade.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            // The DEV panel gains its "2:2" tab in the same pass; Battle is never copied again here.
            if (!MenuDeveloperSchemaIsCurrent()) GameSceneBuilder.UpgradeMenuDeveloperSchema();
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_2V2_LOBBY_READY: mode popup, team seats and the 2:2 DEV tab are saved.");
        }

        private static void ImportSprites()
        {
            foreach (string name in new[] { "TeamBoxBlue", "TeamBoxRed", "ModeCard", "ModePill", "TeamLineBlue", "TeamLineRed" })
            {
                string path = ArtFolder + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing 2:2 lobby art: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                // Card/boxes/lines use the original SVGs at 4x; the existing pill stays at 2x.
                importer.spriteBorder = name == "ModeCard" ? new Vector4(120, 120, 120, 120)
                    : name == "ModePill" ? new Vector4(45, 45, 45, 45) : Vector4.zero;
                if (name != "ModePill")
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    foreach (string platform in new[] { "Standalone", "iPhone" })
                    {
                        var settings = importer.GetPlatformTextureSettings(platform);
                        settings.overridden = true; settings.maxTextureSize = 2048;
                        settings.format = TextureImporterFormat.RGBA32;
                        importer.SetPlatformTextureSettings(settings);
                    }
                }
                importer.SaveAndReimport();
            }
        }

        private static Sprite Art(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + name + ".png");
            if (sprite == null) throw new InvalidOperationException("The 2:2 sprite was not imported: " + name);
            return sprite;
        }

        // A presentation-only update must not resave current DEV drafts or unrelated Scene data.
        private static bool MenuDeveloperSchemaIsCurrent()
        {
            Scene previous = SceneManager.GetActiveScene();
            bool current = true;
            foreach (string path in new[] { GameScenePaths.Home, GameScenePaths.Lobby })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool owned = !scene.IsValid() || !scene.isLoaded;
                if (owned) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    GameScreen screen = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<GameScreen>(true)).SingleOrDefault();
                    current &= screen != null && screen.developerInputs?.Length == LabConfig.DeveloperFields.Count
                        && screen.developerSliders?.Length == LabConfig.DeveloperFields.Count
                        && screen.developerScopeButtons?.Length == 5
                        && screen.developerInputs.All(input => input != null)
                        && screen.developerSliders.All(slider => slider != null)
                        && screen.developerScopeButtons.All(button => button != null);
                }
                finally { if (owned) EditorSceneManager.CloseScene(scene, true); }
            }
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            return current;
        }

        // One-time migration; repeated menu runs retain subsequent authored edits.
        private static void ApplyFigma303(GameLobbyView view)
        {
            Transform card = view.modePanel.transform.Find("Card");
            Place(card, 28, 298, 345, 253);
            Place(card.Find("Title"), 0, 32, 345, 38);
            card.Find("Title").GetComponent<UnityEngine.UI.Text>().fontSize = 24;
            Place(card.Find("Hint"), 0, 72.5f, 345, 30);
            card.Find("Hint").GetComponent<UnityEngine.UI.Text>().fontSize = 18;
            Place(card.Find("BasicMode"), 21, 123, 303, 48);
            Place(card.Find("TeamMode"), 21, 180, 303, 48);

            Transform layout = view.teamLayout.transform;
            var title = layout.Find("ModeTitle").GetComponent<UnityEngine.UI.Text>();
            Place(title.transform, 28, 120, 346, 38);
            title.fontSize = 27; title.supportRichText = true;
            title.text = "< 2:2 <size=19>땅따먹기</size> >";
            // Exported SVG bounds include the 1.5-unit outer half of the dashed stroke.
            Place(layout.Find("BlueBox"), 45.56f, 365.5f, 118, 270);
            Place(layout.Find("RedBox"), 239.03f, 369.5f, 118, 270);
            foreach (string side in new[] { "Blue", "Red" })
            {
                Transform line = layout.Find(side + "Line");
                Place(line, side == "Blue" ? 63.53f : 257.53f, 510, 82, 16);
                line.localEulerAngles = new Vector3(0, 0, 90);
                var image = line.GetComponent<UnityEngine.UI.Image>();
                image.sprite = Art("TeamLine" + side); image.color = Color.white;
                Place(layout.Find(side + "Team"), side == "Blue" ? 52.06f : 251.06f, 646.5f, 100, 26);
                layout.Find(side + "Team").GetComponent<UnityEngine.UI.Text>().font = view.mediumFont;
            }
            layout.Find("Versus").gameObject.SetActive(false);
            Vector2[] centers = { new Vector2(103.03f,447.5f), new Vector2(103.03f,585.5f),
                new Vector2(298.03f,447.5f), new Vector2(298.03f,585.5f) };
            for (int i = 0; i < centers.Length; i++)
                view.seatsTeam[i].anchoredPosition = new Vector2(centers[i].x, -centers[i].y);
            Transform anchors = view.teamShiftAnchors[0].parent;
            Place(anchors.Find("Heading"), 70.5f, 154, 261, 50);
            Place(anchors.Find("Hint"), 61, 204.5f, 280, 28);
            Place(anchors.Find("RoomPaper"), 28, 271, 346, 418);
            Place(anchors.Find("RoomName"), 45.5f, 285.5f, 313, 32);
            Place(anchors.Find("Joined"), 45.5f, 316.5f, 160, 25);
            Place(anchors.Find("Selected"), 200.5f, 316.5f, 158, 25);
            Place(anchors.Find("Seal"), 28, 730, 346, 58);
            view.teamHeadingFontSize = 29; view.teamLocalLabelFontSize = 20; view.teamStartFontSize = 25;
            view.teamLobbyDesignRevision = 2;
        }

        private static void Place(Transform rect, float x, float y, float width, float height)
            => LabLobbyView.Place((RectTransform)rect, x, y, width, height);

        private static void ApplyInk303(GameLobbyView view)
        {
            foreach (var text in view.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                string color = ColorUtility.ToHtmlStringRGB(text.color);
                if (color == "000000" || color == "020202" || color == "292921" || color == "373737")
                    text.color = new Color(Ink.r, Ink.g, Ink.b, text.color.a);
            }
            // These secondary browser labels also changed to primary ink in the latest references.
            view.emptyRooms.transform.Find("Hint").GetComponent<UnityEngine.UI.Text>().color = Ink;
            view.roomTemplate.transform.Find("Count").GetComponent<UnityEngine.UI.Text>().color = Ink;
            view.modePanel.transform.Find("Card/Hint").GetComponent<UnityEngine.UI.Text>().color = LabLobbyView.Html("394759");
            view.teamLobbyDesignRevision = 3;
        }

        /// <summary>"어떤 걸로 동지를 부르겠소?" over the dimmed room browser, opened by "새 방 만들기 +".</summary>
        private static void BuildModePanel(GameLobbyView view)
        {
            Font brush = view.heading.font, medium = view.mediumFont != null ? view.mediumFont : view.subheading.font;
            RectTransform panel = Child(view.design, "ModeSelect");
            Stretch(panel);
            panel.SetSiblingIndex(view.browserPage.transform.GetSiblingIndex() + 1);
            view.modePanel = panel.gameObject;

            RectTransform dim = Child(panel, "Dim");
            Stretch(dim);
            var dimImage = Component<UnityEngine.UI.Image>(dim);
            dimImage.sprite = null; dimImage.color = new Color(0f, 0f, 0f, .5f);
            view.modeDismissButton = Component<UnityEngine.UI.Button>(dim);
            view.modeDismissButton.transition = UnityEngine.UI.Selectable.Transition.None;
            view.modeDismissButton.targetGraphic = dimImage;

            RectTransform card = Child(panel, "Card");
            LabLobbyView.Place(card, 28f, 298f, 346f, 250f);
            Sliced(Component<UnityEngine.UI.Image>(card), Art("ModeCard"), Color.white);
            Text(Child(card, "Title"), brush, "어떤 걸로 동지를 부르겠소?", 25, Ink, 0f, 37f, 346f, 34f);
            Text(Child(card, "Hint"), medium, "하고 싶은 유형을 선택하시오.", 14, LabLobbyView.Html("575750"), 0f, 78f, 346f, 22f);
            view.basicModeButton = Pill(card, "BasicMode", brush, "기본 모드", 21f, 115f, 304f, 44f, 20);
            view.teamModeButton = Pill(card, "TeamMode", brush, "2 : 2 땅따먹기", 21f, 168f, 304f, 44f, 20);

            // Development builds only: start alone on the Blue team to check overwriting and the ratio.
            view.teamSoloButton = Pill(panel, "TeamSolo", medium, "DEV · 혼자 2:2 확인", 126f, 562f, 150f, 34f, 13);
            // Saved closed like the Direct IP panel; "새 방 만들기 +" opens it.
            panel.gameObject.SetActive(false);
        }

        /// <summary>The Figma 2:2 seat screen: mode title, Blue and Red dashed boxes, VS and editable seat anchors.</summary>
        private static void BuildTeamScreen(GameLobbyView view)
        {
            Transform page = view.positionPage.transform;
            Font brush = view.heading.font;
            RectTransform layout = Child(page, "TeamLayout");
            Stretch(layout);
            // Behind every seat button. Move it only when it is not already before P1, so running again keeps the order.
            Transform firstSlot = view.slotButtons[0].transform;
            if (layout.GetSiblingIndex() > firstSlot.GetSiblingIndex()) layout.SetSiblingIndex(firstSlot.GetSiblingIndex());
            view.teamLayout = layout.gameObject;
            Text(Child(layout, "ModeTitle"), brush, "< 2:2 땅따먹기 >", 30, Ink, 28f, 134f, 346f, 38f);
            Box(layout, "BlueBox", Art("TeamBoxBlue"), 49f, 403f);
            Box(layout, "RedBox", Art("TeamBoxRed"), 242f, 403f);
            Bar(layout, "BlueLine", 104f);
            Bar(layout, "RedLine", 298f);
            Text(Child(layout, "Versus"), brush, "VS", 22, Ink, 172f, 520f, 70f, 30f);
            Text(Child(layout, "BlueTeam"), view.mediumFont, "청팀", 20, Blue, 53f, 671f, 100f, 26f);
            Text(Child(layout, "RedTeam"), view.mediumFont, "홍팀", 20, Red, 247f, 671f, 100f, 26f);

            Transform layouts = page.Find("EditableSeatLayouts")
                ?? throw new InvalidOperationException("The authored EditableSeatLayouts group is missing.");
            RectTransform seats = Child(layouts, "Team4");
            Stretch(seats);
            Vector2[] centers = { new Vector2(105f, 474f), new Vector2(105f, 611f), new Vector2(299f, 474f), new Vector2(299f, 611f) };
            view.seatsTeam = new RectTransform[LabTeams.Players];
            for (int i = 0; i < centers.Length; i++)
            {
                RectTransform anchor = Child(seats, "P" + (i + 1), out bool created);
                if (created) LabLobbyView.Place(anchor, centers[i].x - 34.41555f, centers[i].y - 33.9867f, 68.8311f, 67.9734f);
                view.seatsTeam[i] = anchor;
            }

            RectTransform screen = Child(layouts, "TeamScreen");
            Stretch(screen);
            view.teamShifted = new RectTransform[Shifted.Length];
            view.teamShiftAnchors = new RectTransform[Shifted.Length];
            for (int i = 0; i < Shifted.Length; i++)
            {
                var item = page.Find(Shifted[i]) as RectTransform
                    ?? throw new InvalidOperationException("The authored position page has no " + Shifted[i] + ".");
                RectTransform anchor = Child(screen, Shifted[i], out bool created);
                if (created)
                {
                    anchor.anchorMin = item.anchorMin; anchor.anchorMax = item.anchorMax; anchor.pivot = item.pivot;
                    anchor.sizeDelta = item.sizeDelta; anchor.localRotation = item.localRotation;
                    anchor.anchoredPosition = item.anchoredPosition + Vector2.down * ShiftDown;
                }
                view.teamShifted[i] = item;
                view.teamShiftAnchors[i] = anchor;
            }
        }

        private static void Box(Transform parent, string name, Sprite sprite, float x, float y)
        {
            RectTransform rect = Child(parent, name, out bool created);
            if (created) LabLobbyView.Place(rect, x, y, 116f, 263f);
            var image = Component<UnityEngine.UI.Image>(rect);
            image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Simple; image.color = Color.white;
            image.raycastTarget = false;
        }

        private static void Bar(Transform parent, string name, float x)
        {
            RectTransform rect = Child(parent, name, out bool created);
            if (created) LabLobbyView.Place(rect, x, 511f, 3f, 63f);
            var image = Component<UnityEngine.UI.Image>(rect);
            if (created) { image.sprite = null; image.color = Line; }
            image.raycastTarget = false;
        }

        private static UnityEngine.UI.Button Pill(Transform parent, string name, Font font, string label,
            float x, float y, float width, float height, int size)
        {
            RectTransform rect = Child(parent, name, out bool created);
            if (created) LabLobbyView.Place(rect, x, y, width, height);
            var image = Component<UnityEngine.UI.Image>(rect);
            Sliced(image, Art("ModePill"), Dark);
            var button = Component<UnityEngine.UI.Button>(rect);
            button.targetGraphic = image;
            if (name == "BasicMode" || name == "TeamMode")
            {
                // A native solid pill keeps its edge in the same ink; the old sprite baked in a black outline.
                image.enabled = false;
                RectTransform solid = Child(rect, "SolidInk");
                Stretch(solid); solid.SetAsFirstSibling();
                var rounded = Component<LabRoundedGraphic>(solid);
                rounded.radius = 27f; rounded.color = Dark;
                button.targetGraphic = rounded;
            }
            RectTransform text = Child(rect, "Label");
            Stretch(text);
            UnityEngine.UI.Text value = Component<UnityEngine.UI.Text>(text);
            value.font = font; value.fontSize = size; value.color = Light; value.text = label;
            value.alignment = TextAnchor.MiddleCenter; value.raycastTarget = false;
            return button;
        }

        private static void Sliced(UnityEngine.UI.Image image, Sprite sprite, Color color)
        {
            image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Sliced; image.fillCenter = true;
            image.pixelsPerUnitMultiplier = sprite.name.StartsWith("ModeCard", StringComparison.Ordinal) ? 4f : 2f;
            image.color = color;
        }

        private static void Text(RectTransform rect, Font font, string value, int size, Color color,
            float x, float y, float width, float height)
        {
            UnityEngine.UI.Text text = Component<UnityEngine.UI.Text>(rect);
            if (string.IsNullOrEmpty(text.text))
            {
                LabLobbyView.Place(rect, x, y, width, height);
                text.text = value; text.fontSize = size; text.color = color;
            }
            text.font = font; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private static RectTransform Child(Transform parent, string name) => Child(parent, name, out _);

        private static RectTransform Child(Transform parent, string name, out bool created)
        {
            Transform found = parent.Find(name);
            created = found == null;
            if (found != null) return (RectTransform)found;
            var item = new GameObject(name, typeof(RectTransform));
            item.layer = parent.gameObject.layer;
            item.transform.SetParent(parent, false);
            return (RectTransform)item.transform;
        }

        // The Editor's GetComponent returns a fake null for a missing component, so compare with Unity's == null.
        private static T Component<T>(Component owner) where T : Component
        {
            T found = owner.GetComponent<T>();
            return found != null ? found : owner.gameObject.AddComponent<T>();
        }

        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = rect.anchoredPosition = Vector2.zero; }
    }
}
