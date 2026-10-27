using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// Fever time (#334): with the set seconds left in a fox's battle the yokai stops, the gimmicks still going end, and
    /// stamina refills faster by itself. Other stages and the 2:2 battle have none.
    /// </summary>
    public sealed class LabFeverTimeTests
    {
        private static readonly ulong[] Players = { 10, 20, 30 };
        private LabConfig config;

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[2] = 120f;                  // the two-minute battle (#328)
            values[3] = values[4] = 120f;      // a full six-orb bar (#328)
            values[5] = 20f;                   // one orb's stamina
            values[6] = 20f; values[7] = 1f;   // one orb's worth a second
            values[74] = LabModel.MaximumParticipants; // a drum for everyone
            values[205] = values[206] = 0f;    // no gimmicks unless a test turns them on
            Apply(values);
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private void Apply(float[] values) =>
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);

        private LabModel Room(LabStageYokai stage = LabStageYokai.Gumiho)
        {
            var model = new LabModel();
            model.Begin("fever-" + stage, Players, 0d, config, stage);
            return model;
        }

        private static float Stamina(LabModel model, double at) => model.Snapshot(at).players.Single(p => p.id == 10ul).stamina;

        [Test]
        public void FeverTimeAppendsTwoDevRowsInItsOwnGroup()
        {
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(240));
            Assert.That(LabConfig.DeveloperFields.Skip(238).Select(field => field.Label), Is.EqualTo(new[]
            {
                "피버 타임 시작 (남은 초, 0 = 끔)", "피버 타임 충전 배율"
            }));
            Assert.That(LabConfig.DeveloperFields.Skip(238).Select(field => field.Min), Is.EqualTo(new[] { 0f, 1f }));
            Assert.That(LabConfig.DeveloperFields.Skip(238).Select(field => field.Max), Is.EqualTo(new[] { 60f, 5f }));
            Assert.That(LabConfig.DeveloperCategories.Single(category => category.Title == "피버 타임 (Hapio 구미호 전용)").FieldIndices,
                Is.EqualTo(new[] { 238, 239 }));
            LabConfig saved = Resources.Load<LabConfig>("LabConfig");
            Assert.That(saved.CaptureDeveloperValues().Skip(238), Is.EqualTo(new[] { 20f, 2f }), "Saved: the last 20 s, twice the refill.");
            float[] values = config.CaptureDeveloperValues();
            values[238] = 25f; values[239] = 3f;
            Apply(values);
            Assert.That(config.FeverTimeSeconds, Is.EqualTo(25f));
            Assert.That(config.FeverTimeRecoveryMultiplier, Is.EqualTo(3f));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
        }

        [Test]
        public void TheFoxsFeverTimeStartsWithTwentySecondsLeftByDefault()
        {
            Assert.That(config.FeverTimeSeconds, Is.EqualTo(20f));
            Assert.That(config.FeverTimeRecoveryMultiplier, Is.EqualTo(2f));
            LabModel model = Room();
            Assert.That(model.Snapshot(0d).feverTimeAt, Is.EqualTo(100d), "120 s battle, the last 20 s.");
            Assert.That(model.FeverTimeOn(99.9d), Is.False);
            Assert.That(model.Snapshot(99.9d).FeverTimeOn(99.9d), Is.False);
            Assert.That(model.FeverTimeOn(100d), Is.True);
            Assert.That(model.Snapshot(100d).FeverTimeOn(100d), Is.True, "Every screen reads the same start time.");
        }

        [TestCase(LabStageYokai.None)]
        [TestCase(LabStageYokai.Yaguang)]
        [TestCase(LabStageYokai.Jigwi)]
        public void OnlyTheFoxHasFeverTimeForNow(LabStageYokai stage)
        {
            LabModel model = Room(stage);
            Assert.That(model.Snapshot(0d).feverTimeAt, Is.EqualTo(-1d));
            Assert.That(model.FeverTimeOn(110d), Is.False);
        }

        [Test]
        public void ZeroTurnsItOffAndALongerFeverThanTheBattleStartsAtOnce()
        {
            float[] values = config.CaptureDeveloperValues();
            values[LabConfig.FeverTimeSecondsField] = 0f;
            Apply(values);
            Assert.That(Room().Snapshot(0d).feverTimeAt, Is.EqualTo(-1d));
            values[2] = 30f; values[LabConfig.FeverTimeSecondsField] = 40f;
            Apply(values);
            LabModel model = Room();
            Assert.That(model.Snapshot(0d).feverTimeAt, Is.Zero);
            Assert.That(model.FeverTimeOn(0d), Is.True);
        }

        [Test]
        public void OnlyTheRefillByItselfIsFasterDuringFeverTime()
        {
            LabModel model = Room();
            // Spend the full bar (six orbs) just before and just after the fever starts, and watch it refill.
            for (int i = 0; i < 6; i++) Assert.That(model.TryGenerate(10, "early-" + i, 90d, out _, out string reason), Is.True, reason);
            Assert.That(Stamina(model, 90d), Is.Zero.Within(.001f));
            Assert.That(Stamina(model, 91d), Is.EqualTo(20f).Within(.001f), "One orb's worth a second before the fever.");
            for (int i = 0; i < 6; i++) Assert.That(model.TryGenerate(10, "late-" + i, 99.5d, out _, out string reason), Is.True, reason);
            Assert.That(Stamina(model, 100d), Is.EqualTo(10f).Within(.001f), "Half a second at the usual rate up to the start...");
            Assert.That(Stamina(model, 100.5d), Is.EqualTo(30f).Within(.001f), "...then twice as fast.");
            Assert.That(Stamina(model, 101d), Is.EqualTo(50f).Within(.001f));
            Assert.That(model.Snapshot(101d).players.Single(p => p.id == 20ul).stamina, Is.EqualTo(120f), "Never over the bar.");

            // A single long step that spans the start counts each side at its own rate.
            var jump = Room();
            for (int i = 0; i < 6; i++) Assert.That(jump.TryGenerate(10, "jump-" + i, 99d, out _, out _), Is.True);
            Assert.That(Stamina(jump, 101d), Is.EqualTo(20f + 40f).Within(.001f));
        }

        [Test]
        public void NoGimmickIsCastFromTheStartOfFeverTimeEvenOneDueThen()
        {
            float[] values = config.CaptureDeveloperValues();
            values[205] = 10f; values[206] = 20f; // casts due at 10, 20, ... 100, 110
            Apply(values);
            LabModel model = Room();
            model.Tick(119.9d);
            LabSabotage[] casts = model.DrainSabotages();
            Assert.That(casts, Is.Not.Empty);
            Assert.That(casts.Max(cast => cast.At), Is.LessThan(100d), "The fox stops at the fever's start.");
        }

        [TestCase(LabSabotageKind.GourdBomb)]
        [TestCase(LabSabotageKind.SwipeBlock)]
        [TestCase(LabSabotageKind.FoxFire)]
        [TestCase(LabSabotageKind.Charm)]
        public void TheGimmickStillGoingEndsWhenFeverTimeStarts(LabSabotageKind kind)
        {
            float[] values = config.CaptureDeveloperValues();
            bool common = kind == LabSabotageKind.GourdBomb || kind == LabSabotageKind.SwipeBlock;
            values[205] = common ? 95f : 0f;
            values[206] = common ? 0f : 98f;
            values[169] = kind == LabSabotageKind.GourdBomb ? 100f : 0f; values[170] = 100f - values[169];
            values[171] = kind == LabSabotageKind.FoxFire ? 100f : 0f; values[172] = 100f - values[171];
            Apply(values);
            LabModel model = Room();
            model.Tick(99.9d);
            LabSabotage cast = model.DrainSabotages().Single();
            Assert.That(cast.Kind, Is.EqualTo(kind));
            model.DrainOrbExpiries();
            LabSnapshot before = model.Snapshot(99.9d);
            LabSnapshot after = model.Snapshot(100.2d);
            switch (kind)
            {
                case LabSabotageKind.GourdBomb:
                    LabOrbState bomb = before.orbs.Single(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd);
                    Assert.That(bomb.lockSwipes, Is.GreaterThan(0));
                    Assert.That(after.orbs.Any(orb => orb.id == bomb.id), Is.False);
                    Assert.That(model.DrainOrbExpiries().Select(item => item.OrbId), Does.Contain(bomb.id), "It goes up in smoke...");
                    Assert.That(model.DrainBombExplosions(), Is.Empty, "...without a blast.");
                    break;
                case LabSabotageKind.SwipeBlock:
                    Assert.That(before.SwipeBlockOn(cast.Target), Is.True);
                    Assert.That(after.SwipeBlockOn(cast.Target), Is.False, "The block lifts.");
                    break;
                case LabSabotageKind.FoxFire:
                    string[] foxes = before.orbs.Where(orb => orb.kind == LabOrbKind.FoxFire).Select(orb => orb.id).ToArray();
                    Assert.That(foxes, Is.Not.Empty);
                    Assert.That(after.orbs.Any(orb => orb.kind == LabOrbKind.FoxFire), Is.False);
                    Assert.That(model.DrainOrbExpiries().Select(item => item.OrbId), Is.SupersetOf(foxes), "In their blue smoke.");
                    break;
                case LabSabotageKind.Charm:
                    Assert.That(before.CharmWarningAt(99.9d), Is.True, "Warned at 98 s, due to start at 101 s.");
                    Assert.That(after.CharmWarningAt(100.2d), Is.False, "The warning is called off...");
                    Assert.That(model.Snapshot(101.5d).CharmOn(cast.Target), Is.False, "...and the charm never starts.");
                    break;
            }
        }
    }
}
