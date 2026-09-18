using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace C6.Prototype.Battle
{
    /// <summary>
    /// Presentation-only floating damage numbers. Each popup follows its world hit point through the local
    /// battle camera, rises, and fades. It never changes HP, damage, hits, or the snapshot.
    /// The template Text lives in the scene Canvas so font, size, color, and outline stay editable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamagePopupLayer : MonoBehaviour
    {
        [SerializeField] private Text template;
        [SerializeField] private Text missTemplate;   // #20: MISS look, edited separately in the scene
        [SerializeField, Min(.05f)] private float duration = .9f;   // DEMO_TUNING_VALUE
        [SerializeField] private float risePixels = 90f;             // Canvas units (390 × 844 reference)
        [SerializeField, Min(1f)] private float popScale = 1.35f;    // starts larger, settles to 1
        private readonly List<Popup> active = new List<Popup>();

        private sealed class Popup { public Text text; public Color color; public Vector3 world; public Camera camera; public float elapsed; }

        public void Show(int damage, Vector3 worldPosition, Camera battleCamera)
        {
            if (damage > 0) Spawn(template, damage.ToString(), "Damage " + damage, worldPosition, battleCamera);
        }

        public void ShowMiss(Vector3 worldPosition, Camera battleCamera) =>
            Spawn(missTemplate, "MISS", "Miss", worldPosition, battleCamera);

        private void Spawn(Text source, string label, string objectName, Vector3 worldPosition, Camera battleCamera)
        {
            if (source == null || battleCamera == null) return;
            var text = Instantiate(source, source.transform.parent, false);
            text.name = objectName;
            text.text = label;
            text.raycastTarget = false;
            text.gameObject.SetActive(true);
            var popup = new Popup { text = text, color = source.color, world = worldPosition, camera = battleCamera };
            active.Add(popup);
            Place(popup);
        }

        /// <summary>Normalized animation: rise with ease-out, hold then fade, quick pop-in scale.</summary>
        public static void Evaluate(float elapsed, float duration, float rise, float popScale,
            out float alpha, out float offset, out float scale)
        {
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            offset = rise * (1f - (1f - t) * (1f - t));
            alpha = t < .4f ? 1f : 1f - (t - .4f) / .6f;
            scale = Mathf.Lerp(popScale, 1f, Mathf.Clamp01(t / .15f));
        }

        private void LateUpdate()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var popup = active[i];
                popup.elapsed += Time.unscaledDeltaTime;
                if (popup.text == null || popup.elapsed >= duration)
                {
                    if (popup.text != null) Destroy(popup.text.gameObject);
                    active.RemoveAt(i);
                    continue;
                }
                Place(popup);
            }
        }

        private void Place(Popup popup)
        {
            Evaluate(popup.elapsed, duration, risePixels, popScale, out float alpha, out float offset, out float scale);
            var rect = (RectTransform)popup.text.transform;
            Vector3 screen = popup.camera.WorldToScreenPoint(popup.world);
            Vector2 local = default;
            bool visible = popup.camera.isActiveAndEnabled && screen.z > 0f &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent, screen, null, out local);
            popup.text.enabled = visible;
            if (!visible) return;
            rect.localPosition = local + Vector2.up * offset;
            rect.localScale = Vector3.one * scale;
            popup.text.color = new Color(popup.color.r, popup.color.g, popup.color.b, popup.color.a * alpha);
        }

        public void Clear()
        {
            foreach (var popup in active) if (popup.text != null) Destroy(popup.text.gameObject);
            active.Clear();
        }
        private void OnDisable() => Clear();
    }
}