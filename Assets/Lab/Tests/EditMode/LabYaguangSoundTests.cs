using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>
    /// 야광귀 섬광 예고와 번쩍 효과음 (#395): 예고 시작은 모든 기기, 눈 빛남/3·2·1/번쩍은 맞는 사람 기기에서 한 번씩.
    /// 늦게 알아챈 순간과 일시정지 중에는 내지 않는다. 소리 파일이 없어도 알아챈 횟수는 센다.
    /// </summary>
    public sealed class LabYaguangSoundTests
    {
        private const ulong Me = 7ul, Other = 9ul;
        private readonly System.Collections.Generic.List<GameObject> hosts = new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject host in hosts) if (host != null) Object.DestroyImmediate(host);
            hosts.Clear();
        }

        private LabYaguangSound Sound()
        {
            var host = new GameObject("yaguang-sound");
            hosts.Add(host);
            return host.AddComponent<LabYaguangSound>();
        }

        // 10초에 예고가 시작해 13초에 번쩍이는 섬광 (예고 3초). 눈 빛은 30% 지점인 10.9초부터.
        private static LabSnapshot Flash(ulong target)
        {
            return new LabSnapshot
            {
                sessionId = "yaguang", round = 1, phase = LabPhase.Playing,
                flashWarningAt = 10d, flashAt = 13d, flashTarget = target, flashTargetAll = false
            };
        }

        [Test]
        public void TheTargetHearsTheWholeWarningOnce()
        {
            LabYaguangSound sound = Sound();
            LabSnapshot snapshot = Flash(Me);
            sound.ObserveFlash(snapshot, 9.5d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashWarn), Is.Zero, "Nothing before the warning.");

            sound.ObserveFlash(snapshot, 10d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashWarn), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCount), Is.EqualTo(1), "3");
            Assert.That(sound.LastCount, Is.EqualTo(3));
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashGlow), Is.Zero, "The eyes are not glowing yet.");

            sound.ObserveFlash(snapshot, 10.95d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashGlow), Is.EqualTo(1));
            sound.ObserveFlash(snapshot, 11d, Me);
            sound.ObserveFlash(snapshot, 11.5d, Me);
            sound.ObserveFlash(snapshot, 12d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCount), Is.EqualTo(3), "3, 2, 1 once each.");
            Assert.That(sound.LastCount, Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashWarn), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashGlow), Is.EqualTo(1));

            // 13초에 번쩍: 섬광 수가 늘고 내가 맞았다.
            snapshot.flashCount = 1; snapshot.flashLastAt = 13d; snapshot.flashLastTarget = Me;
            snapshot.flashWarningAt = snapshot.flashAt = 73d;
            sound.ObserveFlash(snapshot, 13.1d, Me);
            sound.ObserveFlash(snapshot, 13.5d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashBurst), Is.EqualTo(1), "Once per flash.");
        }

        [Test]
        public void OthersHearOnlyTheWarningStart()
        {
            LabYaguangSound sound = Sound();
            LabSnapshot snapshot = Flash(Other);
            for (double now = 10d; now < 13d; now += .25d) sound.ObserveFlash(snapshot, now, Me);
            snapshot.flashCount = 1; snapshot.flashLastAt = 13d; snapshot.flashLastTarget = Other;
            sound.ObserveFlash(snapshot, 13.1d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashWarn), Is.EqualTo(1), "Everyone sees the eyes.");
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashGlow), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCount), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashBurst), Is.Zero);
        }

        [Test]
        public void LateOrPausedMomentsStayQuiet()
        {
            LabYaguangSound sound = Sound();
            LabSnapshot snapshot = Flash(Me);
            // 11.6초에 처음 봄: 예고 시작(10초), 눈 빛(10.9초), 숫자 2(11초)는 모두 0.5초 넘게 지났다.
            sound.ObserveFlash(snapshot, 11.6d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashWarn), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashGlow), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCount), Is.Zero);
            sound.ObserveFlash(snapshot, 12d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCount), Is.EqualTo(1), "The next number is on time.");

            // 일시정지 중에는 새 숫자를 내지 않는다.
            LabYaguangSound paused = Sound();
            LabSnapshot hold = Flash(Me);
            hold.paused = true;
            paused.ObserveFlash(hold, 10d, Me);
            Assert.That(paused.PlayCount(LabYaguangSfx.FlashWarn), Is.Zero);
            Assert.That(paused.PlayCount(LabYaguangSfx.FlashCount), Is.Zero);

            // 들어오자마자 이미 지난 섬광은 번쩍 소리를 내지 않는다.
            LabYaguangSound joined = Sound();
            LabSnapshot after = Flash(Me);
            after.flashCount = 1; after.flashLastAt = 13d; after.flashLastTarget = Me;
            joined.ObserveFlash(after, 13.1d, Me);
            Assert.That(joined.PlayCount(LabYaguangSfx.FlashBurst), Is.Zero);
        }

        [Test]
        public void TheCountGoesUpInPitchAndTheFoldersAreUnderResourcesAudio()
        {
            Assert.That(LabYaguangSound.CountPitch(3, 2f), Is.EqualTo(1f).Within(.0001f));
            Assert.That(LabYaguangSound.CountPitch(2, 2f), Is.EqualTo(Mathf.Pow(2f, 2f / 12f)).Within(.0001f));
            Assert.That(LabYaguangSound.CountPitch(1, 2f), Is.EqualTo(Mathf.Pow(2f, 4f / 12f)).Within(.0001f));
            Assert.That(LabYaguangSound.CountPitch(5, 2f), Is.EqualTo(1f).Within(.0001f), "A longer warning starts at the base pitch.");
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.FlashWarn), Is.EqualTo("Audio/YaguangFlashWarn"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.FlashGlow), Is.EqualTo("Audio/YaguangFlashGlow"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.FlashCount), Is.EqualTo("Audio/YaguangFlashCount"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.FlashBurst), Is.EqualTo("Audio/YaguangFlashBurst"));
        }
    }
}
