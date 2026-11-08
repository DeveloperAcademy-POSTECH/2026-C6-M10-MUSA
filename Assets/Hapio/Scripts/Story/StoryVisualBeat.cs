using UnityEngine;

namespace Hapio
{
    /// <summary>Samples a visual accent on the same clock as the cut, with a one-shot sound at its cue.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class StoryVisualBeat : MonoBehaviour
    {
        public enum Motion { PaperDrop, Impact, LightOut, Ember, SealStamp }
        public Motion motion;
        [Min(0)] public float delay;
        [Min(.01f)] public float duration = .35f;
        public Vector2 offset;
        [Min(0)] public float interactionDelay;
        public AudioClip sound;
        [Range(0, 1)] public float volume = .7f;
        public float Duration => Mathf.Max(0, delay) + Mathf.Max(.01f, duration);

        private RectTransform rect;
        private CanvasGroup group;
        private Vector2 rest;
        private Vector3 scale;
        private Quaternion rotation;
        private bool played;

        public void Sample(float elapsed) => Sample(elapsed, false);

        /// <summary>Settle a skipped cut without starting a cue, and consume cues already passed.</summary>
        public void SampleWithoutSound(float elapsed) => Sample(elapsed, true);

        private void Sample(float elapsed, bool silent)
        {
            if (rect == null)
            {
                rect = (RectTransform)transform;
                group = GetComponent<CanvasGroup>();
                rest = rect.anchoredPosition;
                scale = rect.localScale;
                rotation = rect.localRotation;
            }

            float t = Mathf.Clamp01((elapsed - delay) / Mathf.Max(.01f, duration));
            float settle = 1 - Mathf.Pow(1 - t, 3);
            if (!played && elapsed > 0 && elapsed >= (motion == Motion.PaperDrop ? Duration : delay))
            {
                played = true;
                if (!silent) PlaySound(gameObject, sound, volume);
            }

            if (motion == Motion.PaperDrop)
            {
                // Flip owns horizontal scale; the entrance only moves and rotates the card.
                rect.anchoredPosition = rest + offset * (1 - settle) - Vector2.up * (Mathf.Sin(t * Mathf.PI) * t * 10);
                rect.localRotation = rotation * Quaternion.Euler(0, 0, -9 * (1 - settle));
                group.alpha = Mathf.Clamp01(t * 4);
                group.blocksRaycasts = elapsed >= interactionDelay;
            }
            else if (motion == Motion.Impact)
            {
                group.alpha = elapsed < delay ? 0 : 1;
                rect.localScale = scale * (1 + .22f * Mathf.Exp(-5 * t) * Mathf.Cos(9 * t) * (1 - t));
            }
            else if (motion == Motion.SealStamp)
            {
                const float fallSeconds = .085f;
                float fall = Mathf.Clamp01(1 + (elapsed - delay) / fallSeconds);
                float lift = 1 - fall * fall * fall;
                float compression = Mathf.Pow(1 - t, 4);
                group.alpha = Mathf.Clamp01(fall * 3);
                rect.anchoredPosition = rest + offset * lift;
                rect.localScale = elapsed < delay
                    ? scale * (1 + .85f * lift)
                    : Vector3.Scale(scale, new Vector3(1 + .12f * compression, 1 - .18f * compression, 1));
            }
            else if (motion == Motion.LightOut)
            {
                float flicker = 1 - .25f * Mathf.Sin(t * Mathf.PI * 5) * (1 - t);
                group.alpha = (1 - settle) * flicker;
            }
            else
            {
                rect.anchoredPosition = rest + offset * t + Vector2.up * (Mathf.Sin(t * Mathf.PI) * 18);
                group.alpha = Mathf.Sin(t * Mathf.PI) * (1 - .35f * t);
                rect.localScale = scale * Mathf.Lerp(.8f, .35f, t);
            }
        }

        internal static void PlaySound(GameObject owner, AudioClip clip, float volume)
        {
            if (!Application.isPlaying || clip == null || !owner.activeInHierarchy) return;
            var source = owner.GetComponent<AudioSource>();
            if (source == null)
            {
                source = owner.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0;
            }
            source.PlayOneShot(clip, volume);
        }
    }
}
