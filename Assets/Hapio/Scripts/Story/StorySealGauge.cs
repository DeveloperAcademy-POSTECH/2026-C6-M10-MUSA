using UnityEngine;
using UnityEngine.UI;
namespace Hapio
{
    /// <summary>Animate a supplied result; never calculate or change the battle's seal rate.</summary>
    public sealed class StorySealGauge : MonoBehaviour
    {
        public Image fill;
        public Text percentage;
        [Range(0, 1)] public float target = .78f;
        [Min(.01f)] public float duration = 1.35f;
        [Min(0)] public float delay = .15f;
        public float Displayed { get; private set; }
        private float elapsed;
        private Vector3 labelScale;
        private Text capturedLabel;
        private const float EmphasisSeconds = .32f;
        private void OnEnable() => Show(target);
        public void Show(float normalizedSealRate)
        {
            target = float.IsNaN(normalizedSealRate) ? 0 : Mathf.Clamp01(normalizedSealRate);
            if (percentage != capturedLabel)
            {
                capturedLabel = percentage;
                if (percentage != null) labelScale = percentage.rectTransform.localScale;
            }
            elapsed = 0;
            Apply(0);
            Emphasize(0);
        }
        private void Update() => Advance(Time.unscaledDeltaTime);
        internal void Advance(float seconds)
        {
            elapsed += Mathf.Max(0, seconds);
            float counting = Mathf.Max(.01f, duration);
            float time = elapsed - Mathf.Max(0, delay);
            float t = Mathf.Clamp01(time / counting);
            Apply(target * (1 - Mathf.Pow(1 - t, 3)));
            Emphasize(target > 0 ? Mathf.Sin(Mathf.PI * Mathf.Clamp01((time - counting) / EmphasisSeconds)) : 0);
        }
        private void Emphasize(float amount)
        {
            if (percentage != null) percentage.rectTransform.localScale = labelScale * (1 + .12f * Mathf.Max(0, amount));
        }

        private void Apply(float value)
        {
            Displayed = value;
            if (fill != null) fill.fillAmount = value;
            if (percentage != null) percentage.text = Mathf.RoundToInt(value * 100) + "%";
        }
    }
}
