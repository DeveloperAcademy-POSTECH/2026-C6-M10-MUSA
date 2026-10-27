using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// #311's single gimmick dispatcher runs #314's Yaguang schedule. Assertions observe actual
    /// casts, snapshots and Host clock boundaries, rather than reproducing scheduler internals.
    /// </summary>
    public sealed class LabGimmickTimelineIntegrationTests
    {
        private static readonly ulong[] Players = { 0, 20, 30 };
        private LabConfig config;

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[155] = values[184] = 1f;
            values[158] = 13f; values[159] = values[162] = 8f; values[164] = 4f;
            values[188] = 3f; values[189] = 11f; values[190] = 1f;
            values[153] = 2f;
            values[2] = 120f; // The authored two-minute battle every stage now shares (#328).
            Apply(values);
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private void Apply(float[] values) =>
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);

        private LabModel Room(string id = "integrated-yaguang", LabStageYokai stage = LabStageYokai.Yaguang)
        {
            var model = new LabModel();
            model.Begin(id, Players, 0d, config, stage);
            return model;
        }

        private static bool Common(LabSabotage cast) =>
            cast.Kind == LabSabotageKind.GourdBomb || cast.Kind == LabSabotageKind.SwipeBlock;

        private static LabSnapshot At(LabModel model, double now) => model.Snapshot(now);
        private static int GourdBombs(LabSnapshot snapshot) =>
            snapshot.orbs.Count(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd);

        [TestCase(LabStageYokai.None)]
        [TestCase(LabStageYokai.Gumiho)]
        [TestCase(LabStageYokai.Yaguang)]
        [TestCase(LabStageYokai.Jigwi)]
        public void EveryStageUsesTheCommonBattleDuration(LabStageYokai stage)
        {
            Assert.That(Room(stage: stage).Snapshot(0d).remaining, Is.EqualTo(120d));
            float[] values = config.CaptureDeveloperValues();
            values[2] = 150f;
            values[211] = 90f; // The Yaguang's own battle time is retired (#328); nothing reads it.
            Apply(values);
            Assert.That(Room(stage: stage).Snapshot(0d).remaining, Is.EqualTo(150d));
        }

        [Test]
        public void UniqueWarningsAndEffectsFollowTheFourApprovedSlots()
        {
            LabModel room = Room();
            Assert.That(At(room, 9.99d).FlashWarningOn(9.99d), Is.False);
            LabSnapshot flash = At(room, 10d);
            Assert.That(flash.flashWarningAt, Is.EqualTo(10d));
            Assert.That(flash.flashAt, Is.EqualTo(13d));
            Assert.That(room.FlashGlared(0, 12.99d), Is.False);
            At(room, 13d);
            Assert.That(Players.All(player => room.FlashGlared(player, 13d)), Is.True);
            At(room, 25d);
            Assert.That(Players.Any(player => room.FlashGlared(player, 25d)), Is.False);

            LabSnapshot warning = At(room, 35d);
            Assert.That(warning.comboWarningAt, Is.EqualTo(35d));
            Assert.That(warning.comboNextAt, Is.EqualTo(39d));
            Assert.That(At(room, 38.99d).comboActive, Is.False);
            LabSnapshot combo = At(room, 39d);
            Assert.That(combo.comboActive, Is.True);
            Assert.That(combo.comboEndsAt, Is.EqualTo(52d));
            LabSnapshot timeout = At(room, 52d);
            Assert.That(timeout.comboActive, Is.False);
            Assert.That(timeout.players.All(player => player.comboPenaltyUntil == 60d), Is.True);

            Assert.That(At(room, 60d).flashWarningAt, Is.EqualTo(60d));
            At(room, 63d);
            Assert.That(Players.All(player => room.FlashGlared(player, 63d)), Is.True);
            Assert.That(At(room, 75d).comboWarningAt, Is.EqualTo(75d));
            Assert.That(At(room, 79d).comboActive, Is.True);
            LabSnapshot last = At(room, 92d);
            Assert.That(last.comboCount, Is.EqualTo(2u));
            Assert.That(last.flashCount, Is.EqualTo(2u));
            Assert.That(last.players.All(player => player.comboPenaltyUntil == 100d), Is.True);
        }

        [Test]
        public void CommonSlotsAlternateAndTheSixtySecondCastCoexistsWithTheFlashWarning()
        {
            LabModel room = Room();
            Assert.That(room.TimelineCommonIsBomb(0), Is.Not.EqualTo(room.TimelineCommonIsBomb(1)));
            Assert.That(room.TimelineCommonIsBomb(1), Is.Not.EqualTo(room.TimelineCommonIsBomb(2)));
            Assert.That(room.TimelineCommonIsBomb(2), Is.Not.EqualTo(room.TimelineCommonIsBomb(3)));
            At(room, 24.99d);
            Assert.That(room.DrainSabotages().Where(Common), Is.Empty);
            double[] slots = { 25d, 40d, 60d, 80d };
            for (int i = 0; i < slots.Length; i++)
            {
                LabSnapshot snapshot = At(room, slots[i]);
                LabSabotage cast = room.DrainSabotages().Where(Common).Single();
                Assert.That(cast.At, Is.EqualTo(slots[i]));
                Assert.That(cast.Kind, Is.EqualTo(room.TimelineCommonIsBomb(i)
                    ? LabSabotageKind.GourdBomb : LabSabotageKind.SwipeBlock));
                if (slots[i] == 60d) Assert.That(snapshot.flashWarningAt, Is.EqualTo(60d),
                    "The common cast and unique warning both occur, through the same scheduler.");
            }
        }

        [TestCase(true, false, LabSabotageKind.GourdBomb)]
        [TestCase(false, true, LabSabotageKind.SwipeBlock)]
        public void ACommonSwitchFiltersTheScheduleWithoutFallingBackToAnInterval(
            bool bomb, bool block, LabSabotageKind expected)
        {
            float[] values = config.CaptureDeveloperValues();
            values[207] = bomb ? 1f : 0f; values[208] = block ? 1f : 0f;
            values[205] = 1f; // Deliberately different: ignored by the Yaguang schedule.
            Apply(values);
            LabModel room = Room();
            At(room, 99d);
            LabSabotage[] casts = room.DrainSabotages().Where(Common).ToArray();
            Assert.That(casts.Select(cast => cast.At), Is.EqualTo(new[] { 25d, 40d, 60d, 80d }));
            Assert.That(casts.Select(cast => cast.Kind), Is.All.EqualTo(expected));
        }

        [Test]
        public void TheIntegratedStatisticsCountEachWarningOrCommonCastOnce()
        {
            LabModel room = Room();
            At(room, 99d);
            LabGimmickStats stats = room.GimmickStats();
            Assert.That((stats.total, stats.gourdBomb + stats.swipeBlock, stats.flash, stats.combo, stats.simultaneous),
                Is.EqualTo((8, 4, 2, 2, 1)),
                "Four common casts and four real unique warnings; only the 60 s common/unique pair coincides.");
            Assert.That(stats.total, Is.EqualTo(stats.gourdBomb + stats.swipeBlock + stats.foxFire
                + stats.charm + stats.flash + stats.combo), "The six named kinds explain the complete total.");
            stats.total = 99;
            Assert.That(room.GimmickStats().total, Is.EqualTo(8), "Records remain independent copies.");
            At(room, 119d);
            Assert.That(room.GimmickStats().total, Is.EqualTo(8), "Cutoff cannot add a second record or a late event.");
        }

        [Test]
        public void AllCommonSwitchesOffLeaveOnlyTheScheduledUniqueEffects()
        {
            float[] values = config.CaptureDeveloperValues();
            values[207] = values[208] = 0f;
            Apply(values);
            LabModel room = Room();
            LabSnapshot snapshot = At(room, 99d);
            Assert.That(room.DrainSabotages().Where(Common), Is.Empty);
            Assert.That(snapshot.comboCount, Is.EqualTo(2u));
            Assert.That(snapshot.flashCount, Is.EqualTo(2u));
        }

        [TestCase(false, true, 0u, 2u)]
        [TestCase(true, false, 2u, 0u)]
        [TestCase(false, false, 0u, 0u)]
        public void UniqueSwitchesRemoveTheirOwnSlotsWithoutReplacingTheirKind(
            bool combo, bool flash, uint combos, uint flashes)
        {
            float[] values = config.CaptureDeveloperValues();
            values[155] = combo ? 1f : 0f; values[184] = flash ? 1f : 0f;
            Apply(values);
            LabSnapshot snapshot = At(Room(), 99d);
            Assert.That(snapshot.comboCount, Is.EqualTo(combos));
            Assert.That(snapshot.flashCount, Is.EqualTo(flashes));
        }

        [Test]
        public void AnImpossibleCommonCastIsSkippedRatherThanReplayedAfterTheBlockEnds()
        {
            float[] values = config.CaptureDeveloperValues();
            values[207] = 0f; values[208] = 1f; values[153] = 60f;
            values[155] = values[184] = 0f;
            Apply(values);
            LabModel room = Room();
            At(room, 99d);
            LabSabotage[] casts = room.DrainSabotages().Where(Common).ToArray();
            Assert.That(casts.Select(cast => cast.At), Is.EqualTo(new[] { 25d }),
                "The global block occupies 25..85 s, so the slots at 40/60/80 are skipped and never queued.");
        }

        [Test]
        public void TheHundredSecondBoundaryCancelsAnActiveComboAndBoardBombWithoutANewPenaltyOrBlast()
        {
            float[] values = config.CaptureDeveloperValues();
            values[219] = 90f; values[224] = 95f;
            values[207] = 1f; values[208] = 0f;
            Apply(values);
            LabModel room = Room();
            LabSnapshot before = At(room, 99.9d);
            Assert.That(before.comboActive, Is.True);
            Assert.That(GourdBombs(before), Is.GreaterThan(0));
            room.DrainBombExplosions();
            room.DrainSabotages();
            LabSnapshot after = At(room, 100d);
            Assert.That(after.comboActive, Is.False);
            Assert.That(after.comboResultCount, Is.EqualTo(before.comboResultCount),
                "Stopping the unfinished combo does not create a failure or success result.");
            Assert.That(after.players.All(player => player.comboPenaltyUntil <= 100d
                && player.comboRewardUntil <= 100d), Is.True);
            Assert.That(GourdBombs(after), Is.Zero);
            Assert.That(room.DrainBombExplosions(), Is.Empty, "Cutoff cleanup is silent.");
            Assert.That(after.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(after.remaining, Is.EqualTo(20d));
            LabSnapshot quiet = At(room, 119.99d);
            Assert.That(quiet.comboActive || quiet.comboNextAt >= 0d || quiet.flashAt >= 0d, Is.False);
            Assert.That(room.DrainSabotages().Where(Common), Is.Empty);
            Assert.That(At(room, 120d).phase, Is.EqualTo(LabPhase.Defeat));
        }

        [Test]
        public void CutoffReleasesEveryGlaredPlayerAndStopsASwipeBlock()
        {
            float[] values = config.CaptureDeveloperValues();
            values[217] = 95f; values[219] = 0f; values[224] = 95f;
            values[207] = 0f; values[208] = 1f; values[153] = 15f;
            Apply(values);
            LabModel room = Room();
            LabSnapshot before = At(room, 99d);
            Assert.That(Players.All(player => room.FlashGlared(player, 99d)), Is.True);
            Assert.That(before.SwipeBlockOn(before.swipeBlockedPlayer), Is.True);
            LabSnapshot after = At(room, 100d);
            Assert.That(Players.Any(player => room.FlashGlared(player, 100d)), Is.False);
            Assert.That(after.players.Select(player => player.flashReleasedAt), Is.All.EqualTo(100d));
            Assert.That(after.SwipeBlockOn(before.swipeBlockedPlayer), Is.False);
        }

        [Test]
        public void CloselyConfiguredUniqueSlotsWaitForThePreviousEffectWithoutDroppingTheNextOne()
        {
            float[] values = config.CaptureDeveloperValues();
            values[215] = 11f; // The first combo's reservation meets the first flash's 10..25 s presentation.
            Apply(values);
            LabModel room = Room();
            LabSnapshot held = At(room, 14d);
            Assert.That(held.comboActive || held.ComboWarningOn(14d), Is.False);
            Assert.That(room.FlashGlared(0, 14d), Is.True);
            LabSnapshot warning = At(room, 25d);
            Assert.That(warning.comboWarningAt, Is.EqualTo(25d));
            Assert.That(warning.comboNextAt, Is.EqualTo(29d),
                "A queued unique effect receives its full warning once the previous effect has finished.");
            Assert.That(At(room, 29d).comboCount, Is.EqualTo(1u));
        }

        [Test]
        public void TiedUniqueReservationsKeepTheExistingComboFirstPriority()
        {
            float[] values = config.CaptureDeveloperValues();
            values[215] = 10f; // First combo and first flash reserve the same warning time.
            Apply(values);
            LabModel room = Room();
            LabSnapshot warning = At(room, 10d);
            Assert.That(warning.ComboWarningOn(10d), Is.True);
            Assert.That(warning.FlashWarningOn(10d), Is.False);
            LabSnapshot combo = At(room, 14d);
            Assert.That(combo.comboCount, Is.EqualTo(1u));
            Assert.That(combo.flashCount, Is.Zero);
            Assert.That(combo.comboEndsAt, Is.EqualTo(27d));
            LabSnapshot next = At(room, 35d);
            Assert.That(next.flashWarningAt, Is.EqualTo(35d),
                "The tied flash follows the combo's full warning, active phase and 8 s penalty.");
            Assert.That(next.flashAt, Is.EqualTo(38d));
            Assert.That(At(room, 38d).flashCount, Is.EqualTo(1u));
        }

        [Test]
        public void ZeroCutoffAllowsTheExplicitLateSlotsAndZeroSlotsStayEmpty()
        {
            float[] values = config.CaptureDeveloperValues();
            values[212] = 0f;
            values[213] = 0f; // no first flash
            values[219] = 110f;
            values[221] = values[222] = values[223] = 0f;
            values[224] = 110f;
            Apply(values);
            LabModel room = Room();
            LabSnapshot late = At(room, 114d);
            Assert.That(late.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(late.comboActive, Is.True, "The configured 110 s warning runs when the cutoff is disabled.");
            Assert.That(late.flashCount, Is.EqualTo(1u), "The zero first-flash slot is absent, without affecting flash two.");
            Assert.That(room.DrainSabotages().Where(Common).Select(cast => cast.At), Is.EqualTo(new[] { 110d }));
        }

        [Test]
        public void LargeTicksProduceTheSameCastsEffectsAndBoardStateAsQuarterSecondTicks()
        {
            LabModel late = Room("same-clock");
            LabModel stepped = Room("same-clock");
            for (int i = 1; i <= 480; i++) stepped.Tick(i * .25d);
            late.Tick(120d);
            Assert.That(late.DrainSabotages().Select(cast => (cast.Kind, cast.Target, cast.At)),
                Is.EqualTo(stepped.DrainSabotages().Select(cast => (cast.Kind, cast.Target, cast.At))));
            Assert.That(SnapshotValues(late.Snapshot(120d)), Is.EqualTo(SnapshotValues(stepped.Snapshot(120d))));
        }

        [Test]
        public void PausedHostTimeCannotAdvanceTheScheduleAndRetryStartsItsSlotsAfresh()
        {
            var clock = new LabBattleClock();
            var room = new LabModel();
            room.Begin("paused-timeline", Players, clock.Now(100d), config, LabStageYokai.Yaguang);
            Assert.That(clock.Pause(109d), Is.True);
            LabSnapshot frozen = At(room, clock.Now(500d));
            Assert.That(frozen.playedSeconds, Is.EqualTo(9d));
            Assert.That(frozen.flashCount, Is.Zero);
            Assert.That(frozen.FlashWarningOn(clock.Now(500d)), Is.False);
            Assert.That(clock.Resume(500d), Is.True);
            Assert.That(At(room, clock.Now(501d)).flashWarningAt, Is.EqualTo(110d));
            At(room, clock.Now(612d));
            Assert.That(room.Phase, Is.EqualTo(LabPhase.Defeat));
            room.Begin("paused-timeline", Players, 230d, config, LabStageYokai.Yaguang);
            LabSnapshot fresh = At(room, 230d);
            Assert.That(fresh.round, Is.EqualTo(2u));
            Assert.That(fresh.remaining, Is.EqualTo(120d));
            Assert.That(fresh.comboCount, Is.Zero);
            Assert.That(fresh.flashCount, Is.Zero);
            Assert.That(room.DrainSabotages(), Is.Empty);
            Assert.That(At(room, 240d).flashWarningAt, Is.EqualTo(240d));
        }

        [Test]
        public void TerritoryKeepsItsOwnDurationAndNeverUsesYaguangSlots()
        {
            float[] values = config.CaptureDeveloperValues();
            values[196] = 1f; values[197] = 145f;
            Apply(values);
            var room = new LabModel();
            room.Begin("territory-not-timeline", new ulong[] { 0, 20, 30, 40 }, 0d, config, LabStageYokai.Yaguang);
            Assert.That(room.StageYokai, Is.EqualTo(LabStageYokai.None));
            Assert.That(room.Snapshot(0d).remaining, Is.EqualTo(config.TeamBattleSeconds));
            LabSnapshot snapshot = At(room, Math.Min(99d, config.TeamBattleSeconds));
            Assert.That(snapshot.comboCount, Is.Zero);
            Assert.That(snapshot.flashCount, Is.Zero);
            Assert.That(room.DrainSabotages(), Is.Empty);
        }

        private static string SnapshotValues(LabSnapshot snapshot)
        {
            LabOrbState[] Normalize(LabOrbState[] orbs) => orbs.Select(orb =>
            {
                LabOrbState copy = orb.Copy();
                copy.id = string.Empty;
                return copy;
            }).OrderBy(orb => JsonUtility.ToJson(orb), StringComparer.Ordinal).ToArray();
            snapshot.orbs = Normalize(snapshot.orbs);
            snapshot.drums = Normalize(snapshot.drums);
            return JsonUtility.ToJson(snapshot);
        }
    }
}
