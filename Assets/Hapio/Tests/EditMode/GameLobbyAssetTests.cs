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
            // #394: the 2~6 player chips sit in one horizontal scroll, in order; "함께할 인원" stays outside it.
            var chipScroll = view.countButtons[0].GetComponentInParent<UnityEngine.UI.ScrollRect>(true);
            Assert.That(chipScroll, Is.Not.Null);
            Assert.That(chipScroll.horizontal && !chipScroll.vertical, Is.True);
            Assert.That(chipScroll.GetComponent<UnityEngine.UI.RectMask2D>(), Is.Not.Null);
            for (int i = 0; i < view.countButtons.Length; i++)
            {
                Assert.That(view.countButtons[i].transform.parent, Is.SameAs(chipScroll.content));
                Assert.That(view.countButtons[i].GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo((i + 2) + "명"));
            }
            Assert.That(chipScroll.content.rect.width, Is.GreaterThan(((RectTransform)chipScroll.transform).rect.width),
                "All five chips cannot fit at once, so the row really scrolls.");
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
            // #394: one reason line under each mode button, saved hidden until the chosen count blocks that mode.
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
                Is.GreaterThan(((RectTransform)view.teamModeButton.transform).anchoredPosition.y + 24f),
                "The basic reason fits between the two buttons.");
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
            // 3:3: each team page has the same triangle, P1/P4 bottom centre, P2/P5 upper left, P3/P6 upper right.
            Assert.That(view.seatsThreeOnThree, Has.Length.EqualTo(6));
            Assert.That(view.seatsThreeOnThree.All(anchor => anchor != null && anchor.IsChildOf(view.positionPage.transform)), Is.True);
            for (int i = 0; i < 3; i++)
                Assert.That(view.seatsThreeOnThree[i + 3].anchoredPosition, Is.EqualTo(view.seatsThreeOnThree[i].anchoredPosition));
            Assert.That(view.seatsThreeOnThree[1].anchoredPosition.x, Is.LessThan(view.seatsThreeOnThree[0].anchoredPosition.x));
            Assert.That(view.seatsThreeOnThree[2].anchoredPosition.x, Is.GreaterThan(view.seatsThreeOnThree[0].anchoredPosition.x));
            Assert.That(view.seatsThreeOnThree[1].anchoredPosition.y, Is.GreaterThan(view.seatsThreeOnThree[0].anchoredPosition.y));
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
                Assert.That(screen.developerInputs, Has.Length.EqualTo(253));
                Assert.That(screen.developerSliders, Has.Length.EqualTo(253));
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
