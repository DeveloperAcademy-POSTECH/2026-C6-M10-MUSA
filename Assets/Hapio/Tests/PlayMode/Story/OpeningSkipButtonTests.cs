#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hapio.Tests
{
    public sealed class OpeningSkipButtonTests
    {
        GameObject root;
        StoryPlayer player;
        [SetUp] public void SetUp()
        {
            root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Story/GameOpening.prefab"));
            player = root.GetComponent<StoryPlayer>();
            root.SetActive(true);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); }

        [UnityTest] public IEnumerator VisibilityFollowsAll28CutsAndSkipStillFinishesOnce()
        {
            Assert.That(player.skipButton.gameObject.activeSelf, Is.False);
            player.Play(player.sequence);
            var show = typeof(StoryPlayer).GetMethod("ShowCut", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 28; i++)
            {
                show.Invoke(player, new object[] { i });
                int frame = player.CurrentView.sourceFigmaNumber;
                bool visible = frame < 14 || frame > 16;
                Assert.That(player.skipButton.gameObject.activeSelf, Is.EqualTo(visible), "Frame " + frame);
                if (!visible)
                {
                    player.RequestSkip();
                    Assert.That(player.IsPlaying, Is.True, "Hidden skip must not interrupt the accident");
                }
            }
            player.Previous();
            Assert.That(player.skipButton.gameObject.activeSelf, Is.True);
            int exits = 0;
            player.Finished += reason => { Assert.That(reason, Is.EqualTo(StoryExitReason.Skipped)); exits++; };
            yield return null;
            player.skipButton.onClick.Invoke();
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(exits, Is.EqualTo(1));
            Assert.That(player.skipButton.gameObject.activeSelf, Is.False);
            player.Replay();
            Assert.That(player.skipButton.gameObject.activeSelf, Is.True);
            player.Cancel();
            Assert.That(player.skipButton.gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator AuthoredGeometryRemainsAboveTapSurfaceAcrossAspectRatios()
        {
            player.Play(player.sequence);
            var button = (RectTransform)player.skipButton.transform;
            Assert.That(button.anchoredPosition, Is.EqualTo(new Vector2(7, -53)));
            Assert.That(button.sizeDelta, Is.EqualTo(new Vector2(115, 38)));
            Assert.That(button.parent.GetSiblingIndex(), Is.GreaterThan(root.transform.Find("Tap To Continue").GetSiblingIndex()));
            var image = player.skipButton.GetComponent<Image>();
            Assert.That(image.sprite.rect.size, Is.EqualTo(new Vector2(460, 152)));
            Assert.That(image.color, Is.EqualTo(Color.white));
            Assert.That(image.raycastTarget, Is.True);
            Assert.That(player.skipButton.GetComponentsInChildren<Text>(), Is.Empty);
            foreach (var size in new[] { new Vector2(402,874), new Vector2(430,932), new Vector2(768,1024) })
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.enabled = false;
                ((RectTransform)root.transform).sizeDelta = size;
                root.transform.localScale = Vector3.one;
                var artFit = player.cutHost.parent.GetComponent<StoryPageFit>();
                var buttonFit = button.parent.GetComponent<StoryPageFit>();
                artFit.Apply(); buttonFit.Apply();
                Assert.That(buttonFit.transform.localScale, Is.EqualTo(artFit.transform.localScale));
                Assert.That(((RectTransform)buttonFit.transform).anchoredPosition, Is.EqualTo(((RectTransform)artFit.transform).anchoredPosition));
            }
            yield return null;
        }
    }
}
#endif
