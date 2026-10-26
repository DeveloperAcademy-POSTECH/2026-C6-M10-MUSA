using UnityEngine;
using UnityEngine.iOS;

namespace C6Lab
{
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
        private SystemGestureDeferMode appliedMode;

        public const SystemGestureDeferMode BattleGestureEdges = SystemGestureDeferMode.LeftEdge
            | SystemGestureDeferMode.BottomEdge | SystemGestureDeferMode.RightEdge;
        public SystemGestureDeferMode DesiredGestureMode { get; private set; }

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
            Device.deferSystemGesturesMode = SystemGestureDeferMode.None;
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
        public static SystemGestureDeferMode SelectGestureMode(LabPhase phase, bool boardInteractive,
            bool paused, bool startHeld, bool foreground, bool connected)
        {
            return connected && foreground && boardInteractive && !paused && !startHeld
                && phase == LabPhase.Playing ? BattleGestureEdges : SystemGestureDeferMode.None;
        }

        private void SetGestureMode(SystemGestureDeferMode mode)
        {
            DesiredGestureMode = mode;
            if (modeApplied && appliedMode == mode) return;
            appliedMode = mode;
            modeApplied = true;
#if UNITY_IOS && !UNITY_EDITOR
            Device.deferSystemGesturesMode = mode;
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (hasFocus) ApplyPortrait();
            Refresh();
            LogLifecycle("focus", hasFocus);
        }

        private void OnApplicationPause(bool paused)
        {
            backgrounded = paused;
            if (!paused) ApplyPortrait();
            Refresh();
            LogLifecycle("pause", paused);
        }

        private void OnDisable()
        {
            SetGestureMode(SystemGestureDeferMode.None);
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
