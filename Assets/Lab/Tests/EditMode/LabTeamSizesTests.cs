using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// 땅따먹기 1:1 · 3:3 (#394): 2명·6명도 팀전이 된다. 앞 절반이 청, 뒤 절반이 홍이다. 1:1은 구슬을 넘기지 않고(양쪽 벽),
    /// 3:3은 팀끼리 앞뒤로 앉아 양쪽 모두 팀원에게 넘긴다. 북은 팀마다 하나, 피버는 1:1 6번 · 3:3 18번이다.
    /// </summary>
    public sealed class LabTeamSizesTests
    {
        private static readonly ulong[] Two = { 10, 20 }, Six = { 10, 20, 30, 40, 50, 60 };
        private LabConfig config;
        private int request;

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[5] = 1f;                       // 생성 비용을 낮춰 한 사람이 여러 번 만든다
            values[104] = 0f; values[105] = 5f;   // 작은 박, 한 번에 5%
            values[134] = (float)LabPaintPattern.Splatter;
            values[196] = 1f; values[197] = 30f; values[198] = 15f; values[199] = 8f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private LabModel Begin(string id, ulong[] ids)
        {
            var model = new LabModel();
            model.Begin(id, ids, 0d, config);
            return model;
        }

        private static ulong DrumHolder(LabModel model, ulong[] ids, LabTeam team) => ids.Single(id =>
            LabTeams.SeatTeam(Array.IndexOf(ids, id), ids.Length) == team
            && model.CopyOwnedStates(id).Any(state => state.kind.IsDrum()));

        private LabOrbState Generate(LabModel model, ulong owner, double at, LabOrbKind? kind = null)
        {
            for (int i = 0; i < 40; i++)
            {
                Assert.That(model.TryGenerate(owner, "g" + ++request, at, out var orb, out string reason), Is.True, reason);
                if (kind == null || orb.kind == kind) return orb;
            }
            Assert.Fail("Could not generate " + kind);
            return null;
        }

        private void Hit(LabModel model, ulong owner, double at, Vector3 direction)
        {
            var yin = Generate(model, owner, at, LabOrbKind.Yin);
            var yang = Generate(model, owner, at, LabOrbKind.Yang);
            Assert.That(model.TryCombineAt(owner, yin.id, yang.id, "c" + ++request, at, new Vector2(.5f, .5f),
                Vector2.zero, out var combined, out string reason), Is.True, reason);
            Assert.That(model.TryLaunch(owner, combined.id, "l" + ++request, at, out _, out reason), Is.True, reason);
            Assert.That(model.ConfirmHit(combined.id, at, direction, out _, out _, out _), Is.True);
        }

        private bool Transfer(LabModel model, ulong owner, string id, bool right, double at, out LabOrbState moved,
            out string reason, LabEdgeCrossingKind crossing = LabEdgeCrossingKind.Swipe) =>
            model.TryTransferAt(owner, id, right, new Vector2(right ? .99f : .01f, .5f), new Vector2(right ? 1f : -1f, 0f),
                "t" + ++request, at, crossing, out moved, out reason);

        private static LabSnapshot TeamSnapshot(ulong[] ids) => new LabSnapshot
        {
            teamTerritory = true,
            players = ids.Select((id, seat) => new LabPlayerState { id = id, seat = seat }).ToArray()
        };

        [Test]
        public void TwoFourAndSixPlayersSplitIntoBlueAndRedHalves()
        {
            Assert.That(Enumerable.Range(1, 7).Where(LabTeams.IsTeamCount), Is.EqualTo(new[] { 2, 4, 6 }));
            Assert.That(new[] { 2, 4, 6 }.Select(LabTeams.TeamSize), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(Enumerable.Range(0, 2).Select(seat => LabTeams.SeatTeam(seat, 2)),
                Is.EqualTo(new[] { LabTeam.Blue, LabTeam.Red }));
            Assert.That(Enumerable.Range(0, 6).Select(seat => LabTeams.SeatTeam(seat, 6)), Is.EqualTo(new[]
                { LabTeam.Blue, LabTeam.Blue, LabTeam.Blue, LabTeam.Red, LabTeam.Red, LabTeam.Red }));
            Assert.That(Enumerable.Range(0, 4).Select(seat => LabTeams.SeatTeam(seat, 4)),
                Is.EqualTo(Enumerable.Range(0, 4).Select(LabTeams.TeamOfSeat)), "2:2 keeps its halves.");
            Assert.That(LabTeams.SeatTeam(3, 2), Is.EqualTo(LabTeam.None), "No such seat in a two-player room.");
            Assert.That(LabTeams.SeatTeam(0, 5), Is.EqualTo(LabTeam.None), "Five players cannot split.");
        }

        [Test]
        public void SeatsAroundTheGourdFaceEachOtherAndThreeOnThreeSitsByTeam()
        {
            // 1:1: P1 and P2 face each other.
            Assert.That(new[] { 0, 1 }.Select(seat => LabTeams.RingSpot(seat, 2)), Is.EqualTo(new[] { 0, 1 }));
            // 2:2 is unchanged: P1, P3, P2, P4 alternate.
            Assert.That(Enumerable.Range(0, 4).Select(seat => LabTeams.RingSpot(seat, 4)),
                Is.EqualTo(Enumerable.Range(0, 4).Select(LabTeams.RingSeat)));
            // 3:3: Blue sits P2, P1, P3 and Red P5, P4, P6, each team side by side with P1 facing P4.
            int[] ring = Enumerable.Range(0, 6).Select(seat => LabTeams.RingSpot(seat, 6)).ToArray();
            Assert.That(ring.OrderBy(slot => slot), Is.EqualTo(Enumerable.Range(0, 6)), "One ring spot each.");
            Assert.That(ring[3] - ring[0], Is.EqualTo(3), "P1 and P4 sit opposite, in the middle of their teams.");
            Assert.That(new[] { ring[1], ring[2] }.OrderBy(slot => slot), Is.EqualTo(new[] { 1, 5 }), "P2 and P3 flank P1.");
            Assert.That(new[] { ring[4], ring[5] }.OrderBy(slot => slot), Is.EqualTo(new[] { 2, 4 }), "P5 and P6 flank P4.");
            Assert.That(LabTeams.RingSpot(4, 5), Is.EqualTo(4), "A story room keeps the P-number order.");
        }

        [Test]
        public void OneOnOneNeverPassesAndThreeOnThreePassesAroundItsOwnTeam()
        {
            foreach (int seat in new[] { 0, 1 })
                foreach (bool right in new[] { true, false })
                    Assert.That(LabTeams.ReceiverSeat(seat, right, 2), Is.EqualTo(-1), "1:1 has no teammate.");
            for (int seat = 0; seat < 4; seat++)
                foreach (bool right in new[] { true, false })
                    Assert.That(LabTeams.ReceiverSeat(seat, right, 4),
                        Is.EqualTo(LabTeams.EdgeOpen(seat, right) ? LabTeams.TeammateSeat(seat) : -1), "2:2 unchanged.");
            // 3:3, right edge: P1 → P3 → P2 → P1 and P4 → P6 → P5 → P4; left edge goes the other way round.
            Assert.That(Enumerable.Range(0, 6).Select(seat => LabTeams.ReceiverSeat(seat, true, 6)),
                Is.EqualTo(new[] { 2, 0, 1, 5, 3, 4 }));
            Assert.That(Enumerable.Range(0, 6).Select(seat => LabTeams.ReceiverSeat(seat, false, 6)),
                Is.EqualTo(new[] { 1, 2, 0, 4, 5, 3 }));
        }

        [Test]
        public void OneOnOneClosesBothEdgesAndThreeOnThreeNone()
        {
            LabSnapshot two = TeamSnapshot(Two), six = TeamSnapshot(Six);
            Assert.That(Two.Select(id => LabTeams.ClosedSide(two, id)), Is.All.EqualTo(LabTeams.BothSides));
            Assert.That(LabTeams.SideClosed(LabTeams.BothSides, true) && LabTeams.SideClosed(LabTeams.BothSides, false), Is.True);
            Assert.That(Six.Select(id => LabTeams.ClosedSide(six, id)), Is.All.Zero);
            Assert.That(LabTeams.TeamOf(six, 40), Is.EqualTo(LabTeam.Red));
            Assert.That(LabTeams.VisibleTeammates(six, 20).Select(player => player.id), Is.EqualTo(new ulong[] { 10, 20, 30 }));
            Assert.That(LabTeams.VisibleTeammates(two, 20).Select(player => player.id), Is.EqualTo(new ulong[] { 20 }));
        }

        [Test]
        public void OnlyTerritoryTakesSixPlayersAndTwoOrSixBecomeTeams()
        {
            Assert.That(Begin("one-on-one", Two).TeamTerritory, Is.True);
            Assert.That(Begin("three-on-three", Six).TeamTerritory, Is.True);
            Assert.That(LabModel.MaximumParticipants, Is.EqualTo(5), "The story stays three to five (two for DEV checks).");
            Assert.That(LabModel.TerritoryMaximumParticipants, Is.EqualTo(6));
            float[] values = config.CaptureDeveloperValues();
            values[196] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(() => Begin("story-six", Six), Throws.ArgumentException, "Six players only for 3:3 territory.");
            Assert.That(Begin("story-two", Two).TeamTerritory, Is.False);
        }

        [Test]
        public void OneOnOneGivesEachPlayerADrumAndNoPassEitherWay()
        {
            LabModel model = Begin("one-on-one-drums", Two);
            LabOrbState[] drums = model.Snapshot(0d).drums;
            Assert.That(drums.Select(drum => drum.owner).OrderBy(owner => owner), Is.EqualTo(Two));
            LabOrbState orb = Generate(model, 10, 1d);
            foreach (bool right in new[] { true, false })
            {
                Assert.That(Transfer(model, 10, orb.id, right, 1d, out _, out string reason), Is.False);
                Assert.That(reason, Is.EqualTo("TEAM_EDGE"));
                Assert.That(Transfer(model, 10, orb.id, right, 1d, out _, out reason, LabEdgeCrossingKind.Pushed), Is.False,
                    "A blast cannot push it across either.");
            }
        }

        [Test]
        public void ThreeOnThreeHasOneDrumPerTeamAndPassesStayInTheTeam()
        {
            foreach (string id in new[] { "s1", "s2", "s3", "s4" })
            {
                LabOrbState[] drums = Begin(id, Six).Snapshot(0d).drums;
                Assert.That(drums, Has.Length.EqualTo(2));
                Assert.That(drums.Select(drum => LabTeams.SeatTeam(Array.IndexOf(Six, drum.owner), 6)).OrderBy(team => team),
                    Is.EqualTo(new[] { LabTeam.Blue, LabTeam.Red }));
            }
            LabModel model = Begin("three-passes", Six);
            foreach (LabTeam team in new[] { LabTeam.Blue, LabTeam.Red })
                foreach (bool right in new[] { true, false })
                {
                    ulong owner = DrumHolder(model, Six, team);
                    LabOrbState orb = Generate(model, owner, 1d);
                    for (int pass = 0; pass < 4; pass++)
                    {
                        int seat = Array.IndexOf(Six, owner);
                        Assert.That(Transfer(model, owner, orb.id, right, 1d, out var moved, out string reason), Is.True, reason);
                        Assert.That(moved.owner, Is.EqualTo(Six[LabTeams.ReceiverSeat(seat, right, 6)]));
                        Assert.That(LabTeams.SeatTeam(Array.IndexOf(Six, moved.owner), 6), Is.EqualTo(team), "No walls, and never the other team.");
                        owner = moved.owner;
                    }
                }
        }

        [Test]
        public void FeverNeedsSixHitsInOneOnOneAndEighteenInThreeOnThree()
        {
            Assert.That(new[] { 2, 4, 6, 1 }.Select(config.TeamFeverHitsFor), Is.EqualTo(new[] { 6, 15, 18, 15 }));
            LabModel model = Begin("one-on-one-fever", Two);
            for (int hit = 0; hit < 5; hit++) Hit(model, 10, 1d, Vector3.forward);
            Assert.That(model.Snapshot(1d).blueFeverHits, Is.EqualTo(5));
            Assert.That(model.Snapshot(1d).FeverOn(LabTeam.Blue), Is.False);
            Hit(model, 10, 1d, Vector3.forward);
            Assert.That(model.Snapshot(1d).FeverOn(LabTeam.Blue), Is.True, "The sixth hit starts the 1:1 fever.");

            JsonUtility.FromJsonOverwrite("{\"teamFeverHitsThreeOnThree\":2}", config);
            Assert.That(config.TeamFeverHitsFor(6), Is.EqualTo(2), "Each size has its own Inspector value.");
            LabModel six = Begin("three-on-three-fever", Six);
            ulong red = DrumHolder(six, Six, LabTeam.Red);
            Hit(six, red, 1d, Vector3.forward);
            Assert.That(six.Snapshot(1d).FeverOn(LabTeam.Red), Is.False);
            Hit(six, red, 1d, Vector3.up);
            Assert.That(six.Snapshot(1d).FeverOn(LabTeam.Red), Is.True);
        }
    }
}
