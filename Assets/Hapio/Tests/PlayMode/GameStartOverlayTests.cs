using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hapio.Tests
{
    public sealed class GameStartOverlayTests
    {
        [UnityTest]
        public IEnumerator PreparationAndCountdownBlockTouchesButStartAndHideReleaseThem()
        {
            var parent = new GameObject("Start overlay test owner");
            try
            {
                GameStartOverlay view = GameStartOverlay.Create(parent.transform);
                Assert.That(view.Visible, Is.False);
                Assert.That(view.BlocksInput, Is.False);
                view.ShowPreparing(2, 3);
                Assert.That(view.DisplayText, Does.Contain("전투 준비 중"));
                Assert.That(view.DisplayText, Does.Contain("2/3"));
                Assert.That(view.Visible && view.BlocksInput, Is.True);
                foreach (int second in new[] { 3, 2, 1 })
                {
                    view.ShowCountdown(second);
                    Assert.That(view.DisplayText, Is.EqualTo(second.ToString()));
                    Assert.That(view.BlocksInput, Is.True);
                }
                Assert.Throws<ArgumentOutOfRangeException>(() => view.ShowCountdown(0));
                view.ShowStarted();
                Assert.That(view.DisplayText, Is.EqualTo("시작!"));
                Assert.That(view.Visible, Is.True);
                Assert.That(view.BlocksInput, Is.False);
                view.Hide();
                Assert.That(view.Visible || view.BlocksInput, Is.False);
                Assert.That(view.DisplayText, Is.Empty);
                // Every new stage replaces the old caption, even after Started/Hidden.
                view.ShowPreparing(0, 3);
                Assert.That(view.DisplayText, Does.Contain("0/3"));
                Assert.That(view.BlocksInput, Is.True);
                yield return null;
            }
            finally { UnityEngine.Object.Destroy(parent); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SessionOwnedOverlaySurvivesTheBattleSceneLoadBoundary()
        {
            var owner = new GameObject("Persistent start overlay test owner");
            UnityEngine.Object.DontDestroyOnLoad(owner);
            Scene transient = default;
            try
            {
                GameStartOverlay view = GameStartOverlay.Create(owner.transform);
                view.ShowPreparing(1, 2);
                Scene original = SceneManager.GetActiveScene();
                transient = SceneManager.CreateScene("Start overlay transition fixture");
                SceneManager.SetActiveScene(transient);
                SceneManager.SetActiveScene(original);
                yield return SceneManager.UnloadSceneAsync(transient);
                Assert.That(view, Is.Not.Null);
                Assert.That(view.gameObject.scene, Is.EqualTo(owner.scene));
                Assert.That(view.Visible && view.BlocksInput, Is.True);
                Assert.That(view.DisplayText, Does.Contain("1/2"));
                view.ShowCountdown(3);
                Assert.That(view.DisplayText, Is.EqualTo("3"));
            }
            finally
            {
                if (transient.IsValid() && transient.isLoaded) SceneManager.UnloadSceneAsync(transient);
                UnityEngine.Object.Destroy(owner);
            }
            yield return null;
        }
    }
}
