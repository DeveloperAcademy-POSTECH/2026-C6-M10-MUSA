using UnityEngine;

namespace Hapio
{
    /// <summary>Two paper clouds close over a page, then travel apart to reveal the next page.</summary>
    [DisallowMultipleComponent]
    public sealed class StoryCloudWipe : MonoBehaviour
    {
        public RectTransform upper;
        public RectTransform lower;
        [Min(.1f)] public float closeSeconds = .38f;
        [Min(0f)] public float coveredSeconds = .07f;
        [Min(.1f)] public float openSeconds = .43f;
        public Vector2 upperOutside = new Vector2(-240, 550);
        public Vector2 lowerOutside = new Vector2(320, -520);
        private Vector2 upperClosed, lowerClosed;
        private bool captured;

        public float SwapTime => closeSeconds;
        public float Duration => closeSeconds + coveredSeconds + openSeconds;

        public void Sample(float time)
        {
            if (!captured)
            {
                upperClosed = upper.anchoredPosition;
                lowerClosed = lower.anchoredPosition;
                captured = true;
            }
            float open = time - closeSeconds - coveredSeconds;
            float top = open > 0 ? Ease(open / (openSeconds * .9f))
                : 1 - Ease(time / (closeSeconds * .88f));
            float bottom = open > 0 ? Ease((open - openSeconds * .12f) / (openSeconds * .88f))
                : 1 - Ease((time - closeSeconds * .12f) / (closeSeconds * .88f));
            upper.anchoredPosition = upperClosed + upperOutside * top;
            lower.anchoredPosition = lowerClosed + lowerOutside * bottom;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }
    }
}
