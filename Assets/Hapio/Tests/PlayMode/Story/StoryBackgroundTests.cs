using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hapio.Tests
{
    public sealed class StoryBackgroundTests
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();

        private StoryLayer CreateLayer(Transform parent, string name, bool background)
        {
            var layerObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(StoryLayer));
            layerObject.transform.SetParent(parent, false);
            var layer = layerObject.GetComponent<StoryLayer>();
            layer.artwork = layerObject.GetComponent<UnityEngine.UI.Image>();
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
            cleanup.Add(sprite);
            layer.artwork.sprite = sprite;
            layer.isBackground = background;
            layer.delay = background ? 2f : 0f;
            layer.enterSeconds = background ? 3f : 1f;
            layer.enterOffset = new Vector2(10f, -20f);
            layer.startHeight = .1f;
            layer.idleAmplitude = new Vector2(3f, 5f);
            return layer;
        }

        private StoryCutView CreateCut(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(StoryCutView));
            cleanup.Add(root);
            var view = root.GetComponent<StoryCutView>();
            view.layers = new[]
            {
                CreateLayer(root.transform, "Background", true),
                CreateLayer(root.transform, "Foreground", false)
            };
            return view;
        }

        [UnityTest]
        public IEnumerator BackgroundStaysOpaqueAtAuthoredTransformWhileForegroundUnfolds()
        {
            var view = CreateCut("Cut");
            var background = view.layers[0];
            var foreground = view.layers[1];
            var backgroundRect = (RectTransform)background.transform;
            var foregroundRect = (RectTransform)foreground.transform;
            var position = new Vector2(18f, -34f);
            var scale = new Vector3(1.2f, .9f, 1f);
            backgroundRect.anchoredPosition = position;
            backgroundRect.localScale = scale;

            view.Sample(0f);
            Assert.That(background.artwork.enabled, Is.True);
            Assert.That(background.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(backgroundRect.anchoredPosition, Is.EqualTo(position));
            Assert.That(backgroundRect.localScale, Is.EqualTo(scale));
            Assert.That(background.EntranceDuration, Is.Zero);
            Assert.That(view.EntranceDuration, Is.EqualTo(foreground.EntranceDuration));
            Assert.That(foreground.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(foregroundRect.localScale.y, Is.EqualTo(.1f).Within(.0001f));

            view.Sample(.5f);
            Assert.That(foreground.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(foregroundRect.localScale.y, Is.GreaterThan(.1f).And.LessThan(1f));
            view.Sample(8f);
            Assert.That(background.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(backgroundRect.anchoredPosition, Is.EqualTo(position));
            Assert.That(backgroundRect.localScale, Is.EqualTo(scale));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CutHandoffPreparesOpaqueSuccessorBeforeDisablingPreviousPicture()
        {
            var root = new GameObject("Player", typeof(RectTransform));
            root.SetActive(false);
            cleanup.Add(root);
            var player = root.AddComponent<StoryPlayer>();
            player.playOnStart = false;
            player.cutHost = root.GetComponent<RectTransform>();
            var sequence = ScriptableObject.CreateInstance<StorySequence>();
            cleanup.Add(sequence);
            for (int i = 0; i < 2; i++)
            {
                var cut = ScriptableObject.CreateInstance<StoryCutDefinition>();
                cleanup.Add(cut);
                cut.cutId = "cut-" + i;
                cut.viewPrefab = CreateCut("Cut " + i);
                sequence.cuts.Add(cut);
            }
            root.SetActive(true);
            Assert.That(player.Play(sequence), Is.True);
            var previous = player.CurrentView;
            bool successorReadyWhenPreviousDisabled = false;
            var probe = previous.gameObject.AddComponent<StoryBackgroundHandoffProbe>();
            probe.Disabled = () =>
            {
                var successor = player.CurrentView;
                successorReadyWhenPreviousDisabled = successor != null && successor != previous &&
                    successor.gameObject.activeInHierarchy && player.CurrentCutIndex == 1 &&
                    successor.layers[0].GetComponent<CanvasGroup>().alpha == 1f;
            };

            player.Next(); // Finish this picture's entrance without changing its page/cut.
            Assert.That(player.CurrentView, Is.SameAs(previous));
            player.Next();
            Assert.That(successorReadyWhenPreviousDisabled, Is.True);
            Assert.That(previous.gameObject.activeSelf, Is.False);
            Assert.That(player.CurrentCutIndex, Is.EqualTo(1));
            Assert.That(player.IsReading, Is.False);
            Assert.That(player.CurrentView.layers[0].GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(player.CurrentView.layers[1].GetComponent<CanvasGroup>().alpha, Is.Zero);
            probe.Disabled = null;
            yield return null;
            Assert.That(previous == null, Is.True);
            Assert.That(player.cutHost.GetComponentsInChildren<StoryCutView>().Length, Is.EqualTo(1));
            Assert.That(player.CurrentView.layers[0].GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in cleanup)
                if (item != null) UnityEngine.Object.Destroy(item);
            cleanup.Clear();
            yield return null;
        }
    }

    public sealed class StoryBackgroundHandoffProbe : MonoBehaviour
    {
        public Action Disabled;
        private void OnDisable() => Disabled?.Invoke();
    }
}
