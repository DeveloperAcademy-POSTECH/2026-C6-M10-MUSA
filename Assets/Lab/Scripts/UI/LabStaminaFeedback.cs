using System;
using System.Globalization;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// A-target recovery fire and common approved-spend feedback on the existing stamina window.
    /// It owns only passive child graphics and never edits source colours, fill amount or layout.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(600)]
    public sealed class LabStaminaFeedback : MonoBehaviour
    {
        [SerializeField] private LabStaminaFeedbackProfile profile;
        private LabStaminaFeedbackProfile fallback;
        private LabNetwork network, subscribed;
        private LabHud hud;
        private UnityEngine.UI.Image source, pulse;
        private RectTransform root;
        private LabStaminaFlameGraphic rim;
        private LabStaminaFlameArtGraphic flames;
        private UnityEngine.UI.Text multiplier;
        private LabJigwiRewardFireProfile fireArt;
        private string eventId, session;
        private uint round;
        private bool hasIdentity, korean;
        private float fireAge, pulseAge = -1f, pendingCost, pulseFraction;

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
        public bool FireVisible { get; private set; }
        public bool PulseVisible { get; private set; }
        public int ConsumptionPulseCount { get; private set; }
        public float FireAnimationSeconds => fireAge;
        public float FireFillFraction { get; private set; }
        public float PulseAlpha { get; private set; }
        public float PulseFillFraction => pulseFraction;
        public RectTransform EffectRoot => root;
        public UnityEngine.UI.Image ConsumptionImage => pulse;
        public LabStaminaFlameGraphic FlameRim => rim;
        public LabStaminaFlameArtGraphic FlameArtwork => flames;
        public UnityEngine.UI.Text MultiplierLabel => multiplier;

        public void Bind(LabNetwork target, LabHud targetHud)
        {
            Unsubscribe(); network = target; hud = targetHud;
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
                network.config != null ? network.config.JigwiABPersonalRecoveryMultiplier : 4f,
                network.config != null ? network.config.StaminaMax : 120f);
        }
        /// <summary>Explicit visual-clock entry point, also used for isolated UI verification.</summary>
        public void Present(LabSnapshot snapshot, ulong localPlayer, double now, bool clockHeld,
            float deltaTime, float recoveryMultiplier, float staminaMaximum = 120f)
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

            LabJigwiABRound current = snapshot.jigwiAB?.Active(now);
            bool personal = Profile.PersonalFireEnabled && snapshot.jigwiAB?.enabled == true
                && current != null && current.kind == LabJigwiABKind.PersonalBurn
                && current.targetPlayer == localPlayer && current.Active(now);
            if (personal)
            {
                if (eventId != current.id)
                {
                    eventId = current.id;
                    // A late first snapshot may already be paused well after ignition.
                    // Seed from the battle clock so it shows the existing fire instead of
                    // waiting forever at a zero-width sweep. Do not count this frame twice.
                    fireAge = (float)Math.Max(0d, now - current.activeAt);
                }
                else fireAge += dt;
            }
            else { eventId = null; fireAge = 0f; }
            FireVisible = personal;
            FireFillFraction = personal ? fraction * IgnitionAmount(fireAge, Profile.IgnitionSeconds) : 0f;
            rim.gameObject.SetActive(personal); flames.gameObject.SetActive(personal);
            multiplier.gameObject.SetActive(personal);
            if (personal)
            {
                rim.Configure(FireFillFraction, fireAge, Profile.BorderWidthRatio, Profile.FireOpacity);
                if (fireArt == null)
                    using (LabFirstUse.Measure(LabFirstUseKind.RewardFire))
                        fireArt = Resources.Load<LabJigwiRewardFireProfile>(LabJigwiRewardFireProfile.ResourcesPath);
                Texture2D texture = Profile.FlameAtlas != null ? Profile.FlameAtlas : fireArt != null ? fireArt.FlameTexture : null;
                flames.Configure(texture, FireFillFraction, fireAge, Profile.FlameHeightRatio,
                    Profile.AnimationFps, Profile.FireOpacity);
                float value = float.IsNaN(recoveryMultiplier) || float.IsInfinity(recoveryMultiplier) ? 4f : Mathf.Max(1f, recoveryMultiplier);
                string label = (korean ? "회복 " : "RECOVERY x") + value.ToString("0.#", CultureInfo.InvariantCulture) + (korean ? "배" : string.Empty);
                if (multiplier.text != label) multiplier.text = label;
                multiplier.color = new Color(1f, .88f, .39f, .92f + .08f * Mathf.Sin(fireAge * 8f));
            }
            root.gameObject.SetActive(personal || PulseVisible);
            // The parent's whole subtree remains below the existing orb foreground. A local
            // last sibling keeps the short spend flash visible over the separate fever colours.
            root.SetAsLastSibling();
        }
        public static float IgnitionAmount(float age, float seconds) => Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(Mathf.Max(0f, age) / Mathf.Max(.001f, seconds)));
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
            rim = Rect("Jigwi A Fire Border", root).gameObject.AddComponent<LabStaminaFlameGraphic>();
            Stretch(rim.rectTransform); rim.raycastTarget = false;
            flames = Rect("Jigwi A Flame Artwork", root).gameObject.AddComponent<LabStaminaFlameArtGraphic>();
            Stretch(flames.rectTransform); flames.raycastTarget = false;
            multiplier = Rect("Jigwi A Recovery Multiplier", root).gameObject.AddComponent<UnityEngine.UI.Text>();
            var label = multiplier.rectTransform;
            label.anchorMin = new Vector2(.55f, 1f); label.anchorMax = Vector2.one;
            label.pivot = new Vector2(1f, 0f); label.anchoredPosition = new Vector2(0f, 7f);
            label.sizeDelta = new Vector2(0f, 26f);
            Font font = FindFont(); korean = font != null && font.HasCharacter('회');
            multiplier.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            multiplier.fontSize = 20; multiplier.resizeTextForBestFit = true;
            multiplier.resizeTextMinSize = 9; multiplier.resizeTextMaxSize = 20;
            multiplier.alignment = TextAnchor.MiddleRight;
            multiplier.raycastTarget = false;
            var outline = multiplier.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(.13f, .035f, .01f, .85f); outline.effectDistance = new Vector2(1f, -1f);
            SyncGeometry(); root.gameObject.SetActive(false);
        }
        private Font FindFont()
        {
            if (hud == null) return null;
            Font candidate = hud.timeText != null ? hud.timeText.font : null;
            if (candidate != null && candidate.HasCharacter('회')) return candidate;
            foreach (UnityEngine.UI.Text text in hud.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (text.font != null && text.font.HasCharacter('회')) return text.font;
            return candidate;
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
            eventId = null; fireAge = 0f; pulseAge = -1f; pendingCost = 0f; pulseFraction = 0f;
            FireVisible = PulseVisible = false; FireFillFraction = PulseAlpha = 0f;
            if (root != null) root.gameObject.SetActive(false);
        }
        private void DestroyView()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject);
            }
            root = null; pulse = null; rim = null; flames = null; multiplier = null;
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
