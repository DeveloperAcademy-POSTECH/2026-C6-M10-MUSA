using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hapio.Tests
{
    /// <summary>
    /// #757 엔딩 컷 효과음과 테이프 멈춤. 리뷰 1번: 테이프 멈춤 도중 컷을 넘기거나 되돌아가면 다음 곡을 멈추지 않고
    /// 빠르기를 되돌려야 한다.
    /// </summary>
    public sealed class StoryCutSoundTests
    {
        private readonly List<Object> cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.Destroy(cleanup[i]);
            cleanup.Clear();
        }

        private AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 44100 * 8, 1, 44100, false);
            cleanup.Add(clip);
            return clip;
        }

        private StoryCutView View(AudioClip music, bool tapeStop, AudioClip sound = null, float delay = 0f)
        {
            var go = new GameObject("Cut", typeof(RectTransform), typeof(StoryCutView));
            var layerObject = new GameObject("Layer", typeof(RectTransform), typeof(StoryLayer));
            layerObject.transform.SetParent(go.transform, false);
            var layer = layerObject.GetComponent<StoryLayer>();
            layer.enterSeconds = .04f;
            var view = go.GetComponent<StoryCutView>();
            view.layers = new[] { layer };
            view.music = music; view.musicVolume = .5f;
            if (tapeStop || sound != null)
            {
                var extra = go.AddComponent<StoryCutSound>();
                extra.entries.Add(new StoryCutSound.Entry { clip = sound, delay = delay, volume = .5f, stopMusic = tapeStop, stopSeconds = 1f });
            }
            cleanup.Add(go);
            return view;
        }

        private (StoryPlayer, StoryCutSoundDriver) Player(params StoryCutView[] views)
        {
            var root = new GameObject("Player", typeof(RectTransform));
            root.SetActive(false);
            var player = root.AddComponent<StoryPlayer>();
            player.playOnStart = false;
            player.cutHost = root.GetComponent<RectTransform>();
            var sequence = ScriptableObject.CreateInstance<StorySequence>();
            for (int i = 0; i < views.Length; i++)
            {
                var cut = ScriptableObject.CreateInstance<StoryCutDefinition>();
                cut.cutId = "cut-" + i; cut.viewPrefab = views[i];
                cut.autoReadSeconds = 30f;
                cut.pages.Add(new StoryDialoguePage { dialogue = "page" });
                sequence.cuts.Add(cut); cleanup.Add(cut);
            }
            player.sequence = sequence;
            cleanup.Add(root); cleanup.Add(sequence);
            root.SetActive(true);
            var driver = StoryCutSoundDriver.Ensure(player);
            Assert.That(player.Play(sequence), Is.True);
            return (player, driver);
        }

        private static AudioSource Music(StoryPlayer player)
        {
            Transform music = player.transform.Find("Story Music");
            return music != null ? music.GetComponent<AudioSource>() : null;
        }

        private static IEnumerator Seconds(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }

        private static void GoTo(StoryPlayer player, int index)
        {
            for (int guard = 0; guard < 10 && player.CurrentCutIndex < index; guard++) player.Next();
            Assert.That(player.CurrentCutIndex, Is.EqualTo(index));
        }

        [UnityTest]
        public IEnumerator TheTapeStopSlowsAndStopsTheSongWhenTheCutStays()
        {
            AudioClip a = Clip("A");
            var (player, driver) = Player(View(a, true));
            yield return Seconds(.3f);
            AudioSource music = Music(player);
            Assert.That(music, Is.Not.Null);
            Assert.That(driver.TapeStopping, Is.True);
            Assert.That(music.pitch, Is.LessThan(1f), "It slows down like a stopping tape.");
            yield return Seconds(1.1f);
            Assert.That(driver.TapeStopping, Is.False);
            Assert.That(music.isPlaying, Is.False);
            Assert.That(music.pitch, Is.EqualTo(1f).Within(1e-4f), "The source is left at normal speed for the next song.");
        }

        [UnityTest]
        public IEnumerator MovingOnDuringTheTapeStopKeepsTheNextSongPlaying()
        {
            AudioClip a = Clip("A"), b = Clip("B");
            var (player, driver) = Player(View(a, true), View(b, false));
            yield return Seconds(.4f);
            Assert.That(driver.TapeStopping, Is.True);
            GoTo(player, 1);
            yield return Seconds(1.3f);
            AudioSource music = Music(player);
            Assert.That(music.clip, Is.SameAs(b));
            Assert.That(music.isPlaying, Is.True, "The next cut's song is not stopped by the old tape stop.");
            Assert.That(music.pitch, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(driver.TapeStopping, Is.False);
        }

        [UnityTest]
        public IEnumerator GoingBackDuringTheTapeStopKeepsTheSameSongPlaying()
        {
            AudioClip a = Clip("A");
            var (player, driver) = Player(View(a, false), View(a, true));
            GoTo(player, 1);
            yield return Seconds(.4f);
            Assert.That(driver.TapeStopping, Is.True);
            player.Previous();
            Assert.That(player.CurrentCutIndex, Is.Zero);
            yield return Seconds(1.3f);
            AudioSource music = Music(player);
            Assert.That(music.clip, Is.SameAs(a));
            Assert.That(music.isPlaying, Is.True, "Back on the same song: it keeps playing at normal speed.");
            Assert.That(music.pitch, Is.EqualTo(1f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator ACutSoundPlaysOnceAtItsTime()
        {
            var (player, driver) = Player(View(null, false, Clip("Sound"), .3f));
            yield return Seconds(.15f);
            Assert.That(driver.PlayedCount, Is.Zero);
            yield return Seconds(.35f);
            Assert.That(driver.PlayedCount, Is.EqualTo(1));
            yield return Seconds(.3f);
            Assert.That(driver.PlayedCount, Is.EqualTo(1));
        }
    }
}
