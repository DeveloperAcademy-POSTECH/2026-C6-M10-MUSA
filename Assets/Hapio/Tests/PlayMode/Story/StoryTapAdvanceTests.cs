using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Hapio.Tests
{
    public sealed class StoryTapAdvanceTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private EventSystem events;
        private StoryPlayer player;
        private StoryTapAdvance surface;
        private Button nextButton;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null) Object.Destroy(cleanup[i]);
            cleanup.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RaycastSurfaceAdvancesOnlyOnCompletedTapAndKeepsDirectNextSynchronous()
        {
            CreatePresentation();
            // New Canvas graphics need their first render registration before a real raycast.
            yield return null;
            Canvas.ForceUpdateCanvases();
            var pointer = Pointer(1, new Vector2(Screen.width * .5f, Screen.height * .5f));
            GameObject hit = Raycast(pointer);
            Assert.That(hit, Is.SameAs(surface.gameObject));
            Assert.That(player.dialogueGroup.alpha, Is.Zero, "The first page is intentionally silent.");

            GameObject pressed = Press(hit, pointer);
            Assert.That(player.IsReading, Is.False, "Pointer down must not consume the entrance.");
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(player.IsReading, Is.False, "The completed click, rather than pointer up alone, advances.");
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(player.IsReading, Is.True);
            Assert.That(player.CurrentPageIndex, Is.Zero);
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(player.CurrentPageIndex, Is.Zero, "A duplicate callback in the same frame must not skip a page.");
            nextButton.onClick.Invoke();
            Assert.That(player.CurrentPageIndex, Is.Zero, "Toolbar and picture taps share the same user-input frame gate.");
            player.ToggleAuto();
            Assert.That(player.AutoAdvance, Is.False, "Game opening stays manual even if AUTO is called externally.");

            yield return null;
            Tap(surface.gameObject, Pointer(2, pointer.position));
            Assert.That(player.CurrentPageIndex, Is.EqualTo(1));
            Assert.That(player.dialogueGroup.alpha, Is.EqualTo(1));
            player.Next();
            Assert.That(player.CurrentCutIndex, Is.EqualTo(1), "The user-input gate must not throttle the existing direct Next API.");
        }

        [UnityTest]
        public IEnumerator ToolbarButtonWinsTheRaycastWithoutAlsoAdvancingTheScreenSurface()
        {
            CreatePresentation();
            // New Canvas graphics need their first render registration before a real raycast.
            yield return null;
            Canvas.ForceUpdateCanvases();
            var position = RectTransformUtility.WorldToScreenPoint(null, nextButton.transform.position);
            var pointer = Pointer(1, position);
            GameObject hit = Raycast(pointer);
            Assert.That(hit, Is.SameAs(nextButton.gameObject));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit), Is.SameAs(nextButton.gameObject));
            Tap(hit, pointer);
            Assert.That(player.IsReading, Is.True);
            Assert.That(player.CurrentPageIndex, Is.Zero, "One toolbar click may finish the entrance, but must not also advance the page.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DragAndOverlappingPointersCancelTheGestureAndAFreshTapWorks()
        {
            CreatePresentation();
            Vector2 position = new Vector2(Screen.width * .5f, Screen.height * .5f);
            var drag = Pointer(1, position);
            Press(surface.gameObject, drag);
            drag.position += Vector2.right * (events.pixelDragThreshold + 20f);
            ExecuteEvents.Execute(surface.gameObject, drag, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(surface.gameObject, drag, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(surface.gameObject, drag, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(surface.gameObject, drag, ExecuteEvents.pointerClickHandler);
            Assert.That(player.IsReading, Is.False, "Dragging must never become a next-page tap.");

            yield return null;
            var first = Pointer(2, position);
            var second = Pointer(3, position + Vector2.right * 30f);
            Press(surface.gameObject, first);
            Press(surface.gameObject, second);
            ExecuteEvents.Execute(surface.gameObject, first, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(surface.gameObject, first, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(surface.gameObject, second, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(surface.gameObject, second, ExecuteEvents.pointerClickHandler);
            Assert.That(player.IsReading, Is.False, "Neither release in a multi-pointer gesture may advance.");

            yield return null;
            Tap(surface.gameObject, Pointer(4, position));
            Assert.That(player.IsReading, Is.True, "A canceled gesture must not leave future taps locked.");
            Assert.That(player.CurrentPageIndex, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LastPageFinishesOnceOnReleaseClickRatherThanPointerDown()
        {
            CreatePresentation(1);
            // New Canvas graphics need their first render registration before a real raycast.
            yield return null;
            Canvas.ForceUpdateCanvases();
            player.Next();
            player.Next();
            Assert.That(player.CurrentPageIndex, Is.EqualTo(1));
            int finished = 0;
            player.Finished += reason => { Assert.That(reason, Is.EqualTo(StoryExitReason.Completed)); finished++; };
            var pointer = Pointer(1, new Vector2(Screen.width * .5f, Screen.height * .5f));
            GameObject pressed = Press(Raycast(pointer), pointer);
            Assert.That(player.IsPlaying, Is.True);
            Assert.That(finished, Is.Zero);
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(finished, Is.Zero);
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(finished, Is.EqualTo(1));
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(finished, Is.EqualTo(1));
            yield return null;
        }

        private void CreatePresentation(int cutCount = 2)
        {
            var eventObject = new GameObject("Story Tap Test EventSystem", typeof(EventSystem));
            cleanup.Add(eventObject);
            events = eventObject.GetComponent<EventSystem>();
            EventSystem.current = events;
            var root = new GameObject("Story Tap Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            cleanup.Add(root);
            root.SetActive(false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 30000;
            player = root.AddComponent<StoryPlayer>();
            player.playOnStart = false;
            player.touchOnly = true;
            player.cutHost = Child(root.transform, "Cut Host");
            var dialogue = Child(root.transform, "Dialogue");
            player.dialogueGroup = dialogue.gameObject.AddComponent<CanvasGroup>();

            var input = Child(root.transform, "Tap Surface");
            input.gameObject.AddComponent<Image>().color = Color.clear;
            surface = input.gameObject.AddComponent<StoryTapAdvance>();
            surface.player = player;
            var toolbar = Child(root.transform, "Toolbar");
            var button = Child(toolbar, "Next");
            button.anchorMin = button.anchorMax = new Vector2(1f, 1f);
            button.pivot = new Vector2(1f, 1f);
            button.anchoredPosition = new Vector2(-20f, -20f);
            button.sizeDelta = new Vector2(120f, 60f);
            button.gameObject.AddComponent<Image>();
            nextButton = button.gameObject.AddComponent<Button>();
            player.nextButton = nextButton;

            var prefab = new GameObject("Story Tap Test Cut", typeof(RectTransform), typeof(StoryCutView));
            cleanup.Add(prefab);
            var layerRect = Child(prefab.transform, "Layer");
            var layer = layerRect.gameObject.AddComponent<StoryLayer>();
            layer.enterSeconds = 60f;
            var view = prefab.GetComponent<StoryCutView>();
            view.layers = new[] { layer };
            view.hideEmptyDialogue = true;
            var sequence = ScriptableObject.CreateInstance<StorySequence>();
            cleanup.Add(sequence);
            for (int i = 0; i < cutCount; i++)
            {
                var cut = ScriptableObject.CreateInstance<StoryCutDefinition>();
                cleanup.Add(cut);
                cut.cutId = "tap-cut-" + i;
                cut.viewPrefab = view;
                cut.pages.Add(new StoryDialoguePage());
                cut.pages.Add(new StoryDialoguePage { dialogue = "Visible second page" });
                sequence.cuts.Add(cut);
            }
            player.sequence = sequence;
            root.SetActive(true);
            Assert.That(player.Play(sequence), Is.True);
            Canvas.ForceUpdateCanvases();
        }

        private static RectTransform Child(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            child.SetParent(parent, false);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.sizeDelta = Vector2.zero;
            return child;
        }

        private PointerEventData Pointer(int id, Vector2 position) => new PointerEventData(events)
        {
            pointerId = id,
            position = position,
            pressPosition = position,
            button = PointerEventData.InputButton.Left,
            eligibleForClick = true
        };

        private GameObject Raycast(PointerEventData pointer)
        {
            Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "The saved-order input surface must be reachable through the EventSystem.");
            pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
            return hits[0].gameObject;
        }

        private static GameObject Press(GameObject hit, PointerEventData pointer)
        {
            var pressed = ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            pointer.pointerPress = pressed;
            pointer.pointerClick = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            return pressed;
        }

        private static void Tap(GameObject hit, PointerEventData pointer)
        {
            var pressed = Press(hit, pointer);
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(pointer.pointerClick, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
