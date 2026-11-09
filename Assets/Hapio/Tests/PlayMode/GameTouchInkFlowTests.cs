using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>터치 먹물 번짐 (#587): 실제 화면에서 찍히고 사라지는 흐름, 터치를 막지 않는 구성, 연타 상한.</summary>
    public sealed class GameTouchInkFlowTests
    {
        private GameObject holder;

        [TearDown]
        public void Cleanup()
        {
            if (holder != null) Object.Destroy(holder);
        }

        private GameTouchInk CreateInk()
        {
            holder = new GameObject("Touch ink test");
            return GameTouchInk.Create(holder.transform, GameTouchInkArt.Load(), new System.Random(3));
        }

        private static Vector2 ScreenMiddle => new Vector2(Screen.width * .5f, Screen.height * .5f);

        [UnityTest]
        public IEnumerator ATouchLeavesInkThatFadesAtOnceAndThenGoes()
        {
            GameTouchInk ink = CreateInk();
            GameTouchInkArt art = GameTouchInkArt.Load();
            ink.Stamp(ScreenMiddle);
            Assert.That(ink.ActiveMarks, Is.EqualTo(1));
            Assert.That(ink.NewestMarkAlpha, Is.EqualTo(art.inkStrength).Within(.001f), "Strongest the moment it lands.");
            yield return new WaitForSecondsRealtime(art.fadeSeconds * .5f);
            Assert.That(ink.NewestMarkAlpha, Is.GreaterThan(0f).And.LessThan(art.inkStrength), "Fading.");
            float deadline = Time.realtimeSinceStartup + art.fadeSeconds + 2f;
            while (ink.ActiveMarks > 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ink.ActiveMarks, Is.Zero, "Gone when the fade ends.");
        }

        [UnityTest]
        public IEnumerator TheInkNeverTakesATouch()
        {
            GameTouchInk ink = CreateInk();
            ink.Stamp(ScreenMiddle);
            yield return null;
            Assert.That(ink.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty, "Nothing on this canvas can be hit.");
            Graphic[] pictures = ink.GetComponentsInChildren<Graphic>(true);
            Assert.That(pictures.Length, Is.GreaterThan(1));
            foreach (Graphic picture in pictures) Assert.That(picture.raycastTarget, Is.False, picture.name);
            foreach (CanvasGroup group in ink.GetComponentsInChildren<CanvasGroup>(true)) Assert.That(group.blocksRaycasts, Is.False);
        }

        [UnityTest]
        public IEnumerator RapidTapsKeepOnlyTheLimitOfMarks()
        {
            GameTouchInk ink = CreateInk();
            GameTouchInkArt art = GameTouchInkArt.Load();
            for (int tap = 0; tap < art.maxMarks * 3; tap++) ink.Stamp(ScreenMiddle);
            Assert.That(ink.ActiveMarks, Is.EqualTo(art.maxMarks));
            Assert.That(ink.NewestMarkAlpha, Is.EqualTo(art.inkStrength).Within(.001f), "The newest tap still shows.");
            yield return null;
            Assert.That(ink.GetComponentsInChildren<CanvasGroup>(true).Length, Is.EqualTo(art.maxMarks), "Old marks are reused, not piled up.");
        }

        [UnityTest]
        public IEnumerator EachFingerAndTheMouseLeaveAMarkWithoutDoubling()
        {
            GameTouchInk ink = CreateInk();
            Touchscreen touchscreen = InputSystem.AddDevice<Touchscreen>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return null;
                InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = ScreenMiddle });
                InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 2, phase = UnityEngine.InputSystem.TouchPhase.Began, position = ScreenMiddle + Vector2.right * 40f });
                InputSystem.QueueStateEvent(mouse, new MouseState { position = ScreenMiddle }.WithButton(MouseButton.Left));
                InputSystem.Update();
                ink.ReadTouches();
                Assert.That(ink.ActiveMarks, Is.EqualTo(2), "One for each finger; the mouse does not add a third.");
            }
            finally
            {
                InputSystem.RemoveDevice(touchscreen);
                InputSystem.RemoveDevice(mouse);
            }
        }
    }
}
