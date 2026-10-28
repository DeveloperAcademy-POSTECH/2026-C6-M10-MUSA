using System;
using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Hapio.Tests
{
    public sealed class GameOpeningAssetTests
    {
        private const string OpeningPrefab = "Assets/Hapio/Story/GameOpening.prefab";
        private const string OpeningSequence = "Assets/Hapio/Story/Opening/Opening.asset";

        [Test]
        public void HomeConnectsAnIndependentEditableOpeningToTheExistingLobbyAndCanonicalStory()
        {
            WithScene(GameScenePaths.Home, scene =>
            {
                GameOpeningController controller = All<GameOpeningController>(scene).Single();
                Assert.That(controller.gameObject.activeSelf && controller.enabled, Is.True);
                Assert.That(controller.player, Is.Not.Null);
                Assert.That(controller.lobbyView, Is.SameAs(All<GameLobbyView>(scene).Single()));
                Assert.That(controller.openingRoot, Is.Not.Null);
                Assert.That(controller.transform.IsChildOf(controller.openingRoot.transform), Is.False);
                Assert.That(controller.lobbyView.transform.IsChildOf(controller.openingRoot.transform), Is.False);
                Assert.That(controller.player.transform.IsChildOf(controller.openingRoot.transform), Is.True);
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(controller.openingRoot), Is.EqualTo(OpeningPrefab));
                Assert.That(AssetDatabase.GetAssetPath(controller.player.sequence), Is.EqualTo(OpeningSequence));
                Assert.That(controller.player.playOnStart, Is.False, "The app-opening controller owns the single start.");
                Assert.That(controller.player.sequence.cuts.Count, Is.EqualTo(24));
                Assert.That(controller.player.sequence.cuts.Sum(cut => cut.PageCount), Is.EqualTo(26));
                Assert.That(All<EventSystem>(scene), Has.Length.EqualTo(1));
                Assert.That(All<LabNetwork>(scene), Is.Empty, "The opening must reuse the persistent session bootstrap.");
                Assert.That(All<Camera>(scene), Has.Length.EqualTo(1), "An overlay opening does not need another camera.");
            });
        }

        [Test]
        public void OpeningPrefabOwnsItsCanvasSafeControlsAndLocalAudioWithoutAnotherNavigationGate()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OpeningPrefab);
            Assert.That(prefab, Is.Not.Null);
            Canvas canvas = prefab.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            Assert.That(canvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(402, 874)));
            Assert.That(prefab.GetComponentsInChildren<GameSafeArea>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<EventSystem>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<StoryOpeningTitle>(true), Is.Empty, "The preview title must not intercept app playback.");
            Assert.That(prefab.GetComponentsInChildren<GameSession>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<LabNetwork>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioListener>(true), Has.Length.EqualTo(1));
            StoryPlayer player = prefab.GetComponentsInChildren<StoryPlayer>(true).Single();
            Assert.That(player.sequence, Is.SameAs(AssetDatabase.LoadAssetAtPath<StorySequence>(OpeningSequence)));
            Assert.That(player.cutHost, Is.Not.Null);
            Assert.That(player.nextButton, Is.Not.Null); Assert.That(player.previousButton, Is.Not.Null);
            Assert.That(player.skipButton, Is.Not.Null); Assert.That(player.autoButton, Is.Not.Null);
            Assert.That(player.voiceSource, Is.Not.Null);
            Assert.That(player.voiceSource.playOnAwake, Is.False);
        }

        [Test]
        public void StoryPreviewRetainsItsIndependentTitleAndDoesNotBootstrapTheAppOpening()
        {
            WithScene("Assets/Hapio/Story/StoryPreview.unity", scene =>
            {
                Assert.That(All<GameOpeningController>(scene), Is.Empty);
                StoryOpeningTitle title = All<StoryOpeningTitle>(scene).Single();
                Assert.That(title.player, Is.Not.Null);
                Assert.That(title.beginButton, Is.Not.Null);
                Assert.That(title.titlePage, Is.Not.Null);
                Assert.That(title.controls, Is.Not.Null);
                Assert.That(All<GameSession>(scene), Is.Empty);
            });
        }

        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void WithScene(string path, Action<Scene> inspect)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(path);
            bool owned = !scene.IsValid() || !scene.isLoaded;
            if (owned) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { inspect(scene); }
            finally
            {
                if (owned) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
