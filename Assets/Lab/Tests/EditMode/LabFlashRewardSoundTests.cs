using System.Linq;
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
        public void TheFiveColorHitClipsAreInResourcesAndTakeTurns()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>(LabFlashRewardSound.FiveColorHitFolder);
            Assert.That(clips.Select(clip => clip.name), Is.EquivalentTo(new[] { "FiveColorHit_1", "FiveColorHit_2" }));
            Assert.That(Resources.LoadAll<AudioClip>(LabFlashRewardSound.FlashClearFolder).Select(clip => clip.name),
                Is.EqualTo(new[] { "FlashClear" }));

            host = new GameObject("flash-reward-sound");
            LabFlashRewardSound sound = host.AddComponent<LabFlashRewardSound>();
            // 에디터 테스트에서는 Awake가 저절로 불리지 않을 수 있어 직접 불러 소리 파일을 읽힌다.
            if (sound.FiveColorHitClipCount == 0)
                typeof(LabFlashRewardSound).GetMethod("Awake", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).Invoke(sound, null);
            Assert.That(sound.FiveColorHitClipCount, Is.EqualTo(2));
            var turns = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 4; i++)
            {
                turns.Add(sound.NextFiveColorHitIndex);
                sound.OnFiveColorHit();
            }
            Assert.That(turns, Is.EqualTo(new[] { 0, 1, 0, 1 }), "The two hit sounds take turns.");
        }

        [Test]
        public void TheFlashClearSoundWaitsForTheFullShakeCount()
        {
            host = new GameObject("flash-reward-sound");
            LabFlashRewardSound sound = host.AddComponent<LabFlashRewardSound>();
            // 눈부심이 먼저 풀려도(예: 6번) 소리는 10번을 채운 순간 한 번만 난다.
            for (int count = 1; count < 10; count++)
                Assert.That(sound.ObserveShakes(count, 10), Is.False, count + " shakes");
            Assert.That(sound.ObserveShakes(10, 10), Is.True);
            Assert.That(sound.ObserveShakes(10, 10), Is.False, "Only once per flash.");
            // 다음 섬광: 수가 0으로 돌아가면 다시 센다.
            Assert.That(sound.ObserveShakes(0, 10), Is.False);
            Assert.That(sound.ObserveShakes(10, 10), Is.True);
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
