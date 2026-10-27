using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Actual Host generation, including paths which never create a raw entity (#324).</summary>
    public sealed class LabRawStreakTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private LabConfig config;
        private int request;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[5] = .001f;
            values[6] = 0f;
            values[9] = 100f;
            values[10] = .1f;
            values[74] = 5f;
            values[155] = values[184] = values[196] = 0f;
            values[205] = values[206] = 0f;
            Apply(values);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(config);

        [Test]
        public void DefaultAcceptedRawGenerationsNeverExceedThreeEvenAfterOldEntitiesExpire()
        {
            Assert.That(config.RawStreakCorrectionEnabled, Is.True);
            Assert.That(config.RawStreakOppositeChancePercent, Is.EqualTo(75));
            Assert.That(config.RawStreakMaximum, Is.EqualTo(3));
            LabModel model = Solo("raw-default");
            LabOrbKind previous = LabOrbKind.Combined;
            int run = 0, softOpposites = 0, softRepeats = 0;
            var seen = new HashSet<LabOrbKind>();
            for (int i = 0; i < 800; i++)
            {
                LabOrbState orb = Generate(model, 10, i * .11d);
                seen.Add(orb.kind);
                if (run == 2)
                {
                    if (orb.kind == previous) softRepeats++;
                    else softOpposites++;
                }
                run = orb.kind == previous ? run + 1 : 1;
                previous = orb.kind;
                Assert.That(run, Is.LessThanOrEqualTo(3), "accepted generation " + i);
                AssertHistory(model, 10, previous, run);
                Assert.That(model.CopyOwnedStates(10).Count(state => state.kind.IsRaw()), Is.EqualTo(1),
                    "Entity expiry must not reset generation history.");
            }
            Assert.That(seen, Is.EquivalentTo(new[] { LabOrbKind.Yin, LabOrbKind.Yang }));
            Assert.That(softOpposites, Is.GreaterThan(softRepeats));
            Assert.That(softRepeats, Is.GreaterThan(0), "75% is a soft correction before the hard maximum.");
        }

        [TestCase(LabOrbKind.Yin)]
        [TestCase(LabOrbKind.Yang)]
        public void EveryPercentileRollGivesExactlySeventyFiveOppositesAfterTwoRaw(LabOrbKind previous)
        {
            SetCorrection(true, 75, 3);
            LabModel model = Solo("raw-percentiles");
            MethodInfo record = typeof(LabModel).GetMethod("RecordRawGeneration", Private);
            record.Invoke(model, new object[] { 10ul, previous });
            record.Invoke(model, new object[] { 10ul, previous });
            string before = History(model);
            uint seed = (uint)typeof(LabModel).GetField("seed", Private).GetValue(model);
            uint salt = (uint)typeof(LabModel).GetField("RawStreakDrawSalt",
                BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
            MethodInfo draw = typeof(LabModel).GetMethod("DrawFor", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo correct = typeof(LabModel).GetMethod("CorrectRawGenerationKind", Private);
            var representative = new Dictionary<int, uint>();
            for (uint index = 0; index < 10000 && representative.Count < 100; index++)
            {
                int roll = (int)((ulong)draw.Invoke(null, new object[] { seed ^ salt, 10ul, index }) % 100ul);
                if (!representative.ContainsKey(roll)) representative.Add(roll, index);
            }
            Assert.That(representative.Count, Is.EqualTo(100));
            foreach (LabOrbKind weighted in new[] { LabOrbKind.Yin, LabOrbKind.Yang })
            {
                int opposites = 0;
                foreach (var percentile in representative)
                {
                    LabOrbKind actual = (LabOrbKind)correct.Invoke(model,
                        new object[] { weighted, 10ul, percentile.Value });
                    bool opposite = actual != previous;
                    Assert.That(opposite, Is.EqualTo(percentile.Key < 75), "roll=" + percentile.Key + ", weighted=" + weighted);
                    if (opposite) opposites++;
                }
                Assert.That(opposites, Is.EqualTo(75),
                    "A failed soft roll must retain the previous raw, including a weighted opposite draw.");
            }
            Assert.That(History(model), Is.EqualTo(before), "Choosing a candidate cannot itself record generation.");
        }

        [TestCase(1, 0)]
        [TestCase(1, 100)]
        [TestCase(2, 0)]
        [TestCase(2, 100)]
        [TestCase(3, 0)]
        [TestCase(3, 100)]
        public void ActualGenerationHonorsMaximumBeforeSoftChance(int maximum, int chance)
        {
            SetCorrection(true, chance, maximum);
            LabModel model = Solo("raw-extremes");
            LabOrbKind previous = LabOrbKind.Combined;
            int run = 0, reachedTwo = 0;
            for (int i = 0; i < 240; i++)
            {
                int priorRun = run;
                LabOrbKind prior = previous;
                LabOrbState orb = Generate(model, 10, i * .11d);
                if (priorRun >= maximum || priorRun >= 2 && chance == 100)
                    Assert.That(orb.kind, Is.Not.EqualTo(prior), "The next raw must be the opposite at " + i);
                else if (priorRun >= 2 && chance == 0)
                    Assert.That(orb.kind, Is.EqualTo(prior), "A failed soft roll retains the previous polarity.");
                run = orb.kind == previous ? run + 1 : 1;
                previous = orb.kind;
                if (run == 2) reachedTwo++;
                Assert.That(run, Is.LessThanOrEqualTo(maximum));
            }
            if (maximum > 1) Assert.That(reachedTwo, Is.GreaterThan(0));
        }

        [TestCase(LabOrbKind.Yin)]
        [TestCase(LabOrbKind.Yang)]
        public void AZeroWeightPolarityIsNeverRevivedEvenAtMaximumOne(LabOrbKind only)
        {
            SetCorrection(true, 100, 1);
            float[] values = config.CaptureDeveloperValues();
            values[39] = only == LabOrbKind.Yin ? 100f : 0f;
            values[40] = only == LabOrbKind.Yang ? 100f : 0f;
            Apply(values);
            LabModel model = Solo("raw-single-polarity");
            for (int i = 0; i < 32; i++)
            {
                Assert.That(Generate(model, 10, i * .11d).kind, Is.EqualTo(only));
                AssertHistory(model, 10, only, 1);
            }
        }

        [Test]
        public void ItemsKeepTheirWeightedKindLockAndLaunchAndDoNotBreakTheRawHistory()
        {
            SetCorrection(true, 100, 3);
            SetChances(40, 40, 5, 5, 5, 5);
            LabModel corrected = Solo("raw-items");
            SetCorrection(false, 100, 3);
            LabModel weighted = Solo("raw-items");
            var items = new HashSet<LabOrbKind>();
            for (int i = 0; i < 240; i++)
            {
                string before = History(corrected);
                LabOrbState expected = Generate(weighted, 10, i * .11d);
                LabOrbState actual = Generate(corrected, 10, i * .11d);
                Assert.That(actual.kind.IsItem(), Is.EqualTo(expected.kind.IsItem()), "draw " + i);
                Assert.That(new Vector2(actual.vx, actual.vy), Is.EqualTo(new Vector2(expected.vx, expected.vy)),
                    "Correction must not consume the launch draw.");
                if (!expected.kind.IsItem()) continue;
                items.Add(actual.kind);
                Assert.That(actual.kind, Is.EqualTo(expected.kind), "weighted item draw " + i);
                Assert.That(actual.lockSwipes, Is.EqualTo(expected.lockSwipes));
                Assert.That(actual.relayStage, Is.EqualTo(expected.relayStage));
                Assert.That(History(corrected), Is.EqualTo(before), "An item neither advances nor clears the raw run.");
            }
            Assert.That(items, Is.EquivalentTo(new[] { LabOrbKind.Relay, LabOrbKind.SecretBox,
                LabOrbKind.Potion, LabOrbKind.Bomb }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisabledCorrectionRetainsThePre324SeededSequenceExactly(bool items)
        {
            // Golden weighted draws/locks from the pre-#324 counter draw, seed=room/round1, player10.
            const string rawKinds = "yyyyyyYYyYYYYYyYYYYyYyYYyyyyyyYyyYyyYYYYyyYYyyyYyyYyyYYyYyyyYYYy";
            const string mixedKinds = "yyyyyRYYyYYYyyRyyYYyyBYyySPyyyyBRySyYYYYSyYyRRyyyyYyyYyPYPySYYYy";
            const string mixedLocks = "0000020000000040000004000320000330400000300033000000000203020000";
            SetCorrection(false, 100, 1);
            if (items) SetChances(40, 40, 5, 5, 5, 5);
            LabModel legacy = Solo("raw-streak-legacy-fixture");
            SetCorrection(false, 0, 10);
            LabModel otherDisabledProfile = Solo("raw-streak-legacy-fixture");
            string kinds = items ? mixedKinds : rawKinds;
            for (int i = 0; i < kinds.Length; i++)
            {
                LabOrbState first = Generate(legacy, 10, i * .11d);
                LabOrbState second = Generate(otherDisabledProfile, 10, i * .11d);
                Assert.That(first.kind, Is.EqualTo(Kind(kinds[i])), "legacy generation " + i);
                Assert.That(first.lockSwipes, Is.EqualTo(items ? mixedLocks[i] - '0' : 0));
                Assert.That(second.kind, Is.EqualTo(first.kind));
                Assert.That(second.lockSwipes, Is.EqualTo(first.lockSwipes));
                Assert.That(new Vector2(second.vx, second.vy), Is.EqualTo(new Vector2(first.vx, first.vy)));
            }
        }

        [Test]
        public void EachPlayerHasTheirOwnSequenceDespiteInterleavedGenerationAndDrumHandoffs()
        {
            SetCorrection(true, 100, 1);
            var interleaved = new LabModel();
            interleaved.Begin("raw-owners", new ulong[] { 10, 20 }, 0d, config);
            var control = new LabModel();
            control.Begin("raw-owners", new ulong[] { 10, 20 }, 0d, config);
            var expected = new Dictionary<ulong, LabOrbKind[]>();
            foreach (ulong owner in new ulong[] { 10, 20 })
                expected[owner] = Enumerable.Range(0, 40).Select(_ => Generate(control, owner, 0d).kind).ToArray();
            for (int i = 0; i < 40; i++)
                foreach (ulong owner in new ulong[] { 20, 10 })
                    Assert.That(Generate(interleaved, owner, 0d).kind, Is.EqualTo(expected[owner][i]));
            string before = History(interleaved);
            LabOrbState drum = interleaved.CopyOwnedStates(10).First(state => state.kind.IsDrum());
            Assert.That(interleaved.TryTransferAt(10, drum.id, false, new Vector2(.01f, .5f), Vector2.left,
                "move-drum", 0d, LabEdgeCrossingKind.Swipe, out _, out string reason), Is.True, reason);
            Assert.That(History(interleaved), Is.EqualTo(before), "The drum does not carry the previous owner's history.");
            LabOrbState expectedNext = Generate(control, 20, 0d);
            Assert.That(interleaved.TryGenerate(20, drum.id, new Vector2(.5f, .5f), "new-drum-tap", 0d,
                out var receivedDrumOutput, out reason), Is.True, reason);
            Assert.That(receivedDrumOutput.kind, Is.EqualTo(expectedNext.kind));
        }

        [Test]
        public void MultipleDrumsOnOneBoardShareTheOwnersRawRun()
        {
            SetCorrection(true, 0, 1);
            LabModel model = Solo("raw-many-drums");
            LabOrbState[] drums = model.CopyOwnedStates(10).Where(state => state.kind.IsDrum()).ToArray();
            Assert.That(drums, Has.Length.EqualTo(5));
            LabOrbKind previous = LabOrbKind.Combined;
            for (int i = 0; i < 32; i++)
            {
                Assert.That(model.TryGenerate(10, drums[i % drums.Length].id, new Vector2(.5f, .5f),
                    "many-" + i, i * .11d, out var orb, out string reason), Is.True, reason);
                Assert.That(orb.kind, Is.Not.EqualTo(previous), "Every tap shares the same owner's maximum-one history.");
                AssertHistory(model, 10, orb.kind, 1);
                previous = orb.kind;
            }
        }

        [Test]
        public void InvalidAndDuplicateAttemptsLeaveHistoryAndGenerationIndexUnchanged()
        {
            SetCorrection(true, 100, 1);
            LabModel model = Solo("raw-invalid");
            LabOrbState first = Generate(model, 10, 0d, "first");
            string before = History(model);
            foreach (var attempt in new[]
            {
                (sender: 10ul, drum: (string)null, position: (Vector2?)null, id: "first", time: 0d, error: "DUPLICATE_REQUEST"),
                (sender: 99ul, drum: (string)null, position: (Vector2?)null, id: "unknown", time: 0d, error: "UNKNOWN_PLAYER"),
                (sender: 10ul, drum: "missing", position: (Vector2?)null, id: "no-drum", time: 0d, error: "NO_DRUM"),
                (sender: 10ul, drum: (string)null, position: (Vector2?)new Vector2(-1f, .5f), id: "bad-point", time: 0d, error: "INVALID_MOTION"),
                (sender: 10ul, drum: (string)null, position: (Vector2?)null, id: "bad-time", time: -1d, error: "INVALID_TIME")
            })
            {
                Assert.That(model.TryGenerate(attempt.sender, attempt.drum, attempt.position, attempt.id, attempt.time,
                    out var rejected, out string reason), Is.False);
                Assert.That(rejected, Is.Null);
                Assert.That(reason, Is.EqualTo(attempt.error));
                Assert.That(History(model), Is.EqualTo(before));
                Assert.That(model.CopyPlayers().Single().generatedTotal, Is.EqualTo(1));
            }
            Assert.That(Generate(model, 10, .11d).kind, Is.Not.EqualTo(first.kind));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void StorageAndStaminaRejectionsDoNotAdvanceRawHistory(bool storage)
        {
            SetCorrection(true, 100, 1);
            float[] values = config.CaptureDeveloperValues();
            values[10] = 8f;
            if (storage) values[9] = 1f;
            else { values[3] = values[4] = 10f; values[5] = 10f; }
            Apply(values);
            LabModel model = Solo("raw-resource-reject");
            Generate(model, 10, 0d);
            string before = History(model);
            Assert.That(model.TryGenerate(10, "rejected", 0d, out var rejected, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo(storage ? "STORAGE_FULL" : "INSUFFICIENT_STAMINA"));
            Assert.That(rejected, Is.Null);
            Assert.That(History(model), Is.EqualTo(before));
            Assert.That(model.CopyPlayers().Single().generatedTotal, Is.EqualTo(1));
        }

        [Test]
        public void OverheatedAttemptsAndFreeJigwiSuppliesDoNotAdvanceRawHistory()
        {
            SetCorrection(true, 100, 1);
            LabModel model = Solo("raw-overheat");
            LabOrbState first = Generate(model, 10, 0d);
            string before = History(model);
            Assert.That(model.TryStartJigwiFire(10, 0d, out string reason), Is.True, reason);
            Assert.That(model.CopyOwnedStates(10).Count(state => state.jigwiSupply), Is.GreaterThan(0));
            Assert.That(History(model), Is.EqualTo(before));
            model.Tick(15d);
            Assert.That(model.JigwiOverheated(10, 15d), Is.True);
            Assert.That(model.TryGenerate(10, "burnt", 15d, out var burnt, out reason), Is.True, reason);
            Assert.That(reason, Is.EqualTo("JIGWI_BURNED"));
            Assert.That(burnt, Is.Null);
            Assert.That(model.CopyPlayers().Single().generatedTotal, Is.EqualTo(2),
                "The paid overheat attempt advances the existing generation counter, not the raw run.");
            Assert.That(History(model), Is.EqualTo(before));
            Assert.That(Generate(model, 10, 19d).kind, Is.Not.EqualTo(first.kind));
        }

        [Test]
        public void FeverCombinedResultsLeaveTheLastActualRawRunIntact()
        {
            SetCorrection(true, 100, 1);
            float[] values = config.CaptureDeveloperValues();
            values[10] = 8f; values[196] = 1f; values[198] = 1f; values[199] = 8f;
            Apply(values);
            LabModel model = Solo("raw-fever");
            LabOrbState first = Generate(model, 10, 0d);
            LabOrbState second = Generate(model, 10, 0d);
            Assert.That(second.kind, Is.Not.EqualTo(first.kind));
            Assert.That(model.TryCombineAt(10, first.id, second.id, "combine", 0d, new Vector2(.5f, .5f),
                Vector2.zero, out var combined, out string reason), Is.True, reason);
            Assert.That(model.TryLaunch(10, combined.id, "launch", 0d, out _, out reason), Is.True, reason);
            Assert.That(model.ConfirmHit(combined.id, 0d, Vector3.forward, out _, out _, out _), Is.True);
            Assert.That(model.Snapshot(0d).FeverOn(LabTeam.Blue), Is.True);
            string before = History(model);
            for (int i = 0; i < 4; i++)
            {
                LabOrbState fever = Generate(model, 10, 1d);
                Assert.That(fever.kind, Is.EqualTo(LabOrbKind.Combined));
                Assert.That(fever.fever, Is.True);
                Assert.That(History(model), Is.EqualTo(before));
            }
            Assert.That(Generate(model, 10, 8.1d).kind, Is.Not.EqualTo(second.kind));
        }

        [Test]
        public void PauseKeepsHistoryAndBeginClearsItForRetryAndAnotherSession()
        {
            SetCorrection(true, 100, 1);
            var clock = new LabBattleClock();
            LabModel model = Solo("raw-reset");
            LabOrbState first = Generate(model, 10, clock.Now(0d));
            string before = History(model);
            Assert.That(clock.Pause(0d), Is.True);
            model.Tick(clock.Now(1000d));
            Assert.That(History(model), Is.EqualTo(before));
            Assert.That(clock.Resume(1000d), Is.True);
            Assert.That(Generate(model, 10, clock.Now(1000.11d)).kind, Is.Not.EqualTo(first.kind));
            model.Tick(181d);
            model.BeginSolo("raw-reset", 10, 182d, config);
            Assert.That(History(model), Is.Empty, "Retry starts with no previous raw, independently of the new seed.");
            Generate(model, 10, 182d);
            model.Tick(363d);
            model.BeginSolo("another-session", 10, 364d, config);
            Assert.That(History(model), Is.Empty);
        }

        private LabModel Solo(string session)
        {
            var model = new LabModel();
            model.BeginSolo(session, 10, 0d, config);
            return model;
        }

        private LabOrbState Generate(LabModel model, ulong owner, double now, string id = null)
        {
            Assert.That(model.TryGenerate(owner, id ?? "raw-" + ++request, now, out var orb, out string reason), Is.True, reason);
            Assert.That(orb, Is.Not.Null, reason);
            return orb;
        }

        private void SetCorrection(bool enabled, int chance, int maximum)
        {
            float[] values = config.CaptureDeveloperValues();
            values[225] = enabled ? 1f : 0f; values[226] = chance; values[227] = maximum;
            Apply(values);
        }

        private void SetChances(params int[] chances)
        {
            float[] values = config.CaptureDeveloperValues();
            for (int i = 0; i < chances.Length; i++) values[39 + i] = chances[i];
            Apply(values);
        }

        private void Apply(float[] values) => Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
        private static IDictionary Streaks(LabModel model) => (IDictionary)typeof(LabModel)
            .GetField("rawGenerationStreaks", Private).GetValue(model);
        private static void AssertHistory(LabModel model, ulong player, LabOrbKind kind, int count)
        {
            object entry = Streaks(model)[player];
            Assert.That(entry, Is.Not.Null);
            Assert.That((LabOrbKind)entry.GetType().GetField("Kind").GetValue(entry), Is.EqualTo(kind));
            Assert.That((int)entry.GetType().GetField("Count").GetValue(entry), Is.EqualTo(count));
        }
        private static string History(LabModel model) => string.Join("|", Streaks(model).Keys.Cast<ulong>().OrderBy(id => id)
            .Select(id =>
            {
                object entry = Streaks(model)[id];
                return id + ":" + entry.GetType().GetField("Kind").GetValue(entry) + ":"
                    + entry.GetType().GetField("Count").GetValue(entry);
            }));
        private static LabOrbKind Kind(char kind)
        {
            switch (kind)
            {
                case 'Y': return LabOrbKind.Yin;
                case 'y': return LabOrbKind.Yang;
                case 'R': return LabOrbKind.Relay;
                case 'S': return LabOrbKind.SecretBox;
                case 'P': return LabOrbKind.Potion;
                case 'B': return LabOrbKind.Bomb;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
