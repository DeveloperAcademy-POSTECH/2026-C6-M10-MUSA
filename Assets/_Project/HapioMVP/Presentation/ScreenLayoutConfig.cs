using UnityEngine;

namespace C6.Prototype.Presentation
{
    /// <summary>The existing single configuration source, extended with input and T06 attack tuning.</summary>
    [CreateAssetMenu(menuName = "C6/Presentation/Screen Layout", fileName = "ScreenLayout")]
    public sealed class ScreenLayoutConfig : ScriptableObject
    {
        public const float DefaultUpperFraction = 0.55f;
        public const float MinimalBattleFooterHeight = 80f;
        // DEMO_ASSUMPTION: both cameras retain a nonzero viewport, even after an invalid edit.
        public const float MinimumFraction = 0.01f;
        public const float MaximumFraction = 0.99f;

        // DEMO_TUNING_VALUE. The lower fraction is always derived, never serialized separately.
        [SerializeField, Range(MinimumFraction, MaximumFraction)]
        private float upperFraction = DefaultUpperFraction;

        public float UpperFraction
        {
            get => Sanitize(upperFraction);
            set => upperFraction = Sanitize(value);
        }

        public float LowerFraction => 1f - UpperFraction;

        // T05 DEMO_TUNING_VALUE from PROJECT INPUT 2.9. The same saved asset is retained.
        [SerializeField, Range(.01f, 1f)] private float horizontalSwipeFraction = .18f;
        [SerializeField, Range(1f, 5f)] private float horizontalDominance = 1.25f;
        [SerializeField, Range(.01f, .5f)] private float combinationRadiusFraction = .08f;
        // DEMO_ASSUMPTION: top 18% of the actual lower viewport is the Attack Zone.
        // This is zone geometry, not the deferred upward throw-distance candidate.
        [SerializeField, Range(.01f, .5f)] private float attackZoneHeightFraction = .18f;
        // Presentation tuning: radius in complete screen-width units.
        [SerializeField, Range(.01f, .2f)] private float orbRadiusScreenFraction = .055f;
        // #4 tuning: multiplies the 5x4 grid radius cap in T09BattleController. 1 = previous behavior.
        [SerializeField, Range(1f, 3f)] private float orbRadiusCapScale = 1f;
        // #4 art: optional orb sprites. Empty = generated circle artwork (previous behavior).
        [SerializeField] private OrbArtSet orbArt;
        // #4 catch assist: touch pick radius multiplier for a moving orb, scaled by its speed
        // (0 at rest -> this value at max release speed). 1 = previous behavior. Physics colliders are unchanged.
        [SerializeField, Range(1f, 2f)] private float orbCatchRadiusScale = 1f;
        // 오행 v1: elements that can appear this round. Empty = no elements (previous behavior).
        [SerializeField] private OrbElement[] teamElements =
            { OrbElement.Fire, OrbElement.Water, OrbElement.Wood, OrbElement.Metal, OrbElement.Earth };
        public float HorizontalSwipeFraction => Valid(horizontalSwipeFraction, .18f, .01f, 1f);
        public float HorizontalDominance => Valid(horizontalDominance, 1.25f, 1f, 5f);
        public float CombinationRadiusFraction => Valid(combinationRadiusFraction, .08f, .01f, .5f);
        public float AttackZoneHeightFraction => Valid(attackZoneHeightFraction, .18f, .01f, .5f);
        public float OrbRadiusScreenFraction => Valid(orbRadiusScreenFraction, .055f, .01f, .2f);
        public float OrbRadiusCapScale => Valid(orbRadiusCapScale, 1f, 1f, 3f);
        public OrbArtSet OrbArt => orbArt;
        public float OrbCatchRadiusScale => Valid(orbCatchRadiusScale, 1f, 1f, 2f);
        public OrbElement[] TeamElements => teamElements ?? new OrbElement[0];
        public bool EnableDefense => false;

        [Header("P1 · Flat orb board (board widths / seconds)")]
        [SerializeField, Range(0f, 1f)] private float orbRestitution = .65f;
        [SerializeField, Range(0f, 1f)] private float orbContactFriction = .15f;
        // DEMO_TUNING_VALUE: contact friction alone cannot slow a freely moving top-down orb.
        [SerializeField] private float orbFloorDeceleration = .6f;
        [SerializeField] private float orbStopSpeed = .015f;
        [SerializeField] private float orbMaxReleaseSpeed = 2f;
        [SerializeField] private float orbReleaseSampleWindow = .12f;
        public float OrbRestitution => Valid(orbRestitution, .65f, 0f, 1f);
        public float OrbContactFriction => Valid(orbContactFriction, .15f, 0f, 1f);
        public float OrbFloorDeceleration => Valid(orbFloorDeceleration, .6f, .001f, 20f);
        public float OrbStopSpeed => Valid(orbStopSpeed, .015f, .001f, Mathf.Min(1f, OrbMaxReleaseSpeed));
        public float OrbMaxReleaseSpeed => Valid(orbMaxReleaseSpeed, 2f, .01f, 10f);
        public float OrbReleaseSampleWindow => Valid(orbReleaseSampleWindow, .12f, .02f, .5f);

        [Header("P0 · Benchmark monster (battlefield world units)")]
        [SerializeField] private string monsterTargetId = "dev-training-dummy";
        [SerializeField] private Vector3 monsterPosition = Vector3.zero;
        [SerializeField] private Vector3 monsterHitboxCenter = new Vector3(0f, 1.4f, 0f);

        [Tooltip("Box: X·Y·Z size. Capsule: X = diameter, Y = height (Z ignored).")]
        [SerializeField] private Vector3 monsterHitboxSize = new Vector3(1.2f, 2.6f, .65f);
        public string MonsterTargetId => string.IsNullOrWhiteSpace(monsterTargetId) ? "dev-training-dummy" : monsterTargetId;
        public Vector3 MonsterPosition => Finite(monsterPosition) ? monsterPosition : Vector3.zero;
        public Vector3 MonsterHitboxCenter => Finite(monsterHitboxCenter) ? monsterHitboxCenter : new Vector3(0f, 1.4f, 0f);
        public Vector3 MonsterHitboxSize => new Vector3(Valid(monsterHitboxSize.x, 1.2f, .01f, 30f),
            Valid(monsterHitboxSize.y, 2.6f, .01f, 30f), Valid(monsterHitboxSize.z, .65f, .01f, 30f));

        [Header("P2 · Release throw (screen widths / seconds, world units)")]
        // DEMO_TUNING_VALUE: only the ThrowBattle scene opts into release-based attacks.
        [SerializeField] private float throwMinUpSpeed = .35f;
        public float ThrowMinUpSpeed => Valid(throwMinUpSpeed, .35f, .05f, 8f);
        [SerializeField] private float throwMaxInputSpeed = 8f;
        public float ThrowMaxInputSpeed => Valid(throwMaxInputSpeed, 8f, ThrowMinUpSpeed, 20f);
        [SerializeField] private float throwForwardGain = 8f;
        public float ThrowForwardGain => Valid(throwForwardGain, 8f, .1f, 30f);
        [SerializeField] private float throwUpGain = 2.8f;
        public float ThrowUpGain => Valid(throwUpGain, 2.8f, .1f, 30f);
        [SerializeField] private float throwLateralGain = 6f;
        public float ThrowLateralGain => Valid(throwLateralGain, 6f, .1f, 30f);
        [SerializeField] private float throwMaxWorldSpeed = 30f;
        public float ThrowMaxWorldSpeed => Valid(throwMaxWorldSpeed, 30f, 1f, 100f);
        [SerializeField] private float throwGravity = 9.81f;
        public float ThrowGravity => Valid(throwGravity, 9.81f, .1f, 50f);
        [SerializeField] private float throwBounce = .45f;
        public float ThrowBounce => Valid(throwBounce, .45f, 0f, 1f);
        [SerializeField] private float throwLifetime = 4f;
        public float ThrowLifetime => Valid(throwLifetime, 4f, .1f, 10f);
        [SerializeField] private float throwSampleWindow = .12f;
        public float ThrowSampleWindow => Valid(throwSampleWindow, .12f, .02f, .5f);
        [SerializeField] private float throwMinDuration = .02f;
        public float ThrowMinDuration => Valid(throwMinDuration, .02f, .005f, ThrowSampleWindow);
        [SerializeField] private float battleFramingPaddingFraction = .025f;
        public float BattleFramingPaddingFraction => Valid(battleFramingPaddingFraction, .025f, 0f, .1f);
        [SerializeField] private float throwFloorY = 0f;
        public float ThrowFloorY => Valid(throwFloorY, 0f, -5f, 5f);
        [SerializeField] private float throwFloorFriction = .2f;
        public float ThrowFloorFriction => Valid(throwFloorFriction, .2f, 0f, 1f);
        [SerializeField] private Vector3 throwFloorSize = new Vector3(18f, .2f, 18f);
        public Vector3 ThrowFloorSize => new Vector3(Valid(throwFloorSize.x, 18f, 1f, 100f),
            Valid(throwFloorSize.y, .2f, .01f, 5f), Valid(throwFloorSize.z, 18f, 1f, 100f));

        // T06 values extend the same configuration asset; no second gameplay config.
        [SerializeField] private int monsterMaxHp = 100;
        [SerializeField] private int baseDamage = 20;

        [Header("#29 · Monster HP by participants (frozen multiplayer roster)")]
        [SerializeField] private int monsterMaxHp2Players = 800;
        [SerializeField] private int monsterMaxHp3Players = 1000;
        [SerializeField] private int monsterMaxHp4Players = 1200;
        [SerializeField] private int monsterMaxHp5Players = 1400;
        public int MonsterMaxHp2Players => Mathf.Clamp(monsterMaxHp2Players, 1, 100000);
        public int MonsterMaxHp3Players => Mathf.Clamp(monsterMaxHp3Players, 1, 100000);
        public int MonsterMaxHp4Players => Mathf.Clamp(monsterMaxHp4Players, 1, 100000);
        public int MonsterMaxHp5Players => Mathf.Clamp(monsterMaxHp5Players, 1, 100000);
        /// <summary>2~5 players use the table. Legacy 2-player scenes without a frozen roster pass 0 and keep MonsterMaxHp.</summary>
        public int MonsterMaxHpFor(int participants) => participants switch
        {
            2 => MonsterMaxHp2Players,
            3 => MonsterMaxHp3Players,
            4 => MonsterMaxHp4Players,
            5 => MonsterMaxHp5Players,
            _ => MonsterMaxHp
        };
        [Header("#28 · Monster attack and defense (seconds)")]
        [SerializeField] private float monsterAttackFirstDelaySeconds = 20f;
        [SerializeField] private float monsterAttackIntervalSeconds = 15f;
        [SerializeField] private float monsterAttackWarningSeconds = 3f;
        [SerializeField] private float defenseHoldSeconds = 2f;
        [SerializeField] private float defenseFailPenaltySeconds = 20f;
        public float MonsterAttackFirstDelaySeconds => Valid(monsterAttackFirstDelaySeconds, 20f, 1f, 600f);
        public float MonsterAttackIntervalSeconds => Valid(monsterAttackIntervalSeconds, 15f, 1f, 600f);
        public float MonsterAttackWarningSeconds => Valid(monsterAttackWarningSeconds, 3f, .5f, 30f);
        /// <summary>A defense must fit inside the warning, so the hold never exceeds it.</summary>
        public float DefenseHoldSeconds => Mathf.Min(Valid(defenseHoldSeconds, 2f, .1f, 30f), MonsterAttackWarningSeconds);
        public float DefenseFailPenaltySeconds => Valid(defenseFailPenaltySeconds, 20f, 0f, 600f);
        [SerializeField] private float projectileSpeed = 12f;
        [SerializeField] private float projectileLifetime = 3f;
        // DEMO_ASSUMPTION: T06 attack-view updates only, not the full T10 state system.
        [SerializeField] private float attackSnapshotRateHz = 20f;
        // DEMO_ASSUMPTION: explicit common battlefield coordinates, independent of Camera.main.
        [SerializeField] private Vector3 launchOrigin = new Vector3(0f, 1.08f, -4.5f);
        [SerializeField] private float launchWidth = 3f;
        [SerializeField] private Vector3 launchAim = new Vector3(0f, 1.4f, 0f);
        [SerializeField] private float projectileRadius = .165f;
        public int MonsterMaxHp => Mathf.Clamp(monsterMaxHp, 1, 100000);
        public int BaseDamage => Mathf.Clamp(baseDamage, 1, 100000);
        public float ProjectileSpeed => Valid(projectileSpeed, 12f, .1f, 200f);
        public float ProjectileLifetime => Valid(projectileLifetime, 3f, .02f, 30f);
        public float AttackSnapshotRateHz => Valid(attackSnapshotRateHz, 20f, 1f, 60f);
        public float ProjectileRadius => Valid(projectileRadius, .165f, .01f, 2f);
        public Vector3 LaunchOrigin => Finite(launchOrigin) ? launchOrigin : new Vector3(0f, 1.08f, -4.5f);
        public float LaunchWidth => Valid(launchWidth, 3f, .1f, 30f);
        public Vector3 LaunchAim => Finite(launchAim) ? launchAim : new Vector3(0f, 1.4f, 0f);
        // T07 explicit user tuning. The five visual divisions each represent 20 numeric points.
        [SerializeField] private float staminaMax = 100f;
        [SerializeField] private float staminaStart = 100f;
        [SerializeField] private float generateCost = 20f;
        [SerializeField] private float staminaRecoveryAmount = 20f;
        [SerializeField] private float staminaRecoverySeconds = 3f;
        [SerializeField] private float staminaHitRecovery = 5f;
        [SerializeField] private int orbStorageLimit = 20;
        // Availability of explicit development tools only. Every normal round still starts empty.
        [SerializeField] private bool resourceDebugToolsEnabled = true;
        public float StaminaMax => Valid(staminaMax, 100f, 1f, 100000f);
        public float StaminaStart => Valid(staminaStart, 100f, 0f, StaminaMax);
        public float GenerateCost => Valid(generateCost, 20f, .001f, StaminaMax);
        public float StaminaRecoveryAmount => Valid(staminaRecoveryAmount, 20f, 0f, StaminaMax);
        public float StaminaRecoverySeconds => Valid(staminaRecoverySeconds, 3f, .01f, 3600f);
        public double StaminaRecoveryPerSecond => StaminaRecoveryAmount / (double)StaminaRecoverySeconds;
        public float StaminaHitRecovery => Valid(staminaHitRecovery, 5f, 0f, StaminaMax);
        public int OrbStorageLimit => Mathf.Clamp(orbStorageLimit, 1, 20);
        public bool ResourceDebugToolsEnabled => resourceDebugToolsEnabled;

        // T09 DEMO_TUNING_VALUE: one shared Host-clock battle, not monster attack damage.
        [SerializeField] private float battleDurationSeconds = 180f;
        [SerializeField] private float teamHpDecayPerSecond = 1f;
        public double BattleDurationSeconds => Valid(battleDurationSeconds, 180f, 1f, 3600f);
        public double TeamHpDecayPerSecond => Valid(teamHpDecayPerSecond, 1f, .001f, 1000f);

        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        private static float Valid(float value, float fallback, float min, float max) =>
            Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);

        private void OnValidate() => upperFraction = Sanitize(upperFraction);

        private static float Sanitize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return DefaultUpperFraction;
            return Mathf.Clamp(value, MinimumFraction, MaximumFraction);
        }
    }
}
