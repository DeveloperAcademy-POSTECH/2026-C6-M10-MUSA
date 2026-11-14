using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// VFX 녹화용 실험실 (#610): 에디터 창이 부르는 장면 띄우기가 실제 게임과 같은 상태를 만들고,
    /// 부르지 않으면 야광귀 시간표/피버가 전과 같은지 본다.
    /// </summary>
    public sealed class LabVfxLabTests
    {
        private static readonly ulong[] Players = { 0ul, 20ul };
        private LabConfig config;

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[155] = 1f; // 박 색상 콤보 켜기 (게임의 야광귀처럼)
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.GourdColorComboEnabled, Is.True);
        }
        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private LabModel Room(LabStageYokai stage = LabStageYokai.Yaguang)
        {
            var room = new LabModel();
            room.Begin("vfx-lab", Players, 0d, config, stage);
            return room;
        }

        // ───────── #673: 1~3라운드 모든 기믹 ─────────

        [Test]
        public void TheLabCastsEveryCommonAndFoxGimmickAtOnce()
        {
            LabModel room = Room(LabStageYokai.Gumiho);
            Assert.That(room.VfxLabCastGimmick(LabSabotageKind.GourdBomb, 0ul, 1d), Is.True, "Gourd bomb.");
            Assert.That(room.VfxLabCastGimmick(LabSabotageKind.SwipeBlock, 0ul, 1.1d), Is.True, "Swipe block.");
            Assert.That(room.VfxLabCastGimmick(LabSabotageKind.SwipeBlock, 0ul, 1.2d), Is.False, "Only one swipe block at a time.");
            Assert.That(room.VfxLabCastGimmick(LabSabotageKind.FoxFire, 0ul, 1.3d), Is.True, "Fox fire on every board.");
            Assert.That(room.VfxLabCastGimmick(LabSabotageKind.Charm, 0ul, 1.4d), Is.True, "Charm.");
            Assert.That(room.VfxLabCastGimmick(LabSabotageKind.Charm, 0ul, 1.5d), Is.False, "Only one charm at a time.");
            Assert.That(room.Snapshot(1.6d).orbs.Any(orb => orb.kind == LabOrbKind.Bomb), Is.True, "The gourd bomb is on a board.");
        }

        [Test]
        public void TheLabDetonatesMyLockedBombNow()
        {
            // #741: the bomb on my board blows up right away instead of after its whole fuse.
            LabModel room = Room(LabStageYokai.Gumiho);
            Assert.That(room.VfxLabDetonateBomb(0ul, 1d), Is.False, "No bomb yet.");
            Assert.That(room.VfxLabCastGimmick(LabSabotageKind.GourdBomb, 0ul, 1d), Is.True);
            double landed = 1d; // after the warning, the throw and the fall
            while (room.VfxLabLockedBombId(0ul) == null && landed < 12d) { landed += .25d; room.Tick(landed); }
            Assert.That(room.VfxLabLockedBombId(0ul), Is.Not.Null, "The bomb is on my board, locked.");
            Assert.That(room.VfxLabDetonateBomb(0ul, landed), Is.True);
            room.Tick(landed + 1d);
            Assert.That(room.Snapshot(landed + 1d).orbs.Any(orb => orb.kind == LabOrbKind.Bomb && orb.owner == 0ul), Is.False,
                "It has gone off.");
        }

        [Test]
        public void TheLabStartsTheFoxFeverNow()
        {
            LabModel room = Room(LabStageYokai.Gumiho);
            Assert.That(room.StageFeverOn(5d), Is.False);
            Assert.That(room.VfxLabStartGumihoFever(5d), Is.True);
            Assert.That(room.StageFeverOn(5d), Is.True, "The fever starts at once (100 s moved to now).");
            Assert.That(room.VfxLabStartGumihoFever(6d), Is.False, "Once per battle.");
            Assert.That(Room(LabStageYokai.Yaguang).VfxLabStartGumihoFever(5d), Is.False, "Only the fox has this fever.");
        }

        [Test]
        public void TheLabFailsTheComboIntoTheSmallPenalty()
        {
            LabModel room = Room();
            Assert.That(room.VfxLabStartCombo(1d), Is.True);
            double active = 1d + config.ComboWarningSeconds + .1d;
            Assert.That(room.Snapshot(active).comboActive, Is.True);
            Assert.That(room.VfxLabFailCombo(0ul, active), Is.True);
            LabSnapshot snapshot = room.Snapshot(active + .1d);
            Assert.That(snapshot.comboResult, Is.EqualTo(LabComboResult.WrongColor));
            Assert.That(snapshot.players.Any(player => player.comboPenaltyUntil > active), Is.True, "The SMALL penalty is on.");
        }

        [Test]
        public void TheLabEndsTheSmallPenaltyAtOnce()
        {
            // #741: the SMALL penalty (15 s) ends now, as if its time had run out.
            LabModel room = Room();
            Assert.That(room.VfxLabEndComboEffect(1d), Is.False, "Nothing to end yet.");
            Assert.That(room.VfxLabStartCombo(1d), Is.True);
            double active = 1d + config.ComboWarningSeconds + .1d;
            Assert.That(room.VfxLabFailCombo(0ul, active), Is.True);
            double now = active + 1d;
            Assert.That(room.VfxLabComboEffectOn(now), Is.True);
            Assert.That(room.VfxLabEndComboEffect(now), Is.True);
            Assert.That(room.VfxLabComboEffectOn(now + .01d), Is.False);
            Assert.That(room.Snapshot(now + .01d).players.All(player => player.comboPenaltyUntil <= now), Is.True, "SMALL ended at the press.");
        }

        [Test]
        public void TheLabStartsAFlashWhenTheYaguangFlashIsOn()
        {
            float[] values = config.CaptureDeveloperValues();
            values[184] = 1f; // 섬광 켜기
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            LabModel room = Room();
            Assert.That(room.VfxLabStartFlash(1d), Is.True);
            Assert.That(room.VfxLabStartFlash(1.1d), Is.False, "One flash at a time.");
        }

        [Test]
        public void TheLabPicksTheSmokeOrTheTalismanAndCanSkipTheWarning()
        {
            // #741: 연기 / 부적을 골라 부른다. 다음 차례가 다른 종류면 하나 건너뛰고, skipWarning이면 예고 없이 본체부터.
            LabModel room = Room(LabStageYokai.Jigwi);
            Assert.That(room.VfxLabStartJigwiMission(LabJigwiABKind.GroupFire, true, 1d), Is.True);
            LabJigwiABRound smoke = room.Snapshot(1.1d).jigwiAB.Active(1.1d);
            Assert.That(smoke, Is.Not.Null); Assert.That(smoke.kind, Is.EqualTo(LabJigwiABKind.GroupFire));
            Assert.That(smoke.activeAt, Is.EqualTo(1d).Within(1e-9), "No warning.");
            Assert.That(room.VfxLabStartJigwiMission(LabJigwiABKind.PersonalBurn, false, 1.2d), Is.False, "One mission at a time.");
            Assert.That(room.VfxLabFinishJigwiMission(false, 2d), Is.True);
            Assert.That(room.VfxLabStartJigwiMission(LabJigwiABKind.PersonalBurn, false, 3d), Is.True);
            LabJigwiABRound talisman = room.Snapshot(5.1d).jigwiAB.Active(5.1d);
            Assert.That(talisman, Is.Not.Null); Assert.That(talisman.kind, Is.EqualTo(LabJigwiABKind.PersonalBurn));
            Assert.That(talisman.activeAt, Is.EqualTo(3d + LabJigwiABTimeline.WarningSeconds).Within(1e-9), "With its warning.");
        }

        [Test]
        public void TheLabCanStartTheComboWithoutItsWarning()
        {
            // #741: 콤보 4칸을 예고 없이 바로.
            LabModel room = Room();
            Assert.That(room.VfxLabStartCombo(1d, true), Is.True);
            Assert.That(room.Snapshot(1.05d).comboActive, Is.True, "The four slots are on at once.");
            Assert.That(room.VfxLabWrongCombo(0ul, 1.1d), Is.True, "A wrong colour (forgiven, a heart, or the end).");
        }

        [Test]
        public void TheLabRunsJigwiMissionsNowAndItsFever()
        {
            LabModel room = Room(LabStageYokai.Jigwi);
            Assert.That(room.VfxLabJigwiOn, Is.True);
            Assert.That(room.VfxLabStartNextJigwiMission(1d), Is.True, "The next mission starts with its warning now.");
            Assert.That(room.VfxLabStartNextJigwiMission(1.5d), Is.False, "One mission at a time.");
            LabSnapshot snapshot = room.Snapshot(3.1d);
            LabJigwiABRound round = snapshot.jigwiAB.Active(3.1d);
            Assert.That(round, Is.Not.Null);
            Assert.That(round.activeAt, Is.EqualTo(1d + LabJigwiABTimeline.WarningSeconds).Within(1e-9));
            // The lab runs solo as Host, so no other screen checks this early mission; the game's own checks stay as they are.
            Assert.That(room.VfxLabFinishJigwiMission(false, 4d), Is.True, "Ended as if time ran out.");
            Assert.That(room.Snapshot(4.1d).jigwiAB.Active(4.1d), Is.Null);
            Assert.That(room.VfxLabStartNextJigwiMission(5d), Is.True, "And the one after it.");
            Assert.That(room.VfxLabStartJigwiFever(6d), Is.True);
            snapshot = room.Snapshot(6.1d);
            Assert.That(room.StageFeverOn(6.1d), Is.True, "The fever starts now (100 s moved to now).");
            Assert.That(room.VfxLabJigwiOn, Is.False);
        }

        [Test]
        public void WithoutTheLabTheYaguangFeverStillStartsAtTheEndOfTheSabotage()
        {
            LabModel room = Room();
            Assert.That(room.YaguangFeverStartsAt, Is.EqualTo(config.YaguangTimelineSabotageEndSeconds));
            Assert.That(room.VfxLabComboRunning, Is.False);
        }

        [Test]
        public void TheLabStartsACombo_GivesItsNextColor_AndCompletesItWithTheBigReward()
        {
            LabModel room = Room();
            Assert.That(room.VfxLabStartCombo(1d), Is.True);
            Assert.That(room.VfxLabComboRunning, Is.True, "The combo warning (COMBO 3/2/1) starts at once.");
            Assert.That(room.VfxLabStartCombo(1.5d), Is.False, "Only one combo at a time.");
            double active = 1d + config.ComboWarningSeconds + .1d;
            LabSnapshot snapshot = room.Snapshot(active);
            Assert.That(snapshot.comboActive, Is.True, "The four slots follow the warning as in the game.");

            Assert.That(room.VfxLabGiveComboOrb(0ul, active, out LabOrbElement color), Is.True);
            Assert.That(color, Is.EqualTo(snapshot.comboColors[snapshot.comboProgress]), "The orb is the colour the combo wants next.");
            Assert.That(room.Snapshot(active).orbs.Any(orb => orb.owner == 0ul && orb.element == color && !orb.fiveColor), Is.True);

            Assert.That(room.VfxLabCompleteCombo(0ul, active), Is.True);
            snapshot = room.Snapshot(active + .1d);
            Assert.That(snapshot.comboActive, Is.False);
            Assert.That(snapshot.comboResult, Is.EqualTo(LabComboResult.Success));
            Assert.That(room.ThrowScaleFor(0ul, active + .1d), Is.EqualTo(config.ComboRewardSizeScale), "Success gives everyone the BIG reward.");
            Assert.That(room.ThrowScaleFor(20ul, active + .1d), Is.EqualTo(config.ComboRewardSizeScale));
        }

        [Test]
        public void TheLabGivesFiveColorOrbsOnlyIntoFreeSlots()
        {
            LabModel room = Room();
            int dropped = room.VfxLabGiveFiveColorOrbs(0ul, 2, 1d);
            Assert.That(dropped, Is.EqualTo(2));
            Assert.That(room.Snapshot(1d).orbs.Count(orb => orb.owner == 0ul && orb.fiveColor), Is.EqualTo(2));
            int more = room.VfxLabGiveFiveColorOrbs(0ul, 99, 1.1d);
            Assert.That(room.Snapshot(1.1d).orbs.Count(orb => orb.owner == 0ul), Is.LessThanOrEqualTo(config.OrbStorageLimit),
                "Never more orbs than the board holds.");
            Assert.That(more, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void TheLabPullsTheYaguangFeverToNow_AndNoComboStartsAfterIt()
        {
            LabModel room = Room();
            Assert.That(room.VfxLabStartYaguangFever(5d), Is.True);
            Assert.That(room.YaguangFeverStartsAt, Is.EqualTo(5d));
            Assert.That(room.YaguangFeverRecoveryMultiplier(5d), Is.EqualTo(config.YaguangFeverStaminaMultiplier));
            Assert.That(room.VfxLabStartYaguangFever(6d), Is.False, "The fever starts once.");
            Assert.That(room.VfxLabStartCombo(6d), Is.False, "The timetable is over in the fever.");
        }
    }
}
