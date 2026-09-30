using System;
using System.Collections.Generic;
using System.Reflection;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Attack.Tests
{
    public sealed class RoundSeatOrderTests
    {
        private static readonly ulong[] Roster = { 0, 41, 57, 88, 99 };
        private static ProjectileLaunchBasis Basis => new ProjectileLaunchBasis(new Vector3(0, 1, -5), Vector3.right, 8, new Vector3(0, 1, 5));
        private static ThrowTuning Tuning => new ThrowTuning(.35f, 8f, 8f, 2.8f, 6f, 30f, 9.81f, .45f, 4f, .2f, .12f, .02f);
        private static OrbThrowInput Swipe => new OrbThrowInput(new Vector2(.025f, .15f), .1f);

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void PermutedHostSeatDrivesBothNeighboursAndYawWithoutRenumberingPlayers(int count)
        {
            var roster = new ulong[count];
            Array.Copy(Roster, roster, count);
            var seats = new ulong[count];
            for (int i = 0; i < count; i++) seats[i] = roster[(i + 1) % count];
            Assert.That(ParticipantRing.ValidateSeatOrder(seats, roster), Is.True);
            Assert.That(roster[0], Is.EqualTo(0), "Host admission P1 identity is retained.");
            Assert.That(ParticipantRing.TrySeatIndex(seats, 0, out int seatIndex), Is.True);
            Assert.That(seatIndex, Is.EqualTo(count - 1));
            Assert.That(ParticipantRing.TryNeighbour(seats, 0, true, out var right), Is.True);
            Assert.That(right, Is.EqualTo(41));
            Assert.That(ParticipantRing.TryNeighbour(seats, 0, false, out var left), Is.True);
            Assert.That(left, Is.EqualTo(roster[count - 1]));
            float expectedYaw = 360f * (count - 1) / count;
            Assert.That(ParticipantViewAngle.CalculateSeatYaw(seatIndex + 1, count), Is.EqualTo(expectedYaw).Within(.0001f));
        }

        [Test]
        public void HostThrowUsesCopiedRoundSeatsAndRetryCanInstallAnotherPermutation()
        {
            var roster = new ulong[] { 0, 41, 57 };
            var seats = new ulong[] { 41, 0, 57 };
            var registry = new HostOrbRegistry(true); registry.BeginSession("round-seats", 1);
            var authority = new AttackAuthority(registry, 100, 20);
            authority.ConfigureReleaseThrows(Basis, Tuning);
            authority.ConfigureParticipantThrowFrames(roster);
            authority.ConfigureRoundSeats(seats);
            seats[1] = 999;
            authority.BeginDevelopmentRound();
            var combined = registry.RegisterDevelopmentOrb(0, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
            var first = authority.RequestLaunch(0, Launch(registry.RoundId, combined, "seat-two"));
            Assert.That(first.SpawnRequired && first.BallisticLaunch.HasValue, Is.True);
            Assert.That(ThrowMapping.TryCalculate(Vector2.one * .5f, Swipe, ParticipantLaunchFrame.Calculate(Basis, 2, 3),
                Tuning, out var expected, out _), Is.True);
            Assert.That(first.BallisticLaunch.Value.Position, Is.EqualTo(expected.Position));
            Assert.That(first.BallisticLaunch.Value.InitialVelocity, Is.EqualTo(expected.InitialVelocity));
            Assert.Throws<InvalidOperationException>(() => authority.ConfigureRoundSeats(new ulong[] { 0, 57, 41 }));
            authority.EndDevelopmentRound();
            registry.ResetRound(2);
            authority.ConfigureRoundSeats(new ulong[] { 0, 57, 41 });
            authority.BeginDevelopmentRound();
            combined = registry.RegisterDevelopmentOrb(0, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
            var retry = authority.RequestLaunch(0, Launch(registry.RoundId, combined, "new-seat-one"));
            Assert.That(retry.SpawnRequired, Is.True);
            Assert.That(ThrowMapping.TryCalculate(Vector2.one * .5f, Swipe, ParticipantLaunchFrame.Calculate(Basis, 1, 3),
                Tuning, out expected, out _), Is.True);
            Assert.That(retry.BallisticLaunch.Value.Position, Is.EqualTo(expected.Position));
            Assert.That(retry.BallisticLaunch.Value.InitialVelocity, Is.EqualTo(expected.InitialVelocity));
        }

        [Test]
        public void SeatsRejectMissingDuplicateAndForeignMembers()
        {
            Assert.That(ParticipantRing.ValidateSeatOrder(new ulong[] { 0, 41 }, new ulong[] { 0, 41, 57 }), Is.False);
            Assert.That(ParticipantRing.ValidateSeatOrder(new ulong[] { 0, 41, 41 }, new ulong[] { 0, 41, 57 }), Is.False);
            Assert.That(ParticipantRing.ValidateSeatOrder(new ulong[] { 0, 41, 99 }, new ulong[] { 0, 41, 57 }), Is.False);
            Assert.That(ParticipantRing.TrySeatIndex(new ulong[] { 0, 41, 57 }, 99, out _), Is.False);
        }

        [Test]
        public void SessionStoresAdmissionAndSeatsSeparatelyAndCopiesBothInputs()
        {
            var go = new GameObject("round-seat-session");
            try
            {
                var session = go.AddComponent<AttackSession>();
                var roster = new ulong[] { 0, 41, 57 };
                var seats = new ulong[] { 57, 0, 41 };
                session.ConfigureRoster(roster);
                session.ConfigureRoundSeats(seats);
                roster[0] = seats[0] = 999;
                Assert.That(session.OrderedParticipantIds, Is.EqualTo(new ulong[] { 0, 41, 57 }));
                Assert.That(session.RoundSeatOrder, Is.EqualTo(new ulong[] { 57, 0, 41 }));
                Assert.Throws<ArgumentException>(() => session.ConfigureRoundSeats(new ulong[] { 0, 41, 99 }));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ApprovedGameplayDetachClearsChoicesAndSeatsWhileKeepingTheRoomTransport()
        {
            var go = new GameObject("room-preserving-detach");
            try
            {
                var session = go.AddComponent<AttackSession>();
                var connection = go.GetComponent<DirectConnectionSession>();
                session.SetAggregateMode(true);
                session.ConfigureRoster(new ulong[] { 0, 41 });
                session.ConfigureRoundSeats(new ulong[] { 41, 0 });
                var choices = new Dictionary<ulong, OrbElement> { { 0, OrbElement.Fire }, { 41, OrbElement.Water } };
                session.ConfigureSelectedElements(choices);
                choices[0] = OrbElement.Earth;
                Assert.That(session.TryGetSelectedElement(0, out var selected), Is.True);
                Assert.That(selected, Is.EqualTo(OrbElement.Fire));
                Set(session, "explicitDevelopmentRequested", true);
                Set(session, "approvedSessionId", "approved-room-game");
                Set(session, "approvedRoundId", 1u);
                var roomState = connection.State;
                Assert.That(session.EndApprovedConnection(), Is.True);
                Assert.That(connection.State, Is.EqualTo(roomState), "No Stop or reconnect was requested.");
                Assert.That(session.Snapshot, Is.Null);
                Assert.That(session.RequiresExplicitRawElements, Is.False);
                Assert.That(session.MultiplayerRosterEnabled, Is.False);
                Assert.That(session.RoundSeatOrder.Count, Is.Zero);
                Assert.That(session.TryGetSelectedElement(0, out _), Is.False);
                Assert.DoesNotThrow(() => session.ConfigureRoster(new ulong[] { 0, 57 }));
                Assert.That(session.EndApprovedConnection(), Is.False, "A second detach is not another state transition.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void FullRoomExitAllowsNewRosterWhenClientRejoinsWithAnotherNetworkId()
        {
            var go = new GameObject("new-room-with-reassigned-client-id");
            try
            {
                var session = go.AddComponent<AttackSession>();
                session.SetAggregateMode(true);
                session.ConfigureRoster(new ulong[] { 0, 1 });
                session.ConfigureSelectedElements(new Dictionary<ulong, OrbElement>
                    { { 0, OrbElement.Fire }, { 1, OrbElement.Wood } });
                session.ConfigureRoundSeats(new ulong[] { 1, 0 });
                Set(session, "explicitDevelopmentRequested", true);
                Set(session, "approvedSessionId", "first-room");
                Set(session, "approvedRoundId", 1u);

                session.EndDevelopmentTest();

                Assert.That(session.MultiplayerRosterEnabled, Is.False);
                Assert.That(session.RequiresExplicitRawElements, Is.False);
                Assert.That(session.RoundSeatOrder.Count, Is.Zero);
                Assert.That(session.TryGetSelectedElement(1, out _), Is.False);
                var secondRoomRoster = new ulong[] { 0, 2 };
                Assert.DoesNotThrow(() => session.ConfigureRoster(secondRoomRoster));
                Assert.DoesNotThrow(() => session.ConfigureSelectedElements(new Dictionary<ulong, OrbElement>
                    { { 0, OrbElement.Fire }, { 2, OrbElement.Wood } }));
                Assert.DoesNotThrow(() => session.ConfigureRoundSeats(new ulong[] { 2, 0 }));
                Assert.That(session.OrderedParticipantIds, Is.EqualTo(secondRoomRoster));
                Assert.That(session.RoundSeatOrder, Is.EqualTo(new ulong[] { 2, 0 }));
                Assert.That(session.TryGetSelectedElement(2, out var selected), Is.True);
                Assert.That(selected, Is.EqualTo(OrbElement.Wood));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static OrbActionRequest Launch(uint round, OrbRecord orb, string id) =>
            new OrbActionRequest("round-seats", round, id, orb.OrbId, null, OrbActionKind.Launch, 1, Vector2.one * .5f, Swipe);
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
