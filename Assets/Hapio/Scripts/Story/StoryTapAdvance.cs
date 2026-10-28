using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Hapio
{
    /// <summary>A completed tap on the opening picture, independent of whether its dialogue is visible.</summary>
    [DisallowMultipleComponent]
    public sealed class StoryTapAdvance : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler, IBeginDragHandler, IDragHandler
    {
        public StoryPlayer player;
        private readonly HashSet<int> pointers = new HashSet<int>();
        private bool canceled;
        private int releasedPointer = int.MinValue;
        private bool releasedTap;

        internal static bool HasHeldPointer => PressedTouchCount > 0 ||
            Mouse.current != null && Mouse.current.leftButton.isPressed;

        internal static int PressedTouchCount
        {
            get
            {
                if (Touchscreen.current == null) return 0;
                int count = 0;
                foreach (var touch in Touchscreen.current.touches)
                    if (touch.press.isPressed) count++;
                return count;
            }
        }

        private void Update()
        {
            // A second finger may land on a toolbar Button rather than this surface.
            if (pointers.Count > 0 && PressedTouchCount > 1) canceled = true;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || player == null || !player.IsPlaying) return;
            if (pointers.Count == 0) canceled = false;
            pointers.Add(data.pointerId);
            releasedTap = false;
            if (pointers.Count > 1 || PressedTouchCount > 1) canceled = true;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (!pointers.Remove(data.pointerId)) return;
            int threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10;
            releasedPointer = data.pointerId;
            releasedTap = !canceled && pointers.Count == 0 && !data.dragging &&
                data.button == PointerEventData.InputButton.Left &&
                (data.position - data.pressPosition).sqrMagnitude <= threshold * threshold;
        }

        public void OnPointerClick(PointerEventData data)
        {
            bool advance = releasedTap && releasedPointer == data.pointerId &&
                pointers.Count == 0 && PressedTouchCount == 0;
            releasedTap = false;
            if (advance && player != null && player.isActiveAndEnabled) player.RequestNext();
        }

        // IDragHandler is required for EventSystem to acquire a drag target and send BeginDrag.
        public void OnBeginDrag(PointerEventData data) { canceled = true; releasedTap = false; }
        public void OnDrag(PointerEventData data) { canceled = true; releasedTap = false; }

        private void OnDisable()
        {
            pointers.Clear();
            releasedTap = false;
            canceled = false;
        }
    }
}
