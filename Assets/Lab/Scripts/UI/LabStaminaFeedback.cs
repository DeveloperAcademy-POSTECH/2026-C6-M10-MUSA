using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Common approved-spend feedback on the existing stamina window (#479 removed Jigwi A's recovery fire).
    /// It owns only passive child graphics and never edits source colours, fill amount or layout.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(600)]
    public sealed class LabStaminaFeedback : MonoBehaviour
    {
        [SerializeField] private LabStaminaFeedbackProfile profile;
        private LabStaminaFeedbackProfile fallback;
        private LabNetwork network, subscribed;
        private UnityEngine.UI.Image source, pulse;
        private RectTransform root;
        private string session;
        private uint round;
        private bool hasIdentity;
        private float pulseAge = -1f, pendingCost, pulseFraction;

        public LabStaminaFeedbackProfile Profile
        {
            get
            {
                if (profile != null) return profile;
                if (fallback != null) return fallback;
                profile = Resources.Load<LabStaminaFeedbackProfile>(LabStaminaFeedbackProfile.ResourcesPath);
                if (profile != null) return profile;
                if (fallback == null)
                {
                    fallback = ScriptableObject.CreateInstance<LabStaminaFeedbackProfile>();
                    fallback.hideFlags = HideFlags.HideAndDontSave;
                }
                return fallback;
            }
        }
        public bool PulseVisible { get; private set; }
        public int ConsumptionPulseCount { get; private set; }
        public float PulseAlpha { get; private set; }
        public float PulseFillFraction => pulseFraction;
        public RectTransform EffectRoot => root;
        public UnityEngine.UI.Image ConsumptionImage => pulse;

        public void Bind(LabNetwork target, LabHud targetHud)
        {
            Unsubscribe(); network = target;
            UnityEngine.UI.Image next = targetHud != null ? targetHud.staminaFill : null;
            if (source != next) { DestroyView(); source = next; }
            ResetPresentation(); hasIdentity = false; ConsumptionPulseCount = 0;
            EnsureView(); Subscribe();
        }
        public void SetProfile(LabStaminaFeedbackProfile value) { profile = value; ResetPresentation(); }
        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            if (!isActiveAndEnabled || network == null || subscribed == network) return;
            Unsubscribe(); subscribed = network; subscribed.LocalStaminaSpent += NotifySpent;
        }
        private void Unsubscribe()
        {
            if (subscribed != null) subscribed.LocalStaminaSpent -= NotifySpent;
            subscribed = null;
        }
        /// <summary>Call only for a deduplicated accepted cost spend, not a visual spawn or free supply.</summary>
        public void NotifySpent(float cost)
        {
            if (!(cost > 0f) || float.IsInfinity(cost) || !Profile.ConsumptionEnabled) return;
            if (network != null)
            {
                LabSnapshot snapshot = network.Snapshot;
                if (snapshot == null || snapshot.phase != LabPhase.Playing) return;
                // A new round's first receipt can precede its first presentation frame.
                // Retire old effects before adding this round's approved cost.
                ObserveIdentity(snapshot);
            }
            pendingCost = Mathf.Min(100000f, pendingCost + cost);
            ConsumptionPulseCount++;
        }
        private void LateUpdate()
        {
            if (network == null) { ResetPresentation(); return; }
            LabSnapshot snapshot = network.Snapshot;
            Present(snapshot, network.LocalPlayerId, network.JigwiABPresentationTime,
                snapshot != null && (snapshot.paused || network.GameStartHeld), Time.unscaledDeltaTime,
                network.config != null ? network.config.StaminaMax : 120f);
        }
        /// <summary>Explicit visual-clock entry point, also used for isolated UI verification.</summary>
        public void Present(LabSnapshot snapshot, ulong localPlayer, double now, bool clockHeld,
            float deltaTime, float staminaMaximum = 120f)
        {
            if (source == null || source.transform.parent == null || !source.gameObject.activeInHierarchy
                || snapshot == null || snapshot.phase != LabPhase.Playing
                || double.IsNaN(now) || double.IsInfinity(now))
            { ResetPresentation(); return; }
            ObserveIdentity(snapshot);
            EnsureView(); if (root == null) return;
            SyncGeometry();
            float dt = clockHeld || !(deltaTime > 0f) || float.IsInfinity(deltaTime) ? 0f : deltaTime;
            float fraction = Unit(source.fillAmount);
            if (!Profile.ConsumptionEnabled) { pendingCost = 0f; pulseAge = -1f; }
            else if (pendingCost > 0f)
            {
                // Keep the just-consumed blue coverage briefly visible, including the final
                // 20→0 spend. This is a fading afterimage, never a different stamina value.
                pulseFraction = Mathf.Clamp01(fraction + pendingCost / Mathf.Max(.001f, staminaMaximum));
                pendingCost = 0f; pulseAge = 0f;
            }
            if (pulseAge >= 0f) pulseAge += dt;
            float pulseWeight = ConsumptionAmount(pulseAge, Profile.ConsumptionSeconds);
            if (pulseAge >= Profile.ConsumptionSeconds) pulseAge = -1f;
            PulseAlpha = Profile.ConsumptionColor.a * pulseWeight;
            PulseVisible = PulseAlpha > .001f;
            pulse.sprite = source.sprite; pulse.preserveAspect = source.preserveAspect;
            pulse.type = UnityEngine.UI.Image.Type.Filled;
            pulse.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            pulse.fillOrigin = source.fillOrigin; pulse.fillAmount = pulseFraction;
            Color pulseTint = Profile.ConsumptionColor; pulseTint.a = PulseAlpha; pulse.color = pulseTint;
            pulse.gameObject.SetActive(PulseVisible);

            root.gameObject.SetActive(PulseVisible);
            // The parent's whole subtree remains below the existing orb foreground. A local
            // last sibling keeps the short spend flash visible over the separate fever colours.
            root.SetAsLastSibling();
        }
        public static float ConsumptionAmount(float age, float seconds) => age < 0f || age >= seconds
            ? 0f : 1f - Mathf.SmoothStep(0f, 1f, age / Mathf.Max(.001f, seconds));
        private static float Unit(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        private void EnsureView()
        {
            if (source == null || source.transform.parent == null) return;
            if (root != null && root.parent != source.transform.parent) DestroyView();
            if (root != null) return;
            root = Rect("Stamina Feedback", source.transform.parent);
            pulse = Rect("Approved Spend Blue Flash", root).gameObject.AddComponent<UnityEngine.UI.Image>();
            Stretch(pulse.rectTransform); pulse.raycastTarget = false;
            SyncGeometry(); root.gameObject.SetActive(false);
        }
        private RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = source.gameObject.layer;
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private void SyncGeometry()
        {
            if (source == null || root == null) return;
            RectTransform rect = source.rectTransform;
            root.anchorMin = rect.anchorMin; root.anchorMax = rect.anchorMax; root.pivot = rect.pivot;
            root.sizeDelta = rect.sizeDelta; root.anchoredPosition3D = rect.anchoredPosition3D;
            root.localRotation = rect.localRotation; root.localScale = rect.localScale;
        }
        private void ObserveIdentity(LabSnapshot snapshot)
        {
            if (hasIdentity && (session != snapshot.sessionId || round != snapshot.round)) ResetPresentation();
            session = snapshot.sessionId; round = snapshot.round; hasIdentity = true;
        }
        private void ResetPresentation()
        {
            pulseAge = -1f; pendingCost = 0f; pulseFraction = 0f;
            PulseVisible = false; PulseAlpha = 0f;
            if (root != null) root.gameObject.SetActive(false);
        }
        private void DestroyView()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject);
            }
            root = null; pulse = null;
        }
        private void OnDisable() { Unsubscribe(); ResetPresentation(); }
        private void OnDestroy()
        {
            Unsubscribe(); DestroyView();
            if (fallback != null)
            {
                if (Application.isPlaying) Destroy(fallback); else DestroyImmediate(fallback);
            }
        }
    }
}
