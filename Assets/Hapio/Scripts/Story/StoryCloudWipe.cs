using System;
using UnityEngine;

namespace Hapio
{
    /// <summary>A reusable UI overlay that closes over the current page and reveals its replacement.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class StoryCloudWipe : MonoBehaviour
    {
        private static readonly Vector2 ReferenceSize = new Vector2(402, 874);

        [Serializable]
        private struct Cloud
        {
            public RectTransform rect;
            public Vector2 edge;
            public Vector2 middle;
            public Vector2 covered;
            // Normalized phase windows. Zero end values keep older prefabs compatible.
            [Range(0f, .4f)] public float closeDelay;
            [Range(0f, .4f)] public float openDelay;
            [Range(.5f, 1f)] public float closeEnd;
            [Range(.5f, 1f)] public float openEnd;
            public float openDrift;
            public bool reverseOnOpen;

            public void SamplePhase(float phase, bool opening)
            {
                float delay = Mathf.Clamp(opening ? openDelay : closeDelay, 0f, .4f);
                float authoredEnd = opening ? openEnd : closeEnd;
                float end = authoredEnd > 0f ? Mathf.Clamp(authoredEnd, delay + .1f, 1f) : 1f;
                float progress = Mathf.SmoothStep(0f, 1f, (phase - delay) / (end - delay));
                Sample(opening ? 1f - progress : progress, opening);
            }

            private void Sample(float progress, bool opening)
            {
                Vector2 pathEdge = edge, pathMiddle = middle;
                if (opening && reverseOnOpen)
                {
                    pathEdge.x = 2f * covered.x - edge.x;
                    pathMiddle.x = 2f * covered.x - middle.x;
                }
                float outsideX = pathEdge.x < covered.x ? -rect.rect.width - 1f : ReferenceSize.x + 1f;
                var outside = new Vector2(outsideX, pathEdge.y);
                // One continuous cubic path replaces the three unequal-speed linear segments.
                // Authored edge/middle positions guide the curve; only its endpoints are fixed poses.
                if (progress <= 0f) { rect.anchoredPosition = outside; return; }
                if (progress >= 1f) { rect.anchoredPosition = covered; return; }
                float inverse = 1f - progress;
                Vector2 position = inverse * inverse * inverse * outside
                    + 3f * inverse * inverse * progress * pathEdge
                    + 3f * inverse * progress * progress * pathMiddle
                    + progress * progress * progress * covered;
                // A gentle vertical bow on departure avoids a mechanical retracing of the entrance.
                if (opening) position.y += openDrift * 16f * progress * progress * inverse * inverse;
                rect.anchoredPosition = position;
            }
        }

        [SerializeField] private RectTransform artwork;
        [SerializeField] private CanvasGroup coveredPaper;
        [SerializeField] private UnityEngine.UI.Image inputShield;
        [SerializeField] private Cloud[] clouds;
        [SerializeField, Min(.1f)] private float closeSeconds = .62f;
        [SerializeField, Min(0f)] private float coveredSeconds = .10f;
        [SerializeField, Min(.1f)] private float openSeconds = .72f;

        private Action onCovered;
        private Action onCompleted;
        private bool autoAdvance;
        private float elapsed;

        public float SwapTime => Mathf.Max(.1f, closeSeconds);
        public float Duration => SwapTime + Mathf.Max(0f, coveredSeconds) + Mathf.Max(.1f, openSeconds);
        public bool IsPlaying { get; private set; }
        internal bool HasCovered => elapsed >= SwapTime;
        internal float RevealElapsed => Mathf.Max(0f, elapsed - SwapTime);

        private bool IsConfigured
        {
            get
            {
                if (artwork == null || coveredPaper == null || inputShield == null ||
                    clouds == null || clouds.Length == 0) return false;
                foreach (var cloud in clouds)
                    if (cloud.rect == null) return false;
                return true;
            }
        }

        private void Awake()
        {
            FitArtwork();
            if (IsConfigured) Sample(0f);
        }

        private void OnRectTransformDimensionsChange() => FitArtwork();

        private void FitArtwork()
        {
            if (artwork == null) return;
            var size = ((RectTransform)transform).rect.size;
            float scale = Mathf.Max(size.x / ReferenceSize.x, size.y / ReferenceSize.y);
            artwork.localScale = new Vector3(scale, scale, 1f);
        }

        private void Update()
        {
            if (autoAdvance) Advance(Time.unscaledDeltaTime);
        }

        private void OnDisable() => Cancel();

        /// <summary>Play in unscaled time. Swap page content in onCovered and keep this overlay active until completion.</summary>
        public bool TryPlay(Action onCovered, Action onCompleted = null) =>
            Begin(onCovered, onCompleted, true);

        internal bool BeginManual(Action onCovered) => Begin(onCovered, null, false);

        private bool Begin(Action covered, Action completed, bool automatic)
        {
            if (IsPlaying || !isActiveAndEnabled || !IsConfigured) return false;
            FitArtwork();
            onCovered = covered;
            onCompleted = completed;
            autoAdvance = automatic;
            IsPlaying = true;
            elapsed = 0f;
            transform.SetAsLastSibling();
            inputShield.raycastTarget = true;
            Sample(0f);
            return true;
        }

        internal void Advance(float seconds)
        {
            if (!IsPlaying) return;
            float next = elapsed + Mathf.Max(0f, seconds);
            if (!HasCovered && next >= SwapTime)
            {
                // Keep a fully covered frame even if one slow frame would finish the entire transition.
                elapsed = SwapTime;
                Sample(elapsed);
                var swap = onCovered;
                onCovered = null;
                try
                {
                    swap?.Invoke();
                }
                catch
                {
                    Cancel();
                    throw;
                }
                return;
            }

            elapsed = Mathf.Min(next, Duration);
            Sample(elapsed);
            if (elapsed < Duration) return;

            var completed = onCompleted;
            StopPlayback();
            completed?.Invoke();
        }

        /// <summary>Clear the overlay and drop pending callbacks when its host is leaving.</summary>
        public void Cancel()
        {
            StopPlayback();
            elapsed = 0f;
            if (IsConfigured) Sample(0f);
        }

        private void StopPlayback()
        {
            IsPlaying = false;
            autoAdvance = false;
            onCovered = onCompleted = null;
            if (inputShield != null) inputShield.raycastTarget = false;
        }

        public void Sample(float time)
        {
            if (!IsConfigured) return;
            float openStart = SwapTime + Mathf.Max(0f, coveredSeconds);
            float progress = time < openStart
                ? Mathf.SmoothStep(0f, 1f, time / SwapTime)
                : Mathf.SmoothStep(1f, 0f, (time - openStart) / Mathf.Max(.1f, openSeconds));
            // The illustrated paper fills the small gaps between sprites before content is swapped.
            coveredPaper.alpha = Mathf.InverseLerp(2f / 3f, 1f, progress);
            bool opening = time >= openStart;
            float phase = opening ? (time - openStart) / Mathf.Max(.1f, openSeconds) : time / SwapTime;
            foreach (var cloud in clouds) cloud.SamplePhase(phase, opening);
        }
    }
}
