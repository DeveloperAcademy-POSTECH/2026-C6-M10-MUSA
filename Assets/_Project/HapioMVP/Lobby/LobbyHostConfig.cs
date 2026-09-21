using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using C6.Prototype.Presentation;
using UnityEngine;

namespace C6.Prototype.Lobby
{
    public static class LobbyBuildInfo { public const string Build = "14"; }
    [Serializable]
    public sealed class LobbyHostConfig
    {
        public int schema = 2;
        public int initialOrbs;
        public float upperFraction, combinationRadius, horizontalSwipe, horizontalDominance;
        public bool freeWorkspace = true;
        public string attackTrigger = "BattleBoundary";
        public int monsterHp, damage, storageLimit;

        public int monsterHp2, monsterHp3, monsterHp4, monsterHp5;

        public double staminaMax, staminaStart, generateCost, recoveryAmount, recoverySeconds, hitRecovery, duration, teamHpDecay;
        public float projectileSpeed, projectileLifetime, projectileRadius, snapshotRate, launchWidth;
        public Vector3 launchOrigin, launchAim;
        public float throwMinUpSpeed;
        public float throwMaxInputSpeed;
        public float throwForwardGain;
        public float throwUpGain;
        public float throwLateralGain;
        public float throwMaxWorldSpeed;
        public float throwGravity;
        public float throwBounce;
        public float throwLifetime;
        public float throwSampleWindow;
        public float throwMinDuration;
        public float battleFramingPaddingFraction;
        public float throwFloorY;
        public float throwFloorFriction;
        public Vector3 throwFloorSize;
        public Vector3 monsterPosition;
        public Vector3 monsterHitboxCenter;
        public Vector3 monsterHitboxSize;
        public string monsterTargetId;
        public bool defense;
        public static LobbyHostConfig Capture(ScreenLayoutConfig c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            return new LobbyHostConfig { upperFraction=c.UpperFraction, combinationRadius=c.CombinationRadiusFraction,
                horizontalSwipe=c.HorizontalSwipeFraction, horizontalDominance=c.HorizontalDominance,
                monsterHp=c.MonsterMaxHp,monsterHp2=c.MonsterMaxHp2Players, monsterHp3=c.MonsterMaxHp3Players, monsterHp4=c.MonsterMaxHp4Players, monsterHp5=c.MonsterMaxHp5Players, damage=c.BaseDamage, storageLimit=c.OrbStorageLimit,
                staminaMax=c.StaminaMax, staminaStart=c.StaminaStart, generateCost=c.GenerateCost,
                recoveryAmount=c.StaminaRecoveryAmount, recoverySeconds=c.StaminaRecoverySeconds,
                hitRecovery=c.StaminaHitRecovery, duration=c.BattleDurationSeconds, teamHpDecay=c.TeamHpDecayPerSecond,
                projectileSpeed=c.ProjectileSpeed, projectileLifetime=c.ProjectileLifetime, projectileRadius=c.ProjectileRadius,
                snapshotRate=c.AttackSnapshotRateHz, launchWidth=c.LaunchWidth, launchOrigin=c.LaunchOrigin,
                launchAim=c.LaunchAim, throwMinUpSpeed=c.ThrowMinUpSpeed, throwMaxInputSpeed=c.ThrowMaxInputSpeed, throwForwardGain=c.ThrowForwardGain, throwUpGain=c.ThrowUpGain, throwLateralGain=c.ThrowLateralGain, throwMaxWorldSpeed=c.ThrowMaxWorldSpeed, throwGravity=c.ThrowGravity, throwBounce=c.ThrowBounce, throwLifetime=c.ThrowLifetime, throwSampleWindow=c.ThrowSampleWindow, throwMinDuration=c.ThrowMinDuration, battleFramingPaddingFraction=c.BattleFramingPaddingFraction, throwFloorY=c.ThrowFloorY, throwFloorFriction=c.ThrowFloorFriction, throwFloorSize=c.ThrowFloorSize, monsterPosition=c.MonsterPosition, monsterHitboxCenter=c.MonsterHitboxCenter, monsterHitboxSize=c.MonsterHitboxSize, monsterTargetId=c.MonsterTargetId, defense=c.EnableDefense };
        }
        public static bool TryRead(string json, out LobbyHostConfig value)
        {
            value = null;
            if (!LobbyWire.ValidConfigJson(json) || !new ConfigShape(json).Valid()) return false;
            LobbyHostConfig parsed;
            try { parsed = JsonUtility.FromJson<LobbyHostConfig>(json); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) { return false; }
            if (parsed == null || !ValidValues(parsed)) return false;
            value = parsed;
            return true;
        }
        public int MonsterHpFor(int participants) => participants switch
        { 2 => monsterHp2, 3 => monsterHp3, 4 => monsterHp4, 5 => monsterHp5, _ => monsterHp };
        private static bool ValidValues(LobbyHostConfig value)
        {
            if (value.schema != 2 || value.initialOrbs != 0 || !value.freeWorkspace
                || value.attackTrigger != "BattleBoundary" || value.defense) return false;
            return In(value.upperFraction, .01f, .99f) && In(value.combinationRadius, .01f, .5f)
                && In(value.horizontalSwipe, .01f, 1) && In(value.horizontalDominance, 1, 5)
                && value.monsterHp >= 1 && value.monsterHp <= 100000 && value.damage >= 1 && value.damage <= 100000 && In(value.monsterHp2, 1, 100000) && In(value.monsterHp3, 1, 100000) && In(value.monsterHp4, 1, 100000) && In(value.monsterHp5, 1, 100000)
                && value.storageLimit >= 1 && value.storageLimit <= 20 && In(value.staminaMax, 1, 100000)
                && In(value.staminaStart, 0, value.staminaMax) && In(value.generateCost, .001f, value.staminaMax)
                && In(value.recoveryAmount, 0, value.staminaMax) && In(value.recoverySeconds, .01f, 3600)
                && In(value.hitRecovery, 0, value.staminaMax) && In(value.duration, 1, 3600) && In(value.teamHpDecay, .001f, 1000)
                && In(value.projectileSpeed, .1f, 200) && In(value.projectileLifetime, .02f, 30) && In(value.projectileRadius, .01f, 2)
                && In(value.snapshotRate, 1, 60) && In(value.launchWidth, .1f, 30) && Finite(value.launchOrigin) && Finite(value.launchAim)
                && In(value.throwMinUpSpeed, .05f, 8f) && In(value.throwMaxInputSpeed, value.throwMinUpSpeed, 20f) && In(value.throwForwardGain, .1f, 30f) && In(value.throwUpGain, .1f, 30f) && In(value.throwLateralGain, .1f, 30f) && In(value.throwMaxWorldSpeed, 1f, 100f) && In(value.throwGravity, .1f, 50f) && In(value.throwBounce, 0f, 1f) && In(value.throwLifetime, .1f, 10f) && In(value.throwSampleWindow, .02f, .5f) && In(value.throwMinDuration, .005f, value.throwSampleWindow) && In(value.battleFramingPaddingFraction, 0f, .1f) && In(value.throwFloorY, -5f, 5f) && In(value.throwFloorFriction, 0f, 1f)
                && Finite(value.monsterPosition) && Finite(value.monsterHitboxCenter)
                && In(value.monsterHitboxSize.x,.01f,30) && In(value.monsterHitboxSize.y,.01f,30) && In(value.monsterHitboxSize.z,.01f,30)
                && In(value.throwFloorSize.x,1,100) && In(value.throwFloorSize.y,.01f,5) && In(value.throwFloorSize.z,1,100)
                && !string.IsNullOrWhiteSpace(value.monsterTargetId) && value.monsterTargetId.Length <= 64;
        }

        /// <summary>
        /// This fixed-schema guard runs before JsonUtility, which otherwise ignores absent and duplicate
        /// fields. It accepts reordered fields/JSON whitespace/escaped keys, and requires each known key
        /// exactly once. Values are still materialized exclusively through the installed JsonUtility.
        /// </summary>
        private sealed class ConfigShape
        {
            private enum Kind { Number, Integer, Boolean, Text, Vector }
            private static readonly Dictionary<string, Kind> Fields = new Dictionary<string, Kind>(StringComparer.Ordinal)
            {
                { "schema", Kind.Integer }, { "initialOrbs", Kind.Integer },
                { "upperFraction", Kind.Number }, { "combinationRadius", Kind.Number },
                { "horizontalSwipe", Kind.Number }, { "horizontalDominance", Kind.Number },
                { "freeWorkspace", Kind.Boolean }, { "attackTrigger", Kind.Text },
                { "monsterHp", Kind.Integer }, { "monsterHp2", Kind.Integer }, { "monsterHp3", Kind.Integer }, { "monsterHp4", Kind.Integer }, { "monsterHp5", Kind.Integer }, { "damage", Kind.Integer }, { "storageLimit", Kind.Integer },
                { "staminaMax", Kind.Number }, { "staminaStart", Kind.Number }, { "generateCost", Kind.Number },
                { "recoveryAmount", Kind.Number }, { "recoverySeconds", Kind.Number }, { "hitRecovery", Kind.Number },
                { "duration", Kind.Number }, { "teamHpDecay", Kind.Number }, { "projectileSpeed", Kind.Number },
                { "projectileLifetime", Kind.Number }, { "projectileRadius", Kind.Number }, { "snapshotRate", Kind.Number },
                { "launchWidth", Kind.Number }, { "launchOrigin", Kind.Vector }, { "launchAim", Kind.Vector },
                { "throwMinUpSpeed", Kind.Number },
                { "throwMaxInputSpeed", Kind.Number },
                { "throwForwardGain", Kind.Number },
                { "throwUpGain", Kind.Number },
                { "throwLateralGain", Kind.Number },
                { "throwMaxWorldSpeed", Kind.Number },
                { "throwGravity", Kind.Number },
                { "throwBounce", Kind.Number },
                { "throwLifetime", Kind.Number },
                { "throwSampleWindow", Kind.Number },
                { "throwMinDuration", Kind.Number },
                { "battleFramingPaddingFraction", Kind.Number },
                { "throwFloorY", Kind.Number },
                { "throwFloorFriction", Kind.Number },
                { "throwFloorSize", Kind.Vector },
                { "monsterPosition", Kind.Vector },
                { "monsterHitboxCenter", Kind.Vector },
                { "monsterHitboxSize", Kind.Vector },
                { "monsterTargetId", Kind.Text },
                { "defense", Kind.Boolean }
            };
            private static readonly Dictionary<string, Kind> Coordinates = new Dictionary<string, Kind>(StringComparer.Ordinal)
            { { "x", Kind.Number }, { "y", Kind.Number }, { "z", Kind.Number } };
            private readonly string source;
            private int position;
            internal ConfigShape(string source) { this.source = source; }
            internal bool Valid() { bool valid = Object(Fields); Space(); return valid && position == source.Length; }
            private bool Object(Dictionary<string, Kind> expected)
            {
                if (!Take('{')) return false;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                while (true)
                {
                    if (!Quoted(out string key) || !expected.TryGetValue(key, out var kind) || !seen.Add(key) || !Take(':')) return false;
                    bool valid;
                    switch (kind)
                    {
                        case Kind.Boolean: valid = Literal("true") || Literal("false"); break;
                        case Kind.Text: valid = Quoted(out _); break;
                        case Kind.Vector: valid = Object(Coordinates); break;
                        default: valid = Number(kind == Kind.Integer); break;
                    }
                    if (!valid) return false;
                    if (Take('}')) return seen.Count == expected.Count;
                    if (!Take(',')) return false;
                }
            }
            private bool Number(bool integer)
            {
                Space(); int start = position;
                if (position < source.Length && source[position] == '-') position++;
                if (position >= source.Length) return false;
                if (source[position] == '0') position++;
                else
                {
                    if (source[position] < '1' || source[position] > '9') return false;
                    while (position < source.Length && Digit(source[position])) position++;
                }
                if (!integer && position < source.Length && source[position] == '.')
                {
                    position++; int fraction = position;
                    while (position < source.Length && Digit(source[position])) position++;
                    if (position == fraction) return false;
                }
                if (!integer && position < source.Length && (source[position] == 'e' || source[position] == 'E'))
                {
                    position++;
                    if (position < source.Length && (source[position] == '+' || source[position] == '-')) position++;
                    int exponent = position;
                    while (position < source.Length && Digit(source[position])) position++;
                    if (position == exponent) return false;
                }
                string token = source.Substring(start, position - start);
                if (integer) return int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
                return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                    && !double.IsNaN(number) && !double.IsInfinity(number);
            }
            private bool Quoted(out string result)
            {
                result = null;
                if (!Take('"')) return false;
                var text = new StringBuilder();
                while (position < source.Length && text.Length <= 64)
                {
                    char c = source[position++];
                    if (c == '"') { result = text.ToString(); return true; }
                    if (c < 32) return false;
                    if (c == '\\')
                    {
                        if (position >= source.Length) return false;
                        char escaped = source[position++];
                        switch (escaped)
                        {
                            case '"': c = '"'; break;
                            case '\\': c = '\\'; break;
                            case '/': c = '/'; break;
                            case 'b': c = '\b'; break;
                            case 'f': c = '\f'; break;
                            case 'n': c = '\n'; break;
                            case 'r': c = '\r'; break;
                            case 't': c = '\t'; break;
                            case 'u':
                                if (position + 4 > source.Length || !ushort.TryParse(source.Substring(position, 4),
                                    NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort unicode)) return false;
                                c = (char)unicode; position += 4; break;
                            default: return false;
                        }
                    }
                    text.Append(c);
                }
                return false;
            }
            private bool Literal(string value)
            {
                Space();
                if (position + value.Length > source.Length || string.CompareOrdinal(source, position, value, 0, value.Length) != 0) return false;
                position += value.Length; return true;
            }
            private bool Take(char value)
            { Space(); if (position >= source.Length || source[position] != value) return false; position++; return true; }
            private void Space()
            { while (position < source.Length && (source[position] == ' ' || source[position] == '\t' || source[position] == '\r' || source[position] == '\n')) position++; }
            private static bool Digit(char c) => c >= '0' && c <= '9';
        }
        private static bool In(double x,double lo,double hi)=>!double.IsNaN(x)&&!double.IsInfinity(x)&&x>=lo&&x<=hi;
        private static bool Finite(Vector3 v)=>In(v.x,-100000,100000)&&In(v.y,-100000,100000)&&In(v.z,-100000,100000);
    }
}
