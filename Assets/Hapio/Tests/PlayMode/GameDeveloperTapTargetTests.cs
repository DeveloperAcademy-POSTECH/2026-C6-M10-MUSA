using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    public sealed class GameDeveloperTapTargetTests
    {
        private GameObject owner;
        private GameDeveloperTapTarget target;
        private bool allowed;
        private int opened;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Hidden Developer Entry Test Owner", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.GraphicRaycaster));
            owner.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            allowed = true;
            target = GameDeveloperTapTarget.Attach(owner, () => allowed, () => opened++);
            opened = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Canvas canvas = target != null && target.TapArea != null ? target.TapArea.GetComponentInParent<Canvas>() : null;
            Object.DestroyImmediate(owner);
            if (canvas != null) Object.DestroyImmediate(canvas.gameObject);
        }

        private void Click(PointerEventData.InputButton button = PointerEventData.InputButton.Left, bool dragging = false)
        {
            var data = new PointerEventData(EventSystem.current) { button = button, dragging = dragging };
            ExecuteEvents.Execute(target.TapArea, data, ExecuteEvents.pointerClickHandler);
        }

        [Test]
        public void OnlyThirdPointerClickOpensAndStartsANewSequence()
        {
            Click(); Click(); Assert.That(opened, Is.Zero);
            Click(); Assert.That(opened, Is.EqualTo(1));
            Click(); Assert.That(opened, Is.EqualTo(1));
            Click(); Click(); Assert.That(opened, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ExpiredTapsCannotCompleteTheNextSequence()
        {
            Click(); Click();
            yield return new WaitForSecondsRealtime(GameDeveloperTapTarget.TapWindowSeconds + .1f);
            Click(); Assert.That(opened, Is.Zero);
            Click(); Click(); Assert.That(opened, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UnavailableTargetStopsRaycastsAndClearsPartialEntry()
        {
            Click(); Click(); allowed = false;
            yield return null;
            Assert.That(target.TapArea.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.False);
            Click(); Assert.That(opened, Is.Zero);
            allowed = true; yield return null;
            Assert.That(target.TapArea.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            Click(); Assert.That(opened, Is.Zero);
            Click(); Click(); Assert.That(opened, Is.EqualTo(1));
        }

        [Test]
        public void RightClicksAndDragsDoNotCountOrFinishASequence()
        {
            Click(); Click(); Click(PointerEventData.InputButton.Right);
            Assert.That(opened, Is.Zero);
            Click(); Click(); Click(dragging: true);
            Assert.That(opened, Is.Zero);
            Click(); Click(); Assert.That(opened, Is.Zero);
            Click(); Assert.That(opened, Is.EqualTo(1));
        }

        [Test]
        public void DisabledOrReboundTargetDoesNotRetainPartialTaps()
        {
            Click(); Click(); target.enabled = false;
            target.enabled = true; Click(); Assert.That(opened, Is.Zero);
            target = GameDeveloperTapTarget.Attach(owner, () => allowed, () => opened++);
            Click(); Click(); Assert.That(opened, Is.Zero);
            Click(); Assert.That(opened, Is.EqualTo(1));
            Assert.That(owner.GetComponentsInChildren<GameDeveloperTapTarget>(true).Length, Is.EqualTo(1));
        }

        [Test]
        public void CanvasOwnerCannotReceiveClicksBubblingFromOtherGraphics()
        {
            var background = new GameObject("Ordinary Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            background.transform.SetParent(owner.transform, false);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(background), Is.Null,
                "The screen's Canvas owner must not count background taps as secret entry taps.");
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(owner), Is.Null);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(target.TapArea), Is.SameAs(target.TapArea),
                "Only the dedicated transparent hit area owns the gesture handler.");
        }

        [Test]
        public void HitTargetIsInvisibleAndConfinedToTheSafeTopCentre()
        {
            var image = target.TapArea.GetComponent<UnityEngine.UI.Image>();
            var rect = (RectTransform)target.TapArea.transform;
            Assert.That(image.color.a, Is.Zero);
            Assert.That(image.raycastTarget, Is.True);
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(.5f, 1)));
            Assert.That(rect.anchorMax, Is.EqualTo(rect.anchorMin));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(.5f, 1)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(84, 24)));
            Assert.That(target.TapArea.GetComponentInParent<GameSafeArea>(), Is.Not.Null);
            Canvas canvas = target.TapArea.GetComponentInParent<Canvas>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.sortingOrder, Is.EqualTo(5000));
            Assert.That(canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Length, Is.EqualTo(1));
        }
    }
}
