using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Muted AudioSource lifecycle checks; not a listening test or device loudness approval.</summary>
    public sealed class LabJigwiGroupFireAudioTests
    {
        private GameObject root;
        private LabJigwiGroupFireAudio audio;
        private LabConfig config;
        [SetUp]
        public void Prepare()
        {
            root = new GameObject("Muted B fire audio fixture");
            audio = root.AddComponent<LabJigwiGroupFireAudio>();
            audio.EnsureSource(); audio.Source.mute = true;
            Focus(audio, true);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            yield return null;
        }
        private static void Focus(LabJigwiGroupFireAudio value, bool focused) => value.SendMessage("OnApplicationFocus", focused, SendMessageOptions.RequireReceiver);

        [Test]
        public void ImportedUserLoopIsOneLocalSourceWithTheExistingSfxVolumePath()
        {
            Assert.That(audio.clip, Is.SameAs(Resources.Load<AudioClip>(LabJigwiGroupFireAudio.ClipResource)));
            Assert.That(audio.clip, Is.Not.Null);
            Assert.That(audio.clip.length, Is.GreaterThan(1f)); Assert.That(audio.clip.channels, Is.EqualTo(2));
            Assert.That(root.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
            Assert.That(audio.Source.clip, Is.SameAs(audio.clip));
            Assert.That(audio.Source.playOnAwake, Is.False); Assert.That(audio.Source.loop, Is.True);
            Assert.That(audio.Source.spatialBlend, Is.Zero); Assert.That(audio.Source.dopplerLevel, Is.Zero);
            Assert.That(audio.Source.volume, Is.EqualTo(audio.volume * LabAudioSettings.SfxVolume).Within(.00001f));
            Assert.That(audio.WantsLoop, Is.False); Assert.That(audio.PlaybackStartCount, Is.Zero);
        }

        [Test]
        public void ConsecutiveSnapshotsAndThreeStagesKeepOneLoopWhilePauseResumesWithoutRestart()
        {
            audio.Apply("round/B", true, false, 3);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
            for (int i = 0; i < 30; i++) audio.Apply("round/B", true, false, 3 - i % 3);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1)); Assert.That(audio.WantsLoop, Is.True);
            audio.Apply("round/B", true, true, 1); audio.Apply("round/B", true, true, 1);
            Assert.That(audio.IsPaused, Is.True); Assert.That(audio.PauseCount, Is.EqualTo(1));
            audio.Apply("round/B", true, false, 1); audio.Apply("round/B", true, false, 1);
            Assert.That(audio.IsPaused, Is.False); Assert.That(audio.ResumeCount, Is.EqualTo(1));
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
            Assert.That(root.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void PauseBeforeFirstPresentationWaitsAndApplicationSuspensionHoldsTheSameLoop()
        {
            audio.Apply("late/B", true, true, 3);
            Assert.That(audio.WantsLoop, Is.True); Assert.That(audio.PlaybackStartCount, Is.Zero);
            audio.Apply("late/B", true, false, 3);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
            Focus(audio, false); Assert.That(audio.IsPaused, Is.True);
            Focus(audio, true); Assert.That(audio.ResumeCount, Is.EqualTo(1));
            audio.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
            Assert.That(audio.IsPaused, Is.True);
            audio.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            Assert.That(audio.IsPaused, Is.False); Assert.That(audio.ResumeCount, Is.EqualTo(2));
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
        }

        [Test]
        public void LocalClearAndEndStopEvenPausedWhileRelightOrAnotherEventStartsOnlyOnce()
        {
            audio.Apply("B", true, false, 3);
            audio.Apply("B", true, true, 1);
            audio.Apply("B", false, true, 0);
            Assert.That(audio.WantsLoop, Is.False); Assert.That(audio.FireEventId, Is.Null);
            Assert.That(audio.Source.isPlaying, Is.False);
            audio.Apply("B", true, false, 1); audio.Apply("B", true, false, 2);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(2), "Relight begins one new loop after local complete clear.");
            audio.Apply("later/B", true, false, 3);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(3));
            root.SetActive(false);
            Assert.That(audio.WantsLoop, Is.False); Assert.That(audio.Source.isPlaying, Is.False);
        }

        [Test]
        public void SavedPresentationPlaysOnlyOwnVisibleBAndStopsForAAllClearAndStageExit()
        {
            config = Object.Instantiate(Resources.Load<LabConfig>("LabConfig"));
            var passive = new GameObject("Passive B replica"); passive.transform.SetParent(root.transform, false);
            var network = passive.AddComponent<LabNetwork>(); network.enabled = false; network.config = config;
            var target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 71f, 0f), .8f, 2f); network.target = target;
            var screen = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)); screen.transform.SetParent(root.transform, false);
            var canvas = screen.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var eye = new GameObject("Battle camera"); eye.transform.SetParent(root.transform, false);
            var camera = eye.AddComponent<Camera>(); camera.enabled = false; camera.aspect = 390f / 844f; camera.fieldOfView = 52f;
            camera.transform.position = LabThrowMath.SeatPosition(0, 1, target.transform.position, config.CameraRadius, config.CameraHeightOffset);
            camera.transform.LookAt(target.transform.position + Vector3.up * config.CameraLookHeightOffset);
            var saved = Resources.Load<GameObject>(LabJigwiScreenPresentation.PrefabResource);
            Assert.That(saved, Is.Not.Null);
            var view = Object.Instantiate(saved, canvas.transform, false).GetComponent<LabJigwiScreenPresentation>();
            view.Bind(network, canvas, camera); view.enabled = false;
            view.audioView.Source.mute = true; Focus(view.audioView, true);
            var group = new LabJigwiABRound { id = "scene/B", kind = LabJigwiABKind.GroupFire,
                warningAt = 1d, activeAt = 2d, endsAt = 30d, targetRadius = .3f,
                players = new[] { new LabJigwiABPlayerFire { playerId = 42,
                    targets = Enumerable.Range(0, 3).Select(i => new LabJigwiABFireTarget { id = "point/" + i, lit = true, x = .3f + i * .2f, y = .6f }).ToArray() } } };
            var state = new LabSnapshot { sessionId = "audio-session", round = 1, phase = LabPhase.Playing, serverTime = 1.2d,
                players = new[] { new LabPlayerState { id = 42, seat = 0 } },
                jigwiAB = new LabJigwiABSnapshot { enabled = true, startedAt = 0d, cutoffAt = 100d, rounds = new[] { group } } };
            typeof(LabNetwork).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(network, state);
            Canvas.ForceUpdateCanvases(); view.ApplySnapshot(state, 42, 1.2d);
            Assert.That(view.GroupCurtainVisible, Is.True); Assert.That(view.audioView.WantsLoop, Is.True);
            Assert.That(view.audioView.PlaybackStartCount, Is.EqualTo(1), "Warning appearance starts one local loop.");
            state.serverTime = 2.2d; view.ApplySnapshot(state, 42, 2.2d);
            Assert.That(view.audioView.PlaybackStartCount, Is.EqualTo(1));
            foreach (var point in group.players[0].targets) point.lit = false;
            view.ApplySnapshot(state, 42, 2.3d);
            Assert.That(view.audioView.WantsLoop, Is.False);
            group.players[0].targets[1].lit = true; view.ApplySnapshot(state, 42, 2.4d);
            Assert.That(view.audioView.PlaybackStartCount, Is.EqualTo(2));
            view.ApplySnapshot(state, 99, 2.5d); Assert.That(view.audioView.WantsLoop, Is.False, "An absent local owner cannot play another player's fire.");
            group.kind = LabJigwiABKind.PersonalBurn; group.targetPlayer = 42;
            view.ApplySnapshot(state, 42, 2.6d); Assert.That(view.audioView.WantsLoop, Is.False);
            group.kind = LabJigwiABKind.GroupFire; view.ApplySnapshot(state, 42, 2.7d);
            Assert.That(view.audioView.WantsLoop, Is.True);
            state.phase = LabPhase.Victory; view.ApplySnapshot(state, 42, 2.8d);
            Assert.That(view.audioView.WantsLoop, Is.False);
        }
    }
}
