using UnityEngine;
using UnityEngine.UI;

namespace C6.Prototype.Battle
{
    /// <summary>How the edge glow looks: pulsing warning, both hands down, or the defense stance reached.</summary>
    public enum WarningLook { Pulse, Holding, Stance }

    /// <summary>
    /// #28 edge glow shown only on the attacked player's screen during the warning: pulsing red, steady red while both
    /// hands hold, light green once the defense stance is reached. Presentation only: the edge Images live in the scene
    /// Canvas so their sprite, colors, and width stay editable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterAttackWarning : MonoBehaviour
    {
        [SerializeField] private Graphic[] edges;
        [SerializeField] private DefenseTouchMarker[] defenseMarkers;
        [SerializeField, Min(.1f)] private float pulsesPerSecond = 2.5f;   // DEMO_TUNING_VALUE
        [SerializeField, Range(0f, 1f)] private float minimumAlpha = .35f; // DEMO_TUNING_VALUE: dimmest point of a pulse
        [SerializeField] private Color stanceColor = new Color(.6f, 1f, .6f, .85f); // DEMO_TUNING_VALUE: light green defense stance
        private Color[] baseColors;

        public bool Visible { get; private set; }
        public WarningLook Look { get; private set; }

        private void Awake() { Capture(); Hide(); }

        /// <summary>Starts at full strength, then pulses between minimumAlpha and full.</summary>
        public static float PulseAlpha(double elapsedSeconds, float pulsesPerSecond, float minimumAlpha)
        {
            double wave = .5 + .5 * System.Math.Cos(2 * System.Math.PI * pulsesPerSecond * System.Math.Max(0, elapsedSeconds));
            return Mathf.Lerp(Mathf.Clamp01(minimumAlpha), 1f, (float)wave);
        }

        public void Show(double elapsedSeconds, WarningLook look = WarningLook.Pulse, float holdProgress = 0f)
        {
            if (edges == null) return;
            Capture();
            float alpha = look == WarningLook.Pulse ? PulseAlpha(elapsedSeconds, pulsesPerSecond, minimumAlpha) : 1f;
            for (int i = 0; i < edges.Length; i++)
            {
                if (edges[i] == null) continue;
                var color = look == WarningLook.Stance ? stanceColor : baseColors[i];
                edges[i].color = new Color(color.r, color.g, color.b, color.a * alpha);
                edges[i].enabled = true;
            }
            if (defenseMarkers != null)
                foreach (var marker in defenseMarkers) if (marker != null) marker.Show(look, alpha, holdProgress);
            Look = look; Visible = true;
        }

        public void Hide()
        {
            if (edges != null) foreach (var edge in edges) if (edge != null) edge.enabled = false;
            if (defenseMarkers != null) foreach (var marker in defenseMarkers) if (marker != null) marker.Hide();
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
