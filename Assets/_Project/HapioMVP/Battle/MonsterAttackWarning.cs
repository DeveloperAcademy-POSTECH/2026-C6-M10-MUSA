using UnityEngine;
using UnityEngine.UI;

namespace C6.Prototype.Battle
{
    /// <summary>
    /// #28 red edge glow shown only on the attacked player's screen during the warning. Presentation only:
    /// the edge Images live in the scene Canvas so their sprite, color, and width stay editable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterAttackWarning : MonoBehaviour
    {
        [SerializeField] private Graphic[] edges;
        [SerializeField, Min(.1f)] private float pulsesPerSecond = 2.5f;   // DEMO_TUNING_VALUE
        [SerializeField, Range(0f, 1f)] private float minimumAlpha = .35f; // DEMO_TUNING_VALUE: dimmest point of a pulse
        private Color[] baseColors;

        public bool Visible { get; private set; }

        private void Awake() { Capture(); Hide(); }

        /// <summary>Starts at full strength, then pulses between minimumAlpha and full.</summary>
        public static float PulseAlpha(double elapsedSeconds, float pulsesPerSecond, float minimumAlpha)
        {
            double wave = .5 + .5 * System.Math.Cos(2 * System.Math.PI * pulsesPerSecond * System.Math.Max(0, elapsedSeconds));
            return Mathf.Lerp(Mathf.Clamp01(minimumAlpha), 1f, (float)wave);
        }

        public void Show(double elapsedSeconds)
        {
            if (edges == null) return;
            Capture();
            float alpha = PulseAlpha(elapsedSeconds, pulsesPerSecond, minimumAlpha);
            for (int i = 0; i < edges.Length; i++)
            {
                if (edges[i] == null) continue;
                var color = baseColors[i];
                edges[i].color = new Color(color.r, color.g, color.b, color.a * alpha);
                edges[i].enabled = true;
            }
            Visible = true;
        }

        public void Hide()
        {
            if (edges != null) foreach (var edge in edges) if (edge != null) edge.enabled = false;
            Visible = false;
        }

        private void Capture()
        {
            if (edges == null || baseColors != null && baseColors.Length == edges.Length) return;
            baseColors = new Color[edges.Length];
            for (int i = 0; i < edges.Length; i++) baseColors[i] = edges[i] != null ? edges[i].color : Color.clear;
        }
        private void OnDisable() => Hide();
    }
}
