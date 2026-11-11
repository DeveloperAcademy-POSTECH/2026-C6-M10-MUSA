using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Story participant scaling of round-owned hit budgets, independent of rendered paint.</summary>
    public sealed class LabStoryParticipantPaintScalingTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly LabStageYokai[] StoryStages =
            { LabStageYokai.Gumiho, LabStageYokai.Yaguang, LabStageYokai.Jigwi };
        private LabConfig config;

        [SetUp] public void Create() => config = ScriptableObject.CreateInstance<LabConfig>();
        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private static IEnumerable<TestCaseData> StoryParticipantCases()
        {
            foreach (LabStageYokai stage in StoryStages)
            {
                yield return new TestCaseData(stage, 2, 3.25f, 2.5f, 66, 117);
                yield return new TestCaseData(stage, 3, 2.1666667f, 1.6666667f, 44, 78);
                yield return new TestCaseData(stage, 4, 1.625f, 1.25f, 33, 58);
                yield return new TestCaseData(stage, 5, 1.3f, 1f, 26, 47);
            }
        }

        [TestCaseSource(nameof(StoryParticipantCases))]
        public void FreshAndAuthoredDefaultsUseFivePlayerBudgetsForEveryStoryStage(LabStageYokai stage,
            int participants, float initial, float repeat, int initialCells, int totalCells)
        {
            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
            {
                float[] approved = source.CaptureDeveloperValues();
                Assert.That(source.PaintInitialPercent, Is.EqualTo(1.3f));
                Assert.That(source.PaintRepeatPercent, Is.EqualTo(1f));
                LabPaintGrowthSettings captured = source.CapturePaintGrowthSettings(participants, stage);
                Assert.That(captured.InitialPercent, Is.EqualTo(initial).Within(.000001f));
                Assert.That(captured.RepeatPercent, Is.EqualTo(repeat).Within(.000001f));
                AssertOtherPaintSettingsEqual(captured, source.PaintGrowthSettings);
                Assert.That(source.CaptureDeveloperValues(), Is.EqualTo(approved),
                    "The original DEV values remain five-player base values.");
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void CustomDeveloperBasesScaleOnceWithoutRoundingOrChangingOtherPaintSettings(int participants)
        {
            float[] values = config.CaptureDeveloperValues();
            values[104] = 1f; values[105] = 1.234567f; values[106] = .456789f;
            values[107] = 12.375f; values[108] = 37.125f; values[109] = 2.375f;
            values[132] = 1f; values[133] = .3125f; values[134] = (float)LabPaintPattern.Flow;
            values[138] = .625f; values[139] = .75f;
            values[148] = 1f; values[149] = 7.25f; values[150] = .375f; values[151] = 31.5f;
            Apply(values);
            LabPaintGrowthSettings basis = config.PaintGrowthSettings;
            foreach (LabStageYokai stage in StoryStages)
            {
                LabPaintGrowthSettings first = config.CapturePaintGrowthSettings(participants, stage);
                LabPaintGrowthSettings second = config.CapturePaintGrowthSettings(participants, stage);
                Assert.That(first, Is.Not.SameAs(second));
                Assert.That(first.InitialPercent, Is.EqualTo(1.234567f * (5f / participants)).Within(.0000005f));
                Assert.That(first.RepeatPercent, Is.EqualTo(.456789f * (5f / participants)).Within(.0000005f));
                AssertAllPaintSettingsEqual(first, second);
                AssertOtherPaintSettingsEqual(first, basis);
                first.InitialPercent = first.RepeatPercent = 99f;
                first.GrowthPercentPerSecond = 100f;
                first.DryingEnabled = false;
                AssertAllPaintSettingsEqual(config.CapturePaintGrowthSettings(participants, stage), second);
                AssertAllPaintSettingsEqual(config.PaintGrowthSettings, basis);
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
            }
        }

        [TestCase(2, 50f, 12f, 100f, 30f)]
        [TestCase(3, 10f, 80f, 16.6666667f, 100f)]
        [TestCase(4, 90f, 10f, 100f, 12.5f)]
        [TestCase(5, 100f, 100f, 100f, 100f)]
        public void InitialAndRepeatCapsApplyIndependentlyToTheCapturedValues(int participants,
            float baseInitial, float baseRepeat, float expectedInitial, float expectedRepeat)
        {
            float[] values = config.CaptureDeveloperValues();
            values[105] = baseInitial; values[106] = baseRepeat;
            Apply(values);
            foreach (LabStageYokai stage in StoryStages)
            {
                LabPaintGrowthSettings captured = config.CapturePaintGrowthSettings(participants, stage);
                Assert.That(captured.InitialPercent, Is.EqualTo(expectedInitial).Within(.000002f));
                Assert.That(captured.RepeatPercent, Is.EqualTo(expectedRepeat).Within(.000002f));
                Assert.DoesNotThrow(() => new LabPaintSurface(captured));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
            }
        }

        [Test]
        public void SoloLabTutorialTerritoryAndCountsOutsideStoryRoomsKeepFreshBaseSettings()
        {
            LabPaintGrowthSettings basis = config.PaintGrowthSettings;
            float[] approved = config.CaptureDeveloperValues();
            foreach (LabStageYokai stage in StoryStages)
            {
                foreach (int count in new[] { -1, 0, 1, 6 })
                    AssertAllPaintSettingsEqual(config.CapturePaintGrowthSettings(count, stage), basis);
                foreach (int count in new[] { 1, 2, 4, 6 })
                    AssertAllPaintSettingsEqual(config.CapturePaintGrowthSettings(count, stage, true), basis);
            }
            // Tutorial begins with None just as the standalone Lab does.
            foreach (int count in Enumerable.Range(1, 6))
            {
                LabPaintGrowthSettings captured = config.CapturePaintGrowthSettings(count, LabStageYokai.None);
                Assert.That(captured, Is.Not.SameAs(basis));
                AssertAllPaintSettingsEqual(captured, basis);
            }
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(approved));
        }

        [TestCaseSource(nameof(StoryParticipantCases))]
        public void BeginUsesTheActualSelectedRosterForExactInitialAndRepeatedHitCells(LabStageYokai stage,
            int participants, float initial, float repeat, int initialCells, int totalCells)
        {
            ConfigureExactCells();
            float[] approved = config.CaptureDeveloperValues();
            ulong[] roster = Roster(participants);
            var model = new LabModel();
            model.Begin("story-selected-roster-" + stage + participants, roster, 0d, config, stage);
            Assert.That(model.CopyPlayers().OrderBy(player => player.seat).Select(player => player.id),
                Is.EqualTo(roster), "Host 0 stays in the selected P2 seat.");
            Assert.That(model.StageYokai, Is.EqualTo(stage));
            Hit(model, "first", 0d);
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(initialCells));
            Assert.That(model.PaintSurface.NewlyPaintedCellsBy(0ul), Is.EqualTo(initialCells));
            Hit(model, "repeat", 0d);
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(initialCells),
                "A hit on painted cells supplies growth instead of another initial splash.");
            model.Tick(1d);
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(totalCells));
            Assert.That(model.PaintSurface.NewlyPaintedCellsBy(0ul), Is.EqualTo(totalCells));
            Assert.That(model.PaintSurface.PendingCoverage, Is.LessThan(1d / model.PaintSurface.CellCount));
            Assert.That(model.PaintSurface.PaintCount, Is.EqualTo(2));
            Assert.That(model.CopyPlayers().Single(player => player.id == 0ul).hitTotal, Is.EqualTo(2));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(approved));
        }

        [TestCase(LabStageYokai.None, 1)]
        [TestCase(LabStageYokai.None, 2)]
        [TestCase(LabStageYokai.None, 3)]
        [TestCase(LabStageYokai.None, 4)]
        [TestCase(LabStageYokai.None, 5)]
        [TestCase(LabStageYokai.Gumiho, 1)]
        [TestCase(LabStageYokai.Yaguang, 1)]
        [TestCase(LabStageYokai.Jigwi, 1)]
        public void SoloAndLabBeginRetainExactUnscaledInitialAndRepeatCells(LabStageYokai stage, int participants)
        {
            ConfigureExactCells(1.25f, .625f);
            var model = new LabModel();
            if (participants == 1) model.BeginSolo("unscaled-entry", 0ul, 0d, config, stage);
            else model.Begin("unscaled-entry", Roster(participants), 0d, config, stage);
            Hit(model, "base-first", 0d);
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(25));
            Hit(model, "base-repeat", 0d);
            model.Tick(1d);
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(38));
            Assert.That(model.PaintSurface.PaintCount, Is.EqualTo(2));
        }

        [TestCase(LabStageYokai.Gumiho)]
        [TestCase(LabStageYokai.Yaguang)]
        [TestCase(LabStageYokai.Jigwi)]
        public void BeginFreezesTheScaledBudgetsAndRetryCapturesTheNewBasesOnce(LabStageYokai stage)
        {
            ConfigureExactCells(1.25f, .625f);
            ulong[] roster = Roster(2);
            var model = new LabModel();
            model.Begin("story-retry-scale", roster, 0d, config, stage);
            float[] updated = config.CaptureDeveloperValues();
            updated[105] = 2.5f; updated[106] = 1.25f;
            Apply(updated);
            Hit(model, "old-first", 0d);
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(64));
            Hit(model, "old-repeat", 0d);
            model.Tick(1d);
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(96),
                "An active round retains the bases approved when it began.");
            foreach (ulong player in roster)
                Assert.That(model.TryApplyPlayerDamage(player, int.MaxValue, 1d), Is.True);
            Assert.That(model.Phase, Is.EqualTo(LabPhase.Defeat));
            uint firstRound = model.Round;
            model.Begin("story-retry-scale", roster, 2d, config, stage);
            Assert.That(model.Round, Is.EqualTo(firstRound + 1u));
            Assert.That(model.PaintSurface.CoveredCount, Is.Zero);
            Hit(model, "new-first", 2d);
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(128));
            Hit(model, "new-repeat", 2d);
            model.Tick(3d);
            Assert.That(model.PaintSurface.CoveredCount, Is.EqualTo(192));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(updated));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(6)]
        public void TerritoryBeginKeepsBasePaintDespiteAnExplicitStoryStage(int participants)
        {
            ConfigureExactCells(1.25f, .625f);
            float[] values = config.CaptureDeveloperValues(); values[196] = 1f; Apply(values);
            var model = new LabModel();
            if (participants == 1) model.BeginSolo("territory-base", 0ul, 0d, config, LabStageYokai.Gumiho);
            else model.Begin("territory-base", Roster(participants), 0d, config, LabStageYokai.Gumiho);
            Assert.That(model.TeamTerritory, Is.True);
            Assert.That(model.StageYokai, Is.EqualTo(LabStageYokai.None));
            // Territory retains its own pattern/overwrite rules; measure the supplied area before growth.
            Assert.That(model.PaintSurface.RegisterHit(Vector3.forward, Color.blue, 0ul, 1), Is.True);
            double suppliedCells = model.PaintSurface.CoveredCount
                + model.PaintSurface.PendingCoverage * model.PaintSurface.CellCount;
            Assert.That(suppliedCells, Is.EqualTo(25.6d).Within(.000001d));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
        }

        [TestCase(2, 64, 32)]
        [TestCase(5, 25, 12)]
        public void SmallAndRewardSharesMultiplyTheCapturedParticipantBudgets(int participants,
            int expectedRewardCells, int expectedSmallCells)
        {
            ConfigureExactCells(1.25f, .625f);
            var small = new LabModel();
            small.Begin("story-small", Roster(participants), 0d, config, LabStageYokai.Gumiho);
            Hit(small, "small", 0d, orb => orb.small = true);
            small.Tick(1d);
            Assert.That(small.PaintSurface.CoveredCount, Is.EqualTo(expectedSmallCells));
            var reward = new LabModel();
            reward.Begin("story-reward", Roster(participants), 0d, config, LabStageYokai.Jigwi);
            Hit(reward, "reward", 0d, orb =>
            {
                orb.small = true; orb.jigwiTalismanReward = true; orb.jigwiABRewardPaintScale = 2f;
            });
            reward.Tick(1d);
            Assert.That(reward.PaintSurface.CoveredCount, Is.EqualTo(expectedRewardCells),
                "A half-size orb with a 2x talisman reward gets one full participant-adjusted hit budget.");
        }

        [TestCase(2, 64d)]
        [TestCase(5, 25.6d)]
        public void FiveColorRewardDividesTheParticipantAdjustedBudgetAcrossItsFiveStamps(int participants,
            double expectedSuppliedCells)
        {
            // Equal initial/repeat bases make the total independent of overlap between the five colours.
            ConfigureExactCells(.625f, .625f);
            var model = new LabModel();
            model.Begin("story-five-colors", Roster(participants), 0d, config, LabStageYokai.Yaguang);
            Hit(model, "five-colors", 0d, orb => { orb.fiveColor = true; orb.element = LabOrbElement.None; });
            model.Tick(1d);
            Assert.That(model.LastHitFiveColor, Is.True);
            Assert.That(model.PaintSurface.PaintCount, Is.EqualTo(5));
            double suppliedCells = model.PaintSurface.CoveredCount
                + model.PaintSurface.PendingCoverage * model.PaintSurface.CellCount;
            Assert.That(suppliedCells, Is.EqualTo(expectedSuppliedCells).Within(.000001d),
                "The existing 2x five-colour reward multiplies one captured participant-adjusted budget.");
        }

        private void ConfigureExactCells(float initial = 1.3f, float repeat = 1f)
        {
            float[] values = config.CaptureDeveloperValues();
            values[104] = 0f; values[105] = initial; values[106] = repeat;
            values[107] = 100f; values[108] = 100f;
            values[134] = (float)LabPaintPattern.Legacy; values[148] = 0f;
            Apply(values);
        }

        private void Apply(float[] values) => Assert.That(config.TryApplyDeveloperValues(values,
            out string error), Is.True, error);

        private static ulong[] Roster(int participants) =>
            new ulong[] { 711, 0, 83, 905, 27, 42 }.Take(participants).ToArray();

        private static void Hit(LabModel model, string id, double at, Action<LabOrbState> customize = null)
        {
            // Supply a resolved projectile directly to isolate paint scaling from generation/physics.
            var orb = new LabOrbState { id = id, owner = 0ul, kind = LabOrbKind.Combined,
                element = LabOrbElement.Fire, inFlight = true, createdAt = at };
            customize?.Invoke(orb);
            var orbs = (Dictionary<string, LabOrbState>)typeof(LabModel).GetField("orbs", Private).GetValue(model);
            orbs.Add(id, orb);
            Assert.That(model.ConfirmHit(id, at, Vector3.forward, out _, out _, out _), Is.True);
        }

        private static void AssertOtherPaintSettingsEqual(LabPaintGrowthSettings actual, LabPaintGrowthSettings expected)
        {
            foreach (FieldInfo field in typeof(LabPaintGrowthSettings).GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (field.Name != nameof(LabPaintGrowthSettings.InitialPercent)
                    && field.Name != nameof(LabPaintGrowthSettings.RepeatPercent))
                    Assert.That(field.GetValue(actual), Is.EqualTo(field.GetValue(expected)), field.Name);
        }

        private static void AssertAllPaintSettingsEqual(LabPaintGrowthSettings actual, LabPaintGrowthSettings expected)
        {
            AssertOtherPaintSettingsEqual(actual, expected);
            Assert.That(actual.InitialPercent, Is.EqualTo(expected.InitialPercent));
            Assert.That(actual.RepeatPercent, Is.EqualTo(expected.RepeatPercent));
        }
    }
}
