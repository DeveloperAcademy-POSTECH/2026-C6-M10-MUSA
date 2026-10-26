using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hapio.Tests
{
    public sealed class GamePreviewAspectTests
    {
        [UnityTest]
        public IEnumerator ResultImageKeepsTextureAspectInsideTheAuthoredViewportAfterResize()
        {
            var root = new GameObject("Result aspect test", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var parent = (RectTransform)new GameObject("Saved result panel", typeof(RectTransform)).transform;
            parent.SetParent(root.transform, false);
            parent.sizeDelta = new Vector2(390f, 844f);
            var image = new GameObject("Saved gourd image", typeof(RectTransform), typeof(UnityEngine.UI.RawImage))
                .GetComponent<UnityEngine.UI.RawImage>();
            image.transform.SetParent(parent, false);
            image.rectTransform.anchorMin = new Vector2(.06f, .25f);
            image.rectTransform.anchorMax = new Vector2(.94f, .86f);
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            var square = new Texture2D(32, 32);
            var wide = new Texture2D(64, 32);
            try
            {
                Canvas.ForceUpdateCanvases();
                var original = new Vector3[4];
                image.rectTransform.GetWorldCorners(original);
                image.texture = square;
                GamePreviewAspect.Attach(image);
                GamePreviewAspect.Attach(image);
                yield return null;
                Canvas.ForceUpdateCanvases();
                RectTransform viewport = (RectTransform)image.transform.parent;
                var preserved = new Vector3[4];
                viewport.GetWorldCorners(preserved);
                Assert.That(preserved, Is.EqualTo(original), "Only image fitting changes; the Scene's viewport stays in place.");
                Assert.That(image.rectTransform.rect.width / image.rectTransform.rect.height,
                    Is.EqualTo(1f).Within(.001f), "A square capture must remain square on a portrait result screen.");
                Assert.That(viewport.parent, Is.SameAs(parent), "Repeated binding must not nest extra viewports.");

                parent.sizeDelta = new Vector2(1024f, 768f);
                image.texture = wide;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(image.rectTransform.rect.width / image.rectTransform.rect.height,
                    Is.EqualTo(2f).Within(.001f));
                Assert.That(image.rectTransform.rect.width, Is.LessThanOrEqualTo(viewport.rect.width + .001f));
                Assert.That(image.rectTransform.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + .001f));
                Assert.That(image.texture, Is.SameAs(wide));
                Assert.That(image.raycastTarget, Is.True, "The fitted image remains the gesture surface.");
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(square);
                Object.Destroy(wide);
            }
        }
    }
}
