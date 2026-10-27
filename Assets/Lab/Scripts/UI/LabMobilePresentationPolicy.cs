using UnityEngine;
#if UNITY_IOS || UNITY_EDITOR
using UnityEngine.iOS;
#endif

namespace C6Lab
{
    // Platform-neutral policy: iOS-only Unity types are not linked into a Mac Player.
    [System.Flags]
    public enum LabSystemGestureEdges { None = 0, LeftEdge = 1, BottomEdge = 2, RightEdge = 4, TopEdge = 8 }

    /// <summary>
    /// Shared Lab/Hapio device presentation. It never changes network pause policy or saved Scene layout.
    /// Only native iOS players apply orientation/gesture settings; Editor and Mac keep their presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabMobilePresentationPolicy : MonoBehaviour
    {
        private LabNetwork network;
        private bool focused = true;
        private bool backgrounded;
        private bool modeApplied;
        private LabSystemGestureEdges appliedMode;

        public const LabSystemGestureEdges BattleGestureEdges = LabSystemGestureEdges.LeftEdge
            | LabSystemGestureEdges.BottomEdge | LabSystemGestureEdges.RightEdge;
        public LabSystemGestureEdges DesiredGestureMode { get; private set; }

        /// <summary>Runtime attachment keeps existing Scene/Prefab serialization untouched.</summary>
        public static LabMobilePresentationPolicy Attach(LabNetwork owner)
        {
            if (owner == null) return null;
            var policy = owner.GetComponent<LabMobilePresentationPolicy>();
            if (policy == null) policy = owner.gameObject.AddComponent<LabMobilePresentationPolicy>();
            policy.network = owner;
            return policy;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializePlayer()
        {
            ApplyPortrait();
#if UNITY_IOS && !UNITY_EDITOR
            Device.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.None;
#endif
        }

        private static void ApplyPortrait()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.orientation = ScreenOrientation.Portrait;
#endif
        }

        private void OnEnable()
        {
            if (network == null) network = GetComponent<LabNetwork>();
            ApplyPortrait();
            Refresh();
        }

        private void LateUpdate() => Refresh();

        /// <summary>Only evaluate cached references. No per-frame scene search or native call when unchanged.</summary>
        public void Refresh()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            LabOrbBoard board = network != null ? network.board : null;
            bool boardInteractive = isActiveAndEnabled && network != null && network.isActiveAndEnabled
                && board != null && board.isActiveAndEnabled && board.InteractionEnabled
                && !board.SimulationHeld && !board.UiInputBlocked;
            SetGestureMode(SelectGestureMode(snapshot != null ? snapshot.phase : LabPhase.Idle,
                boardInteractive, snapshot != null && snapshot.paused,
                network != null && network.GameStartHeld, focused && !backgrounded,
                network != null && network.IsConnected));
        }

        /// <summary>Preparing/countdown has a Playing snapshot but cannot defer gestures before input opens.</summary>
        public static LabSystemGestureEdges SelectGestureMode(LabPhase phase, bool boardInteractive,
            bool paused, bool startHeld, bool foreground, bool connected)
        {
            return connected && foreground && boardInteractive && !paused && !startHeld
                && phase == LabPhase.Playing ? BattleGestureEdges : LabSystemGestureEdges.None;
        }

        private void SetGestureMode(LabSystemGestureEdges mode)
        {
            DesiredGestureMode = mode;
            if (modeApplied && appliedMode == mode) return;
            appliedMode = mode;
            modeApplied = true;
#if UNITY_IOS && !UNITY_EDITOR
            var native = UnityEngine.iOS.SystemGestureDeferMode.None;
            if ((mode & LabSystemGestureEdges.LeftEdge) != 0) native |= UnityEngine.iOS.SystemGestureDeferMode.LeftEdge;
            if ((mode & LabSystemGestureEdges.BottomEdge) != 0) native |= UnityEngine.iOS.SystemGestureDeferMode.BottomEdge;
            if ((mode & LabSystemGestureEdges.RightEdge) != 0) native |= UnityEngine.iOS.SystemGestureDeferMode.RightEdge;
            if ((mode & LabSystemGestureEdges.TopEdge) != 0) native |= UnityEngine.iOS.SystemGestureDeferMode.TopEdge;
            Device.deferSystemGesturesMode = native;
#if DEVELOPMENT_BUILD
            Debug.Log("C6_MOBILE_GESTURE desired=" + mode + " applied=" + Device.deferSystemGesturesMode);
#endif
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (hasFocus)
            {
                ApplyPortrait();
                modeApplied = false; // Reapply to a resumed/recreated native view even if policy is unchanged.
            }
            Refresh();
            LogLifecycle("focus", hasFocus);
        }

        private void OnApplicationPause(bool paused)
        {
            backgrounded = paused;
            if (!paused)
            {
                ApplyPortrait();
                modeApplied = false;
            }
            Refresh();
            LogLifecycle("pause", paused);
        }

        private void OnDisable()
        {
            SetGestureMode(LabSystemGestureEdges.None);
            // Re-apply on the next enable even if the desired mode did not change.
            modeApplied = false;
        }

        private static void LogLifecycle(string change, bool value)
        {
#if UNITY_IOS && !UNITY_EDITOR && DEVELOPMENT_BUILD
            // Focus loss alone does not identify a home swipe or a crash. Keep only bounded state transitions.
            Debug.Log("C6_MOBILE_PRESENTATION event=" + change + " value=" + (value ? "1" : "0"));
#endif
        }
    }
}
