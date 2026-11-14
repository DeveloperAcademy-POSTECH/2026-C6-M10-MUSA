using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>#734: 남은 30초 알림은 스토리 라운드에서 30초를 넘는 순간 한 번만, 늦게 들어오면 안 뜬다.</summary>
    public sealed class LabThirtySecondsLeftHudTests
    {
        [Test]
        public void ItFiresOnceAsTheTimeLeftCrossesThirtySeconds()
        {
            Assert.That(LabThirtySecondsLeftHud.Crossed(30.02d, 29.99d, 30f), Is.True, "Just crossed.");
            Assert.That(LabThirtySecondsLeftHud.Crossed(30.5d, 30d, 30f), Is.True, "Exactly 00:30.");
            Assert.That(LabThirtySecondsLeftHud.Crossed(29.99d, 29.97d, 30f), Is.False, "Already past: not again.");
            Assert.That(LabThirtySecondsLeftHud.Crossed(31d, 30.01d, 30f), Is.False, "Not yet.");
            Assert.That(LabThirtySecondsLeftHud.Crossed(double.NaN, 29.5d, 30f), Is.True, "Joined right at 30 s: still shown.");
            Assert.That(LabThirtySecondsLeftHud.Crossed(double.NaN, 120d, 30f), Is.False, "A fresh battle.");
        }

        [Test]
        public void ADeviceThatJoinsWellPastThirtySecondsDoesNotShowIt()
        {
            Assert.That(LabThirtySecondsLeftHud.Crossed(double.NaN, 27d, 30f), Is.False);
            Assert.That(LabThirtySecondsLeftHud.Crossed(35d, 28d, 30f), Is.False, "A long stall (more than 1.5 s past).");
            Assert.That(LabThirtySecondsLeftHud.Crossed(1d, 0d, 30f), Is.False, "Time is up.");
        }

        [Test]
        public void OnlyStoryRoundsShowItNotThePracticeBoardOrTwoOnTwo()
        {
            foreach (LabStageYokai stage in new[] { LabStageYokai.Gumiho, LabStageYokai.Yaguang, LabStageYokai.Jigwi })
                Assert.That(LabThirtySecondsLeftHud.AppliesTo(true, false, false, stage), Is.True, stage.ToString());
            Assert.That(LabThirtySecondsLeftHud.AppliesTo(true, true, false, LabStageYokai.Gumiho), Is.False, "Tutorial.");
            Assert.That(LabThirtySecondsLeftHud.AppliesTo(true, false, true, LabStageYokai.Gumiho), Is.False, "2:2.");
            Assert.That(LabThirtySecondsLeftHud.AppliesTo(true, false, false, LabStageYokai.None), Is.False, "No stage.");
            Assert.That(LabThirtySecondsLeftHud.AppliesTo(false, false, false, LabStageYokai.Gumiho), Is.False, "Lab room.");
        }

        [Test]
        public void TheBubbleSaysThirtySecondsLeftPopsAndFades()
        {
            Assert.That(LabThirtySecondsLeftHud.WordsFor(30f), Is.EqualTo("30초 남았소!"));
            Assert.That(LabThirtySecondsLeftHud.BubbleScale(.16f), Is.EqualTo(1.3f).Within(.001f), "Pops past its size.");
            Assert.That(LabThirtySecondsLeftHud.BubbleScale(.32f), Is.EqualTo(1f).Within(.001f));
            Assert.That(LabThirtySecondsLeftHud.BubbleAlpha(1f, 2.2f), Is.EqualTo(1f));
            Assert.That(LabThirtySecondsLeftHud.BubbleAlpha(2.2f, 2.2f), Is.Zero, "Gone after its time.");
            Assert.That(LabThirtySecondsLeftHud.BubbleShake(.6f), Is.Zero, "Stops shaking.");
        }

        [Test]
        public void TheRedEdgeStaysWhileTheBubbleIsUpAndGoesWithIt()
        {
            Assert.That(LabThirtySecondsLeftHud.EdgeAlphaAt(0f, 1f, 0f), Is.EqualTo(.6f).Within(.001f), "Flashes first.");
            Assert.That(LabThirtySecondsLeftHud.EdgeAlphaAt(1.5f, 1f, 0f), Is.GreaterThan(.2f), "Still red with the bubble up.");
            Assert.That(LabThirtySecondsLeftHud.EdgeAlphaAt(2.2f, 0f, 0f), Is.Zero, "Gone with the bubble.");
        }

        [Test]
        public void TheDefaultsAreThirtySecondsAndAStrongBuzz()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(config.ThirtySecondsLeftAt, Is.EqualTo(30f));
                Assert.That(config.ThirtySecondsLeftBubbleSeconds, Is.EqualTo(2.2f).Within(.001f));
                Assert.That(config.ThirtySecondsLeftHaptic, Is.EqualTo(1f));
                Assert.That(config.ThirtySecondsLeftSound, Is.EqualTo(LabThirtySecondsLeftSound.Gong));
                Assert.That(LabThirtySecondsLeftHud.FolderFor(LabThirtySecondsLeftSound.Gong), Is.EqualTo("ThirtySecondsLeftGong"));
                Assert.That(LabThirtySecondsLeftHud.FolderFor(LabThirtySecondsLeftSound.Drum), Is.EqualTo("ThirtySecondsLeftDrum"));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
