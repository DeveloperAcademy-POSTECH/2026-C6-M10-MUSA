using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>섬광 파훼 보상 (#329): 끝까지 흔든 사람 판에 오색구슬, 오색구슬은 다섯 원소 색으로 넓게 칠하고 사라지지 않는다.</summary>
    public sealed class LabFlashRewardTests
    {
        private static readonly ulong[] Players = { 10, 20, 30 };
        private LabConfig config;

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            // 모두에게 5초에 번쩍, 예고 없음, 흔들어 풀기 켬, 오색구슬 3개.
            JsonUtility.FromJsonOverwrite("{\"yaguangFlashEnabled\":true,\"yaguangFlashTargetOne\":false,"
                + "\"yaguangFlashFirstStartSeconds\":5,\"yaguangFlashWarningSeconds\":0,\"yaguangFlashGlareSeconds\":12,"
                + "\"yaguangFlashRecoverySeconds\":1,\"yaguangFlashShakeRelease\":true,\"gourdColorComboEnabled\":false,"
                + "\"yaguangFiveColorOrbCount\":3,\"yaguangFiveColorOrbPaintScale\":2}", config);
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private LabModel Flashed()
        {
            var model = new LabModel();
            model.Begin("flash-reward", Players, 0d, config);
            model.Tick(5d);
            Assert.That(Players.All(player => model.FlashGlared(player, 5d)), Is.True);
            return model;
        }

        private static LabOrbState[] Bonus(LabModel model, double now, ulong owner) =>
            model.Snapshot(now).orbs.Where(orb => orb.fiveColor && orb.owner == owner).ToArray();

        [Test]
        public void TheFlashIsUsedUpOnlyByShakingNotByTime()
        {
            LabModel model = Flashed();
            model.Tick(40d);
            Assert.That(Players.All(player => model.FlashGlared(player, 40d)), Is.True,
                "Long after the 12 s glare time, an unshaken player is still dazzled.");
            Assert.That(model.TryFlashShakeRelease(20, 1u, 40d), Is.True);
            Assert.That(model.FlashGlared(20, 40d), Is.False);
            Assert.That(model.FlashGlared(10, 40d), Is.True);

            JsonUtility.FromJsonOverwrite("{\"yaguangFlashShakeRelease\":false}", config);
            var timed = new LabModel();
            timed.Begin("flash-timed", Players, 0d, config);
            timed.Tick(5d);
            Assert.That(timed.FlashGlared(10, 16.9d), Is.True);
            Assert.That(timed.FlashGlared(10, 17d), Is.False, "Without the shake release the glare still wears off with time.");
        }

        [Test]
        public void ShakingOnToTheMaximumDropsFiveColorOrbsOnceOnTheShakersBoard()
        {
            LabModel model = Flashed();
            Assert.That(model.TryFlashShakeRelease(10, 1u, 6d), Is.True, "Released after shaking as many times as there were orbs.");
            Assert.That(model.TryFlashFiveColorBonus(10, 1u, 6.5d, out int dropped), Is.True, "...then shook on to the maximum.");
            Assert.That(dropped, Is.EqualTo(3));
            LabOrbState[] bonus = Bonus(model, 6.5d, 10);
            Assert.That(bonus, Has.Length.EqualTo(3));
            Assert.That(bonus.All(orb => orb.kind == LabOrbKind.Combined && orb.element == LabOrbElement.None && !orb.inFlight), Is.True,
                "A five-colour orb is an element-less combined orb, ready to throw.");
            Assert.That(model.Snapshot(6.5d).players.Single(player => player.id == 10).flashBonusFlash, Is.EqualTo(1u));
            Assert.That(Bonus(model, 6.5d, 20), Is.Empty, "Only the shaker gets them.");

            Assert.That(model.TryFlashFiveColorBonus(10, 1u, 7d, out _), Is.False, "Once per flash.");
            Assert.That(model.TryFlashFiveColorBonus(30, 2u, 7d, out _), Is.False, "Only for the current flash.");

            // 구슬이 최대 횟수보다 많아 마지막 흔들기에서야 다 풀린 사람: 보상과 함께 풀린다.
            Assert.That(model.TryFlashFiveColorBonus(20, 1u, 7d, out dropped), Is.True);
            Assert.That(model.FlashGlared(20, 7d), Is.False);
            Assert.That(dropped, Is.EqualTo(3));

            model.Tick(170d);
            Assert.That(Bonus(model, 170d, 10), Has.Length.EqualTo(3), "Five-colour orbs never run out like ordinary orbs.");
        }

        [Test]
        public void NoOrbsWhenTheRewardIsOffOrThePlayerWasNeverFlashed()
        {
            JsonUtility.FromJsonOverwrite("{\"yaguangFiveColorOrbCount\":0}", config);
            LabModel model = Flashed();
            Assert.That(model.TryFlashShakeRelease(10, 1u, 6d), Is.True);
            Assert.That(model.TryFlashFiveColorBonus(10, 1u, 6.5d, out _), Is.False);
            Assert.That(Bonus(model, 6.5d, 10), Is.Empty);

            JsonUtility.FromJsonOverwrite("{\"yaguangFiveColorOrbCount\":3}", config);
            var early = new LabModel();
            early.Begin("flash-early", Players, 0d, config);
            Assert.That(early.TryFlashFiveColorBonus(10, 1u, 1d, out _), Is.False, "No flash yet.");
        }

        [Test]
        public void AFiveColorOrbPaintsAllFiveElementColours()
        {
            LabModel model = Flashed();
            Assert.That(model.TryFlashFiveColorBonus(10, 1u, 6d, out _), Is.True);
            LabOrbState orb = Bonus(model, 6d, 10)[0];
            Assert.That(model.TryLaunch(10, orb.id, "five-color-throw", 6d, out LabOrbState flying, out string reason), Is.True, reason);
            Assert.That(flying.fiveColor, Is.True, "It flies as a five-colour orb.");
            Color32[] colours = LabModel.FiveColorPaintOrder
                .Select(element => LabPaintSurface.Opaque(model.PaintColorFor(element))).ToArray();
            Assert.That(colours.Distinct().Count(), Is.EqualTo(5));
            Assert.That(colours.All(colour => model.PaintSurface.CountColor(colour) == 0), Is.True);
            Assert.That(model.ConfirmHit(orb.id, 6.1d, Vector3.forward, out LabOrbElement element, out _, out _), Is.True);
            Assert.That(model.LastHitFiveColor, Is.True);
            Assert.That(element, Is.EqualTo(LabOrbElement.None), "It has no element, so a combo never counts it.");
            foreach (Color32 colour in colours)
                Assert.That(model.PaintSurface.CountColor(colour), Is.GreaterThan(0), "Each of the five element colours is painted.");
        }
    }
}
