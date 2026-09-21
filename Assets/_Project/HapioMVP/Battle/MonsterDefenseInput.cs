using System;
using C6.Prototype.Attack;
using C6.Prototype.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace C6.Prototype.Battle
{
    public enum DefenseZone { None, Left, Right }

    /// <summary>
    /// #28 the attacked player's two-hand defense: one touch that began in the left half of the upper battle area and
    /// one that began in the right half, held together for the Host hold time inside this screen's warning. Orb drags
    /// and throws begin in the lower orb area, so they can never count. Development builds and the Editor also accept
    /// holding the D key. A completed hold is only a report; the Host decides whether the attack was defended.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterDefenseInput : MonoBehaviour
    {
        private BattleSession battle;
        private AttackSession attack;
        private SplitScreenLayout layout;
        private Action<int> report;
        private Func<double?> hostClock;
        private bool touchEnabled;
        private readonly DefenseHoldTracker tracker = new DefenseHoldTracker();

        /// <summary>Both hands are down during this screen's warning and the hold has not completed yet.</summary>
        public bool Holding => tracker.Holding;

        public void Configure(BattleSession battleSession, AttackSession attackSession, SplitScreenLayout splitLayout, Action<int> reportDefense)
        { battle = battleSession; attack = attackSession; layout = splitLayout; report = reportDefense; }
        public void ConfigureHostClock(Func<double?> clock) => hostClock = clock;
        public void ConfigureReport(Action<int> reportDefense) => report = reportDefense;

        /// <summary>A defense touch belongs to the half of the upper battle area where it began; anywhere else never counts.</summary>
        public static DefenseZone Classify(Vector2 start, Rect battleArea) =>
            battleArea.width <= 0f || battleArea.height <= 0f || !battleArea.Contains(start) ? DefenseZone.None
            : start.x < battleArea.center.x ? DefenseZone.Left : DefenseZone.Right;

        private static bool DebugKeyAllowed => Application.isEditor || Debug.isDebugBuild || !Application.isMobilePlatform;

        private void OnEnable()
        {
            // Input System uses a reference count; this Disable balances only this Enable.
            if (!touchEnabled) { EnhancedTouchSupport.Enable(); touchEnabled = true; }
        }
        private void OnDisable()
        {
            if (touchEnabled) { EnhancedTouchSupport.Disable(); touchEnabled = false; }
            tracker.Reset();
        }

        private double? HostNow()
        {
            if (hostClock != null) return hostClock();
            return battle != null && battle.IsHost ? Time.realtimeSinceStartupAsDouble : (double?)null;
        }

        private void Update()
        {
            var state = battle != null ? battle.Snapshot : null;
            double? now = HostNow();
            bool warning = now.HasValue && attack != null && layout != null && MonsterAttackPresenter.Warns(state, attack.LocalPlayerId, now.Value);
            bool bothHeld = warning && BothZonesHeld();
            float hold = layout != null ? layout.Config.DefenseHoldSeconds : 0f;
            if (!tracker.Update(state?.roundId ?? 0, state?.attackSequence ?? 0, warning, bothHeld, Time.unscaledDeltaTime, hold)) return;
            Debug.Log($"C6_MONSTER_DEFENSE_REPORT round={state.roundId} attack={state.attackSequence} player={attack.LocalPlayerId} heldSeconds={tracker.HeldSeconds:F2} holdSeconds={hold:F2}");
            report?.Invoke(state.attackSequence);
        }

        private bool BothZonesHeld()
        {
            bool left = false, right = false;
            Rect area = layout.TopPixelRect;
            if (touchEnabled)
                foreach (var touch in EnhancedTouch.activeTouches)
                {
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
                    var zone = Classify(touch.startScreenPosition, area);
                    left |= zone == DefenseZone.Left; right |= zone == DefenseZone.Right;
                }
            var keyboard = Keyboard.current;
            if (DebugKeyAllowed && keyboard != null && keyboard.dKey.isPressed) left = right = true;
            return left && right;
        }
    }

    /// <summary>
    /// #28 local hold timer for one attack. It counts only while both hands are held inside the warning, restarts when
    /// either hand lifts, and completes at most once per attack so the Host receives a single report.
    /// </summary>
    public sealed class DefenseHoldTracker
    {
        private uint round;
        private int sequence;
        public double HeldSeconds { get; private set; }
        public bool Reported { get; private set; }
        public bool Holding { get; private set; }

        /// <summary>Returns true exactly once, on the frame the hold reaches holdSeconds for this attack.</summary>
        public bool Update(uint roundId, int attackSequence, bool warning, bool bothHeld, double deltaSeconds, double holdSeconds)
        {
            if (roundId != round || attackSequence != sequence)
            { round = roundId; sequence = attackSequence; HeldSeconds = 0; Reported = false; }
            Holding = attackSequence > 0 && warning && bothHeld && !Reported;
            if (!Holding) { if (!Reported) HeldSeconds = 0; return false; }
            HeldSeconds += Math.Max(0, deltaSeconds);
            if (holdSeconds <= 0 || HeldSeconds + 1e-6 < holdSeconds) return false;
            Reported = true; Holding = false;
            return true;
        }

        public void Reset() { round = 0; sequence = 0; HeldSeconds = 0; Reported = false; Holding = false; }
    }
}
