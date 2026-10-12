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
        [SerializeField, Min(.05f)] private float lifetime = .65f;
        [SerializeField, Min(1f)] private float initialRingDiameter = 18f;
        [SerializeField, Min(1f)] private float finalRingDiameter = 105f;
        [SerializeField, Min(1f)] private float ringThickness = 4f;
        [SerializeField, Min(1f)] private float numberRise = 46f;
        [SerializeField, Min(1)] private int numberFontSize = 28;

        private const int RingSegmentCount = 20;
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
            public RectTransform Ring;
            public UnityEngine.UI.Image[] Segments;
            public UnityEngine.UI.Text Number;
            public Color Color;
            public float StartedAt;
        }

        /// <summary>
        /// targetLocalPoint is relative to the real MeshCollider's transform, as sent by the
        /// Host. Keeping that local coordinate makes the marker follow pendulum motion.
        /// </summary>
        public void PlayHit(LabTarget target, Vector3 targetLocalPoint,
            LabOrbElement element, int actualDamage)
        {
            if (actualDamage <= 0 || target == null || target.Hitbox == null || !isActiveAndEnabled)
                return;
            if (!Finite(targetLocalPoint) || !EnsureLayer()) return;

            Transform hitbox = target.Hitbox.transform;
            Color color = LabOrbVisualFactory.TemporaryElementColor(element);
            var hit = new ActiveHit
            {
                Anchor = hitbox,
                AnchorLocalPoint = targetLocalPoint,
                Color = color,
                StartedAt = Time.unscaledTime
            };
            hit.Root = NewRect("Hit Feedback", effectLayer);
            hit.Root.anchorMin = hit.Root.anchorMax = new Vector2(.5f, .5f);
            hit.Root.sizeDelta = Vector2.zero;
            hit.Ring = NewRect("Expanding Hit Ring", hit.Root);
            hit.Ring.anchorMin = hit.Ring.anchorMax = new Vector2(.5f, .5f);
            hit.Ring.sizeDelta = Vector2.zero;
            hit.Segments = CreateRing(hit.Ring, color);
            hit.Number = CreateNumber(hit.Root, actualDamage, color);
            activeHits.Add(hit);
            UpdateHit(hit, 0f);
        }

        /// <summary>Remove presentation immediately when a round or room ends.</summary>
        public void Clear()
        {
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

        private UnityEngine.UI.Image[] CreateRing(RectTransform parent, Color color)
        {
            var segments = new UnityEngine.UI.Image[RingSegmentCount];
            float radius = Mathf.Max(initialRingDiameter, finalRingDiameter) * .5f;
            float length = 2f * Mathf.PI * radius / RingSegmentCount * 1.03f;
            for (int i = 0; i < RingSegmentCount; i++)
            {
                float degrees = 360f * i / RingSegmentCount;
                float radians = degrees * Mathf.Deg2Rad;
                RectTransform segment = NewRect("Ring Segment", parent);
                segment.anchorMin = segment.anchorMax = new Vector2(.5f, .5f);
                segment.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
                segment.sizeDelta = new Vector2(length, ringThickness);
                segment.localRotation = Quaternion.Euler(0f, 0f, degrees + 90f);
                var image = segment.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = color;
                image.raycastTarget = false;
                segments[i] = image;
            }
            return segments;
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
                float progress = (Time.unscaledTime - hit.StartedAt) / Mathf.Max(.05f, lifetime);
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
            float diameter = Mathf.Lerp(initialRingDiameter, finalRingDiameter, progress);
            hit.Ring.localScale = Vector3.one * (diameter / Mathf.Max(initialRingDiameter, finalRingDiameter));
            float opacity = 1f - progress;
            Color faded = hit.Color;
            faded.a = opacity;
            for (int i = 0; i < hit.Segments.Length; i++)
                hit.Segments[i].color = faded;
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
            initialRingDiameter = Mathf.Max(1f, initialRingDiameter);
            finalRingDiameter = Mathf.Max(initialRingDiameter, finalRingDiameter);
            ringThickness = Mathf.Max(1f, ringThickness);
            numberRise = Mathf.Max(1f, numberRise);
            numberFontSize = Mathf.Max(1, numberFontSize);
        }
    }
}
