using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>섬광 파훼 보상 (#329): 끝까지 흔든 사람 판에 황금알, 황금알은 금빛으로 넓게 칠하고 사라지지 않는다.</summary>
    public sealed class LabFlashRewardTests
    {
        private static readonly ulong[] Players = { 10, 20, 30 };
        private LabConfig config;

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            // 모두에게 5초에 번쩍, 예고 없음, 흔들어 풀기 켬, 황금알 3개.
            JsonUtility.FromJsonOverwrite("{\"yaguangFlashEnabled\":true,\"yaguangFlashTargetOne\":false,"
                + "\"yaguangFlashFirstStartSeconds\":5,\"yaguangFlashWarningSeconds\":0,\"yaguangFlashGlareSeconds\":12,"
                + "\"yaguangFlashRecoverySeconds\":1,\"yaguangFlashShakeRelease\":true,\"gourdColorComboEnabled\":false,"
                + "\"yaguangGoldenEggCount\":3,\"yaguangGoldenEggPaintScale\":2}", config);
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

        private static LabOrbState[] Eggs(LabModel model, double now, ulong owner) =>
            model.Snapshot(now).orbs.Where(orb => orb.golden && orb.owner == owner).ToArray();

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
        public void ShakingOnToTheMaximumDropsGoldenEggsOnceOnTheShakersBoard()
        {
            LabModel model = Flashed();
            Assert.That(model.TryFlashShakeRelease(10, 1u, 6d), Is.True, "Released after shaking as many times as there were orbs.");
            Assert.That(model.TryFlashGoldenBonus(10, 1u, 6.5d, out int dropped), Is.True, "...then shook on to the maximum.");
            Assert.That(dropped, Is.EqualTo(3));
            LabOrbState[] eggs = Eggs(model, 6.5d, 10);
            Assert.That(eggs, Has.Length.EqualTo(3));
            Assert.That(eggs.All(egg => egg.kind == LabOrbKind.Combined && egg.element == LabOrbElement.None && !egg.inFlight), Is.True,
                "A golden egg is a colourless combined orb, ready to throw.");
            Assert.That(model.Snapshot(6.5d).players.Single(player => player.id == 10).flashBonusFlash, Is.EqualTo(1u));
            Assert.That(Eggs(model, 6.5d, 20), Is.Empty, "Only the shaker gets them.");

            Assert.That(model.TryFlashGoldenBonus(10, 1u, 7d, out _), Is.False, "Once per flash.");
            Assert.That(model.TryFlashGoldenBonus(30, 2u, 7d, out _), Is.False, "Only for the current flash.");

            // 구슬이 최대 횟수보다 많아 마지막 흔들기에서야 다 풀린 사람: 보상과 함께 풀린다.
            Assert.That(model.TryFlashGoldenBonus(20, 1u, 7d, out dropped), Is.True);
            Assert.That(model.FlashGlared(20, 7d), Is.False);
            Assert.That(dropped, Is.EqualTo(3));

            model.Tick(170d);
            Assert.That(Eggs(model, 170d, 10), Has.Length.EqualTo(3), "Golden eggs never run out like ordinary orbs.");
        }

        [Test]
        public void NoEggsWhenTheRewardIsOffOrThePlayerWasNeverFlashed()
        {
            JsonUtility.FromJsonOverwrite("{\"yaguangGoldenEggCount\":0}", config);
            LabModel model = Flashed();
            Assert.That(model.TryFlashShakeRelease(10, 1u, 6d), Is.True);
            Assert.That(model.TryFlashGoldenBonus(10, 1u, 6.5d, out _), Is.False);
            Assert.That(Eggs(model, 6.5d, 10), Is.Empty);

            JsonUtility.FromJsonOverwrite("{\"yaguangGoldenEggCount\":3}", config);
            var early = new LabModel();
            early.Begin("flash-early", Players, 0d, config);
            Assert.That(early.TryFlashGoldenBonus(10, 1u, 1d, out _), Is.False, "No flash yet.");
        }

        [Test]
        public void AGoldenEggPaintsGoldAndWider()
        {
            LabModel model = Flashed();
            Assert.That(model.TryFlashGoldenBonus(10, 1u, 6d, out _), Is.True);
            LabOrbState egg = Eggs(model, 6d, 10)[0];
            Assert.That(model.TryLaunch(10, egg.id, "golden-throw", 6d, out LabOrbState flying, out string reason), Is.True, reason);
            Assert.That(flying.golden, Is.True, "It flies as a golden egg.");
            Color32 gold = LabPaintSurface.Opaque(config.YaguangGoldenEggColor);
            Assert.That(model.PaintSurface.CountColor(gold), Is.Zero);
            Assert.That(model.ConfirmHit(egg.id, 6.1d, Vector3.forward, out LabOrbElement element, out _, out _), Is.True);
            Assert.That(model.LastHitGolden, Is.True);
            Assert.That(element, Is.EqualTo(LabOrbElement.None), "It has no element, so a combo never counts it.");
            int painted = model.PaintSurface.CountColor(gold);
            Assert.That(painted, Is.GreaterThan(0), "It paints the gourd gold.");
        }
    }
}
