using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>2:2 청홍 땅따먹기 (#281)의 규칙 핵심: 팀 자리·전달·색·덮어쓰기·승패·피버·끄는 것.</summary>
    public sealed class LabTeamTerritoryTests
    {
        private static readonly ulong[] Ids = { 10, 20, 30, 40 };
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
            values[205] = 5f; values[153] = 2f;  // 공용·고유 기믹(#311), 콤보, 섬광을 켜 두어도 팀전에서는 꺼져야 한다
            values[155] = 1f; values[156] = 1f;
            values[166] = 1f; values[206] = 3f;
            values[184] = 1f; values[186] = 1f;
            values[196] = 1f; values[197] = 30f; values[198] = 0f; values[199] = 8f;
            Apply(values);
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private void Apply(float[] values) =>
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);

        private void Set(int index, float value)
        {
            float[] values = config.CaptureDeveloperValues();
            values[index] = value;
            Apply(values);
        }

        private LabModel Team(string id)
        {
            var model = new LabModel();
            model.Begin(id, Ids, 0d, config);
            return model;
        }

        private static int SeatOf(ulong id) => Array.IndexOf(Ids, id);

        // Reads the ledger without a snapshot, so it never asks the model to go back in time.
        private static ulong DrumHolder(LabModel model, LabTeam team) => Ids.Single(id =>
            LabTeams.TeamOfSeat(SeatOf(id)) == team && model.CopyOwnedStates(id).Any(state => state.kind.IsDrum()));

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

        private LabOrbState Combine(LabModel model, ulong owner, double at)
        {
            var yin = Generate(model, owner, at, LabOrbKind.Yin);
            var yang = Generate(model, owner, at, LabOrbKind.Yang);
            Assert.That(model.TryCombineAt(owner, yin.id, yang.id, "c" + ++request, at, new Vector2(.5f, .5f),
                Vector2.zero, out var combined, out string reason), Is.True, reason);
            return combined;
        }

        private LabOrbElement Hit(LabModel model, ulong owner, double at, Vector3 direction)
        {
            var combined = Combine(model, owner, at);
            Assert.That(model.TryLaunch(owner, combined.id, "l" + ++request, at, out _, out string reason), Is.True, reason);
            Assert.That(model.ConfirmHit(combined.id, at, direction, out var element, out _, out _), Is.True);
            return element;
        }

        private bool Transfer(LabModel model, ulong owner, string id, bool right, double at, out LabOrbState moved,
            out string reason, LabEdgeCrossingKind crossing = LabEdgeCrossingKind.Swipe) =>
            model.TryTransferAt(owner, id, right, new Vector2(right ? .99f : .01f, .5f), new Vector2(right ? 1f : -1f, 0f),
                "t" + ++request, at, crossing, out moved, out reason);

        [Test]
        public void SeatRulesPairP1WithP2AndP3WithP4AndAlternateAroundTheGourd()
        {
            Assert.That(Enumerable.Range(0, 4).Select(LabTeams.TeamOfSeat),
                Is.EqualTo(new[] { LabTeam.Blue, LabTeam.Blue, LabTeam.Red, LabTeam.Red }));
            Assert.That(Enumerable.Range(0, 4).Select(LabTeams.TeammateSeat), Is.EqualTo(new[] { 1, 0, 3, 2 }));
            // 팀의 왼쪽 사람(P1·P3)은 오른쪽으로만, 오른쪽 사람(P2·P4)은 왼쪽으로만 넘긴다.
            Assert.That(LabTeams.EdgeOpen(0, true) && !LabTeams.EdgeOpen(0, false), Is.True);
            Assert.That(LabTeams.EdgeOpen(1, false) && !LabTeams.EdgeOpen(1, true), Is.True);
            Assert.That(LabTeams.EdgeOpen(2, true) && !LabTeams.EdgeOpen(2, false), Is.True);
            Assert.That(LabTeams.EdgeOpen(3, false) && !LabTeams.EdgeOpen(3, true), Is.True);
            // 박 둘레: P1, P3, P2, P4 = 청·홍·청·홍
            int[] ring = Enumerable.Range(0, 4).Select(LabTeams.RingSeat).ToArray();
            Assert.That(Enumerable.Range(0, 4).Select(slot => LabTeams.TeamOfSeat(Array.IndexOf(ring, slot))),
                Is.EqualTo(new[] { LabTeam.Blue, LabTeam.Red, LabTeam.Blue, LabTeam.Red }));
            Assert.That(LabTeams.ElementOf(LabTeam.Blue), Is.EqualTo(LabOrbElement.Water));
            Assert.That(LabTeams.ElementOf(LabTeam.Red), Is.EqualTo(LabOrbElement.Fire));
        }

        [Test]
        public void TeamModeNeedsTheSettingAndFourPlayersOrSolo()
        {
            Assert.That(Team("four").TeamTerritory, Is.True);
            var solo = new LabModel();
            solo.BeginSolo("solo", 10, 0d, config);
            Assert.That(solo.TeamTerritory, Is.True);
            var three = new LabModel();
            three.Begin("three", new ulong[] { 10, 20, 30 }, 0d, config);
            Assert.That(three.TeamTerritory, Is.False, "Other player counts keep the ordinary rules.");
            Assert.That(three.Snapshot(0d).teamTerritory, Is.False);
            Set(196, 0f);
            Assert.That(Team("off").TeamTerritory, Is.False);
        }

        [Test]
        public void EachTeamStartsWithOneDrumOnARandomMember()
        {
            foreach (string id in new[] { "d1", "d2", "d3", "d4", "d5" })
            {
                LabOrbState[] drums = Team(id).Snapshot(0d).drums;
                Assert.That(drums, Has.Length.EqualTo(2));
                Assert.That(drums.Select(drum => LabTeams.TeamOfSeat(SeatOf(drum.owner))).OrderBy(team => team),
                    Is.EqualTo(new[] { LabTeam.Blue, LabTeam.Red }));
            }
        }

        [Test]
        public void HandoffsGoOnlyToTheTeammateThroughTheInnerEdge()
        {
            LabModel model = Team("edges");
            foreach (LabTeam team in new[] { LabTeam.Blue, LabTeam.Red })
            {
                ulong holder = DrumHolder(model, team);
                LabOrbState orb = Generate(model, holder, 1d);
                ulong owner = holder;
                for (int pass = 0; pass < 3; pass++)
                {
                    int seat = SeatOf(owner);
                    bool inner = LabTeams.EdgeOpen(seat, true);
                    Assert.That(Transfer(model, owner, orb.id, !inner, 1d, out _, out string blocked), Is.False);
                    Assert.That(blocked, Is.EqualTo("TEAM_EDGE"), "The outer edge is closed.");
                    Assert.That(Transfer(model, owner, orb.id, !inner, 1d, out _, out blocked, LabEdgeCrossingKind.Pushed),
                        Is.False, "A blast cannot push an orb out through the closed edge either.");
                    Assert.That(Transfer(model, owner, orb.id, inner, 1d, out var moved, out string reason), Is.True, reason);
                    Assert.That(moved.owner, Is.EqualTo(Ids[LabTeams.TeammateSeat(seat)]));
                    Assert.That(LabTeams.TeamOfSeat(SeatOf(moved.owner)), Is.EqualTo(team));
                    owner = moved.owner;
                }
            }
        }

        [Test]
        public void CombiningAlwaysMakesTheOwnTeamColor()
        {
            LabModel model = Team("colors");
            for (int i = 0; i < 3; i++)
            {
                Assert.That(Combine(model, DrumHolder(model, LabTeam.Blue), 1d).element, Is.EqualTo(LabOrbElement.Water));
                Assert.That(Combine(model, DrumHolder(model, LabTeam.Red), 1d).element, Is.EqualTo(LabOrbElement.Fire));
            }
        }

        [Test]
        public void SoloChecksOverwritingWithAlternatingColorsAndHasNoNeighbor()
        {
            var solo = new LabModel();
            solo.BeginSolo("solo-colors", 10, 0d, config);
            Assert.That(solo.Snapshot(0d).drums, Has.Length.EqualTo(1));
            Assert.That(Combine(solo, 10, 1d).element, Is.EqualTo(LabOrbElement.Water));
            Assert.That(Combine(solo, 10, 1d).element, Is.EqualTo(LabOrbElement.Fire));
            Assert.That(Combine(solo, 10, 1d).element, Is.EqualTo(LabOrbElement.Water));
            var orb = Generate(solo, 10, 1d);
            Assert.That(Transfer(solo, 10, orb.id, true, 1d, out _, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("NO_NEIGHBOR"));
        }

        [Test]
        public void SurfaceOverwritesOnlyTheOtherColorInsideTheHitStain()
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings
            {
                Resolution = 0, InitialPercent = 5f, Pattern = LabPaintPattern.Splatter
            }, 7u);
            Color blue = LabPaintPalette.ColorFor(LabOrbElement.Water), red = LabPaintPalette.ColorFor(LabOrbElement.Fire);
            Color32 blueCell = LabPaintSurface.Opaque(blue), redCell = LabPaintSurface.Opaque(red);
            Assert.That(surface.RegisterTerritoryHit(Vector3.forward, blue, 10, 1, 1f, out int bluePainted, out int blueStolen), Is.True);
            Assert.That(blueStolen, Is.Zero);
            Assert.That(bluePainted, Is.GreaterThan(0));
            Assert.That(surface.CountColor(blueCell), Is.EqualTo(surface.CoveredCount));
            int blueBefore = surface.CountColor(blueCell);

            Assert.That(surface.RegisterTerritoryHit(Vector3.forward, red, 30, 3, 1f, out int redPainted, out int redStolen), Is.True);
            Assert.That(redStolen, Is.GreaterThan(0), "A hit on the other color takes the cells under its stain.");
            Assert.That(surface.CountColor(blueCell), Is.EqualTo(blueBefore - redStolen));
            Assert.That(surface.CountColor(redCell), Is.EqualTo(redPainted + redStolen));
            Assert.That(surface.CoveredCount, Is.EqualTo(blueBefore + redPainted), "Overwriting never uncovers or double counts a cell.");
            int[] painters = surface.CountPainterSlots();
            Assert.That(painters[3], Is.EqualTo(redPainted + redStolen), "The P number moves with the overwritten color.");
            Assert.That(painters[1], Is.EqualTo(blueBefore - redStolen));

            // 반대편을 맞히면 빼앗을 것이 없다.
            int redBefore = surface.CountColor(redCell);
            Assert.That(surface.RegisterTerritoryHit(Vector3.back, blue, 10, 1, 1f, out _, out int farStolen), Is.True);
            Assert.That(farStolen, Is.Zero);
            Assert.That(surface.CountColor(redCell), Is.EqualTo(redBefore));
        }

        [Test]
        public void AFullButMixedGourdKeepsPlayingAndTheDeadlineComparesTeamCells()
        {
            LabModel model = Team("deadline");
            Assert.That(Hit(model, DrumHolder(model, LabTeam.Blue), 1d, Vector3.forward), Is.EqualTo(LabOrbElement.Water));
            Assert.That(Hit(model, DrumHolder(model, LabTeam.Red), 2d, Vector3.forward), Is.EqualTo(LabOrbElement.Fire));
            Assert.That(Hit(model, DrumHolder(model, LabTeam.Red), 3d, Vector3.back), Is.EqualTo(LabOrbElement.Fire));
            LabSnapshot playing = model.Snapshot(3d);
            Assert.That(playing.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(playing.teamTerritory, Is.True);
            Assert.That(playing.blueCells, Is.GreaterThan(0));
            Assert.That(playing.redCells, Is.GreaterThan(playing.blueCells));
            Assert.That(playing.blueCells + playing.redCells, Is.EqualTo(playing.paintCoveredCount));
            Assert.That(playing.teamWinner, Is.EqualTo(LabTeam.None));
            LabPlayerState redHolder = playing.players.Single(player => player.id == DrumHolder(model, LabTeam.Red));
            Assert.That(redHolder.stolenCells, Is.GreaterThan(0));
            Assert.That(playing.players.Sum(player => player.paintedCells), Is.EqualTo(playing.paintCoveredCount));

            LabSnapshot ended = model.Snapshot(31d);
            Assert.That(ended.phase, Is.EqualTo(LabPhase.Victory), "Time running out ends a team battle, never in a defeat.");
            Assert.That(ended.remaining, Is.Zero);
            Assert.That(ended.teamWinner, Is.EqualTo(ended.blueCells > ended.redCells ? LabTeam.Blue
                : ended.redCells > ended.blueCells ? LabTeam.Red : LabTeam.None));
            Assert.That(ended.teamWinner, Is.EqualTo(LabTeam.Red));
        }

        [Test]
        public void EqualCellsAtTheDeadlineIsADraw()
        {
            LabModel model = Team("draw");
            LabSnapshot ended = model.Snapshot(31d);
            Assert.That(ended.phase, Is.EqualTo(LabPhase.Victory));
            Assert.That(ended.blueCells, Is.EqualTo(ended.redCells));
            Assert.That(ended.teamWinner, Is.EqualTo(LabTeam.None), "Victory with no winning team is a draw.");
        }

        [Test]
        public void OneTeamHoldingEveryCellWinsAtOnce()
        {
            Set(105, 100f);
            LabModel model = Team("sweep");
            Hit(model, DrumHolder(model, LabTeam.Red), 1d, Vector3.forward);
            LabSnapshot swept = model.Snapshot(1d);
            Assert.That(swept.redCells, Is.EqualTo(swept.paintCellCount));
            Assert.That(swept.phase, Is.EqualTo(LabPhase.Victory));
            Assert.That(swept.teamWinner, Is.EqualTo(LabTeam.Red));
            Assert.That(swept.remaining, Is.GreaterThan(0d));
        }

        [Test]
        public void FeverTurnsTheTeamBoardsIntoTeamOrbsAndGeneratesTeamOrbsUntilItEnds()
        {
            Set(198, 1f);
            LabModel model = Team("fever");
            ulong blue = DrumHolder(model, LabTeam.Blue), red = DrumHolder(model, LabTeam.Red);
            LabOrbState waiting = Generate(model, blue, 1d);
            LabOrbState waitingCombined = Combine(model, blue, 1d);
            ulong combinedRevision = waitingCombined.revision;
            LabOrbState redCombined = Combine(model, red, 1d);
            LabOrbState redRaw = Generate(model, red, 1d);
            Assert.That(waiting.kind.IsRaw(), Is.True);
            Hit(model, blue, 2d, Vector3.forward);

            LabSnapshot fever = model.Snapshot(2d);
            Assert.That(fever.FeverOn(LabTeam.Blue), Is.True);
            Assert.That(fever.FeverOn(LabTeam.Red), Is.False);
            Assert.That(fever.blueFeverStartedAt, Is.EqualTo(2d));
            Assert.That(fever.blueFeverUntil, Is.EqualTo(10d));
            Assert.That(fever.blueFeverHits, Is.Zero, "The gauge empties when the fever starts.");
            LabOrbState converted = fever.orbs.Single(orb => orb.id == waiting.id);
            Assert.That(converted.kind, Is.EqualTo(LabOrbKind.Combined));
            Assert.That(converted.element, Is.EqualTo(LabOrbElement.Water));
            Assert.That(converted.fever, Is.True, "Every board rings a turned orb with a rainbow.");
            LabOrbState keptCombined = fever.orbs.Single(orb => orb.id == waitingCombined.id);
            Assert.That(keptCombined.kind, Is.EqualTo(LabOrbKind.Combined));
            Assert.That(keptCombined.element, Is.EqualTo(LabOrbElement.Water));
            Assert.That(keptCombined.fever, Is.True, "An already combined team orb also receives the fever ring.");
            Assert.That(keptCombined.revision, Is.GreaterThan(combinedRevision), "Peers must receive the metadata change.");
            Assert.That(fever.orbs.Single(orb => orb.id == redCombined.id).fever, Is.False,
                "The opposing team's Combined is not changed.");
            Assert.That(fever.orbs.Single(orb => orb.id == redRaw.id).fever, Is.False);
            Assert.That(fever.orbs.Single(orb => orb.id == redRaw.id).kind.IsRaw(), Is.True, "The other team is untouched.");

            for (int i = 0; i < 4; i++)
            {
                Assert.That(model.TryGenerate(blue, "fever-" + i, 3d, out var made, out _), Is.True);
                Assert.That(made.kind, Is.EqualTo(LabOrbKind.Combined));
                Assert.That(made.element, Is.EqualTo(LabOrbElement.Water));
                Assert.That(made.fever, Is.True);
            }
            Assert.That(model.TryLaunch(blue, waiting.id, "fever-throw", 3d, out _, out string reason), Is.True, reason);

            Assert.That(model.Snapshot(10d).FeverOn(LabTeam.Blue), Is.False);
            LabOrbState after = Generate(model, blue, 10.5d);
            Assert.That(after.kind.IsRaw(), Is.True, "Generation is ordinary after the fever.");
            Assert.That(after.fever, Is.False);
        }

        [Test]
        public void TheGaugeCountsTeamHitsAndZeroHitsMeansNoFever()
        {
            Set(198, 3f);
            LabModel model = Team("gauge");
            ulong blue = DrumHolder(model, LabTeam.Blue);
            Hit(model, blue, 1d, Vector3.forward);
            Hit(model, blue, 1d, Vector3.up);
            LabSnapshot two = model.Snapshot(1d);
            Assert.That(two.blueFeverHits, Is.EqualTo(2));
            Assert.That(two.redFeverHits, Is.Zero);
            Assert.That(two.FeverOn(LabTeam.Blue), Is.False);

            Set(198, 0f);
            LabModel none = Team("no-fever");
            for (int i = 0; i < 4; i++) Hit(none, DrumHolder(none, LabTeam.Blue), 1d, Vector3.forward);
            Assert.That(none.Snapshot(1d).blueFeverHits, Is.Zero);
            Assert.That(none.Snapshot(1d).FeverOn(LabTeam.Blue), Is.False);
        }

        [Test]
        public void YokaiSabotagesComboFlashAndDryingAreOff()
        {
            Set(148, 1f);
            LabModel model = Team("off");
            LabSnapshot later = model.Snapshot(25d);
            Assert.That(model.StageYokai, Is.EqualTo(LabStageYokai.None));
            Assert.That(model.DrainGourdBombs(), Is.Empty);
            Assert.That(model.DrainSabotages(), Is.Empty);
            Assert.That(later.swipeBlockedUntil, Is.EqualTo(-1d));
            Assert.That(later.comboCount, Is.Zero);
            Assert.That(later.comboNextAt, Is.EqualTo(-1d));
            Assert.That(later.flashAt, Is.EqualTo(-1d));
            Assert.That(later.flashCount, Is.Zero);
            Assert.That(later.remaining, Is.EqualTo(5d), "The 2:2 time limit replaces the battle duration.");

            Hit(model, DrumHolder(model, LabTeam.Blue), 25d, Vector3.forward);
            int painted = model.Snapshot(25d).paintCoveredCount;
            Assert.That(model.Snapshot(29.9d).paintCoveredCount, Is.GreaterThanOrEqualTo(painted), "Paint never dries in a team battle.");

            LabModel yokai = new LabModel();
            yokai.Begin("yokai", Ids, 0d, config, LabStageYokai.Gumiho);
            Assert.That(yokai.StageYokai, Is.EqualTo(LabStageYokai.None), "A team battle has no yokai stage.");
        }

        [Test]
        public void TeamStateSurvivesTheSnapshotCopyAndBoardHeader()
        {
            Set(198, 1f);
            LabModel model = Team("copy");
            Hit(model, DrumHolder(model, LabTeam.Red), 1d, Vector3.forward);
            LabSnapshot source = model.Snapshot(1d);
            LabSnapshot copy = LabBoardReplica.CopySnapshot(source);
            Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(JsonUtility.ToJson(source)));

            var publisher = new LabBoardPublisher();
            LabBoardFrame frame = publisher.Build(source, Ids[0], 1, 1, true);
            Assert.That(frame.redFeverHits, Is.EqualTo(source.redFeverHits));
            Assert.That(frame.redFeverStartedAt, Is.EqualTo(1d));
            Assert.That(frame.redFeverUntil, Is.EqualTo(9d));
            Assert.That(frame.blueFeverUntil, Is.EqualTo(-1d));

            // A fever turning an orb changes only its metadata; the owner's next delta must still carry it.
            LabOrbState kept = Generate(model, DrumHolder(model, LabTeam.Blue), 2d);
            ulong owner = kept.owner;
            LabSnapshot before = model.Snapshot(2d);
            publisher.Build(before, owner, 2, 2, true);
            LabSnapshot turned = model.Snapshot(2d);
            turned.orbs = turned.orbs.Select(orb =>
            {
                LabOrbState copy = orb.Copy();
                if (copy.id == kept.id) copy.fever = true;
                return copy;
            }).ToArray();
            LabBoardFrame delta = publisher.Build(turned, owner, 3, 3);
            Assert.That(delta.checkpoint, Is.False);
            Assert.That(delta.states.Any(state => state.id == kept.id && state.fever), Is.True);
        }
    }
}
