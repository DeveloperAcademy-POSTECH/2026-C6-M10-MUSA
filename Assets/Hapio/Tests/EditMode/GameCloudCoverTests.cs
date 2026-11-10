using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>#635: the persistent cloud cover uses #630's shared clouds and draws over everything but the loading screen.</summary>
    public sealed class GameCloudCoverTests
    {
        [Test]
        public void SavedArtUsesThe630SharedClouds()
        {
            GameCloudCoverArt art = GameCloudCoverArt.Load();
            Assert.That(art, Is.Not.Null, GameCloudCoverArt.ResourcePath);
            Assert.That(art.cloudWipe, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(art.cloudWipe), Is.EqualTo("Assets/Hapio/Story/StageStories/Views/StoryCloudWipe.prefab"));
            Assert.That(art.cloudWipe.SwapTime, Is.EqualTo(.62f).Within(1e-4f), "Covers in 0.62 s.");
        }

        [Test]
        public void DrawsOverTheFadeStartCoverAndTutorialButUnderTheLoadingScreen()
        {
            Assert.That(GameCloudCover.SortingOrder, Is.GreaterThan(GameSceneFade.SortingOrder), "Over the scene fade.");
            Assert.That(GameCloudCover.SortingOrder, Is.LessThan(GameLoadingView.SortingOrder), "Under the loading screen.");
            Assert.That(GameCloudCover.SortingOrder, Is.GreaterThan(GameTutorialDirector.OverlaySortingOrder), "Over the tutorial.");
            Assert.That(GameCloudCover.SortingOrder, Is.GreaterThan(GameTouchInk.SortingOrder), "No touch ink over the clouds.");
            var start = AssetDatabase.LoadAssetAtPath<Canvas>("Assets/Hapio/Resources/Hapio/GameStartOverlay.prefab");
            Assert.That(start, Is.Not.Null);
            Assert.That(GameCloudCover.SortingOrder, Is.GreaterThan(start.sortingOrder), "Over the old 준비 중 cover.");
        }
    }
}
