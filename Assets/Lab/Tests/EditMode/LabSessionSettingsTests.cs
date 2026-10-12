using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabSessionSettingsTests
    {
        [Serializable]
        private sealed class JsonSettings
        {
            public float[] values;
        }

        [Test]
        public void FingerprintIncludesAllValuesParticipantCountAndProtocol()
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                float[] values = config.CaptureDeveloperValues();
                string original = LabSessionSettings.Fingerprint(values, 3, 5604);
                Assert.That(LabSessionSettings.Fingerprint(values, 3, 5604), Is.EqualTo(original));
                Assert.That(LabSessionSettings.Fingerprint(values, 2, 5604), Is.Not.EqualTo(original));
                Assert.That(LabSessionSettings.Fingerprint(values, 3, 5605), Is.Not.EqualTo(original));
                values[38] += .1f;
                Assert.That(LabSessionSettings.Fingerprint(values, 3, 5604), Is.Not.EqualTo(original));
                values[38] = float.NaN;
                Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(values, 3, 5604));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void ConfigFingerprintSurvivesUnityJsonRoundTripAtDefaultAndBounds()
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                float[] defaults = config.CaptureDeveloperValues();
                foreach (float[] values in new[] { defaults, ExtremeValidValues(defaults) })
                {
                    string serialized = JsonUtility.ToJson(new JsonSettings { values = values });
                    float[] restored = JsonUtility.FromJson<JsonSettings>(serialized).values;
                    Assert.That(restored.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
                    Assert.That(LabSessionSettings.Fingerprint(restored, 5, 5604),
                        Is.EqualTo(LabSessionSettings.Fingerprint(values, 5, 5604)));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        private static float[] ExtremeValidValues(float[] defaults)
        {
            var values = (float[])defaults.Clone();
            values[0] = 1000000f;
            values[1] = 1000000f;
            values[2] = 86400f;
            values[3] = 1000000f;
            values[4] = 1000000f;
            values[5] = 1000000f;
            values[6] = 1000000f;
            values[8] = 1000000f;
            values[9] = 100f;
            values[13] = 100000f;
            values[14] = 100000f;
            values[24] = 100000f;
            values[27] = 100000f;
            values[28] = 100000f;
            values[32] = 100000f;
            values[38] = 5f;
            return values;
        }

        [Test]
        public void NumericRoutesRetainIpv4Ipv6AndLinkLocalScope()
        {
            Assert.That(LabDualStackEndpoint.TryParse("192.0.2.3", 7777, out var ipv4, out _), Is.True);
            Assert.That(IPAddress.Parse(LabDualStackEndpoint.GetAddress(ipv4)), Is.EqualTo(IPAddress.Parse("192.0.2.3")));
            Assert.That(LabDualStackEndpoint.TryParse("2001:db8::3", 7777, out var ipv6, out _), Is.True);
            Assert.That(IPAddress.Parse(LabDualStackEndpoint.GetAddress(ipv6)), Is.EqualTo(IPAddress.Parse("2001:db8::3")));
            Assert.That(LabDualStackEndpoint.TryParse("fe80::3%7", 7777, out var scoped, out _), Is.True);
            Assert.That(LabDualStackEndpoint.GetScopeId(scoped), Is.EqualTo(7u));
            Assert.That(LabDualStackEndpoint.ToIPEndPoint(scoped).Address.ScopeId, Is.EqualTo(7));
            Assert.That(LabDualStackEndpoint.TryParse("fe80::3", 7777, out _, out _), Is.False);
            Assert.That(LabDualStackEndpoint.TryParse("192.0.2.3%7", 7777, out _, out _), Is.False);
        }

        [Test]
        public void CandidateBudgetReservesBothFamiliesAndAcceptsLateIpv6()
        {
            var manyIpv4 = Enumerable.Range(1, 10).Select(i => "192.0.2." + i).ToArray();
            string[] initial = LabConnectionCandidates.Initial(manyIpv4.Concat(new[] { "2001:db8::7" }), 7777);
            Assert.That(initial.Length, Is.EqualTo(LabConnectionCandidates.InitialRouteLimit));
            Assert.That(initial[0], Is.EqualTo("2001:db8::7"));
            Assert.That(initial, Does.Contain("192.0.2.1"));

            IList<string> delayed = new List<string>(LabConnectionCandidates.Initial(manyIpv4, 7777));
            Assert.That(LabConnectionCandidates.AppendDiscovered(delayed, "fe80::7%3", 7777), Is.True);
            Assert.That(delayed.Last(), Is.EqualTo("fe80::7%3"));
            Assert.That(LabConnectionCandidates.AppendDiscovered(delayed, "fe80::7%3", 7777), Is.False);
            Assert.That(LabConnectionCandidates.AppendDiscovered(delayed, "fe80::7", 7777), Is.False);
        }

        [Test]
        public void FivePlayersWithOneHundredOrbsEachRoundTripsWithoutTruncation()
        {
            var players = new LabPlayerState[5];
            var orbs = new LabOrbState[500];
            for (int i = 0; i < players.Length; i++)
                players[i] = new LabPlayerState { id = (ulong)(i + 1), seat = i, stamina = 100f };
            for (int i = 0; i < orbs.Length; i++)
                orbs[i] = new LabOrbState { id = Guid.NewGuid().ToString("N"), owner = (ulong)(i / 100 + 1),
                    kind = LabOrbKind.Combined, origin = LabOrbOrigin.Combined,
                    originOwner = (ulong)(i / 100 + 1), x = .12345f, y = .98765f,
                    vx = 3.14159f, vy = -2.71828f, createdAt = 12345.6789d,
                    revision = (ulong)(i + 1) };
            var snapshot = new LabSnapshot { sessionId = Guid.NewGuid().ToString("N"),
                round = 1, phase = LabPhase.Playing, hp = 100000, remaining = 180d,
                players = players, orbs = orbs };
            string[] chunks = LabSessionSettings.ChunkSnapshot(snapshot);
            Assert.That(chunks.Length, Is.GreaterThan(1));
            foreach (string chunk in chunks)
            {
                Assert.That(chunk.Length, Is.LessThanOrEqualTo(LabSessionSettings.SnapshotChunkCharacters));
                // Every outer packet is JSON-escaped again; even worst-case six-byte escapes
                // fit safely below the 32 KiB named-message receive bound.
                Assert.That(chunk.Length * 6 + 2048, Is.LessThan(32768));
            }
            LabSnapshot restored = JsonUtility.FromJson<LabSnapshot>(string.Concat(chunks));
            Assert.That(restored.players.Length, Is.EqualTo(5));
            Assert.That(restored.orbs.Length, Is.EqualTo(500));
            Assert.That(restored.orbs[499].id, Is.EqualTo(orbs[499].id));
            Assert.That(restored.orbs[499].owner, Is.EqualTo(5ul));
        }
    }
}
