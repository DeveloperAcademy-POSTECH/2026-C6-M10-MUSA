using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>섬광 해결·오색구슬 피격 효과음 (#338): 소리 파일이 없으면 조용히 넘어가고, 알아챈 횟수는 언제나 센다.</summary>
    public sealed class LabFlashRewardSoundTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void EachCueCountsAndStaysQuietWithoutAClip()
        {
            host = new GameObject("flash-reward-sound");
            LabFlashRewardSound sound = host.AddComponent<LabFlashRewardSound>();
            // 소리 파일이 아직 없으면 아무것도 내지 않고 넘어간다. 파일을 넣으면 낸다.
            Assert.That(sound.OnFlashCleared(), Is.EqualTo(sound.FlashClearClipCount > 0));
            Assert.That(sound.OnFiveColorHit(), Is.EqualTo(sound.FiveColorHitClipCount > 0));
            Assert.That(sound.OnFiveColorHit(), Is.EqualTo(sound.FiveColorHitClipCount > 0));
            Assert.That(sound.FlashClearCount, Is.EqualTo(1));
            Assert.That(sound.FiveColorHitCount, Is.EqualTo(2));
        }

        [Test]
        public void TheClipFoldersAreUnderResourcesAudio()
        {
            Assert.That(LabFlashRewardSound.FlashClearFolder, Is.EqualTo("Audio/FlashClear"));
            Assert.That(LabFlashRewardSound.FiveColorHitFolder, Is.EqualTo("Audio/FiveColorHit"));
            Assert.That(LabComboHitSound.ClipFolder, Is.EqualTo("Audio/ComboHit"));
        }
    }
}
