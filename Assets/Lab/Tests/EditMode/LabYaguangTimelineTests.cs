using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>야광귀 시간표 (#314): 방해가 DEV에 적은 시각에만 나오고, 방해 끝 시각에 남은 효과가 끝난다.</summary>
    public sealed class LabYaguangTimelineTests
    {
        private static readonly ulong[] Players = { 0, 20, 30 };
        private LabConfig config;

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[155] = 1f; // 콤보
            values[184] = 1f; // 섬광
            values[158] = 13f; values[159] = 8f; values[162] = 8f; // 콤보 = 예고 4 + 제한 13 + SMALL/BIG 8
            values[189] = 11f; // 섬광 = 예고 3 + 눈부심 11 + 회복 1
            values[205] = 1f; // 시간표
            Apply(values);
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private void Apply(float[] values) =>
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);

        private LabModel Room(LabStageYokai stage = LabStageYokai.Yaguang)
        {
            var model = new LabModel();
            model.Begin("timeline", Players, 0d, config, stage);
            return model;
        }

        private static LabSnapshot At(LabModel model, double now)
        {
            model.Tick(now);
            return model.Snapshot(now);
        }

        private static int GourdBombs(LabSnapshot snapshot) =>
            snapshot.orbs.Count(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd);

        [Test]
        public void TimelineFieldsDefaultToTheAgreed120SecondPlanAndStayInsideTheBattle()
        {
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(220));
            var fresh = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(fresh.CaptureDeveloperValues().Skip(205).Take(15), Is.EqualTo(new[]
                    { 0f, 120f, 100f, 10f, 0f, 35f, 1f, 60f, 0f, 75f, 1f, 25f, 40f, 60f, 80f }),
                    "Off by default in Lab; flash 10 · combo 35 · flash 60 · combo 75, common 25 · 40 · 60 · 80, end 100 of 120.");
                Assert.That(LabConfig.DeveloperFields[205].IsToggle, Is.True);
                foreach (int kind in new[] { 209, 211, 213, 215 })
                    Assert.That(LabConfig.DeveloperFields[kind].Choices, Is.EqualTo(new[] { "섬광", "콤보" }));
                LabDeveloperField battle = LabConfig.DeveloperFields[206];
                Assert.That((battle.Min + battle.Max) * .5f, Is.EqualTo(120f), "The battle slider starts in the middle.");

                float[] values = fresh.CaptureDeveloperValues();
                values[207] = 121f;
                Assert.That(fresh.TryApplyDeveloperValues(values, out string error), Is.False);
                Assert.That(error, Does.Contain("방해 끝"));
                values[207] = 100f; values[219] = 130f;
                Assert.That(fresh.TryApplyDeveloperValues(values, out error), Is.False);
                Assert.That(error, Does.Contain("공용4"));
                values[219] = 0f; values[212] = 0f;
                Assert.That(fresh.TryApplyDeveloperValues(values, out error), Is.True, "0 leaves a slot empty: " + error);
                Assert.That(fresh.YaguangTimelineCommonSeconds(3), Is.Zero);
                Assert.That(fresh.YaguangTimelineUniqueSeconds(2), Is.Zero);
            }
            finally { Object.DestroyImmediate(fresh); }
        }

        [Test]
        public void TimelineShortensOnlyTheYaguangOrLabBattle()
        {
            Assert.That(Room().Snapshot(0d).remaining, Is.EqualTo(120d));
            Assert.That(Room(LabStageYokai.None).Snapshot(0d).remaining, Is.EqualTo(120d), "Lab DEV can try the timeline.");
            Assert.That(Room(LabStageYokai.Gumiho).Snapshot(0d).remaining, Is.EqualTo(180d), "Other yokai keep the common battle time.");
            float[] values = config.CaptureDeveloperValues();
            values[205] = 0f;
            Apply(values);
            Assert.That(Room().Snapshot(0d).remaining, Is.EqualTo(180d), "With the timeline off nothing changes.");
        }

        [Test]
        public void UniquePatternsWarnAtTheirSlotsAndNeverOverlap()
        {
            LabModel model = Room();
            LabSnapshot warning = At(model, 10d);
            Assert.That(warning.flashWarningAt, Is.EqualTo(10d), "Flash 1 warns at 10 s...");
            Assert.That(warning.flashAt, Is.EqualTo(13d), "...and flashes after the 3 s warning.");
            At(model, 12.9d);
            Assert.That(model.FlashGlared(0, 12.9d), Is.False);
            At(model, 13d);
            Assert.That(model.FlashGlared(0, 13d) && model.FlashGlared(20, 13d), Is.True, "The flash hits everyone.");
            At(model, 24d);
            Assert.That(model.FlashGlared(0, 24d), Is.False, "11 s glare + 1 s recovery: the flash is over by 25 s.");

            Assert.That(At(model, 38.9d).comboActive, Is.False);
            LabSnapshot combo = At(model, 39d);
            Assert.That(combo.comboActive, Is.True, "Combo 1 warns at 35 s and shows its four cells after 4 s.");
            Assert.That(combo.comboEndsAt, Is.EqualTo(52d), "13 s to clear.");
            LabSnapshot timeout = At(model, 52d);
            Assert.That(timeout.comboActive, Is.False);
            Assert.That(timeout.players.All(player => player.comboPenaltyUntil == 60d), Is.True, "SMALL lasts 8 s, to 60 s.");

            LabSnapshot second = At(model, 60d);
            Assert.That(second.flashWarningAt, Is.EqualTo(60d), "Flash 2 warns the moment combo 1 is over.");
            At(model, 63d);
            Assert.That(model.FlashGlared(30, 63d), Is.True);
            Assert.That(At(model, 79d).comboActive, Is.True, "Combo 2 warns at 75 s.");
            LabSnapshot last = At(model, 92d);
            Assert.That(last.players.All(player => player.comboPenaltyUntil == 100d), Is.True, "Combo 2 ends exactly at 100 s.");
            Assert.That(At(model, 101d).comboNextAt, Is.EqualTo(-1d), "No third combo.");
            Assert.That(model.Snapshot(101d).flashAt, Is.EqualTo(-1d), "No third flash.");
        }

        [Test]
        public void CommonPatternsComeOnlyAtTheirSlotsAndAlternate()
        {
            LabModel model = Room();
            Assert.That(model.TimelineCommonIsBomb(0), Is.Not.EqualTo(model.TimelineCommonIsBomb(1)),
                "The same common pattern never comes twice in a row.");
            Assert.That(model.TimelineCommonIsBomb(1), Is.Not.EqualTo(model.TimelineCommonIsBomb(2)));
            Assert.That(model.TimelineCommonIsBomb(2), Is.Not.EqualTo(model.TimelineCommonIsBomb(3)));

            LabSnapshot quiet = At(model, 24.9d);
            Assert.That(GourdBombs(quiet), Is.Zero, "Nothing common before 25 s.");
            Assert.That(quiet.swipeBlockStartedAt, Is.LessThan(0d));

            double[] slots = { 25d, 40d, 60d, 80d };
            for (int slot = 0; slot < slots.Length; slot++)
            {
                LabSnapshot snapshot = At(model, slots[slot]);
                if (model.TimelineCommonIsBomb(slot))
                    Assert.That(snapshot.orbs.Any(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd
                        && orb.createdAt == slots[slot]), Is.True, "Common " + (slot + 1) + " drops a gourd bomb.");
                else
                    Assert.That(snapshot.swipeBlockStartedAt, Is.EqualTo(slots[slot]), "Common " + (slot + 1) + " blocks a swipe.");
            }

            // 같은 방 이름이라도 판마다(seed) 첫 공용이 바뀔 수 있지만, 언제나 번갈아 나온다.
            var counted = new LabModel();
            counted.Begin("timeline-count", Players, 0d, config, LabStageYokai.Yaguang);
            int blocks = 0;
            double lastBlock = -1d;
            for (double t = 0d; t <= 120d; t += .5d)
            {
                LabSnapshot snapshot = At(counted, t);
                if (snapshot.swipeBlockStartedAt >= 0d && snapshot.swipeBlockStartedAt != lastBlock)
                {
                    lastBlock = snapshot.swipeBlockStartedAt;
                    blocks++;
                    Assert.That(new[] { 25d, 40d, 60d, 80d }, Does.Contain(lastBlock), "Swipe blocks start only at common slots.");
                }
            }
            Assert.That(blocks, Is.EqualTo(2), "Four common slots alternate, so two of them are swipe blocks.");
        }

        [Test]
        public void SabotageEndStopsEveryRemainingEffectAndNothingStartsAfterIt()
        {
            float[] values = config.CaptureDeveloperValues();
            values[214] = 90f; // 콤보2를 90초로 미뤄 방해 끝(100초)에 걸리게 한다 (4칸은 94초)
            values[219] = 95f; // 공용4도 95초로
            Apply(values);
            LabModel model = Room();
            LabSnapshot before = At(model, 99.9d);
            Assert.That(before.comboActive, Is.True);
            bool lastIsBomb = model.TimelineCommonIsBomb(3);
            if (lastIsBomb) Assert.That(GourdBombs(before), Is.GreaterThan(0));
            else Assert.That(before.SwipeBlockOn(before.swipeBlockedPlayer), Is.True);

            LabSnapshot after = At(model, 100d);
            Assert.That(after.comboActive, Is.False, "The running combo stops without a result...");
            Assert.That(after.players.All(player => player.comboPenaltyUntil <= 100d && player.comboRewardUntil <= 100d), Is.True,
                "...and gives no SMALL or BIG.");
            Assert.That(GourdBombs(after), Is.Zero, "Gourd bombs still on the boards quietly disappear.");
            Assert.That(after.swipeBlockStartedAt, Is.LessThan(0d), "The swipe block ends.");

            for (double t = 100.5d; t < 120d; t += .5d)
            {
                LabSnapshot snapshot = At(model, t);
                Assert.That(snapshot.comboActive || snapshot.comboNextAt >= 0d || snapshot.flashAt >= 0d, Is.False, "t=" + t);
                Assert.That(GourdBombs(snapshot) == 0 && snapshot.swipeBlockStartedAt < 0d, Is.True, "t=" + t);
            }
        }

        [Test]
        public void SabotageEndReleasesAFlashStillDazzlingEveryone()
        {
            float[] values = config.CaptureDeveloperValues();
            values[212] = 95f; // 섬광2를 95초에: 98초에 번쩍, 방해 끝 100초에도 눈부시다
            values[214] = 0f;
            Apply(values);
            LabModel model = Room();
            At(model, 99d);
            Assert.That(model.FlashGlared(0, 99d), Is.True);
            LabSnapshot after = At(model, 100d);
            Assert.That(model.FlashGlared(0, 100d), Is.False);
            Assert.That(after.players.All(player => player.flashReleasedAt == 100d), Is.True,
                "Every screen clears the flash the same way a shake release does (#301).");
        }

        [Test]
        public void WithoutTheTimelineTheOldSchedulesStay()
        {
            float[] values = config.CaptureDeveloperValues();
            values[205] = 0f;
            Apply(values);
            LabModel model = Room();
            Assert.That(At(model, 7.5d).flashWarningAt, Is.EqualTo(7d), "The flash keeps its own first start (10 s) and warning (3 s).");
            Assert.That(model.Snapshot(7.5d).remaining, Is.EqualTo(172.5d), "...and the common 180 s battle.");
        }
    }
}
