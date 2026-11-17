using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Tests
{
    public sealed class GameLobbyAssetTests
    {
        private const string PrefabPath = "Assets/Hapio/Art/UI/GameLobby.prefab";

        [TestCase(2, true, "땅따먹기 시작")]
        [TestCase(4, true, "땅따먹기 시작")]
        [TestCase(6, true, "땅따먹기 시작")]
        [TestCase(3, false, "요괴 봉인 시작")]
        public void ReadyHostUsesTheActualModeOnTheSavedSeatButton(int capacity, bool territory, string expected)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                GameLobbyView view = contents.GetComponent<GameLobbyView>();
                foreach (var button in view.slotButtons) button.gameObject.SetActive(false);
                view.seatDesign.Present(view, capacity, territory, false, true, true);
                Assert.That(view.sealButton.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo(expected));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        [Test]
        public void SharedFigmaPrefabOwnsOnlyEditablePresentationAndCompleteScrollAndSeatLayouts()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            GameLobbyView view = prefab.GetComponent<GameLobbyView>();
            Assert.That(view, Is.Not.Null); Assert.That(view.screen, Is.Null);
            Assert.That(prefab.GetComponentsInChildren<LabLobbyView>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<LabHud>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<LabNetwork>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Canvas>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true), Is.Empty);
            Assert.That(view.countButtons, Has.Length.EqualTo(5)); Assert.That(view.slotButtons, Has.Length.EqualTo(6));
            // #394: P6 exists only for 3:3 and has its own image, number and local label like P1..P5.
            Assert.That(new[] { view.slotImages.Length, view.slotLabels.Length, view.localLabels.Length }, Is.All.EqualTo(6));
            Assert.That(view.slotButtons.Select(button => button.name), Is.Unique);
            Assert.That(view.slotImages[5].transform.IsChildOf(view.slotButtons[5].transform), Is.True);
            Assert.That(view.slotLabels[5].transform.IsChildOf(view.slotButtons[5].transform), Is.True);
            Assert.That(view.localLabels[5], Is.Not.SameAs(view.localLabels[4]));
            // Figma 3905:145: all five chips fit in one row; their saved buttons remain in the same content.
            var chipScroll = view.countButtons[0].GetComponentInParent<UnityEngine.UI.ScrollRect>(true);
            Assert.That(chipScroll, Is.Not.Null);
            Assert.That(chipScroll.horizontal || chipScroll.vertical, Is.False);
            Assert.That(chipScroll.GetComponent<UnityEngine.UI.RectMask2D>(), Is.Not.Null);
            for (int i = 0; i < view.countButtons.Length; i++)
            {
                Assert.That(view.countButtons[i].transform.parent, Is.SameAs(chipScroll.content));
                Assert.That(view.countButtons[i].GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo((i + 2) + "명"));
            }
            Assert.That(chipScroll.content.rect.width, Is.EqualTo(((RectTransform)chipScroll.transform).rect.width),
                "All five chips are visible without a swipe.");
            Assert.That(view.browserBackground, Is.Not.Null);
            Assert.That(view.modeCard, Is.Not.Null);
            Assert.That(view.browserPage.transform.Find("People").IsChildOf(chipScroll.transform), Is.False);
            Assert.That(view.seats2, Has.Length.EqualTo(2)); Assert.That(view.seats3, Has.Length.EqualTo(3));
            Assert.That(view.seats4, Has.Length.EqualTo(4)); Assert.That(view.seats5, Has.Length.EqualTo(5));
            Assert.That(view.lineLayouts, Has.Length.EqualTo(3));
            foreach (RectTransform anchor in view.seats2.Concat(view.seats3).Concat(view.seats4).Concat(view.seats5).Concat(view.lineLayouts))
                Assert.That(anchor != null && anchor.IsChildOf(view.positionPage.transform), Is.True);
            // 2:2 (#281): the mode popup over the browser and the editable team seat screen are saved objects.
            Assert.That(view.modePanel, Is.Not.Null); Assert.That(view.modePanel.activeSelf, Is.False);
            foreach (UnityEngine.UI.Button button in new[] { view.modeDismissButton, view.basicModeButton, view.teamModeButton, view.teamSoloButton })
                Assert.That(button != null && button.transform.IsChildOf(view.modePanel.transform), Is.True);
            Assert.That(view.basicModeButton.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("기본 모드"));
            Assert.That(view.teamModeButton.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("땅따먹기"));
            // One shared footer position below both buttons; only the blocked mode's reason is visible.
            foreach ((UnityEngine.UI.Text reason, UnityEngine.UI.Button button) in new[]
                { (view.basicModeReason, view.basicModeButton), (view.teamModeReason, view.teamModeButton) })
            {
                Assert.That(reason != null && reason.transform.IsChildOf(view.modePanel.transform), Is.True);
                Assert.That(reason.gameObject.activeSelf, Is.False);
                Assert.That(reason.rectTransform.anchoredPosition.y, Is.LessThan(((RectTransform)button.transform).anchoredPosition.y),
                    "The reason sits under its own button.");
            }
            Assert.That(view.teamModeReason.rectTransform.anchoredPosition.y,
                Is.LessThan(((RectTransform)view.teamModeButton.transform).anchoredPosition.y - 24f));
            Assert.That(view.basicModeReason.rectTransform.anchoredPosition.y,
                Is.EqualTo(view.teamModeReason.rectTransform.anchoredPosition.y),
                "Either blocked mode uses the footer below both buttons.");
            Assert.That(view.teamLayout != null && view.teamLayout.transform.IsChildOf(view.positionPage.transform), Is.True);
            Assert.That(view.teamLayout.transform.GetSiblingIndex(), Is.LessThan(view.slotButtons[0].transform.GetSiblingIndex()),
                "The team boxes stay behind every seat button, also after the upgrade runs again.");
            Assert.That(view.seatsTeam, Has.Length.EqualTo(4));
            Assert.That(view.seatsTeam.All(anchor => anchor != null && anchor.IsChildOf(view.positionPage.transform)), Is.True);
            Assert.That(view.seatsTeam[0].anchoredPosition.x, Is.LessThan(view.seatsTeam[2].anchoredPosition.x), "Blue P1/P2 sit left of Red P3/P4.");
            Assert.That(view.seatsTeam[0].anchoredPosition.y, Is.GreaterThan(view.seatsTeam[1].anchoredPosition.y), "P1 sits above P2.");
            // #293: empty 2:2 seats are dashed circles in their team's colour, Blue for P1/P2 and Red for P3/P4.
            Assert.That(view.blueTeamEmptySlot, Is.Not.Null);
            Assert.That(view.redTeamEmptySlot, Is.Not.Null.And.Not.SameAs(view.blueTeamEmptySlot));
            Assert.That(view.blueTeamEmptySlot.rect.size, Is.EqualTo(view.emptySlots[1].rect.size), "The same dashed circle, recoloured.");
            // #394: filled territory seats are one team colour too, the same circle as a story seat.
            Assert.That(view.blueTeamFilledSlot, Is.Not.Null);
            Assert.That(view.redTeamFilledSlot, Is.Not.Null.And.Not.SameAs(view.blueTeamFilledSlot));
            Assert.That(view.blueTeamFilledSlot.rect.size, Is.EqualTo(view.filledSlots[0].rect.size));
            Assert.That(view.teamLayout.transform.Find("ModeTitle").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("< 땅따먹기 >"),
                "The seat screen title does not name the team size.");
            // 1:1: one seat in each team box, Blue P1 left of Red P2 at the same height.
            Assert.That(view.seatsOneOnOne, Has.Length.EqualTo(2));
            Assert.That(view.seatsOneOnOne.All(anchor => anchor != null && anchor.IsChildOf(view.positionPage.transform)), Is.True);
            Assert.That(view.seatsOneOnOne[0].anchoredPosition.x, Is.LessThan(view.seatsOneOnOne[1].anchoredPosition.x));
            Assert.That(view.seatsOneOnOne[0].anchoredPosition.y, Is.EqualTo(view.seatsOneOnOne[1].anchoredPosition.y));
            // Figma 4074:1017: both teams are visible, each ordered vertically P1..P3 / P4..P6.
            Assert.That(view.seatsThreeOnThree, Has.Length.EqualTo(6));
            Assert.That(view.seatsThreeOnThree.All(anchor => anchor != null && anchor.IsChildOf(view.positionPage.transform)), Is.True);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(view.seatsThreeOnThree[i].anchoredPosition.x, Is.LessThan(view.seatsThreeOnThree[i + 3].anchoredPosition.x));
                if (i < 2)
                {
                    Assert.That(view.seatsThreeOnThree[i].anchoredPosition.y, Is.GreaterThan(view.seatsThreeOnThree[i + 1].anchoredPosition.y));
                    Assert.That(view.seatsThreeOnThree[i + 3].anchoredPosition.y, Is.GreaterThan(view.seatsThreeOnThree[i + 4].anchoredPosition.y));
                }
            }
            Assert.That(view.seatDesign, Is.Not.Null);
            Assert.That(view.seatDesign.paper, Is.Not.Null);
            Assert.That(new[] { view.seatDesign.storyPaper, view.seatDesign.teamPaper, view.seatDesign.sixPlayerPaper }, Is.All.Not.Null);
            Assert.That(view.seatDesign.teamColumns, Has.Length.EqualTo(3));
            foreach (GameObject columns in view.seatDesign.teamColumns)
            {
                Assert.That(columns.transform.IsChildOf(view.teamLayout.transform), Is.True);
                var boxes = columns.GetComponentsInChildren<GameDashedRoundedGraphic>(true);
                Assert.That(boxes, Has.Length.EqualTo(2));
                Assert.That(boxes.All(box => !box.raycastTarget), Is.True, "Decorations cannot cover seat input.");
            }
            Assert.That(view.teamPairParts, Has.Length.EqualTo(4)); Assert.That(view.teamPairLines, Has.Length.EqualTo(2));
            Assert.That(view.teamPairParts.Concat(view.teamPairLines).All(part => part != null && part.transform.IsChildOf(view.teamLayout.transform)), Is.True);
            Assert.That(view.teamTrio != null && view.teamTrio.transform.IsChildOf(view.teamLayout.transform), Is.True);
            Assert.That(view.teamTrio.activeSelf, Is.False, "The 3:3 page is saved hidden; the 2:2 boxes stay the default.");
            foreach (Component part in new Component[] { view.teamTrioBox, view.teamTrioName, view.teamPreviousPage, view.teamNextPage, view.teamSwipe })
                Assert.That(part != null && part.transform.IsChildOf(view.teamTrio.transform), Is.True);
            Assert.That(view.teamSwipe.gameObject, Is.SameAs(view.teamTrioBox.gameObject), "The team box is also the swipe area.");
            Assert.That(view.teamTrioBox.raycastTarget, Is.True);
            Assert.That(view.teamPageDots, Has.Length.EqualTo(2));
            Assert.That(view.blueTrioBox, Is.Not.Null); Assert.That(view.redTrioBox, Is.Not.Null.And.Not.SameAs(view.blueTrioBox));
            foreach (UnityEngine.UI.Button arrow in new[] { view.teamPreviousPage, view.teamNextPage })
                Assert.That(((RectTransform)arrow.transform).rect.size.x >= 44f && ((RectTransform)arrow.transform).rect.size.y >= 44f, Is.True,
                    "Page arrows keep a 44pt touch area.");
            Assert.That(view.teamShifted, Has.Length.EqualTo(7)); Assert.That(view.teamShiftAnchors, Has.Length.EqualTo(7));
            for (int i = 0; i < view.teamShifted.Length; i++)
                Assert.That(view.teamShifted[i].name, Is.EqualTo(view.teamShiftAnchors[i].name));
            var scroll = view.roomContent.GetComponentInParent<UnityEngine.UI.ScrollRect>(true);
            Assert.That(scroll.content, Is.SameAs(view.roomContent));
            Assert.That(scroll.viewport.parent, Is.SameAs(scroll.transform));
            Assert.That(view.roomContent.parent, Is.SameAs(scroll.viewport));
            Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Image>(), Is.Not.Null);
            Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic, Is.False);
            Assert.That(view.roomContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>(), Is.Not.Null);
            Assert.That(view.roomContent.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit,
                Is.EqualTo(UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize));
            Assert.That(view.mediumFont, Is.SameAs(AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/Shilla_Culture(M).otf")));
            Assert.That(view.developerButton, Is.Not.Null); Assert.That(view.soloButton, Is.Not.Null);
            Assert.That(view.directConnectionPanel.activeSelf, Is.False);
        }

        [Test]
        public void ThreePlayerOriginalSpritesPreserveTheirTransparentMarginsWithoutCompression()
        {
            string[] names = Enumerable.Range(1, 3).Select(i => "Empty" + i)
                .Concat(Enumerable.Range(1, 5).SelectMany(i => new[] { "Seat" + i, "SeatHost" + i, "Choice" + i, "ChoiceTaken" + i, "ChoiceSelected" + i }))
                .Concat(new[] { "Taken", "ChoiceCheck", "Close", "PopupPanel" }).ToArray();
            foreach (string name in names)
            {
                Sprite sprite = Resources.Load<Sprite>(GameThreePlayerLobbyArt.Root + name);
                Assert.That(sprite, Is.Not.Null, "The saved original asset must be packaged: " + name);
                string path = AssetDatabase.GetAssetPath(sprite);
                Assert.That(path, Does.StartWith("Assets/Hapio/Resources/"), name);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), name);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), name);
                Assert.That(importer.alphaIsTransparency, Is.True, name);
                Assert.That(importer.mipmapEnabled, Is.False, "Fine outlines stay crisp at their authored UI size: " + name);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), name);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect),
                    "Retain Figma's exported transparent padding rather than trimming to the opaque silhouette: " + name);
                // Inspect the actual PNG rather than an imported GPU texture (which stays non-readable in the game).
                // A valid source ID/hash can still describe a bad export with the ancestor canvas baked behind it.
                string diskAsset = System.IO.Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                try
                {
                    Assert.That(ImageConversion.LoadImage(decoded, System.IO.File.ReadAllBytes(diskAsset)), Is.True, name);
                    foreach (Vector2Int corner in new[] { Vector2Int.zero, new Vector2Int(decoded.width - 1, 0),
                        new Vector2Int(0, decoded.height - 1), new Vector2Int(decoded.width - 1, decoded.height - 1) })
                        Assert.That(decoded.GetPixel(corner.x, corner.y).a, Is.Zero,
                            "The original PNG corner must remain transparent; do not bake a canvas background into " + name + " at " + corner);
                }
                finally { Object.DestroyImmediate(decoded); }
            }
            AssertThreePlayerCharacterSourceIdentity();
        }

        [System.Serializable]
        private sealed class FigmaLobbySource
        {
            public FigmaLobbySourceAsset[] assets;
        }

        [System.Serializable]
        private sealed class FigmaLobbySourceAsset
        {
            public string name, sourceNodeId, asset, sha256, pickerState, stateReferenceNodeId;
        }

        private static void AssertThreePlayerCharacterSourceIdentity()
        {
            const string sourcePath = "Assets/Hapio/Art/UI/ThreePlayerLobby/FigmaSource.json";
            string diskSource = System.IO.Path.Combine(Application.dataPath, sourcePath.Substring("Assets/".Length));
            var source = JsonUtility.FromJson<FigmaLobbySource>(System.IO.File.ReadAllText(diskSource));
            Assert.That(source?.assets, Is.Not.Null, "Preserve the exported art's Figma source identity.");
            // Game IDs are not the visual picker order: 2 is Soli, 3 is Muki. Loading the wrong picture can
            // still pass every seat/character rule, so verify each original source rather than only non-null sprites.
            Assert.That(GameBattleUiPresentation.CharacterNames, Is.EqualTo(new[] { "불이", "솔이", "묵이", "금옥", "만복" }));
            Assert.That(LabPlayerSlots.CharacterOrder, Is.EqualTo(new[] { 1, 3, 2, 4, 5 }));
            string[][] expected =
            {
                new[] { "Seat", "5808:5334", "5808:5338", "5808:5337", "5808:5335", "5808:5336" },
                new[] { "SeatHost", "5808:5339", "5808:5343", "5808:5342", "5808:5340", "5808:5341" },
                new[] { "Choice", "5820:32883", "5820:32893", "5820:32889", "5820:32897", "5820:32904" },
                new[] { "ChoiceTaken", "5820:32883", "5820:32893", "5820:32889", "5820:32897", "5820:32904" },
                new[] { "ChoiceSelected", "5820:32883", "5820:32893", "5820:32889", "5820:32897", "5820:32904" }
            };
            foreach (string[] family in expected)
            for (int character = 1; character <= 5; character++)
            {
                string name = family[0] + character;
                var entry = source.assets.Single(asset => asset.name == name);
                Assert.That(entry.sourceNodeId, Is.EqualTo(family[character]), name + " must depict the actual character ID.");
                bool picker = family[0].StartsWith("Choice", System.StringComparison.Ordinal);
                var state = family[0] == "ChoiceTaken" ? GameThreePlayerLobbyArt.ChoiceState.Taken
                    : family[0] == "ChoiceSelected" ? GameThreePlayerLobbyArt.ChoiceState.Selected
                    : GameThreePlayerLobbyArt.ChoiceState.Available;
                Sprite sprite = picker ? GameThreePlayerLobbyArt.Choice(character, state)
                    : GameThreePlayerLobbyArt.Seat(character, family[0] == "SeatHost");
                Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(entry.asset), name);
                if (picker)
                {
                    Assert.That(entry.pickerState, Is.EqualTo(state.ToString()), name);
                    string reference = state == GameThreePlayerLobbyArt.ChoiceState.Available ? "5820:32889"
                        : state == GameThreePlayerLobbyArt.ChoiceState.Selected ? "5820:32922"
                        : character == 1 ? "5820:32883" : "5820:32897";
                    Assert.That(entry.stateReferenceNodeId, Is.EqualTo(reference),
                        name + " must use the original state's border and opacity, not just the right portrait.");
                }
                string diskAsset = System.IO.Path.Combine(Application.dataPath, entry.asset.Substring("Assets/".Length));
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    string actual = System.BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(diskAsset)))
                        .Replace("-", string.Empty).ToLowerInvariant();
                    Assert.That(actual, Is.EqualTo(entry.sha256), name + " PNG must match its recorded original export.");
                }
            }
        }

        [TestCase(4)]
        [TestCase(5)]
        public void OnePickerCanSwitchBackToExistingRoomLayoutsAndRestoreThreePlayerArt(int otherPlayers)
        {
            var fixture = new GameObject("Three-player picker layout isolation fixture"); fixture.SetActive(false);
            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                var network = fixture.AddComponent<LabNetwork>(); network.config = config;
                Assert.That(network.SetGameFlowEnabled(true), Is.True);
                Assert.That(network.SetTargetParticipantCount(3), Is.True);
                var parent = new GameObject("Authored lobby reference", typeof(RectTransform)).GetComponent<RectTransform>();
                parent.SetParent(fixture.transform, false); parent.sizeDelta = new Vector2(402, 874);
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
                var picker = GameLobbyCharacterPicker.Create(parent, font, null, network, (seat, character) => { });
                // No transport is opened in this presentation check. Open refreshes the style, then correctly closes
                // because the device is disconnected; the same instance is reused for the next room configuration.
                picker.Open(1);
                Assert.That(picker.UsesThreePlayerArt, Is.True);
                var originalChoice = picker.Choice(0);
                var originalLayer = originalChoice.transform.parent.gameObject;
                Assert.That(originalChoice.transform.Find("Portrait").GetComponent<UnityEngine.UI.Image>().sprite,
                    Is.SameAs(GameThreePlayerLobbyArt.Choice(1)));

                Assert.That(network.SetTargetParticipantCount(otherPlayers), Is.True);
                picker.Open(1);
                Assert.That(picker.UsesThreePlayerArt, Is.False);
                Assert.That(originalLayer.activeSelf, Is.False, "An old hidden picker must not leave hit targets over the new one.");
                var existingChoice = picker.Choice(0);
                Assert.That(existingChoice, Is.Not.SameAs(originalChoice));
                Assert.That(existingChoice.transform.Find("Portrait").GetComponent<UnityEngine.UI.Image>().sprite,
                    Is.SameAs(GameLobbyCharacterPicker.Portrait(1)), "Other capacities retain the pre-existing portrait.");

                Assert.That(network.SetTargetParticipantCount(3), Is.True);
                picker.Open(1);
                Assert.That(picker.UsesThreePlayerArt, Is.True);
                Assert.That(picker.Choice(0), Is.SameAs(originalChoice), "Reopening restores the cached original layout without duplicate controls.");
                Assert.That(network.SetGameFlowEnabled(false), Is.True);
                picker.Open(1);
                Assert.That(picker.UsesThreePlayerArt, Is.False, "A three-person standalone Lab must retain its existing presentation too.");
                Assert.That(picker.Choice(0), Is.SameAs(existingChoice));
            }
            finally
            {
                Object.DestroyImmediate(fixture);
                Object.DestroyImmediate(config);
            }
        }

        [TestCase(GameScenePaths.Home, GameScreenKind.Home)]
        [TestCase(GameScenePaths.Lobby, GameScreenKind.Lobby)]
        public void HomeAndLobbyUseTheSamePrefabWithoutReplacingTheExistingScopedDeveloperPanel(string path, GameScreenKind kind)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(path);
            bool owned = !scene.IsValid() || !scene.isLoaded;
            if (owned) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                GameScreen screen = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameScreen>(true)).Single();
                GameLobbyView view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameLobbyView>(true)).Single();
                Assert.That(screen.kind, Is.EqualTo(kind)); Assert.That(screen.externalNavigation, Is.True);
                Assert.That(view.screen, Is.SameAs(screen));
                Assert.That(view.transform.parent, Is.SameAs(screen.transform), "The authored view fits safe insets once, directly under the existing Canvas.");
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(view.gameObject),
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
                Assert.That(screen.developerButton, Is.SameAs(view.developerButton));
                Assert.That(screen.soloButton, Is.SameAs(view.soloButton));
                Assert.That(screen.developerPanel.transform.IsChildOf(view.transform), Is.False);
                Assert.That(screen.developerInputs, Has.Length.EqualTo(257));
                Assert.That(screen.developerSliders, Has.Length.EqualTo(257));
                Assert.That(screen.developerScopeButtons, Has.Length.EqualTo(5));
                Transform layer = screen.developerPanel.transform;
                while (layer.parent != screen.transform) layer = layer.parent;
                Assert.That(layer.GetSiblingIndex(), Is.GreaterThan(view.transform.GetSiblingIndex()), "The opaque painting must not cover DEV.");
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LabLobbyView>(true)), Is.Empty);
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).Count(), Is.EqualTo(1));
                Assert.That(screen.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Camera[] cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
                Assert.That(cameras.Any(camera => camera.enabled && camera.gameObject.activeInHierarchy
                    && camera.targetDisplay == 0 && camera.targetTexture == null), Is.True,
                    "Direct Home/Lobby Play must render Display 1 without the Editor's no-camera warning.");
                Camera menuCamera = cameras.SingleOrDefault(camera => camera.name == "Hapio UI Background Camera");
                if (menuCamera != null)
                {
                    Assert.That(menuCamera.cullingMask, Is.Zero);
                    Assert.That(menuCamera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
                    Assert.That(menuCamera.orthographic, Is.True);
                    Assert.That(menuCamera.nearClipPlane, Is.GreaterThan(0f));
                    Assert.That(menuCamera.farClipPlane, Is.GreaterThan(menuCamera.nearClipPlane));
                    Assert.That(menuCamera.GetComponent<AudioListener>(), Is.Null);
                }
            }
            finally
            {
                if (owned) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
