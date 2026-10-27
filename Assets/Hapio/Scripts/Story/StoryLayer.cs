using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Animate this parent; replace only the child Image's sprite.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class StoryLayer : MonoBehaviour
    {
        public Image artwork;
        [Min(0f)] public float delay;
        [Min(0.01f)] public float enterSeconds = .6f;
        public Vector2 enterOffset = new Vector2(0f, -35f);
        [Range(.01f, 1f)] public float startHeight = .08f;
        [Tooltip("Unscaled local drift after unfolding. Zero disables drift.")]
        public Vector2 idleAmplitude;
        [Min(.1f)] public float idlePeriod = 6f;

        private RectTransform rect;
        private CanvasGroup group;
        private Vector2 restPosition;
        private Vector3 restScale;
        private bool captured;

        public float EntranceDuration => Mathf.Max(0f, delay) + Mathf.Max(.01f, enterSeconds);

        private void Capture()
        {
            if (captured) return;
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            restPosition = rect.anchoredPosition;
            restScale = rect.localScale;
            captured = true;
        }

        public void Sample(float elapsed)
        {
            Capture();
            if (artwork != null) artwork.enabled = artwork.sprite != null;
            float progress = Mathf.Clamp01((elapsed - Mathf.Max(0f, delay)) / Mathf.Max(.01f, enterSeconds));
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            Vector2 drift = progress < 1f ? Vector2.zero : idleAmplitude *
                Mathf.Sin((elapsed - EntranceDuration) * Mathf.PI * 2f / Mathf.Max(.1f, idlePeriod));
            rect.anchoredPosition = restPosition + enterOffset * (1f - eased) + drift;
            rect.localScale = new Vector3(restScale.x, restScale.y * Mathf.Lerp(startHeight, 1f, eased), restScale.z);
            group.alpha = eased;
        }

        public void Restore()
        {
            Capture();
            rect.anchoredPosition = restPosition;
            rect.localScale = restScale;
            group.alpha = 1f;
        }
    }
}
