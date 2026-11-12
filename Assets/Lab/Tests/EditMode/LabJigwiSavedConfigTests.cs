using NUnit.Framework;
using UnityEditor;

namespace C6Lab.Tests
{
    /// <summary>
    /// #626: 저장된 LabConfig(빌드와 QA가 쓰는 값)가 지귀의 실제 흐름(연기 10 / 부적 45 / 연기 66 / 부적 83)을 쓰는지 본다.
    /// #595 때 시험용 "A Test Only"(부적만 17초마다)가 켜진 채 저장되어 QA에서 부적만 나온 일이 있었다.
    /// </summary>
    public sealed class LabJigwiSavedConfigTests
    {
        private const string ConfigPath = "Assets/Lab/Resources/LabConfig.asset";

        [Test]
        public void TheSavedConfigRunsTheRealJigwiFlowNotATestSchedule()
        {
            var config = AssetDatabase.LoadAssetAtPath<LabConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null, ConfigPath);
            Assert.That(config.JigwiATestOnly, Is.False, "A Test Only (talisman only) is for testing; turn it off before committing.");
            Assert.That(config.JigwiBTestOnly, Is.False, "B Test Only (smoke only) is for testing; turn it off before committing.");
            Assert.That(config.JigwiSmokeFlow, Is.True, "The game runs the smoke flow (#595).");
            Assert.That(config.JigwiRelayTimeline, Is.True,
                "#713: smoke at 6 s, then each mission 10 s after the one before has fully ended (common gimmicks from 25 s every 20 s).");
            Assert.That(config.JigwiRelayGapSeconds, Is.EqualTo(10f).Within(.0001f));
            Assert.That(config.JigwiCommonStartSeconds, Is.EqualTo(25f).Within(.0001f));
            Assert.That(config.JigwiCommonIntervalSeconds, Is.EqualTo(20f).Within(.0001f));
        }

        [Test]
        public void TheSavedTalismanIsEasierAndItsFailureBurnsLonger()
        {
            var config = AssetDatabase.LoadAssetAtPath<LabConfig>(ConfigPath);
            Assert.That(config.JigwiTalismanCoverage, Is.EqualTo(.85f).Within(.0001f), "#626 QA: 85% of the letter completes it.");
            Assert.That(config.JigwiAFailDrumSlipSeconds, Is.EqualTo(20f).Within(.0001f), "#626 QA: a failed talisman's drum burns up to 20 s.");
        }
    }
}
