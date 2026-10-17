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
                string original = LabSessionSettings.Fingerprint(values, 3, 5606);
                Assert.That(LabSessionSettings.Fingerprint(values, 3, 5606), Is.EqualTo(original));
                Assert.That(LabSessionSettings.Fingerprint(values, 2, 5606), Is.Not.EqualTo(original));
                Assert.That(LabSessionSettings.Fingerprint(values, 3, 5605), Is.Not.EqualTo(original));
                float defaultThrowDamping = values[38];
                values[38] += .1f;
                Assert.That(LabSessionSettings.Fingerprint(values, 3, 5606), Is.Not.EqualTo(original));
                values[38] = defaultThrowDamping;
                values[48] = 120f;
                Assert.That(LabSessionSettings.Fingerprint(values, 3, 5606), Is.Not.EqualTo(original),
                    "Player HP setting must be part of the shared room fingerprint.");
                values[48] = config.PlayerMaxHp;
                values[57] = 90f;
                Assert.That(LabSessionSettings.Fingerprint(values, 3, 5606), Is.Not.EqualTo(original),
                    "Target spin tuning must be part of the shared room fingerprint.");
                values[38] = float.NaN;
                Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(values, 3, 5606));
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
                    Assert.That(LabSessionSettings.Fingerprint(restored, 5, 5606),
                        Is.EqualTo(LabSessionSettings.Fingerprint(values, 5, 5606)));
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
            values[48] = 1f;
            values[57] = 200f;
            values[58] = 10f;
            values[59] = 1440f;
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
        public void BonjourAddressesFormOneSelectableRowPerRoomAndOnlySelectedRoutesJoin()
        {
            var targets = new[]
            {
                new LabBonjourTarget("192.0.2.10", 7777, 1, "C6Lab-alpha"),
                new LabBonjourTarget("2001:db8::10", 7777, 1, "C6Lab-alpha"),
                new LabBonjourTarget("fe80::10%4", 7777, 4, "C6Lab-alpha"),
                new LabBonjourTarget("192.0.2.20", 7777, 1, "C6Lab-beta"),
                new LabBonjourTarget("192.0.2.30", 7778, 1, "C6Lab-alpha")
            };
            var rooms = LabRoomCatalog.GroupTargets(targets);
            Assert.That(rooms.Count, Is.EqualTo(3));
            var selected = rooms.Single(room => room.Name == "C6Lab-alpha" && room.Port == 7777);
            var routes = LabRoomCatalog.RoutesForRoom(targets, selected.Key);
            Assert.That(routes.Select(route => route.Address), Is.EquivalentTo(new[]
                { "192.0.2.10", "2001:db8::10", "fe80::10%4" }));
            Assert.That(LabConnectionCandidates.Initial(routes.Select(route => route.Address), selected.Port)[0],
                Is.EqualTo("2001:db8::10"));
        }

        [Test]
        public void StaleOrUnusableBonjourRoomCannotSupplyJoinRoutes()
        {
            var first = new LabBonjourTarget("192.0.2.10", 7777, 1, "C6Lab-first");
            var second = new LabBonjourTarget("192.0.2.20", 7777, 1, "C6Lab-second");
            string staleKey = LabRoomCatalog.GroupTargets(new[] { first, second })
                .Single(room => room.Name == "C6Lab-first").Key;
            Assert.That(LabRoomCatalog.RoutesForRoom(new[] { second }, staleKey), Is.Empty);
            Assert.That(LabRoomCatalog.GroupTargets(new[]
                { new LabBonjourTarget("fe80::10", 7777, 1, "C6Lab-unusable"), second }).Count,
                Is.EqualTo(1));
        }

        [Test]
        public void FivePlayersWithOneHundredOrbsEachRoundTripsWithoutTruncation()
        {
            var players = new LabPlayerState[5];
            var orbs = new LabOrbState[500];
            for (int i = 0; i < players.Length; i++)
                players[i] = new LabPlayerState { id = (ulong)(i + 1), seat = i, stamina = 100f,
                    personalHp = 300 - i * 50 };
            for (int i = 0; i < orbs.Length; i++)
                orbs[i] = new LabOrbState { id = Guid.NewGuid().ToString("N"), owner = (ulong)(i / 100 + 1),
                    kind = LabOrbKind.Combined, origin = LabOrbOrigin.Combined,
                    originOwner = (ulong)(i / 100 + 1), x = .12345f, y = .98765f,
                    vx = 3.14159f, vy = -2.71828f, createdAt = 12345.6789d,
                    unlockedAt = i == 499 ? 12340.5d : -1d, revision = (ulong)(i + 1) };
            var snapshot = new LabSnapshot { sessionId = Guid.NewGuid().ToString("N"),
                round = 1, phase = LabPhase.Playing, hp = 100000, remaining = 180d,
                targetYawDegrees = 475.25f, targetYawSpeedDegreesPerSecond = -320.5f,
                players = players, orbs = orbs,
                drums = new[] { new LabOrbState { id = "drum", owner = 3ul, kind = LabOrbKind.Drum, x = .5f, y = .5f,
                    arrival = LabDrumArrival.Stop, stopAt = .37f, arrivedAt = 12345.5d } } };
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
            Assert.That(restored.players.Select(player => player.personalHp),
                Is.EqualTo(new[] { 300, 250, 200, 150, 100 }));
            Assert.That(restored.orbs.Length, Is.EqualTo(500));
            Assert.That(restored.orbs[499].id, Is.EqualTo(orbs[499].id));
            Assert.That(restored.orbs[499].owner, Is.EqualTo(5ul));
            Assert.That(restored.orbs[499].unlockedAt, Is.EqualTo(12340.5d), "The unlock time reaches every screen.");
            Assert.That(restored.orbs[0].unlockedAt, Is.EqualTo(-1d));
            Assert.That(restored.drums.Single().kind, Is.EqualTo(LabOrbKind.Drum), "Drums reach every screen.");
            Assert.That(restored.drums.Single().owner, Is.EqualTo(3ul));
            Assert.That(restored.drums.Single().arrival, Is.EqualTo(LabDrumArrival.Stop), "How a handed-on drum arrives reaches every screen.");
            Assert.That(restored.drums.Single().stopAt, Is.EqualTo(.37f));
            Assert.That(restored.drums.Single().arrivedAt, Is.EqualTo(12345.5d));
            Assert.That(restored.orbs[0].arrival, Is.EqualTo(LabDrumArrival.None));
            Assert.That(restored.targetYawDegrees, Is.EqualTo(475.25f));
            Assert.That(restored.targetYawSpeedDegreesPerSecond, Is.EqualTo(-320.5f));
        }
    }
}
