using UnityEngine;
using UnityEngine.UI;

namespace C6.Prototype.Battle
{
    /// <summary>Visual guide at the center of an existing two-hand defense input zone.</summary>
    [DisallowMultipleComponent]
    public sealed class DefenseTouchMarker : MonoBehaviour
    {
        [SerializeField] private Image outline;
        [SerializeField] private Image holdProgress;
        [SerializeField] private Image handIcon;
        [SerializeField] private Color warningColor = new Color(1f, .94f, .78f, .9f);
        [SerializeField] private Color progressColor = new Color(1f, .58f, .08f, 1f);
        [SerializeField] private Color stanceColor = new Color(.6f, 1f, .6f, .85f);

        public Image Outline => outline;
        public Image HoldProgress => holdProgress;
        public Image HandIcon => handIcon;

        public void Show(WarningLook look, float alpha, float progress)
        {
            Color baseColor = look == WarningLook.Stance ? stanceColor : warningColor;
            baseColor.a *= alpha;
            Color arcColor = look == WarningLook.Stance ? stanceColor : progressColor;
            arcColor.a *= alpha;

            if (outline != null) { outline.color = baseColor; outline.enabled = true; }
            if (handIcon != null) { handIcon.color = baseColor; handIcon.enabled = true; }
            if (holdProgress != null)
            {
                holdProgress.color = arcColor;
                holdProgress.fillAmount = look == WarningLook.Stance ? 1f :
                    look == WarningLook.Holding ? Mathf.Clamp01(progress) : 0f;
                holdProgress.enabled = holdProgress.fillAmount > 0f;
            }
        }

        public void Hide()
        {
            if (outline != null) outline.enabled = false;
            if (holdProgress != null) holdProgress.enabled = false;
            if (handIcon != null) handIcon.enabled = false;
        }

        private void OnDisable() => Hide();
    }
}
