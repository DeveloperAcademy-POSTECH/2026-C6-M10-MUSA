using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hapio.Tests
{
    public sealed class GameStartOverlayAssetTests
    {
        [Test]
        public void SavedStartOverlayOwnsItsScreenSpaceCanvasAndKoreanLabels()
        {
            var prefab = Resources.Load<GameObject>(GameStartOverlay.ResourcePath);
            Assert.That(prefab, Is.Not.Null);
            var overlay = prefab.GetComponent<GameStartOverlay>();
            Assert.That(overlay, Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(prefab.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(1000));
            Assert.That(prefab.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            var scaler = prefab.GetComponent<UnityEngine.UI.CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(390f, 844f)));
            Assert.That(prefab.GetComponentsInChildren<GameSafeArea>(true), Has.Length.EqualTo(1));
            var fields = new SerializedObject(overlay);
            Assert.That(fields.FindProperty("visibility").objectReferenceValue, Is.EqualTo(prefab.GetComponent<CanvasGroup>()));
            var background = fields.FindProperty("background").objectReferenceValue as UnityEngine.UI.Image;
            Assert.That(background, Is.Not.Null);
            Assert.That(background.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(background.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            var caption = fields.FindProperty("caption").objectReferenceValue as UnityEngine.UI.Text;
            Assert.That(caption, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(caption.font), Is.EqualTo("Assets/Lab/Art/Fonts/ShillaCultureBold.otf"));
            Assert.That(caption.raycastTarget, Is.False);
            Assert.That(caption.rectTransform.anchorMin.x, Is.GreaterThan(0f));
            Assert.That(caption.rectTransform.anchorMax.x, Is.LessThan(1f));
            Assert.That(caption.rectTransform.anchorMax.y, Is.GreaterThan(caption.rectTransform.anchorMin.y));
            Assert.That(prefab.GetComponentsInChildren<Camera>(true), Is.Empty);
        }
    }
}
