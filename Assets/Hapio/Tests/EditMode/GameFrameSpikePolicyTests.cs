using C6Lab;
using NUnit.Framework;

namespace Hapio.Tests
{
    /// <summary>Thresholds of the battle frame probe and the first-use notes it reads (#481).</summary>
    public sealed class GameFrameSpikePolicyTests
    {
        [Test]
        public void OnlyFramesOverTwentyFiveMillisecondsCountAndOnlyDebugBuildsAttachTheProbe()
        {
            Assert.That(GameFrameSpikePolicy.IsSpike(25f), Is.False);
            Assert.That(GameFrameSpikePolicy.IsSpike(25.01f), Is.True);
            Assert.That(GameFrameSpikePolicy.IsSpike(16.7f), Is.False);
            Assert.That(GameFrameSpikePolicy.ProbeEnabled(true), Is.True);
            Assert.That(GameFrameSpikePolicy.ProbeEnabled(false), Is.False, "A release player never attaches it.");
            foreach (GameFlowPhase phase in new[] { GameFlowPhase.LoadingBattle, GameFlowPhase.SynchronizingBattle,
                GameFlowPhase.Countdown, GameFlowPhase.Playing })
                Assert.That(GameFrameSpikePolicy.IsBattle(phase), Is.True, phase.ToString());
            foreach (GameFlowPhase phase in new[] { GameFlowPhase.Home, GameFlowPhase.Lobby, GameFlowPhase.StageResult,
                GameFlowPhase.Completed, GameFlowPhase.GameOver })
                Assert.That(GameFrameSpikePolicy.IsBattle(phase), Is.False, phase.ToString());
        }

        [Test]
        public void PercentileReadsTheSortedSamplesAndToleratesNone()
        {
            var sorted = new float[100];
            for (int i = 0; i < sorted.Length; i++) sorted[i] = i + 1;
            Assert.That(GameFrameSpikePolicy.Percentile(sorted, 100, .99f), Is.EqualTo(99f));
            Assert.That(GameFrameSpikePolicy.Percentile(sorted, 100, 1f), Is.EqualTo(100f));
            Assert.That(GameFrameSpikePolicy.Percentile(sorted, 1, .99f), Is.EqualTo(1f));
            Assert.That(GameFrameSpikePolicy.Percentile(sorted, 0, .99f), Is.Zero);
        }

        [Test]
        public void FirstUseNotesCountEveryUseAndDescribeTheFrameMask()
        {
            int before = LabFirstUse.CountOf(LabFirstUseKind.BrushDistance);
            LabFirstUse.Note(LabFirstUseKind.BrushDistance);
            using (LabFirstUse.Measure(LabFirstUseKind.Projectile)) { }
            Assert.That(LabFirstUse.CountOf(LabFirstUseKind.BrushDistance), Is.EqualTo(before + 1));
            int mask = LabFirstUse.FlagsFor(UnityEngine.Time.frameCount);
            Assert.That(mask & (1 << (int)LabFirstUseKind.BrushDistance), Is.Not.Zero);
            Assert.That(mask & (1 << (int)LabFirstUseKind.Projectile), Is.Not.Zero);
            Assert.That(LabFirstUse.FlagsFor(UnityEngine.Time.frameCount + 1), Is.Zero, "Notes belong to their own frame.");
            Assert.That(LabFirstUse.Describe((1 << (int)LabFirstUseKind.PatternMask) | (1 << (int)LabFirstUseKind.Sound)),
                Is.EqualTo("PatternMask,Sound"));
            Assert.That(LabFirstUse.Describe(0), Is.EqualTo("-"));
            Assert.That(System.Enum.GetValues(typeof(LabFirstUseKind)).Length, Is.EqualTo(LabFirstUse.KindCount));
        }
    }
}
