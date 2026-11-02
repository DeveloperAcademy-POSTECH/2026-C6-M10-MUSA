using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>야광귀 피버 북 신호 (#414): 두근두근 박동 모양과 연타 수 세기.</summary>
    public sealed class LabYaguangFeverDrumCueTests
    {
        [Test]
        public void TheDrumBeatsTwiceEachBeatAndRestsBetween()
        {
            const float rate = 1f;
            Assert.That(LabYaguangFeverDrumCue.Heartbeat(.07f, rate), Is.EqualTo(1f).Within(.01f), "The big 두.");
            Assert.That(LabYaguangFeverDrumCue.Heartbeat(.29f, rate), Is.EqualTo(.6f).Within(.01f), "The smaller 근.");
            Assert.That(LabYaguangFeverDrumCue.Heartbeat(.6f, rate), Is.Zero, "Then it rests until the next beat.");
            Assert.That(LabYaguangFeverDrumCue.Heartbeat(1.07f, rate), Is.EqualTo(1f).Within(.01f));
            Assert.That(LabYaguangFeverDrumCue.Heartbeat(.07f, 0f), Is.Zero, "No beat at rate 0.");
        }

        [Test]
        public void QuickTapsCountUpAndAPauseStartsAgain()
        {
            int count = LabYaguangFeverDrumCue.NextTapCount(0, -999f, 10f, .8f);
            Assert.That(count, Is.EqualTo(1));
            count = LabYaguangFeverDrumCue.NextTapCount(count, 10f, 10.5f, .8f);
            Assert.That(count, Is.EqualTo(2));
            count = LabYaguangFeverDrumCue.NextTapCount(count, 10.5f, 11.2f, .8f);
            Assert.That(count, Is.EqualTo(3));
            count = LabYaguangFeverDrumCue.NextTapCount(count, 11.2f, 12.5f, .8f);
            Assert.That(count, Is.EqualTo(1), "A pause longer than the window starts again from 1.");
        }
    }
}
