using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Shows an approved hit at its actual point on the moving target. This is presentation only:
    /// it never performs a collision test, changes HP, or intercepts touches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabImpactFeedback : MonoBehaviour
    {
        [SerializeField] private Camera battleCamera;
        [SerializeField] private LabImpactParticleVfx particleVfx;
        [SerializeField, Min(.05f)] private float lifetime = .65f;
        [SerializeField, Min(1f)] private float numberRise = 46f;
        [SerializeField, Min(1)] private int numberFontSize = 28;

        private readonly List<ActiveHit> activeHits = new List<ActiveHit>();
        private RectTransform canvasRect;
        private RectTransform effectLayer;
        private Canvas canvas;
        private Font font;

        private sealed class ActiveHit
        {
            public Transform Anchor;
            public Vector3 AnchorLocalPoint;
            public RectTransform Root;
            public UnityEngine.UI.Text Number;
            public Color Color;
            public float StartedAt;
        }

        /// <summary>
        /// targetLocalPoint is relative to the real MeshCollider's transform, as sent by the
        /// Host. Keeping that local coordinate makes the marker follow pendulum motion.
        /// </summary>
        public void PlayHit(LabTarget target, Vector3 targetLocalPoint,
            LabOrbElement element, int actualDamage, Vector3 impactVelocity = default,
            bool showDamageNumber = true)
        {
            if ((showDamageNumber && actualDamage <= 0) || target == null
                || target.Hitbox == null || !isActiveAndEnabled)
                return;
            if (!showDamageNumber)
            {
                if (Finite(targetLocalPoint)) particleVfx?.Play(target, targetLocalPoint, element, impactVelocity);
                return;
            }
            if (!Finite(targetLocalPoint) || !EnsureLayer()) return;

            Transform hitbox = target.Hitbox.transform;
            Color color = LabOrbVisualFactory.TemporaryElementColor(element);
            var hit = new ActiveHit
            {
                Anchor = hitbox,
                AnchorLocalPoint = targetLocalPoint,
                Color = color,
                StartedAt = Time.time
            };
            hit.Root = NewRect("Hit Feedback", effectLayer);
            hit.Root.anchorMin = hit.Root.anchorMax = new Vector2(.5f, .5f);
            hit.Root.sizeDelta = Vector2.zero;
            hit.Number = CreateNumber(hit.Root, actualDamage, color);
            activeHits.Add(hit);
            UpdateHit(hit, 0f);
            particleVfx?.Play(target, targetLocalPoint, element, impactVelocity);
        }

        /// <summary>Remove presentation immediately when a round or room ends.</summary>
        public void Clear()
        {
            particleVfx?.Clear();
            for (int i = 0; i < activeHits.Count; i++)
                if (activeHits[i].Root != null) activeHits[i].Root.gameObject.SetActive(false);
            activeHits.Clear();
            if (effectLayer != null)
            {
                effectLayer.gameObject.SetActive(false);
                Destroy(effectLayer.gameObject);
            }
            effectLayer = null;
        }

        private bool EnsureLayer()
        {
            if (battleCamera == null) battleCamera = Camera.main;
            if (battleCamera == null) return false;
            if (effectLayer != null && canvasRect != null) return true;

            canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            canvas = canvas.rootCanvas;
            canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect == null) return false;
            UnityEngine.UI.Text[] existing = canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            for (int i = 0; i < existing.Length && font == null; i++)
                font = existing[i].font;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            effectLayer = NewRect("Impact Feedback", canvasRect);
            effectLayer.anchorMin = Vector2.zero;
            effectLayer.anchorMax = Vector2.one;
            effectLayer.offsetMin = Vector2.zero;
            effectLayer.offsetMax = Vector2.zero;
            effectLayer.SetAsLastSibling();
            return true;
        }

        private UnityEngine.UI.Text CreateNumber(RectTransform parent, int damage, Color color)
        {
            RectTransform rect = NewRect("Damage Number", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(140f, 60f);
            var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = font;
            label.fontSize = numberFontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = "-" + damage;
            label.color = color;
            label.raycastTarget = false;
            var outline = rect.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, .9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return label;
        }

        private void Update()
        {
            for (int i = activeHits.Count - 1; i >= 0; i--)
            {
                ActiveHit hit = activeHits[i];
                float progress = (Time.time - hit.StartedAt) / Mathf.Max(.05f, lifetime);
                if (progress >= 1f || hit.Anchor == null || hit.Root == null)
                {
                    if (hit.Root != null) Destroy(hit.Root.gameObject);
                    activeHits.RemoveAt(i);
                    continue;
                }
                UpdateHit(hit, Mathf.Clamp01(progress));
            }
        }

        private void UpdateHit(ActiveHit hit, float progress)
        {
            Vector3 screen = battleCamera.WorldToScreenPoint(hit.Anchor.TransformPoint(hit.AnchorLocalPoint));
            bool visible = screen.z > 0f && battleCamera.pixelRect.Contains(
                new Vector2(screen.x, screen.y));
            hit.Root.gameObject.SetActive(visible);
            if (!visible) return;

            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                    new Vector2(screen.x, screen.y), uiCamera, out Vector2 canvasPoint))
            {
                hit.Root.gameObject.SetActive(false);
                return;
            }
            hit.Root.anchoredPosition = canvasPoint;
            float opacity = 1f - progress;
            Color faded = hit.Color;
            faded.a = opacity;
            hit.Number.rectTransform.anchoredPosition = new Vector2(0f,
                26f + numberRise * progress);
            hit.Number.color = faded;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnValidate()
        {
            lifetime = Mathf.Max(.05f, lifetime);
            numberRise = Mathf.Max(1f, numberRise);
            numberFontSize = Mathf.Max(1, numberFontSize);
        }
    }
}
