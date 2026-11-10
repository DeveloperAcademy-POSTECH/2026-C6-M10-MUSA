using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Tests
{
    public sealed class GameStageStoryAssetTests
    {
        [TestCase(GameScenePaths.StageResult)]
        [TestCase(GameScenePaths.GameOver)]
        public void SavedResultScreensKeepRuntimeStoryAndExistingViewerBindings(string path)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                GameScreen screen = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<GameScreen>(true)).Single();
                var adapter = screen.GetComponent<GameStageStoryPresentation>();
                Assert.That(adapter, Is.Not.Null);
                Assert.That(adapter.screen, Is.SameAs(screen));
                Assert.That(screen.stageStory, Is.SameAs(adapter));
                Assert.That(adapter.presentationRoot, Is.Not.Null.And.Not.SameAs(screen.gameObject));
                Assert.That(screen.transform.IsChildOf(adapter.presentationRoot.transform), Is.False,
                    "Hiding the story must preserve the authoritative screen and completed-gourd viewer.");
                Assert.That(adapter.player, Is.Not.Null);
                Assert.That(adapter.player.transform.IsChildOf(adapter.presentationRoot.transform), Is.True);
                Assert.That(adapter.player.playOnStart, Is.False);
                Assert.That(adapter.player.hideInlineContinueButtons, Is.True,
                    "Only the game opts out of authored inline progression controls.");
                Assert.That(adapter.presentationRoot.activeSelf, Is.False, "Runtime chooses the correct branch before showing art.");
                Assert.That(adapter.presentationGroup, Is.Not.Null);
                Assert.That(adapter.afterStages, Has.Length.EqualTo(3));
                foreach (StorySequence sequence in adapter.afterStages)
                {
                    Assert.That(sequence, Is.Not.Null);
                    Assert.That(sequence.Validate(out string error), Is.True, error);
                    Assert.That(sequence.cuts, Has.Count.EqualTo(3));
                    Assert.That(sequence.cuts[0].viewPrefab.GetComponentInChildren<StoryCardRecords>(true), Is.Not.Null);
                }
                Assert.That(adapter.failure.Validate(out string failureError), Is.True, failureError);
                Assert.That(adapter.failure.cuts, Has.Count.EqualTo(2));
                Assert.That(adapter.ending.Validate(out string endingError), Is.True, endingError);
                Assert.That(adapter.ending.cuts, Has.Count.EqualTo(7), "Summary followed by six ending cuts; Home owns the title.");
                Assert.That(adapter.ending.cuts.Select(x => x.name), Does.Not.Contain("Ending_07"));
                Assert.That(new[] { adapter.advanceButton, adapter.exitButton, adapter.viewerButton, adapter.overallButton, adapter.skipButton }, Is.All.Not.Null);
                Assert.That(adapter.statusText, Is.Not.Null);
                Assert.That(adapter.player.nextButton.gameObject.activeSelf, Is.False,
                    "The saved game Canvas removes the authoring Next control while keeping the authoring preview separate.");
                Assert.That(adapter.viewerButton.transform.parent, Is.SameAs(adapter.advanceButton.transform.parent));
                Assert.That(adapter.advanceButton.transform.parent.parent, Is.SameAs(adapter.player.cutHost.parent),
                    "Result actions and authored pictures share the same design page fit.");
                Assert.That(adapter.player.cutHost.parent.GetComponent<StoryPageFit>(), Is.Not.Null);
                if (path == GameScenePaths.StageResult)
                {
                    Assert.That(screen.viewerPanel, Is.Not.Null);
                    Assert.That(screen.viewerImage, Is.Not.Null);
                }
                else Assert.That(screen.resultView.viewerButton, Is.Null, "A failed battle does not reuse a completed gourd.");
                Assert.That(screen.resultView, Is.Not.Null, "Territory/overall and unavailable-data fallback remain wired.");
                Assert.That(adapter.presentationRoot.GetComponentsInChildren<StageStoryShowcase>(true), Is.Empty);
                Assert.That(scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).Count(), Is.EqualTo(1));
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [TestCase("Assets/Hapio/Story/StageStories/StageStoryPreview.unity")]
        [TestCase("Assets/Hapio/Story/StoryPreview.unity")]
        public void AuthoringPreviewsKeepTheirInlineContinueControls(string path)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                StoryPlayer player = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<StoryPlayer>(true)).Single();
                Assert.That(player.hideInlineContinueButtons, Is.False,
                    "Game result cleanup must not opt the authored story preview into game-only restrictions.");
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void GameBuildRetainsAuthoredViewsWithoutAddingTheAuthoringPreviewScene()
        {
            string[] dependencies = AssetDatabase.GetDependencies(GameScenePaths.All, true);
            Assert.That(dependencies, Does.Contain("Assets/Hapio/Story/StageStories/AfterGumiho.asset"));
            Assert.That(dependencies, Does.Contain("Assets/Hapio/Story/StageStories/AfterYagwanggui.asset"));
            Assert.That(dependencies, Does.Contain("Assets/Hapio/Story/StageStories/AfterJigwi.asset"));
            Assert.That(dependencies, Does.Contain("Assets/Hapio/Story/StageStories/Failure.asset"));
            Assert.That(dependencies, Does.Contain("Assets/Hapio/Story/StageStories/GameEnding.asset"));
            Assert.That(dependencies.Any(x => x.EndsWith("StageStoryPreview.unity", StringComparison.Ordinal)), Is.False);
            Assert.That(EditorBuildSettings.scenes.Any(x => x.path.EndsWith("StageStoryPreview.unity", StringComparison.Ordinal)), Is.False);
        }
    }
}
