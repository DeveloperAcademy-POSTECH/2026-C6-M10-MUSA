using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Round-art storage and authority observation; no live-device or GPU claims.</summary>
    public sealed class LabPaintArtJournalTests
    {
        static string PathFor() => Path.Combine(Path.GetTempPath(), "hapio-art-test-" + Guid.NewGuid().ToString("N") + ".art");
        static LabPaintArtJournal Journal(string path = null, bool receiver = false, int cacheBytes = 4096,
            long maxBytes = LabPaintArtJournal.DefaultMaximumBytes) => new LabPaintArtJournal(path ?? PathFor(),
                "test-round/1", 64, 32, "test-style-v1", receiver, maxBytes, cacheBytes);
        static LabPaintVisualStamp Stamp(int i = 0) => new LabPaintVisualStamp
        {
            center = Vector3.forward, right = Vector3.right, radius = .2f, pattern = LabPaintPattern.Splatter,
            color = i % 2 == 0 ? new Color32(200, 60, 40, 255) : new Color32(30, 80, 180, 255),
            painterSlot = (byte)(i % 5 + 1), seed = (uint)(123 + i)
        };
        static List<LabPaintArtOperation> ReadAll(LabPaintArtJournal journal, int batch = 11)
        {
            var result = new List<LabPaintArtOperation>(); var next = new List<LabPaintArtOperation>();
            ulong cursor = 0;
            while (cursor < journal.LastSequence)
            {
                Assert.That(journal.ReadOperations(cursor, batch, next), Is.True, journal.Error);
                Assert.That(next.Count, Is.GreaterThan(0));
                result.AddRange(next); cursor = next[next.Count - 1].Sequence;
            }
            return result;
        }

        [Test]
        public void MoreThan128StrokesSurviveDiskPagesDuplicatesAndSmallReplayBatches()
        {
            string path = PathFor();
            using (var source = Journal(path, cacheBytes: 4096))
            using (var receiver = Journal(receiver: true, cacheBytes: 0))
            {
                for (int i = 0; i < 180; i++)
                {
                    uint stroke = source.BeginStroke(Stamp(i));
                    Assert.That(stroke, Is.EqualTo((uint)i + 1));
                    Assert.That(source.AppendPaint(stroke, i, (uint)i + 1), Is.True);
                    if (i % 19 == 0) Assert.That(source.Flush(), Is.True);
                }
                Assert.That(source.Seal(180, new string('a', 64)), Is.True, source.Error);
                Assert.That(source.PageCount, Is.GreaterThan(1));
                Assert.That(source.TryReadPage(1, out byte[] second), Is.True);
                Assert.That(receiver.TryImportPage(1, second), Is.False, "A gap cannot advance the receiver.");
                Assert.That(receiver.LastSequence, Is.Zero);
                for (int page = 0; page < source.PageCount; page++)
                {
                    Assert.That(source.TryReadPage(page, out byte[] raw), Is.True);
                    Assert.That(raw.Length, Is.LessThanOrEqualTo(4096));
                    Assert.That(receiver.TryImportPage(page, raw), Is.True, receiver.Error);
                    ulong through = receiver.LastSequence;
                    Assert.That(receiver.TryImportPage(page, raw), Is.True);
                    Assert.That(receiver.LastSequence, Is.EqualTo(through));
                }
                var original = ReadAll(source, 1); var replay = ReadAll(receiver, 47);
                Assert.That(replay.Select(o => o.Sequence), Is.EqualTo(original.Select(o => o.Sequence)));
                Assert.That(replay.Select(o => o.Kind), Is.EqualTo(original.Select(o => o.Kind)));
                Assert.That(replay.Count(o => o.Kind == LabPaintArtOperationKind.StrokeBegin), Is.EqualTo(180));
                Assert.That(replay.Where(o => o.Kind == LabPaintArtOperationKind.PaintCells).SelectMany(o => o.Cells),
                    Is.EqualTo(Enumerable.Range(0, 180)));
                Assert.That(receiver.IsSealed, Is.True);
                Assert.That(receiver.PrefixHash, Is.EqualTo(source.PrefixHash));
                Assert.That(receiver.SealSurfaceHash, Is.EqualTo(new string('a', 64)));
                Assert.That(receiver.TryGetStroke(1, out var oldest), Is.True);
                Assert.That(oldest.seed, Is.EqualTo(123));
                Assert.That(receiver.TryGetStroke(180, out var last), Is.True);
                Assert.That(last.seed, Is.EqualTo(302));
                Assert.That(source.CachedPageBytes, Is.LessThanOrEqualTo(4096));
                Assert.That(receiver.CachedPageBytes, Is.Zero);
            }
            Assert.That(File.Exists(path), Is.False);
            Assert.That(File.Exists(path + ".strokes"), Is.False);
        }

        [Test]
        public void CorruptPageCannotPartlyInstallAndAnIntactRetryStillSucceeds()
        {
            using (var source = Journal())
            using (var destination = Journal(receiver: true))
            {
                uint stroke = source.BeginStroke(Stamp()); source.AppendPaint(stroke, 12, 1);
                source.Flush(); Assert.That(source.TryReadPage(0, out byte[] raw), Is.True);
                byte[] damaged = (byte[])raw.Clone(); damaged[damaged.Length - 1] ^= 1;
                Assert.That(destination.TryImportPage(0, damaged), Is.False);
                Assert.That(destination.PageCount, Is.Zero); Assert.That(destination.LastSequence, Is.Zero);
                Assert.That(destination.TryImportPage(0, raw), Is.True, destination.Error);
                Assert.That(ReadAll(destination).Last().Cells, Is.EqualTo(new[] { 12 }));
                damaged = (byte[])raw.Clone(); damaged[30] ^= 1;
                Assert.That(destination.TryImportPage(0, damaged), Is.False);
                Assert.That(destination.LastSequence, Is.EqualTo(source.LastSequence));
            }
        }

        [Test]
        public void FullStorageLatchesAnExplicitErrorWithoutEvictingOldArt()
        {
            using (var source = Journal(maxBytes: 4096))
            {
                uint stroke = source.BeginStroke(Stamp());
                for (int i = 0; i < 1000 && source.Error == null; i++) source.AppendPaint(stroke, i % 2048, (uint)i);
                Assert.That(source.Error, Does.Contain("storage limit"));
                ulong through = source.LastSequence;
                Assert.That(source.AppendPaint(stroke, 1, 1001), Is.False);
                Assert.That(source.LastSequence, Is.EqualTo(through));
                Assert.That(source.IsSealed, Is.False);
                Assert.That(source.RawBytes, Is.LessThanOrEqualTo(4096));
            }
        }

        [Test]
        public void ExternalDiskCorruptionIsDetectedOnAnUncachedRead()
        {
            using (var source = Journal(cacheBytes: 0))
            {
                uint stroke = source.BeginStroke(Stamp()); source.AppendPaint(stroke, 1, 1); source.Flush();
                FieldInfo fileField = typeof(LabPaintArtJournal).GetField("file", BindingFlags.NonPublic | BindingFlags.Instance);
                var file = (FileStream)fileField.GetValue(source);
                file.Position = 4 + 64; int before = file.ReadByte(); file.Position--; file.WriteByte((byte)(before ^ 1)); file.Flush();
                Assert.That(source.TryReadPage(0, out _), Is.False);
                Assert.That(source.Error, Does.Contain("digest"));
            }
        }

        [TestCase(LabPaintPattern.Legacy)] [TestCase(LabPaintPattern.Broad)]
        [TestCase(LabPaintPattern.Splatter)] [TestCase(LabPaintPattern.Flow)]
        public void ObservingArtDoesNotChangeAuthorityMapsHintsBudgetsOrRandomState(LabPaintPattern pattern)
        {
            var settings = new LabPaintGrowthSettings { Resolution = 0, Pattern = pattern,
                InitialPercent = 2f, RepeatPercent = 1.5f, GrowthPercentPerSecond = 4f,
                DryingEnabled = true, DryingDelaySeconds = .2f, DryingPercentPerSecond = 2f };
            var plain = new LabPaintSurface(settings, 553); var recorded = new LabPaintSurface(settings, 553);
            using (var journal = Journal())
            {
                Assert.That(recorded.AttachArtJournal(journal), Is.True);
                for (int i = 0; i < 16; i++)
                {
                    Vector3 direction = i % 3 == 0 ? Vector3.forward : new Vector3(Mathf.Sin(i), .2f, Mathf.Cos(i)).normalized;
                    Color color = i % 2 == 0 ? Color.red : Color.blue;
                    Assert.That(recorded.RegisterHit(direction, color, (ulong)(i % 5 + 1), i % 5 + 1),
                        Is.EqualTo(plain.RegisterHit(direction, color, (ulong)(i % 5 + 1), i % 5 + 1)));
                    recorded.Advance(.3d); plain.Advance(.3d);
                    Equivalent(plain, recorded);
                }
                Assert.That(journal.Error, Is.Null);
                AssertReplayMatches(recorded, ReadAll(journal));
                // A broken recorder must remain cosmetic, even when further hits consume old supplies.
                journal.Fail("simulated disk failure");
                plain.RegisterHit(Vector3.forward, Color.green, 3, 3);
                recorded.RegisterHit(Vector3.forward, Color.green, 3, 3);
                plain.Advance(2d); recorded.Advance(2d); Equivalent(plain, recorded);
            }
        }

        [Test]
        public void OneInitialSplatterSharesOneStrokeAcrossDetachedComponents()
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 0,
                Pattern = LabPaintPattern.Splatter, InitialPercent = 10f, GrowthPercentPerSecond = 0f }, 91);
            using (var journal = Journal())
            {
                Assert.That(surface.AttachArtJournal(journal), Is.True);
                Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 1, 1), Is.True);
                var records = ReadAll(journal);
                Assert.That(records.Count(o => o.Kind == LabPaintArtOperationKind.StrokeBegin), Is.EqualTo(1));
                Assert.That(records.Where(o => o.Kind == LabPaintArtOperationKind.PaintCells).Select(o => o.StrokeId),
                    Is.All.EqualTo(1u));
                AssertReplayMatches(surface, records);
            }
        }

        [Test]
        public void FractionalCellIsAttributedToTheFinalSupplyStrokeAndOwner()
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 0,
                Pattern = LabPaintPattern.Legacy, InitialPercent = .1f, RepeatPercent = .1f }, 101);
            using (var journal = Journal())
            {
                surface.AttachArtJournal(journal);
                surface.RegisterHit(Vector3.forward, Color.red, 100, 1);
                surface.RegisterHit(Vector3.forward, Color.blue, 900, 5);
                surface.Advance(100d);
                var records = ReadAll(journal);
                var strokes = records.Where(o => o.Kind == LabPaintArtOperationKind.StrokeBegin)
                    .ToDictionary(o => o.StrokeId, o => o.Stamp);
                var painted = records.Where(o => o.Kind == LabPaintArtOperationKind.PaintCells).ToArray();
                Assert.That(painted.Length, Is.EqualTo(4));
                Assert.That(painted.Take(2).Select(o => strokes[o.StrokeId].painterSlot), Is.All.EqualTo((byte)1));
                Assert.That(painted.Skip(2).Select(o => strokes[o.StrokeId].painterSlot), Is.All.EqualTo((byte)5));
                Assert.That(painted.Skip(2).Select(o => strokes[o.StrokeId].color), Is.All.EqualTo((Color32)Color.blue));
                AssertReplayMatches(surface, records);
            }
        }

        [Test]
        public void DryingThenRepaintingReplaysDeletionBeforeTheNewLayerAndClearDetaches()
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 0,
                Pattern = LabPaintPattern.Legacy, InitialPercent = 5f, RepeatPercent = 0f,
                GrowthPercentPerSecond = 0f, DryingEnabled = true, DryingDelaySeconds = .1f,
                DryingPercentPerSecond = 1000f }, 77);
            using (var journal = Journal())
            {
                surface.AttachArtJournal(journal);
                surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
                surface.Advance(1d); Assert.That(surface.CoveredCount, Is.Zero);
                surface.RegisterHit(Vector3.forward, Color.blue, 2, 2);
                var records = ReadAll(journal);
                int erased = records.FindIndex(o => o.Kind == LabPaintArtOperationKind.EraseCells);
                int blue = records.FindIndex(o => o.Kind == LabPaintArtOperationKind.StrokeBegin && o.Stamp.painterSlot == 2);
                Assert.That(erased, Is.GreaterThan(0)); Assert.That(blue, Is.GreaterThan(erased));
                AssertReplayMatches(surface, records);
                surface.Clear(); Assert.That(surface.ArtJournal, Is.Null);
                Assert.That(journal.Error, Is.Null);
            }
        }

        [Test]
        public void OpeningAnExistingPathFailsWithoutDeletingOrTruncatingThatFile()
        {
            string path = PathFor(); File.WriteAllText(path, "preserve-existing-file");
            try
            {
                using (var failed = Journal(path))
                    Assert.That(failed.Error, Is.Not.Null);
                Assert.That(File.ReadAllText(path), Is.EqualTo("preserve-existing-file"));
            }
            finally { File.Delete(path); }
        }

        static void Equivalent(LabPaintSurface expected, LabPaintSurface actual)
        {
            Assert.That(Pixels(actual), Is.EqualTo(Pixels(expected)));
            Assert.That(Owners(actual), Is.EqualTo(Owners(expected)));
            Assert.That(actual.CopyVisualStamps(), Is.EqualTo(expected.CopyVisualStamps()));
            Assert.That(actual.CoveredCount, Is.EqualTo(expected.CoveredCount));
            Assert.That(actual.PaintCount, Is.EqualTo(expected.PaintCount));
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.PendingCoverage, Is.EqualTo(expected.PendingCoverage));
            var field = typeof(LabPaintSurface).GetField("randomState", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field.GetValue(actual), Is.EqualTo(field.GetValue(expected)));
        }
        static void AssertReplayMatches(LabPaintSurface surface, List<LabPaintArtOperation> records)
        {
            var pixels = new Color32[surface.CellCount]; var owners = new byte[surface.CellCount];
            var strokes = new Dictionary<uint, LabPaintVisualStamp>();
            foreach (var op in records)
            {
                if (op.Kind == LabPaintArtOperationKind.StrokeBegin) strokes.Add(op.StrokeId, op.Stamp);
                else if (op.Kind == LabPaintArtOperationKind.PaintCells)
                    foreach (int cell in op.Cells)
                    { Assert.That(pixels[cell].a, Is.Zero, "Only actual new authority cells are recorded."); pixels[cell] = strokes[op.StrokeId].color; owners[cell] = strokes[op.StrokeId].painterSlot; }
                else if (op.Kind == LabPaintArtOperationKind.EraseCells)
                    foreach (int cell in op.Cells) { pixels[cell] = default; owners[cell] = 0; }
            }
            Assert.That(pixels, Is.EqualTo(Pixels(surface))); Assert.That(owners, Is.EqualTo(Owners(surface)));
        }
        static Color32[] Pixels(LabPaintSurface surface) { var p = new Color32[surface.CellCount]; surface.CopyPixels(p); return p; }
        static byte[] Owners(LabPaintSurface surface) { var p = new byte[surface.CellCount]; surface.CopyPainterSlots(p); return p; }
    }
}
