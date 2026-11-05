using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Muted local source lifecycle/clock tests. These do not approve word markers by ear or prove device latency.</summary>
    public sealed class LabJigwiPatternAudioTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root;
        private LabJigwiPatternAudio audio;
        private LabJigwiPatternAudioProfile profile;
        private AudioClip personal, group;
        private float oldSfx;
        private bool hadSfxKey;

        [SetUp]
        public void PrepareMutedVoice()
        {
            hadSfxKey = PlayerPrefs.HasKey("C6Lab.Audio.SfxVolume"); oldSfx = LabAudioSettings.SfxVolume;
            root = new GameObject("Jigwi spoken forewarning fixture");
            personal = AudioClip.Create("A voice fixture", 48000 * 2, 2, 48000, false);
            group = AudioClip.Create("B voice fixture", 48000 * 4, 2, 48000, false);
            profile = ScriptableObject.CreateInstance<LabJigwiPatternAudioProfile>();
            Set("personalClip", personal); Set("groupClip", group);
            audio = root.AddComponent<LabJigwiPatternAudio>();
            audio.enabled = false; // Only Observe advances this fixture's shared clock.
            audio.SetProfile(profile); audio.Source.mute = true;
            Focus(true);
        }
        [TearDown]
        public void Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (profile != null) Object.DestroyImmediate(profile);
            if (personal != null) Object.DestroyImmediate(personal);
            if (group != null) Object.DestroyImmediate(group);
            LabAudioSettings.SfxVolume = oldSfx;
            if (!hadSfxKey) { PlayerPrefs.DeleteKey("C6Lab.Audio.SfxVolume"); PlayerPrefs.Save(); }
        }
        [Test]
        public void ImportedProfileBindsTheTwoOriginalClipsAndMarkersAreInsideTheRecordings()
        {
            var saved = Resources.Load<LabJigwiPatternAudioProfile>(LabJigwiPatternAudioProfile.ResourcesPath);
            Assert.That(saved, Is.Not.Null);
            foreach (LabJigwiABKind kind in new[] { LabJigwiABKind.PersonalBurn, LabJigwiABKind.GroupFire })
            {
                AudioClip clip = saved.ClipFor(kind);
                Assert.That(clip, Is.SameAs(Resources.Load<AudioClip>(kind == LabJigwiABKind.PersonalBurn
                    ? LabJigwiPatternAudioProfile.PersonalClipResource : LabJigwiPatternAudioProfile.GroupClipResource)));
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.length, Is.EqualTo(kind == LabJigwiABKind.PersonalBurn ? 2f : 3.76f).Within(.02f));
                Assert.That(saved.CueSecondsFor(kind), Is.GreaterThan(0f).And.LessThan(clip.length));
            }
        }
        [Test]
        public void PersistentAttachKeepsOneDedicatedNonLoopingSourceAndPreservesTheFireLoop()
        {
            var network = root.AddComponent<LabNetwork>(); network.enabled = false;
            Assert.That(LabJigwiPatternAudio.Attach(network), Is.SameAs(audio));
            Assert.That(LabJigwiPatternAudio.Attach(network), Is.SameAs(audio));
            Assert.That(root.GetComponents<LabJigwiPatternAudio>(), Has.Length.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<AudioSource>(true), Has.Length.EqualTo(1));
            Assert.That(audio.Source.transform.parent, Is.SameAs(network.transform));
            Assert.That(audio.Source.playOnAwake, Is.False); Assert.That(audio.Source.loop, Is.False);
            Assert.That(audio.Source.spatialBlend, Is.Zero); Assert.That(audio.Source.pitch, Is.EqualTo(1f));
            var loopRoot = new GameObject("Existing B loop"); loopRoot.transform.SetParent(root.transform, false);
            var loop = loopRoot.AddComponent<LabJigwiGroupFireAudio>(); loop.EnsureSource(); loop.Source.mute = true;
            loop.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            loop.Apply("same-B", true, false, 3);
            audio.Observe(State(), Start(1) + .02d);
            Assert.That(loop.PlaybackStartCount, Is.EqualTo(1)); Assert.That(loop.WantsLoop, Is.True);
            Assert.That(loop.Source.loop, Is.True); Assert.That(loop.Source, Is.Not.SameAs(audio.Source));
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
        }
        [TestCase(0)] [TestCase(1)]
        public void ForewarningStartsBeforeTheOneSecondHostRoundExistsAndItsWordIsAtTheUnchangedActivation(int slot)
        {
            LabSnapshot state = State(); double begins = Start(slot), activation = LabJigwiABTimeline.ActiveOffset(slot);
            Assert.That(state.jigwiAB.rounds, Is.Empty);
            Assert.That(begins, Is.LessThan(activation - LabJigwiABTimeline.WarningSeconds));
            audio.Observe(state, begins - .01d); Assert.That(audio.PlaybackStartCount, Is.Zero);
            audio.Observe(state, begins + .02d);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1)); Assert.That(audio.CurrentSlot, Is.EqualTo(slot));
            Assert.That(audio.Source.clip, Is.SameAs(profile.ClipFor(LabJigwiABTimeline.KindForSlot(slot))));
            Assert.That(audio.CurrentVoiceStartAt, Is.EqualTo(begins).Within(.00001d));
            Assert.That(audio.CurrentWordAt, Is.EqualTo(activation));
            Assert.That(audio.Source.time, Is.EqualTo(.02f).Within(.002f));
        }
        [Test]
        public void TheSixFixedSlotsPlayOnceEachWithoutAnUnboundedEventHistory()
        {
            var state = State();
            for (int slot = 0; slot < LabJigwiABTimeline.SlotCount; slot++)
            {
                double now = Start(slot) + .02d;
                audio.Observe(state, now);
                for (int copy = 0; copy < 20; copy++) audio.Observe(state, now);
                Assert.That(audio.PlaybackStartCount, Is.EqualTo(slot + 1));
            }
            audio.Observe(state, 99d);
            Assert.That(audio.ConsumedSlotCount, Is.EqualTo(6));
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(6)); Assert.That(audio.CurrentSlot, Is.EqualTo(-1));
        }
        [Test]
        public void LateObservationBeforeTheWordSeeksTheSharedCursorInsteadOfRestartingThePhrase()
        {
            double now = Start(0) + .6d;
            audio.Observe(State(), now);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
            Assert.That(audio.Source.time, Is.EqualTo(.6f).Within(.002f));
            Assert.That(audio.CurrentWordAt, Is.EqualTo(5d));
        }
        [Test]
        public void ABackwardSnapshotCorrectionCannotRewindAndReplayTheCurrentWord()
        {
            var state = State();
            audio.Observe(state, Start(0) + .8d);
            audio.Observe(state, 5.1d);
            audio.Observe(state, 5.08d);
            Assert.That(audio.CurrentSlot, Is.EqualTo(0), "Small clock jitter remains within the existing seek tolerance.");
            audio.Observe(state, 4.9d);
            Assert.That(audio.CurrentSlot, Is.EqualTo(-1));
            Assert.That(audio.Source.clip, Is.Null);
            audio.Observe(state, 4.99d); audio.Observe(state, 5.2d);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
            Assert.That(audio.ConsumedSlotCount, Is.EqualTo(1));
        }
        [Test]
        public void AFirstObservationPastTheWordConsumesItWithoutPlayingAnOldWarning()
        {
            audio.Observe(State(), 5.01d);
            Assert.That(audio.PlaybackStartCount, Is.Zero); Assert.That(audio.ConsumedSlotCount, Is.EqualTo(1));
            audio.Observe(State(), 5.02d);
            Assert.That(audio.PlaybackStartCount, Is.Zero);
        }
        [Test]
        public void SameScopeRebindAndDisableDoNotReplayAnAlreadySpokenPhrase()
        {
            var network = root.AddComponent<LabNetwork>(); network.enabled = false;
            audio.Bind(network);
            audio.Observe(State(), Start(0) + .02d);
            audio.Bind(network); audio.Observe(State(), Start(0) + .03d);
            audio.SendMessage("OnDisable", SendMessageOptions.RequireReceiver);
            audio.SendMessage("OnEnable", SendMessageOptions.RequireReceiver); Focus(true);
            audio.Observe(State(), Start(0) + .04d);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
            Assert.That(audio.CurrentSlot, Is.EqualTo(-1));
        }
        [Test]
        public void PauseBeforeAWindowDoesNotQueueButTheHeldBattleClockKeepsTheCurrentWindowEligible()
        {
            double now = Start(0) + .02d;
            audio.Observe(State(), now, true); audio.Observe(State(), now, true);
            Assert.That(audio.PlaybackStartCount, Is.Zero);
            audio.Observe(State(), now, false);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
        }
        [Test]
        public void ARealBattlePauseFreezesTheCursorAndResumesTheSameNativeVoice()
        {
            var state = State(); double now = Start(0) + .3d;
            audio.Observe(state, now);
            audio.Observe(state, now, true);
            float held = audio.Source.time;
            for (int i = 0; i < 20; i++) audio.Observe(state, now, true);
            Assert.That(audio.IsPaused, Is.True); Assert.That(audio.PauseCount, Is.EqualTo(1));
            Assert.That(audio.Source.time, Is.EqualTo(held).Within(.002f));
            audio.Observe(state, now, false);
            Assert.That(audio.ResumeCount, Is.EqualTo(1)); Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
        }
        [TestCase("OnApplicationFocus", false, true)] [TestCase("OnApplicationPause", true, false)]
        public void ApplicationSuspensionPausesImmediatelyAndReconcilesTheClockBeforeTheWord(string message, bool suspend, bool resume)
        {
            var state = State(); double begins = Start(0);
            audio.Observe(state, begins + .1d);
            audio.SendMessage(message, suspend, SendMessageOptions.RequireReceiver);
            Assert.That(audio.IsPaused, Is.True); Assert.That(audio.PauseCount, Is.EqualTo(1));
            audio.Observe(state, begins + .4d);
            audio.SendMessage(message, resume, SendMessageOptions.RequireReceiver);
            audio.Observe(state, begins + .6d);
            Assert.That(audio.ResumeCount, Is.EqualTo(1)); Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
            Assert.That(audio.Source.time, Is.EqualTo(.6f).Within(.002f));
        }
        [Test]
        public void FocusReturningAfterActivationStopsTheMissedWordInsteadOfResumingItLate()
        {
            var state = State(); audio.Observe(state, Start(0) + .1d);
            Focus(false); audio.Observe(state, 5.2d);
            Focus(true); audio.Observe(state, 5.3d);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1)); Assert.That(audio.ResumeCount, Is.Zero);
            Assert.That(audio.CurrentSlot, Is.EqualTo(-1)); Assert.That(audio.Source.clip, Is.Null);
        }
        [Test]
        public void AWindowMissedEntirelyWhileUnfocusedDoesNotWaitInAQueue()
        {
            Focus(false); audio.Observe(State(), Start(0) + .1d);
            Focus(true); audio.Observe(State(), 5.1d);
            Assert.That(audio.PlaybackStartCount, Is.Zero);
            Assert.That(audio.ConsumedSlotCount, Is.EqualTo(1));
        }
        [TestCase("round")] [TestCase("session")] [TestCase("stage")]
        public void OnlyANewScopeRearmsTheFirstCue(string change)
        {
            var first = State(); audio.Observe(first, Start(0) + .02d);
            var next = State();
            if (change == "round") next.round++;
            else if (change == "session") next.sessionId = "another-session";
            else { next.jigwiAB.startedAt = 10d; next.jigwiAB.cutoffAt = 110d; }
            audio.Observe(next, next.jigwiAB.startedAt + Start(0) + .02d);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(2)); Assert.That(audio.ConsumedSlotCount, Is.EqualTo(1));
        }
        [Test]
        public void BattleEndAndResolvedPatternStopTheVoiceWithoutClearingItsReplayFence()
        {
            var state = State(); double now = Start(0) + .1d;
            audio.Observe(state, now);
            state.jigwiAB.rounds = new[] { new LabJigwiABRound { slot = 0, resolution = LabJigwiABResolution.Cancelled } };
            audio.Observe(state, now + .1d); Assert.That(audio.CurrentSlot, Is.EqualTo(-1));
            state.jigwiAB.rounds = Array.Empty<LabJigwiABRound>(); audio.Observe(state, now + .2d);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
            state.round++; audio.Observe(state, now); Assert.That(audio.PlaybackStartCount, Is.EqualTo(2));
            state.phase = LabPhase.Victory; audio.Observe(state, now + .1d);
            Assert.That(audio.CurrentSlot, Is.EqualTo(-1)); Assert.That(audio.Source.clip, Is.Null);
        }
        [Test]
        public void AMissingClipIsConsumedOnceAndDoesNotPlayWhenAssignedLate()
        {
            Set("personalClip", null);
            LogAssert.Expect(LogType.Warning, "Jigwi pattern voice clip is missing: PersonalBurn");
            double now = 5d - profile.CueSecondsFor(LabJigwiABKind.PersonalBurn) + .02d;
            audio.Observe(State(), now); audio.Observe(State(), now + .01d);
            Assert.That(audio.PlaybackStartCount, Is.Zero); Assert.That(audio.MissingClipCount, Is.EqualTo(1));
            Set("personalClip", personal); audio.Observe(State(), now + .02d);
            Assert.That(audio.PlaybackStartCount, Is.Zero);
        }
        [Test]
        public void ADisabledPersonalShoutIsSkippedButBStillSpeaks()
        {
            // #479: the saved profile turns the A shout off; B's warning keeps its cue.
            Set("personalVoiceEnabled", false);
            audio.Observe(State(), Start(0) + .02d); audio.Observe(State(), Start(0) + .5d);
            Assert.That(audio.PlaybackStartCount, Is.Zero);
            audio.Observe(State(), Start(1) + .02d);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
            var saved = Resources.Load<LabJigwiPatternAudioProfile>(LabJigwiPatternAudioProfile.ResourcesPath);
            Assert.That(saved.VoiceEnabledFor(LabJigwiABKind.PersonalBurn), Is.False);
            Assert.That(saved.VoiceEnabledFor(LabJigwiABKind.GroupFire), Is.True);
        }
        [Test]
        public void MutedSfxDoesNotDeferTheCueUntilAfterItIsUnmuted()
        {
            LabAudioSettings.SfxVolume = 0f;
            audio.Observe(State(), Start(0) + .02d);
            Assert.That(audio.Source.volume, Is.Zero); Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
            audio.Observe(State(), 6d); LabAudioSettings.SfxVolume = 1f;
            audio.Observe(State(), 6.1d);
            Assert.That(audio.PlaybackStartCount, Is.EqualTo(1)); Assert.That(audio.CurrentSlot, Is.EqualTo(-1));
        }
        [Test]
        public void OrdinaryStagesInvalidClockAndCutoffNeverCreateOrReplayJigwiSpeech()
        {
            var ordinary = State(); ordinary.jigwiAB.enabled = false;
            audio.Observe(ordinary, Start(0) + .02d);
            audio.Observe(State(), double.NaN); audio.Observe(State(), double.PositiveInfinity);
            Assert.That(audio.PlaybackStartCount, Is.Zero);
            audio.Observe(State(), Start(0) + .02d); audio.Observe(State(), 100d);
            Assert.That(audio.CurrentSlot, Is.EqualTo(-1)); Assert.That(audio.PlaybackStartCount, Is.EqualTo(1));
        }
        private double Start(int slot) => LabJigwiABTimeline.ActiveOffset(slot) - profile.CueSecondsFor(LabJigwiABTimeline.KindForSlot(slot));
        private void Set(string field, object value) => typeof(LabJigwiPatternAudioProfile).GetField(field, Private).SetValue(profile, value);
        private void Focus(bool focused) => audio.SendMessage("OnApplicationFocus", focused, SendMessageOptions.RequireReceiver);
        private static LabSnapshot State() => new LabSnapshot
        {
            sessionId = "spoken-forewarning", round = 1, phase = LabPhase.Playing,
            jigwiAB = new LabJigwiABSnapshot { enabled = true, startedAt = 0d, cutoffAt = 100d,
                rounds = Array.Empty<LabJigwiABRound>() }
        };
    }
}
