using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>One numeric field in the development-build tuning panel.</summary>
    public readonly struct LabDeveloperField
    {
        public readonly string Label;
        public readonly float Min;
        public readonly float Max;
        public readonly bool WholeNumber;

        public LabDeveloperField(string label, float min, float max, bool wholeNumber = false)
        {
            Label = label;
            Min = min;
            Max = max;
            WholeNumber = wholeNumber;
        }
    }

    /// <summary>
    /// The lab's single Inspector-editable gameplay tuning asset. Its values are copied into a
    /// running host session; visual prefabs may be replaced without changing these rules.
    /// </summary>
    [CreateAssetMenu(fileName = "LabConfig", menuName = "C6 Physics Lab/Config")]
    public sealed class LabConfig : ScriptableObject
    {
        // Keep this order aligned with CaptureDeveloperValues and TryApplyDeveloperValues.
        // Bounds apply only to in-game developer input; existing Inspector assets are unchanged.
        public static IReadOnlyList<LabDeveloperField> DeveloperFields { get; } = Array.AsReadOnly(new[]
        {
            new LabDeveloperField("Monster Max HP", 1f, 1000000f, true),
            new LabDeveloperField("Damage per Hit", 1f, 1000000f, true),
            new LabDeveloperField("Battle Duration (seconds)", 1f, 86400f),
            new LabDeveloperField("Maximum Stamina", 1f, 1000000f),
            new LabDeveloperField("Starting Stamina", 0f, 1000000f),
            new LabDeveloperField("Orb Generation Cost", .001f, 1000000f),
            new LabDeveloperField("Stamina Recovery Amount", 0f, 1000000f),
            new LabDeveloperField("Stamina Recovery Interval (seconds)", .001f, 86400f),
            new LabDeveloperField("Stamina Reward per Hit", 0f, 1000000f),
            new LabDeveloperField("Stored Orb Limit", 1f, 100f, true),
            new LabDeveloperField("Orb Lifetime (seconds)", .1f, 86400f),
            new LabDeveloperField("Orb Bounce", 0f, 1f),
            new LabDeveloperField("Orb Floor Deceleration", 0f, 100000f),
            new LabDeveloperField("Orb Stop Speed", 0f, 100000f),
            new LabDeveloperField("Orb Maximum Release Speed", .001f, 100000f),
            new LabDeveloperField("Orb Radius", .001f, 100f),
            new LabDeveloperField("Orb Board Height", .1f, 1000f),
            new LabDeveloperField("Spawn Rise Distance", 0f, 1000f),
            new LabDeveloperField("Spawn Rise Duration (seconds)", 0f, 86400f),
            new LabDeveloperField("Camera Radius", .001f, 1000f),
            new LabDeveloperField("Camera Height Offset", -1000f, 1000f),
            new LabDeveloperField("Throw Origin Radius", .001f, 1000f),
            new LabDeveloperField("Throw Origin Height Offset", -1000f, 1000f),
            new LabDeveloperField("Throw Origin Lateral Range", 0f, 1000f),
            new LabDeveloperField("Throw Minimum Swipe Speed", .001f, 100000f),
            new LabDeveloperField("Throw Minimum Swipe Distance", .001f, 1000f),
            new LabDeveloperField("Throw Upward Dominance", 0f, 1000f),
            new LabDeveloperField("Throw Maximum Swipe Speed", .001f, 100000f),
            new LabDeveloperField("Throw Minimum Upward Speed", .001f, 100000f),
            new LabDeveloperField("Throw Lateral Gain", .001f, 100000f),
            new LabDeveloperField("Throw Upward Gain", .001f, 100000f),
            new LabDeveloperField("Throw Forward Gain", .001f, 100000f),
            new LabDeveloperField("Throw Maximum World Speed", .001f, 100000f),
            new LabDeveloperField("Throw Gravity", .001f, 100000f),
            new LabDeveloperField("Throw Lifetime (seconds)", .001f, 86400f),
            new LabDeveloperField("Throw Radius", .001f, 100f),
            new LabDeveloperField("Throw Power Exponent", .25f, 3f),
            new LabDeveloperField("Throw Loft Offset (degrees)", -30f, 30f),
            new LabDeveloperField("Throw Air Damping", 0f, 5f)
        });

        [Header("Battle")]
        [SerializeField, Min(1)] private int monsterMaxHp = 1000;
        [SerializeField, Min(1)] private int damage = 20;
        [SerializeField, Min(1f)] private float battleDurationSeconds = 180f;

        [Header("Stamina and generation")]
        [SerializeField, Min(1f)] private float staminaMax = 100f;
        [SerializeField, Min(0f)] private float staminaStart = 100f;
        [SerializeField, Min(.001f)] private float generateCost = 20f;
        [SerializeField, Min(0f)] private float staminaRecoveryAmount = 20f;
        [SerializeField, Min(.001f)] private float staminaRecoverySeconds = 3f;
        [SerializeField, Min(0f)] private float hitRecovery = 5f;
        [SerializeField, Range(1, 100)] private int orbStorageLimit = 20;

        [Header("Orb lifetime")]
        [SerializeField, Min(.1f)] private float orbLifetimeSeconds = 8f;

        [Header("Flat orb board")]
        [SerializeField, Range(0f, 1f)] private float orbRestitution = .8f;
        [SerializeField, Min(0f)] private float orbFloorDeceleration = 2f;
        [SerializeField, Min(0f)] private float orbStopSpeed = .065f;
        [SerializeField, Min(.001f)] private float orbMaxReleaseSpeed = 3f;
        [SerializeField, Min(.001f)] private float orbRadius = .32f;
        [SerializeField, Min(.1f)] private float boardHeight = 5f;

        [Header("Spawn presentation")]
        [SerializeField, Min(0f)] private float spawnRiseDistance = .65f;
        [SerializeField, Min(0f)] private float spawnRiseDuration = .25f;

        [Header("Replaceable orb presentation")]
        [SerializeField] private LabOrbAppearance orbAppearance;

        [Header("Battle view and throw origin")]
        [SerializeField, Min(.001f)] private float cameraRadius = 8f;
        [SerializeField] private float cameraHeightOffset = .65f;
        [SerializeField, Min(.001f)] private float throwOriginRadius = 4f;
        [SerializeField] private float throwOriginHeightOffset = -.5f;
        [SerializeField, Min(0f)] private float throwOriginLateralRange = 1f;

        [Header("Release throw")]
        [SerializeField, Min(.001f)] private float throwSwipeMinSpeed = .8f;
        [SerializeField, Min(.001f)] private float throwSwipeMinDistance = .12f;
        [SerializeField, Min(0f)] private float throwSwipeUpDominance = .65f;
        [SerializeField, Min(.001f)] private float throwMaxSwipeSpeed = 6f;
        [SerializeField, Min(.001f)] private float throwMinUpSpeed = 2f;
        [SerializeField, Min(.001f)] private float throwLateralGain = 2.5f;
        [SerializeField, Min(.001f)] private float throwUpGain = 2.1f;
        [SerializeField, Min(.001f)] private float throwForwardGain = 4.5f;
        [SerializeField, Min(.001f)] private float throwMaxWorldSpeed = 18f;
        [SerializeField, Min(.001f)] private float throwGravity = 9.81f;
        [SerializeField, Min(.001f)] private float throwLifetime = 4f;
        [SerializeField, Min(.001f)] private float throwRadius = .165f;
        [SerializeField, Range(.25f, 3f)] private float throwPowerExponent = 1f;
        [SerializeField, Range(-30f, 30f)] private float throwLoftOffsetDegrees;
        [SerializeField, Range(0f, 5f)] private float throwAirDamping;

        public int MonsterMaxHp => Mathf.Max(1, monsterMaxHp);
        public int Damage => Mathf.Max(1, damage);
        public float BattleDurationSeconds => Positive(battleDurationSeconds, 180f);
        public float StaminaMax => Positive(staminaMax, 100f);
        public float StaminaStart => Mathf.Clamp(Finite(staminaStart, 100f), 0f, StaminaMax);
        public float GenerateCost => Mathf.Clamp(Positive(generateCost, 20f), .001f, StaminaMax);
        public float StaminaRecoveryAmount => Mathf.Clamp(Finite(staminaRecoveryAmount, 20f), 0f, StaminaMax);
        public float StaminaRecoverySeconds => Positive(staminaRecoverySeconds, 3f);
        public float StaminaRecoveryPerSecond => StaminaRecoveryAmount / StaminaRecoverySeconds;
        public float HitRecovery => Mathf.Clamp(Finite(hitRecovery, 5f), 0f, StaminaMax);
        public int OrbStorageLimit => Mathf.Clamp(orbStorageLimit, 1, 100);
        public float OrbLifetimeSeconds => Positive(orbLifetimeSeconds, 8f);
        public float OrbRestitution => Mathf.Clamp01(Finite(orbRestitution, .8f));
        public float OrbFloorDeceleration => Mathf.Max(0f, Finite(orbFloorDeceleration, 2f));
        public float OrbStopSpeed => Mathf.Clamp(Finite(orbStopSpeed, .065f), 0f, OrbMaxReleaseSpeed);
        public float OrbMaxReleaseSpeed => Positive(orbMaxReleaseSpeed, 3f);
        public float OrbRadius => Positive(orbRadius, .32f);
        public float BoardHeight => Positive(boardHeight, 5f);
        public float SpawnRiseDistance => Mathf.Max(0f, Finite(spawnRiseDistance, .65f));
        public float SpawnRiseDuration => Mathf.Max(0f, Finite(spawnRiseDuration, .25f));
        public LabOrbAppearance OrbAppearance => orbAppearance;
        public float CameraRadius => Positive(cameraRadius, 8f);
        public float CameraHeightOffset => Finite(cameraHeightOffset, .65f);
        public float ThrowOriginRadius => Positive(throwOriginRadius, 4f);
        public float ThrowOriginHeightOffset => Finite(throwOriginHeightOffset, -.5f);
        public float ThrowOriginLateralRange => Mathf.Max(0f, Finite(throwOriginLateralRange, 1f));
        public float ThrowSwipeMinSpeed => Positive(throwSwipeMinSpeed, .8f);
        public float ThrowSwipeMinDistance => Positive(throwSwipeMinDistance, .12f);
        public float ThrowSwipeUpDominance => Mathf.Max(0f, Finite(throwSwipeUpDominance, .65f));
        public float ThrowMaxSwipeSpeed => Positive(throwMaxSwipeSpeed, 6f);
        public float ThrowMinUpSpeed => Positive(throwMinUpSpeed, 2f);
        public float ThrowLateralGain => Positive(throwLateralGain, 2.5f);
        public float ThrowUpGain => Positive(throwUpGain, 2.1f);
        public float ThrowForwardGain => Positive(throwForwardGain, 4.5f);
        public float ThrowMaxWorldSpeed => Positive(throwMaxWorldSpeed, 18f);
        public float ThrowGravity => Positive(throwGravity, 9.81f);
        public float ThrowLifetime => Positive(throwLifetime, 4f);
        public float ThrowRadius => Positive(throwRadius, .165f);
        public float ThrowPowerExponent => Mathf.Clamp(throwPowerExponent <= 0f
            ? 1f : Finite(throwPowerExponent, 1f), .25f, 3f);
        public float ThrowLoftOffsetDegrees => Mathf.Clamp(Finite(throwLoftOffsetDegrees, 0f), -30f, 30f);
        public float ThrowAirDamping => Mathf.Clamp(Finite(throwAirDamping, 0f), 0f, 5f);

        /// <summary>Returns effective, Inspector-clamped values in DeveloperFields order.</summary>
        public float[] CaptureDeveloperValues() => new[]
        {
            (float)MonsterMaxHp, (float)Damage, BattleDurationSeconds,
            StaminaMax, StaminaStart, GenerateCost, StaminaRecoveryAmount,
            StaminaRecoverySeconds, HitRecovery, (float)OrbStorageLimit,
            OrbLifetimeSeconds, OrbRestitution, OrbFloorDeceleration, OrbStopSpeed,
            OrbMaxReleaseSpeed, OrbRadius, BoardHeight, SpawnRiseDistance,
            SpawnRiseDuration, CameraRadius, CameraHeightOffset, ThrowOriginRadius,
            ThrowOriginHeightOffset, ThrowOriginLateralRange, ThrowSwipeMinSpeed,
            ThrowSwipeMinDistance, ThrowSwipeUpDominance, ThrowMaxSwipeSpeed,
            ThrowMinUpSpeed, ThrowLateralGain, ThrowUpGain, ThrowForwardGain,
            ThrowMaxWorldSpeed, ThrowGravity, ThrowLifetime, ThrowRadius,
            ThrowPowerExponent, ThrowLoftOffsetDegrees, ThrowAirDamping
        };

        /// <summary>Validates the complete form before modifying any gameplay value.</summary>
        public bool TryApplyDeveloperValues(float[] values, out string error)
        {
            if (values == null || values.Length != DeveloperFields.Count)
            {
                error = "The developer settings form is incomplete.";
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                LabDeveloperField field = DeveloperFields[i];
                float value = values[i];
                if (float.IsNaN(value) || float.IsInfinity(value) || value < field.Min || value > field.Max)
                {
                    error = field.Label + " must be a finite number from " + field.Min + " to " + field.Max + ".";
                    return false;
                }
                if (field.WholeNumber && value != Mathf.Round(value))
                {
                    error = field.Label + " must be a whole number.";
                    return false;
                }
            }

            if (values[4] > values[3]) { error = "Starting Stamina cannot exceed Maximum Stamina."; return false; }
            if (values[5] > values[3]) { error = "Orb Generation Cost cannot exceed Maximum Stamina."; return false; }
            if (values[6] > values[3]) { error = "Stamina Recovery Amount cannot exceed Maximum Stamina."; return false; }
            if (values[8] > values[3]) { error = "Stamina Reward per Hit cannot exceed Maximum Stamina."; return false; }
            if (values[13] > values[14]) { error = "Orb Stop Speed cannot exceed Orb Maximum Release Speed."; return false; }
            if (values[24] > values[27]) { error = "Throw Minimum Swipe Speed cannot exceed Throw Maximum Swipe Speed."; return false; }
            if (values[28] > values[32]) { error = "Throw Minimum Upward Speed cannot exceed Throw Maximum World Speed."; return false; }

            monsterMaxHp = (int)values[0];
            damage = (int)values[1];
            battleDurationSeconds = values[2];
            staminaMax = values[3];
            staminaStart = values[4];
            generateCost = values[5];
            staminaRecoveryAmount = values[6];
            staminaRecoverySeconds = values[7];
            hitRecovery = values[8];
            orbStorageLimit = (int)values[9];
            orbLifetimeSeconds = values[10];
            orbRestitution = values[11];
            orbFloorDeceleration = values[12];
            orbStopSpeed = values[13];
            orbMaxReleaseSpeed = values[14];
            orbRadius = values[15];
            boardHeight = values[16];
            spawnRiseDistance = values[17];
            spawnRiseDuration = values[18];
            cameraRadius = values[19];
            cameraHeightOffset = values[20];
            throwOriginRadius = values[21];
            throwOriginHeightOffset = values[22];
            throwOriginLateralRange = values[23];
            throwSwipeMinSpeed = values[24];
            throwSwipeMinDistance = values[25];
            throwSwipeUpDominance = values[26];
            throwMaxSwipeSpeed = values[27];
            throwMinUpSpeed = values[28];
            throwLateralGain = values[29];
            throwUpGain = values[30];
            throwForwardGain = values[31];
            throwMaxWorldSpeed = values[32];
            throwGravity = values[33];
            throwLifetime = values[34];
            throwRadius = values[35];
            throwPowerExponent = values[36];
            throwLoftOffsetDegrees = values[37];
            throwAirDamping = values[38];
            error = string.Empty;
            return true;
        }

        private static float Finite(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        private static float Positive(float value, float fallback) => Mathf.Max(.001f, Finite(value, fallback));
    }
}
