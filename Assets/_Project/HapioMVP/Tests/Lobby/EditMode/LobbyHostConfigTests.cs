using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using C6.Prototype.Lobby;
using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Tests.Lobby
{
    public sealed class LobbyHostConfigTests
    {
        private ScreenLayoutConfig config;
        private string json;
        private static IEnumerable<string> RequiredFields => typeof(LobbyHostConfig).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal);
        [SetUp] public void SetUp()
        { config = ScriptableObject.CreateInstance<ScreenLayoutConfig>(); json = JsonUtility.ToJson(LobbyHostConfig.Capture(config)); }
        [TearDown] public void TearDown() { if (config != null) UnityEngine.Object.DestroyImmediate(config); }
        private static Match Field(string value, string key)
        {
            var match = Regex.Match(value, "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(?:\\{[^{}]*\\}|\\\"(?:\\\\.|[^\\\"\\\\])*\\\"|[^,}\\s]+)");
            Assert.That(match.Success, Is.True, key); return match;
        }
        private static string Without(string value, string key)
        {
            var match = Field(value, key); int index = match.Index, length = match.Length;
            if (index + length < value.Length && value[index + length] == ',') length++;
            else if (index > 0 && value[index - 1] == ',') { index--; length++; }
            return value.Remove(index, length);
        }
        private static string Replace(string value, string key, string token)
        {
            var match = Field(value, key);
            return value.Substring(0, match.Index) + "\"" + key + "\":" + token + value.Substring(match.Index + match.Length);
        }
        private static void Reject(string value)
        { Assert.That(LobbyHostConfig.TryRead(value, out var parsed), Is.False); Assert.That(parsed, Is.Null); }

        [Test] public void CurrentSingleConfigCaptureRoundTripsEveryFieldWithoutChangingSource()
        {
            string before = JsonUtility.ToJson(config);
            Assert.That(LobbyHostConfig.TryRead(json, out var parsed), Is.True);
            Assert.That(JsonUtility.ToJson(parsed), Is.EqualTo(json));
            Assert.That(JsonUtility.ToJson(config), Is.EqualTo(before));
            Assert.That(parsed.schema, Is.EqualTo(3)); Assert.That(parsed.initialOrbs, Is.Zero);
            Assert.That(parsed.staminaStart, Is.EqualTo(100)); Assert.That(parsed.generateCost, Is.EqualTo(20));
            Assert.That(parsed.recoveryAmount, Is.EqualTo(20)); Assert.That(parsed.recoverySeconds, Is.EqualTo(3));
            Assert.That(parsed.hitRecovery, Is.EqualTo(5)); Assert.That(parsed.duration, Is.EqualTo(180));
            Assert.That(parsed.freeWorkspace, Is.True); Assert.That(parsed.attackTrigger, Is.EqualTo("BattleBoundary"));
        }
        [TestCaseSource(nameof(RequiredFields))]
        public void EveryRequiredTopLevelFieldMustBePresentEvenIfItsDefaultWouldBeAllowed(string key)
        { Reject(Without(json, key)); }
        [TestCaseSource(nameof(RequiredFields))]
        public void DuplicateTopLevelFieldIsRejectedEvenWhenValuesAreIdentical(string key)
        {
            string duplicate = Field(json, key).Value;
            Reject(json.Insert(json.Length - 1, "," + duplicate));
        }
        [TestCase("launchOrigin", "x")] [TestCase("launchOrigin", "y")] [TestCase("launchOrigin", "z")]
        [TestCase("launchAim", "x")] [TestCase("launchAim", "y")] [TestCase("launchAim", "z")]
        public void EveryNestedVectorCoordinateMustBePresent(string vector, string coordinate)
        {
            var match = Field(json, vector); string body = match.Value.Substring(match.Value.IndexOf('{'));
            Reject(Replace(json, vector, Without(body, coordinate)));
        }
        [TestCase("launchOrigin", "x")] [TestCase("launchOrigin", "y")] [TestCase("launchOrigin", "z")]
        [TestCase("launchAim", "x")] [TestCase("launchAim", "y")] [TestCase("launchAim", "z")]
        public void DuplicateNestedVectorCoordinatesAreRejected(string vector, string coordinate)
        {
            var match = Field(json, vector); string body = match.Value.Substring(match.Value.IndexOf('{'));
            Reject(Replace(json, vector, body.Insert(body.Length - 1, "," + Field(body, coordinate).Value)));
        }
        [Test] public void EscapedKeyCannotHideDuplicateFieldOrCoordinate()
        {
            Reject(json.Insert(json.Length - 1, ",\"\\u0073chema\":1"));
            Reject(Replace(json, "launchAim", "{\"x\":0,\"\\u0078\":0,\"y\":1.4,\"z\":0}"));
        }
        [Test] public void JsonWhitespaceAndReorderedFieldsRemainValid()
        {
            string reordered = "{\"defense\":false," + Without(json, "defense").Substring(1);
            string spaced = " \n\t" + reordered.Replace(",", ",\n ").Replace(":", " : ") + "\r\n";
            Assert.That(LobbyHostConfig.TryRead(spaced, out _), Is.True);
        }
        [TestCase("NaN")] [TestCase("Infinity")] [TestCase("-Infinity")]
        [TestCase("1e999")] [TestCase("-1e999")] [TestCase("null")]
        [TestCase("\"100\"")] [TestCase("+100")] [TestCase("01")]
        [TestCase(".5")] [TestCase("1.")] [TestCase("1e")]
        public void InvalidOrNonFiniteNumbersAreRejectedBeforeJsonUtility(string token)
        { Reject(Replace(json, "staminaStart", token)); }
        [TestCase("1.5")] [TestCase("1e0")] [TestCase("2147483648")]
        public void IntegerFieldsCannotBeTruncatedOrOverflowed(string token)
        { Reject(Replace(json, "schema", token)); }
        [TestCase("schema", "1")]
        [TestCase("initialOrbs", "1")]
        [TestCase("freeWorkspace", "false")]
        [TestCase("attackTrigger", "\"AttackBand\"")]
        [TestCase("defense", "true")]
        [TestCase("upperFraction", "0")]
        [TestCase("combinationRadius", "0.51")]
        [TestCase("horizontalSwipe", "1.01")]
        [TestCase("horizontalDominance", "0.9")]
        [TestCase("orbRadiusScreenFraction", "0.21")]
        [TestCase("orbRadiusCapScale", "0.99")]
        [TestCase("monsterHp", "0")]
        [TestCase("damage", "100001")]
        [TestCase("storageLimit", "21")]
        [TestCase("staminaMax", "0")]
        [TestCase("staminaStart", "-0.0000005")]
        [TestCase("staminaStart", "100.1")]
        [TestCase("generateCost", "0")]
        [TestCase("recoveryAmount", "101")]
        [TestCase("recoverySeconds", "0")]
        [TestCase("hitRecovery", "-1")]
        [TestCase("duration", "3601")]
        [TestCase("teamHpDecay", "0")]
        [TestCase("projectileSpeed", "201")]
        [TestCase("projectileLifetime", "31")]
        [TestCase("projectileRadius", "0")]
        [TestCase("snapshotRate", "61")]
        [TestCase("launchWidth", "31")]
        [TestCase("launchOrigin", "{\"x\":100001,\"y\":0,\"z\":0}")]
        [TestCase("launchAim", "{\"x\":0,\"y\":1e999,\"z\":0}")]
        public void OutOfRangeOrUnsupportedConfigDoesNotProduceUsableValue(string key, string token)
        { Reject(Replace(json, key, token)); }
        [Test] public void UnknownKeysWrongKindsTrailingContentAndOversizedPayloadAreRejected()
        {
            Reject(json.Insert(json.Length - 1, ",\"unknown\":1"));
            Reject(Replace(json, "launchAim", "[0,1.4,0]"));
            Reject(Replace(json, "launchAim", "{\"x\":0,\"y\":1.4,\"z\":0,\"w\":0}"));
            Reject(Replace(json, "freeWorkspace", "1"));
            Reject(json + "{}"); Reject(json.Insert(json.Length - 1, ","));
            Reject("{" + new string(' ', LobbyWire.MaximumConfigBytes) + json.Substring(1));
            Reject(null); Reject("{}");
        }
        [Test] public void FiniteExponentNumbersAndExactFloatBoundaryRemainSupported()
        {
            Assert.That(LobbyHostConfig.TryRead(Replace(json, "staminaStart", "1e2"), out _), Is.True);
            config.UpperFraction = ScreenLayoutConfig.MinimumFraction;
            string boundary = JsonUtility.ToJson(LobbyHostConfig.Capture(config));
            Assert.That(LobbyHostConfig.TryRead(boundary, out var parsed), Is.True);
            Assert.That(parsed.upperFraction, Is.EqualTo(ScreenLayoutConfig.MinimumFraction));
        }
    }
}
