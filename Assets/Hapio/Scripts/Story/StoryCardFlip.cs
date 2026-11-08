using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Hapio
{
    /// <summary>Local presentation only; both faces stay editable children of the card.</summary>
    public sealed class StoryCardFlip : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        public RectTransform card;
        public GameObject front, back;
        public GameObject swipeHint;
        public AudioClip flipSound;
        public void SetHintVisible(bool visible) { if (swipeHint != null) swipeHint.SetActive(visible); }
        [Min(.1f)] public float flipSeconds = .55f;
        [Min(1)] public float swipePixels = 35f;
        public UnityEvent onFlipped = new UnityEvent();
        public bool IsBack { get; private set; }
        public bool IsFlipping { get; private set; }
        private bool targetBack, dragged;
        private int pointerId;
        private Vector2 dragStart;
        private float elapsed;
        private Vector3 restScale;
        private bool captured;
        private void OnEnable()
        {
            if (card == null) return;
            Capture();
            ResetFront();
        }
        private void OnDisable() { if (card != null && captured) ResetFront(); }
        private void Capture() { if (!captured && card != null) { restScale = card.localScale; captured = true; } }
        public void ResetFront()
        {
            Capture(); if (card == null) return;
            IsBack = targetBack = IsFlipping = dragged = false;
            elapsed = 0; card.localScale = restScale; ApplyFace();
        }
        public void Flip()
        {
            if (IsFlipping || card == null || front == null || back == null) return;
            Capture(); targetBack = !IsBack; elapsed = 0; IsFlipping = true;
            SetHintVisible(false);
            StoryVisualBeat.PlaySound(gameObject, flipSound, .65f);
        }
        private void Update() => Advance(Time.unscaledDeltaTime);
        internal void Advance(float seconds)
        {
            if (!IsFlipping) return;
            elapsed += Mathf.Max(0, seconds);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, flipSeconds));
            if (t >= .5f) IsBack = targetBack;
            ApplyFace();
            card.localScale = new Vector3(restScale.x * Mathf.Max(.005f, Mathf.Abs(Mathf.Cos(t * Mathf.PI))), restScale.y, restScale.z);
            if (t >= 1) { card.localScale = restScale; IsFlipping = false; onFlipped.Invoke(); }
        }
        private void ApplyFace() { if (front != null) front.SetActive(!IsBack); if (back != null) back.SetActive(IsBack); }
        public void OnPointerDown(PointerEventData e) { dragged = false; }
        public void OnPointerClick(PointerEventData e) { if (!dragged && e.button == PointerEventData.InputButton.Left) Flip(); dragged = false; }
        public void OnBeginDrag(PointerEventData e) { dragged = true; pointerId = e.pointerId; dragStart = e.position; }
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e)
        {
            Vector2 movement = e.position - dragStart;
            if (pointerId == e.pointerId && Mathf.Abs(movement.x) >= swipePixels &&
                Mathf.Abs(movement.x) > Mathf.Abs(movement.y)) Flip();
            // Keep click suppressed for this release; the next press resets it.
        }
    }
}
