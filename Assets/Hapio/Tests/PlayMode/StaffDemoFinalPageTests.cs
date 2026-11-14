using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    public sealed class StaffDemoFinalPageTests
    {
        [UnityTest]
        public IEnumerator EndingGestureCannotSkipTheFinalPageAndOnlyOneNewReturnActionRuns()
        {
            var parent = new GameObject("Demo completion page fixture", typeof(RectTransform));
            InputSettings original = InputSystem.settings;
            HideFlags originalFlags = original.hideFlags;
            string originalJson = JsonUtility.ToJson(original);
            InputSettings fixture = null;
            Mouse mouse = null;
            Touchscreen touch = null;
            StaffDemoFinalPage page = null;
            try
            {
                // A batch Editor has no focused GameView. Keep only the fixture's synthetic pointers
                // pressed across frames without changing the saved or original input settings.
                original.hideFlags = HideFlags.DontUnloadUnusedAsset;
                fixture = ScriptableObject.CreateInstance<InputSettings>();
                JsonUtility.FromJsonOverwrite(originalJson, fixture);
                fixture.hideFlags = HideFlags.HideAndDontSave;
                fixture.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                fixture.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = fixture;
                mouse = InputSystem.AddDevice<Mouse>("Demo final-page mouse");
                touch = InputSystem.AddDevice<Touchscreen>("Demo final-page touchscreen");
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                InputSystem.QueueStateEvent(touch, new TouchState
                    { touchId = 91, phase = UnityEngine.InputSystem.TouchPhase.Began, position = new Vector2(20, 20) });
                InputSystem.Update();
                Assert.That(mouse.leftButton.isPressed && touch.primaryTouch.press.isPressed, Is.True,
                    "The owned ending pointers must actually be pressed before the page is shown.");
                page = StaffDemoFinalPage.Create(parent.transform);
                int returns = 0;
                page.Show(Resources.Load<StaffDemoFinalPageConfig>(StaffDemoFinalPageConfig.ResourcePath), () => returns++);
                Assert.That(page.IsReady || page.returnButton.interactable, Is.False);
                page.returnButton.onClick.Invoke();
                yield return null;
                yield return null;
                Assert.That(mouse.leftButton.isPressed && touch.primaryTouch.press.isPressed, Is.True,
                    "Editor focus must not release the synthetic ending gesture between frames.");
                Assert.That(returns, Is.Zero);
                Assert.That(page.IsReady, Is.False, "A held ending touch must not arm the return button.");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                yield return null;
                yield return null;
                Assert.That(mouse.leftButton.isPressed, Is.False);
                Assert.That(touch.primaryTouch.press.isPressed, Is.True);
                Assert.That(page.IsReady || page.returnButton.interactable, Is.False,
                    "A held finger still shields the final page after the mouse has released.");
                page.returnButton.onClick.Invoke();
                Assert.That(returns, Is.Zero);
                InputSystem.QueueStateEvent(touch, new TouchState
                    { touchId = 91, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = new Vector2(20, 20) });
                InputSystem.Update();
                yield return null;
                yield return null;
                Assert.That(page.IsReady && page.returnButton.interactable, Is.True);
                page.returnButton.onClick.Invoke();
                page.returnButton.onClick.Invoke();
                Assert.That(returns, Is.EqualTo(1), "Repeated clicks cannot send more than one navigation request.");
                Assert.That(page.IsReady || page.returnButton.interactable, Is.False);
                page.Hide();
                page.Show(null, () => returns += 10);
                yield return null;
                yield return null;
                Assert.That(page.placeholder.activeSelf, Is.True);
                Assert.That(page.qrImage.gameObject.activeSelf, Is.False);
                page.returnButton.onClick.Invoke();
                Assert.That(returns, Is.EqualTo(11), "A new showing replaces the old callback.");
            }
            finally
            {
                try
                {
                    if (parent != null) Object.DestroyImmediate(parent);
                    if (touch != null && touch.added) InputSystem.RemoveDevice(touch);
                    if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                }
                finally
                {
                    try { InputSystem.settings = original; }
                    finally
                    {
                        original.hideFlags = originalFlags;
                        if (fixture != null) Object.DestroyImmediate(fixture);
                    }
                }
            }
            Assert.That(InputSystem.settings, Is.SameAs(original));
            Assert.That(original.hideFlags, Is.EqualTo(originalFlags));
            Assert.That(JsonUtility.ToJson(original), Is.EqualTo(originalJson));
            Assert.That(mouse.added || touch.added, Is.False);
        }
    }
}
