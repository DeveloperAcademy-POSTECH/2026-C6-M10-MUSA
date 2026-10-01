using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6.Prototype.Lobby
{
    public enum LobbyDeveloperSetting
    {
        MonsterHp2,
        MonsterHp3,
        MonsterHp4,
        MonsterHp5,
        Damage,
        OrbStorageLimit,
        OrbSizePercent,
        BattleDuration,
        GenerateCost,
        RecoveryAmount,
        RecoverySeconds,
        HitRecovery,
        // #41 orb physics, #52 failed-combination bounce, #51 combined-orb expiry
        OrbRestitution,
        OrbContactFriction,
        OrbFloorDeceleration,
        OrbStopSpeed,
        OrbMaxReleaseSpeed,
        OrbReleaseSampleWindow,
        RejectedCombinationBounce,
        CombinedOrbLifetime,
        CombinedOrbWarning
    }

    /// <summary>
    /// A room-only draft of the saved gameplay defaults. It never writes to the Config asset.
    /// The host serializes the finished draft, so every participant adopts the same values.
    /// </summary>
    public sealed class LobbyDeveloperSettings
    {
        public static readonly IReadOnlyList<LobbyDeveloperSetting> OrderedSettings =
            new[]
            {
                LobbyDeveloperSetting.MonsterHp2,
                LobbyDeveloperSetting.MonsterHp3,
                LobbyDeveloperSetting.MonsterHp4,
                LobbyDeveloperSetting.MonsterHp5,
                LobbyDeveloperSetting.Damage,
                LobbyDeveloperSetting.OrbStorageLimit,
                LobbyDeveloperSetting.OrbSizePercent,
                LobbyDeveloperSetting.BattleDuration,
                LobbyDeveloperSetting.GenerateCost,
                LobbyDeveloperSetting.RecoveryAmount,
                LobbyDeveloperSetting.RecoverySeconds,
                LobbyDeveloperSetting.HitRecovery,
                LobbyDeveloperSetting.OrbRestitution,
                LobbyDeveloperSetting.OrbContactFriction,
                LobbyDeveloperSetting.OrbFloorDeceleration,
                LobbyDeveloperSetting.OrbStopSpeed,
                LobbyDeveloperSetting.OrbMaxReleaseSpeed,
                LobbyDeveloperSetting.OrbReleaseSampleWindow,
                LobbyDeveloperSetting.RejectedCombinationBounce,
                LobbyDeveloperSetting.CombinedOrbLifetime,
                LobbyDeveloperSetting.CombinedOrbWarning
            };

        private readonly Dictionary<LobbyDeveloperSetting, double> values =
            new Dictionary<LobbyDeveloperSetting, double>();
        private LobbyHostConfig defaults;

        public bool Ready => defaults != null;
        public bool Enabled { get; private set; }

        public void LoadDefaults(LobbyHostConfig value)
        {
            defaults = Clone(value ?? throw new ArgumentNullException(nameof(value)));
            ResetValues();
        }

        public void SetEnabled(bool value)
        {
            if (value && !Ready) throw new InvalidOperationException("Room defaults are not ready.");
            Enabled = value;
        }

        public void ResetValues()
        {
            if (!Ready) return;
            values[LobbyDeveloperSetting.MonsterHp2] = defaults.monsterHp2;
            values[LobbyDeveloperSetting.MonsterHp3] = defaults.monsterHp3;
            values[LobbyDeveloperSetting.MonsterHp4] = defaults.monsterHp4;
            values[LobbyDeveloperSetting.MonsterHp5] = defaults.monsterHp5;
            values[LobbyDeveloperSetting.Damage] = defaults.damage;
            values[LobbyDeveloperSetting.OrbStorageLimit] = defaults.storageLimit;
            values[LobbyDeveloperSetting.OrbSizePercent] = 100;
            values[LobbyDeveloperSetting.BattleDuration] = defaults.duration;
            values[LobbyDeveloperSetting.GenerateCost] = defaults.generateCost;
            values[LobbyDeveloperSetting.RecoveryAmount] = defaults.recoveryAmount;
            values[LobbyDeveloperSetting.RecoverySeconds] = defaults.recoverySeconds;
            values[LobbyDeveloperSetting.HitRecovery] = defaults.hitRecovery;
            values[LobbyDeveloperSetting.OrbRestitution] = defaults.orbRestitution;
            values[LobbyDeveloperSetting.OrbContactFriction] = defaults.orbContactFriction;
            values[LobbyDeveloperSetting.OrbFloorDeceleration] = defaults.orbFloorDeceleration;
            values[LobbyDeveloperSetting.OrbStopSpeed] = defaults.orbStopSpeed;
            values[LobbyDeveloperSetting.OrbMaxReleaseSpeed] = defaults.orbMaxReleaseSpeed;
            values[LobbyDeveloperSetting.OrbReleaseSampleWindow] = defaults.orbReleaseSampleWindow;
            values[LobbyDeveloperSetting.RejectedCombinationBounce] = defaults.rejectedCombinationBounceSpeed;
            values[LobbyDeveloperSetting.CombinedOrbLifetime] = defaults.combinedOrbLifetimeSeconds;
            values[LobbyDeveloperSetting.CombinedOrbWarning] = defaults.combinedOrbWarningSeconds;
        }

        public double Value(LobbyDeveloperSetting setting) =>
            values.TryGetValue(setting, out double value) ? value : 0;

        public void Adjust(LobbyDeveloperSetting setting, int direction)
        {
            if (!Ready || direction == 0) return;
            SetValue(setting, Value(setting) + Math.Sign(direction) * Step(setting));
        }

        public void SetValue(LobbyDeveloperSetting setting, double value)
        {
            if (!Ready || double.IsNaN(value) || double.IsInfinity(value)) return;
            double maximum = Maximum(setting);
            double clamped = Math.Max(Minimum(setting), Math.Min(maximum, value));
            if (IsInteger(setting)) clamped = Math.Round(clamped, MidpointRounding.AwayFromZero);
            else if (IsOrbSetting(setting)) clamped = Math.Round(clamped, 4); // no 0.30000000000000004 from repeated steps
            values[setting] = clamped;
            // Keep dependent limits valid: stop speed <= max release speed, blink <= lifetime.
            if (setting == LobbyDeveloperSetting.OrbMaxReleaseSpeed && Value(LobbyDeveloperSetting.OrbStopSpeed) > clamped)
                values[LobbyDeveloperSetting.OrbStopSpeed] = clamped;
            if (setting == LobbyDeveloperSetting.CombinedOrbLifetime && Value(LobbyDeveloperSetting.CombinedOrbWarning) > clamped)
                values[LobbyDeveloperSetting.CombinedOrbWarning] = clamped;
        }

        public string Label(LobbyDeveloperSetting setting)
        {
            switch (setting)
            {
                case LobbyDeveloperSetting.MonsterHp2: return "MONSTER HP · 2 PLAYERS";
                case LobbyDeveloperSetting.MonsterHp3: return "MONSTER HP · 3 PLAYERS";
                case LobbyDeveloperSetting.MonsterHp4: return "MONSTER HP · 4 PLAYERS";
                case LobbyDeveloperSetting.MonsterHp5: return "MONSTER HP · 5 PLAYERS";
                case LobbyDeveloperSetting.Damage: return "ATTACK DAMAGE";
                case LobbyDeveloperSetting.OrbStorageLimit: return "ORBS PER PLAYER";
                case LobbyDeveloperSetting.OrbSizePercent: return "ORB SIZE";
                case LobbyDeveloperSetting.BattleDuration: return "ROUND TIME";
                case LobbyDeveloperSetting.GenerateCost: return "GENERATE COST";
                case LobbyDeveloperSetting.RecoveryAmount: return "RECOVERY AMOUNT";
                case LobbyDeveloperSetting.RecoverySeconds: return "RECOVERY INTERVAL";
                case LobbyDeveloperSetting.HitRecovery: return "HIT RECOVERY";
                case LobbyDeveloperSetting.OrbRestitution: return "ORB BOUNCINESS";
                case LobbyDeveloperSetting.OrbContactFriction: return "ORB CONTACT FRICTION";
                case LobbyDeveloperSetting.OrbFloorDeceleration: return "ORB FLOOR SLOWDOWN";
                case LobbyDeveloperSetting.OrbStopSpeed: return "ORB STOP SPEED";
                case LobbyDeveloperSetting.OrbMaxReleaseSpeed: return "ORB MAX RELEASE SPEED";
                case LobbyDeveloperSetting.OrbReleaseSampleWindow: return "RELEASE SAMPLE TIME";
                case LobbyDeveloperSetting.RejectedCombinationBounce: return "FAILED COMBINE BOUNCE";
                case LobbyDeveloperSetting.CombinedOrbLifetime: return "ORB LIFETIME";
                case LobbyDeveloperSetting.CombinedOrbWarning: return "EXPIRY BLINK TIME";
                default: throw new ArgumentOutOfRangeException(nameof(setting));
            }
        }

        public string DisplayValue(LobbyDeveloperSetting setting)
        {
            double value = Value(setting);
            switch (setting)
            {
                case LobbyDeveloperSetting.OrbSizePercent: return value.ToString("0") + "%";
                case LobbyDeveloperSetting.BattleDuration: return value.ToString("0") + "s";
                case LobbyDeveloperSetting.RecoverySeconds: return value.ToString("0.0") + "s";
                case LobbyDeveloperSetting.OrbReleaseSampleWindow: return value.ToString("0.00") + "s";
                case LobbyDeveloperSetting.CombinedOrbLifetime: return value.ToString("0") + "s";
                case LobbyDeveloperSetting.CombinedOrbWarning: return value.ToString("0.0") + "s";
                case LobbyDeveloperSetting.OrbStopSpeed: return value.ToString("0.000");
                default: return value.ToString(IsInteger(setting) ? "0" : "0.##");
            }
        }

        public LobbyHostConfig BuildRoomConfig()
        {
            LobbyHostConfig result = Clone(defaults ?? throw new InvalidOperationException("Room defaults are not ready."));
            if (!Enabled) return result;

            result.monsterHp2 = Integer(LobbyDeveloperSetting.MonsterHp2);
            result.monsterHp3 = Integer(LobbyDeveloperSetting.MonsterHp3);
            result.monsterHp4 = Integer(LobbyDeveloperSetting.MonsterHp4);
            result.monsterHp5 = Integer(LobbyDeveloperSetting.MonsterHp5);
            result.damage = Integer(LobbyDeveloperSetting.Damage);
            result.storageLimit = Integer(LobbyDeveloperSetting.OrbStorageLimit);
            float orbScale = (float)(Value(LobbyDeveloperSetting.OrbSizePercent) / 100d);
            result.orbRadiusScreenFraction = Mathf.Clamp(defaults.orbRadiusScreenFraction * orbScale, .01f, .2f);
            result.orbRadiusCapScale = Mathf.Clamp(defaults.orbRadiusCapScale * orbScale, 1f, 3f);
            result.duration = Value(LobbyDeveloperSetting.BattleDuration);
            result.generateCost = Value(LobbyDeveloperSetting.GenerateCost);
            result.recoveryAmount = Value(LobbyDeveloperSetting.RecoveryAmount);
            result.recoverySeconds = Value(LobbyDeveloperSetting.RecoverySeconds);
            result.hitRecovery = Value(LobbyDeveloperSetting.HitRecovery);
            result.orbRestitution = (float)Value(LobbyDeveloperSetting.OrbRestitution);
            result.orbContactFriction = (float)Value(LobbyDeveloperSetting.OrbContactFriction);
            result.orbFloorDeceleration = (float)Value(LobbyDeveloperSetting.OrbFloorDeceleration);
            result.orbMaxReleaseSpeed = (float)Value(LobbyDeveloperSetting.OrbMaxReleaseSpeed);
            result.orbStopSpeed = Math.Min((float)Value(LobbyDeveloperSetting.OrbStopSpeed), Math.Min(1f, result.orbMaxReleaseSpeed));
            result.orbReleaseSampleWindow = (float)Value(LobbyDeveloperSetting.OrbReleaseSampleWindow);
            result.rejectedCombinationBounceSpeed = (float)Value(LobbyDeveloperSetting.RejectedCombinationBounce);
            result.combinedOrbLifetimeSeconds = (float)Value(LobbyDeveloperSetting.CombinedOrbLifetime);
            result.combinedOrbWarningSeconds = Math.Min((float)Value(LobbyDeveloperSetting.CombinedOrbWarning), result.combinedOrbLifetimeSeconds);

            string json = JsonUtility.ToJson(result);
            if (!LobbyHostConfig.TryRead(json, out LobbyHostConfig approved))
                throw new InvalidOperationException("Developer room settings are outside the supported range.");
            return approved;
        }

        private int Integer(LobbyDeveloperSetting setting) =>
            (int)Math.Round(Value(setting), MidpointRounding.AwayFromZero);

        private double Maximum(LobbyDeveloperSetting setting)
        {
            switch (setting)
            {
                case LobbyDeveloperSetting.OrbStorageLimit: return 20;
                case LobbyDeveloperSetting.OrbSizePercent: return 200;
                case LobbyDeveloperSetting.BattleDuration:
                case LobbyDeveloperSetting.RecoverySeconds: return 3600;
                case LobbyDeveloperSetting.GenerateCost:
                case LobbyDeveloperSetting.RecoveryAmount:
                case LobbyDeveloperSetting.HitRecovery: return defaults.staminaMax;
                case LobbyDeveloperSetting.OrbRestitution:
                case LobbyDeveloperSetting.OrbContactFriction: return 1;
                case LobbyDeveloperSetting.OrbFloorDeceleration:
                case LobbyDeveloperSetting.OrbMaxReleaseSpeed:
                case LobbyDeveloperSetting.RejectedCombinationBounce: return 10;
                case LobbyDeveloperSetting.OrbStopSpeed: return Math.Min(.5, Value(LobbyDeveloperSetting.OrbMaxReleaseSpeed));
                case LobbyDeveloperSetting.OrbReleaseSampleWindow: return .5;
                case LobbyDeveloperSetting.CombinedOrbLifetime: return 60;
                case LobbyDeveloperSetting.CombinedOrbWarning: return Value(LobbyDeveloperSetting.CombinedOrbLifetime);
                default: return 100000;
            }
        }

        private static double Minimum(LobbyDeveloperSetting setting)
        {
            switch (setting)
            {
                case LobbyDeveloperSetting.OrbStorageLimit: return 1;
                case LobbyDeveloperSetting.OrbSizePercent: return 50;
                case LobbyDeveloperSetting.RecoveryAmount:
                case LobbyDeveloperSetting.HitRecovery: return 0;
                case LobbyDeveloperSetting.RecoverySeconds: return .5;
                case LobbyDeveloperSetting.OrbRestitution:
                case LobbyDeveloperSetting.OrbContactFriction:
                case LobbyDeveloperSetting.RejectedCombinationBounce:
                case LobbyDeveloperSetting.CombinedOrbWarning: return 0;
                case LobbyDeveloperSetting.OrbFloorDeceleration: return .1;
                case LobbyDeveloperSetting.OrbStopSpeed: return .005;
                case LobbyDeveloperSetting.OrbMaxReleaseSpeed: return .5;
                case LobbyDeveloperSetting.OrbReleaseSampleWindow: return .02;
                default: return 1;
            }
        }

        private static double Step(LobbyDeveloperSetting setting)
        {
            switch (setting)
            {
                case LobbyDeveloperSetting.MonsterHp2:
                case LobbyDeveloperSetting.MonsterHp3:
                case LobbyDeveloperSetting.MonsterHp4:
                case LobbyDeveloperSetting.MonsterHp5: return 100;
                case LobbyDeveloperSetting.OrbStorageLimit: return 1;
                case LobbyDeveloperSetting.OrbSizePercent: return 10;
                case LobbyDeveloperSetting.BattleDuration: return 30;
                case LobbyDeveloperSetting.RecoverySeconds: return .5;
                case LobbyDeveloperSetting.HitRecovery: return 1;
                case LobbyDeveloperSetting.OrbRestitution:
                case LobbyDeveloperSetting.OrbContactFriction: return .05;
                case LobbyDeveloperSetting.OrbFloorDeceleration:
                case LobbyDeveloperSetting.OrbMaxReleaseSpeed:
                case LobbyDeveloperSetting.RejectedCombinationBounce: return .25;
                case LobbyDeveloperSetting.OrbStopSpeed: return .005;
                case LobbyDeveloperSetting.OrbReleaseSampleWindow: return .02;
                case LobbyDeveloperSetting.CombinedOrbLifetime: return 1;
                case LobbyDeveloperSetting.CombinedOrbWarning: return .5;
                default: return 5;
            }
        }

        private static bool IsInteger(LobbyDeveloperSetting setting)
        {
            switch (setting)
            {
                case LobbyDeveloperSetting.MonsterHp2:
                case LobbyDeveloperSetting.MonsterHp3:
                case LobbyDeveloperSetting.MonsterHp4:
                case LobbyDeveloperSetting.MonsterHp5:
                case LobbyDeveloperSetting.Damage:
                case LobbyDeveloperSetting.OrbStorageLimit:
                case LobbyDeveloperSetting.OrbSizePercent:
                case LobbyDeveloperSetting.BattleDuration: return true;
                default: return false;
            }
        }

        private static bool IsOrbSetting(LobbyDeveloperSetting setting) => setting >= LobbyDeveloperSetting.OrbRestitution;

        private static LobbyHostConfig Clone(LobbyHostConfig value)
        {
            string json = JsonUtility.ToJson(value);
            if (!LobbyHostConfig.TryRead(json, out LobbyHostConfig clone))
                throw new ArgumentException("Invalid room defaults.", nameof(value));
            return clone;
        }
    }
}
