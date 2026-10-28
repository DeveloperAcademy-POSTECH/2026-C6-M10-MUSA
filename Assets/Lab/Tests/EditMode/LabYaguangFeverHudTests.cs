using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>야광귀 피버타임 시작 연출 (#343): 번쩍임·문구 시간표와 늦게 들어온 기기 처리.</summary>
    public sealed class LabYaguangFeverHudTests
    {
        [Test]
        public void OnlyDevicesThatSeeTheStartShowIt()
        {
            Assert.That(LabYaguangFeverHud.ShouldShow(-1d), Is.False, "Not fever.");
            Assert.That(LabYaguangFeverHud.ShouldShow(0d), Is.True);
            Assert.That(LabYaguangFeverHud.ShouldShow(1d), Is.True);
            Assert.That(LabYaguangFeverHud.ShouldShow(5d), Is.False, "A late joiner skips the start show.");
        }

        [Test]
        public void TheScreenFlashesSeveralTimesThenFades()
        {
            Assert.That(LabYaguangFeverHud.FlashAmount(-.1f, 1.2f, 3), Is.Zero);
            Assert.That(LabYaguangFeverHud.FlashAmount(1.2f, 1.2f, 3), Is.Zero);
            // 3번 번쩍인다: 각 박자 가운데가 밝고 박자 사이는 꺼진다.
            Assert.That(LabYaguangFeverHud.FlashAmount(.2f, 1.2f, 3), Is.GreaterThan(.5f));
            Assert.That(LabYaguangFeverHud.FlashAmount(.4f, 1.2f, 3), Is.LessThan(.05f));
            Assert.That(LabYaguangFeverHud.FlashAmount(.6f, 1.2f, 3), Is.GreaterThan(.3f));
            Assert.That(LabYaguangFeverHud.FlashAmount(1f, 1.2f, 3),
                Is.LessThan(LabYaguangFeverHud.FlashAmount(.2f, 1.2f, 3)), "Later flashes are weaker.");
        }

        [Test]
        public void TheBannerPopsBigThenStaysAndFades()
        {
            Assert.That(LabYaguangFeverHud.BannerScale(0f), Is.LessThan(1f));
            Assert.That(LabYaguangFeverHud.BannerScale(.22f), Is.EqualTo(1.25f).Within(.01f));
            Assert.That(LabYaguangFeverHud.BannerScale(1f), Is.EqualTo(1f).Within(.05f));
            Assert.That(LabYaguangFeverHud.BannerAlpha(1f, 2.6f), Is.EqualTo(1f));
            Assert.That(LabYaguangFeverHud.BannerAlpha(2.35f, 2.6f), Is.EqualTo(.5f).Within(.01f));
            Assert.That(LabYaguangFeverHud.BannerAlpha(2.6f, 2.6f), Is.Zero);
        }
    }
}
