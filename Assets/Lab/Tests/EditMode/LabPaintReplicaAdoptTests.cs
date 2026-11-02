using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Guest replica installs that take or reuse arrays instead of copying them again (#444).</summary>
    public sealed class LabPaintReplicaAdoptTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void AFullAdoptLeavesTheSameReplicaAsTryReplacePixelsWithoutCopyingTheArrays()
        {
            LabPaintSurface host = Host(1, 3);
            var copied = new LabPaintSurface();
            Assert.That(copied.TryReplacePixels(host.Columns, host.Rows, 40u, Pixels(host), Painters(host), 3, host.CopyVisualStamps()), Is.True);

            var adopted = new LabPaintSurface();
            Color32[] pixels = Pixels(host); byte[] painters = Painters(host);
            Assert.That(adopted.TryAdoptReplica(host.Columns, host.Rows, 0u, 40u, pixels, painters, 3, host.CoveredCount,
                host.CopyVisualStamps(), null, 0, out Color32[] recycled, out byte[] recycledPainters), Is.True);
            AssertSameReplica(adopted, copied);
            Assert.That(recycled, Is.Null, "The size changed from the default map, so nothing is handed back.");
            Assert.That(recycledPainters, Is.Null);
            Assert.That(Field<Color32[]>(adopted, "pixels"), Is.SameAs(pixels), "Adopted, not copied.");
            Assert.That(Field<byte[]>(adopted, "painterSlots"), Is.SameAs(painters));
        }

        [Test]
        public void ASameSizeReplicaChecksOnlyTheChangedCellsAndHandsBackItsOldArrays()
        {
            LabPaintSurface host = Host(1, 2);
            var replica = new LabPaintSurface();
            Assert.That(replica.TryReplacePixels(host.Columns, host.Rows, 10u, Pixels(host), Painters(host), 2), Is.True);
            Color32[] before = Field<Color32[]>(replica, "pixels");
            Color32[] next = Pixels(replica); byte[] nextPainters = Painters(replica);
            int painted = Array.FindIndex(next, pixel => pixel.a == 255);
            int empty = Array.FindIndex(next, pixel => pixel.a == 0);
            int recolor = Array.FindLastIndex(next, pixel => pixel.a == 255);
            Assume.That(painted != recolor && empty >= 0, "Needs two painted and one empty cell.");
            next[painted] = new Color32(0, 0, 0, 0); nextPainters[painted] = 0;
            next[empty] = new Color32(10, 20, 30, 255); nextPainters[empty] = 4;
            next[recolor] = new Color32(200, 0, 0, 255); nextPainters[recolor] = 6;
            int[] changed = new[] { painted, empty, recolor }.OrderBy(cell => cell).ToArray();
            int covered = next.Count(pixel => pixel.a == 255);
            Assert.That(covered, Is.EqualTo(replica.CoveredCount), "One cleared and one newly painted cell.");

            Assert.That(replica.TryAdoptReplica(host.Columns, host.Rows, 10u, 11u, next, nextPainters, 3, covered, null,
                changed.Concat(new[] { 99999 }).ToArray(), changed.Length, out Color32[] recycled, out byte[] recycledPainters), Is.True,
                "Entries past changedCount are ignored.");
            Assert.That(replica.Revision, Is.EqualTo(11u));
            Assert.That(replica.PaintCount, Is.EqualTo(3));
            Assert.That(replica.CoveredCount, Is.EqualTo(covered));
            Assert.That(Pixels(replica), Is.EqualTo(next));
            Assert.That(Painters(replica), Is.EqualTo(nextPainters));
            Assert.That(recycled, Is.SameAs(before), "The previous arrays come back for the next transaction.");
            Assert.That(recycledPainters, Has.Length.EqualTo(nextPainters.Length));
        }

        [TestCase(2, 0)]
        [TestCase(0, 3)]
        [TestCase(4, 1)]
        public void TheIncrementalCoveredCountFollowsNewlyPaintedAndClearedCells(int painted, int cleared)
        {
            LabPaintSurface host = Host(1, 2);
            var replica = new LabPaintSurface();
            Assert.That(replica.TryReplacePixels(host.Columns, host.Rows, 10u, Pixels(host), Painters(host), 2), Is.True);
            int before = replica.CoveredCount;
            Color32[] next = Pixels(replica); byte[] nextPainters = Painters(replica);
            int[] empty = Enumerable.Range(0, next.Length).Where(i => next[i].a == 0).Take(painted).ToArray();
            int[] full = Enumerable.Range(0, next.Length).Where(i => next[i].a == 255).Take(cleared).ToArray();
            Assume.That(empty.Length == painted && full.Length == cleared);
            foreach (int cell in empty) { next[cell] = new Color32(0, 90, 200, 255); nextPainters[cell] = 5; }
            foreach (int cell in full) { next[cell] = new Color32(0, 0, 0, 0); nextPainters[cell] = 0; }
            int[] changed = empty.Concat(full).OrderBy(cell => cell).ToArray();
            int expected = before + painted - cleared;
            Assert.That(replica.TryAdoptReplica(host.Columns, host.Rows, 10u, 11u, (Color32[])next.Clone(), (byte[])nextPainters.Clone(),
                2, before, null, changed, changed.Length, out _, out _), Is.False, "The old count no longer matches.");
            Assert.That(replica.TryAdoptReplica(host.Columns, host.Rows, 10u, 11u, next, nextPainters,
                2, expected, null, changed, changed.Length, out _, out _), Is.True);
            Assert.That(replica.CoveredCount, Is.EqualTo(expected));
            Assert.That(replica.CoveredCount, Is.EqualTo(Pixels(replica).Count(pixel => pixel.a == 255)), "Equals a full recount.");
        }

        [Test]
        public void ASurfaceThatIsNotYetAReplicaIsAlwaysCheckedInFull()
        {
            var fresh = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 0 }, 4u);
            Color32[] next = Pixels(fresh); byte[] nextPainters = Painters(fresh);
            nextPainters[0] = 2; // An unpainted cell with an owner, not listed as changed.
            Assert.That(fresh.TryAdoptReplica(fresh.Columns, fresh.Rows, fresh.Revision, fresh.Revision + 1u, next, nextPainters,
                0, fresh.CoveredCount, null, Array.Empty<int>(), 0, out _, out _), Is.False,
                "Only a validated replica may be trusted outside the changed cells.");
        }

        [TestCase("invalid-alpha")]
        [TestCase("unpainted-owner")]
        [TestCase("owner-above-six")]
        [TestCase("unsorted")]
        [TestCase("duplicate")]
        [TestCase("out-of-range")]
        [TestCase("count-too-large")]
        [TestCase("wrong-covered")]
        [TestCase("invalid-stamps")]
        [TestCase("own-array")]
        public void AnInvalidAdoptChangesNothing(string mutation)
        {
            LabPaintSurface host = Host(0, 2);
            var replica = new LabPaintSurface();
            Assert.That(replica.TryReplacePixels(host.Columns, host.Rows, 10u, Pixels(host), Painters(host), 2, host.CopyVisualStamps()), Is.True);
            Color32[] beforePixels = Pixels(replica); byte[] beforePainters = Painters(replica);
            uint version = replica.VisualVersion;
            Color32[] next = Pixels(replica); byte[] nextPainters = Painters(replica);
            int cell = Array.FindIndex(next, pixel => pixel.a == 255);
            int empty = Array.FindIndex(next, pixel => pixel.a == 0);
            int[] changed = { Math.Min(cell, empty), Math.Max(cell, empty) };
            int count = 2, covered = replica.CoveredCount;
            LabPaintVisualStamp[] stamps = host.CopyVisualStamps();
            switch (mutation)
            {
                case "invalid-alpha": next[cell].a = 128; break;
                case "unpainted-owner": nextPainters[empty] = 2; break;
                case "owner-above-six": nextPainters[cell] = (byte)(LabPaintSurface.MaxPainterSlot + 1); break;
                case "unsorted": changed = new[] { changed[1], changed[0] }; break;
                case "duplicate": changed = new[] { cell, cell }; break;
                case "out-of-range": changed = new[] { cell, next.Length }; break;
                case "count-too-large": count = 3; break;
                case "wrong-covered": covered++; break;
                case "invalid-stamps": stamps = new[] { new LabPaintVisualStamp() }; break;
                case "own-array": next = Field<Color32[]>(replica, "pixels"); break;
            }
            Assert.That(replica.TryAdoptReplica(host.Columns, host.Rows, 10u, 11u, next, nextPainters, 3, covered, stamps,
                changed, count, out Color32[] recycled, out _), Is.False);
            Assert.That(recycled, Is.Null);
            Assert.That(replica.Revision, Is.EqualTo(10u));
            Assert.That(replica.PaintCount, Is.EqualTo(2));
            Assert.That(Pixels(replica), Is.EqualTo(beforePixels));
            Assert.That(Painters(replica), Is.EqualTo(beforePainters));
            Assert.That(replica.VisualVersion, Is.EqualTo(version));
        }

        [Test]
        public void AStaleBaseRevisionFallsBackToCheckingEveryCell()
        {
            LabPaintSurface host = Host(0, 1);
            var replica = new LabPaintSurface();
            Assert.That(replica.TryReplacePixels(host.Columns, host.Rows, 10u, Pixels(host), Painters(host), 1), Is.True);
            Color32[] next = Pixels(replica); byte[] nextPainters = Painters(replica);
            int outside = Array.FindIndex(next, pixel => pixel.a == 0);
            nextPainters[outside] = 3; // Invalid, and not listed as changed.
            Assert.That(replica.TryAdoptReplica(host.Columns, host.Rows, 10u, 11u, (Color32[])next.Clone(), (byte[])nextPainters.Clone(),
                1, replica.CoveredCount, null, Array.Empty<int>(), 0, out _, out _), Is.True,
                "From the current revision only listed cells are checked; the caller guarantees the rest.");
            Assert.That(replica.TryAdoptReplica(host.Columns, host.Rows, 9u, 12u, next, nextPainters,
                1, replica.CoveredCount, null, Array.Empty<int>(), 0, out _, out _), Is.False,
                "A different base revision checks every cell and finds the bad one.");
            Assert.That(replica.Revision, Is.EqualTo(11u));
        }

        [Test]
        public void AnAdoptedReplicaHasNoGrowthOrDryingAuthority()
        {
            LabPaintSurface host = Host(0, 1);
            var replica = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 0, DryingEnabled = true }, 3u);
            Assert.That(replica.TryAdoptReplica(host.Columns, host.Rows, 0u, 5u, Pixels(host), Painters(host), 1, host.CoveredCount,
                null, null, 0, out _, out _), Is.True);
            Assert.That(replica.RegisterHit(Vector3.back, Color.blue, 2ul, 2), Is.False);
            Assert.That(replica.Advance(5d), Is.EqualTo(replica.IsComplete ? 0d : -1d));
            Assert.That(replica.Revision, Is.EqualTo(5u));
        }

        [Test]
        public void CopyReplicaFromMatchesTryReplacePixelsAndReusesItsArraysAndVisualVersion()
        {
            LabPaintSurface host = Host(2, 2);
            var expected = new LabPaintSurface();
            Assert.That(expected.TryReplacePixels(host.Columns, host.Rows, host.Revision, Pixels(host), Painters(host), 7, host.CopyVisualStamps()), Is.True);
            var display = new LabPaintSurface();
            Assert.That(display.TryCopyReplicaFrom(host, 7), Is.True);
            AssertSameReplica(display, expected);

            Color32[] array = Field<Color32[]>(display, "pixels");
            byte[] painterArray = Field<byte[]>(display, "painterSlots");
            uint version = display.VisualVersion;
            Assert.That(display.TryCopyReplicaFrom(host, 7), Is.True);
            Assert.That(display.VisualVersion, Is.EqualTo(version), "Identical stamps keep the renderer cache.");
            Hit(host, 9);
            Assert.That(display.TryCopyReplicaFrom(host, 8), Is.True);
            Assert.That(Field<Color32[]>(display, "pixels"), Is.SameAs(array), "Same size: written in place.");
            Assert.That(Field<byte[]>(display, "painterSlots"), Is.SameAs(painterArray));
            Assert.That(display.VisualVersion, Is.Not.EqualTo(version), "New stamps invalidate it.");
            Assert.That(Pixels(display), Is.EqualTo(Pixels(host)));
            Assert.That(display.CoveredCount, Is.EqualTo(host.CoveredCount));
            Assert.That(display.Revision, Is.EqualTo(host.Revision));
            Assert.That(display.PaintCount, Is.EqualTo(8));

            LabPaintSurface smaller = Host(0, 1);
            Assert.That(display.TryCopyReplicaFrom(smaller, 1), Is.True);
            Assert.That(display.Columns, Is.EqualTo(64));
            Assert.That(Pixels(display), Is.EqualTo(Pixels(smaller)));
        }

        [Test]
        public void ACopyFromAnInvalidSourceChangesNothing()
        {
            LabPaintSurface host = Host(0, 1);
            var display = new LabPaintSurface();
            Assert.That(display.TryCopyReplicaFrom(host, 1), Is.True);
            Color32[] before = Pixels(display);
            Assert.That(display.TryCopyReplicaFrom(host, -1), Is.False, "A valid source with a negative hit count.");
            Field<Color32[]>(host, "pixels")[0] = new Color32(1, 2, 3, 128);
            Assert.That(display.TryCopyReplicaFrom(host, 2), Is.False);
            Assert.That(display.TryCopyReplicaFrom(display, 1), Is.False);
            Assert.That(Pixels(display), Is.EqualTo(before));
            Assert.That(display.PaintCount, Is.EqualTo(1));
        }

        private static void AssertSameReplica(LabPaintSurface actual, LabPaintSurface expected)
        {
            Assert.That(actual.Columns, Is.EqualTo(expected.Columns));
            Assert.That(actual.Rows, Is.EqualTo(expected.Rows));
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.CoveredCount, Is.EqualTo(expected.CoveredCount));
            Assert.That(actual.PaintCount, Is.EqualTo(expected.PaintCount));
            Assert.That(Pixels(actual), Is.EqualTo(Pixels(expected)));
            Assert.That(Painters(actual), Is.EqualTo(Painters(expected)));
            Assert.That(LabPaintVisualStamp.Encode(actual.CopyVisualStamps()), Is.EqualTo(LabPaintVisualStamp.Encode(expected.CopyVisualStamps())));
            Assert.That(actual.RegisterHit(Vector3.back, Color.blue, 2ul, 2), Is.False, "A replica never paints locally.");
            Assert.That(actual.RegionCount, Is.EqualTo(expected.RegionCount));
        }

        private static LabPaintSurface Host(int resolution, int hits)
        {
            var host = new LabPaintSurface(new LabPaintGrowthSettings
            { Resolution = resolution, InitialPercent = 2f, RepeatPercent = 1.5f, Pattern = LabPaintPattern.Splatter }, 123u);
            for (int i = 0; i < hits; i++) Hit(host, i + 1);
            return host;
        }

        private static void Hit(LabPaintSurface surface, int seed)
        {
            var random = new System.Random(seed);
            uint revision = surface.Revision;
            for (int attempt = 0; attempt < 50 && surface.Revision == revision; attempt++)
                surface.RegisterHit(new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f,
                    (float)random.NextDouble() * 2f - 1f).normalized, Color.red, 1ul, seed % 6 + 1);
            Assert.That(surface.Revision, Is.Not.EqualTo(revision));
        }

        private static T Field<T>(LabPaintSurface surface, string name) => (T)typeof(LabPaintSurface).GetField(name, Private).GetValue(surface);

        private static Color32[] Pixels(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount];
            surface.CopyPixels(pixels);
            return pixels;
        }

        private static byte[] Painters(LabPaintSurface surface)
        {
            var painters = new byte[surface.CellCount];
            surface.CopyPainterSlots(painters);
            return painters;
        }
    }
}
