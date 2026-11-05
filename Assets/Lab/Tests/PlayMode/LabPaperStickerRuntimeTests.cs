using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Real GPU checks for the opaque path; not device or aesthetic approval.</summary>
    public sealed class LabPaperStickerRuntimeTests
    {
        const int Columns = 64, Rows = 32, OutputColumns = 1024;

        [UnityTest]
        public IEnumerator NewlyPaidCellsRemainOpaqueOutsideTheirOriginalStampProjection()
        {
            RequireGraphics();
            var settings = Settings();
            using (var journal = Journal(settings))
            using (var runtime = new LabWatercolorStampRuntime())
            {
                // The descriptor deliberately faces away from these cells. Actual timed
                // growth can also leave a fixed descriptor UV; its paid cells still exist.
                var stamp = Stamp(Color.red, 1); stamp.center = Vector3.left; stamp.radius = .02f;
                uint stroke = journal.BeginStroke(stamp);
                int[] cells = Rectangle(24, 40, 10, 22);
                Append(journal, stroke, cells);
                Assert.That(journal.Flush(), Is.True);
                string prefix = journal.PrefixHash; ulong sequence = journal.LastSequence;
                yield return Render(runtime, journal, settings, true);
                var pixels = Read(runtime.PaintTexture);
                AssertInterior(pixels, runtime.PaintTexture.width, cells, color =>
                    color.Equals(new Color32(255, 0, 0, 255)));
                AssertUnpaidIsEmpty(pixels, runtime.PaintTexture.width, cells);
                Assert.That(journal.LastSequence, Is.EqualTo(sequence));
                Assert.That(journal.PrefixHash, Is.EqualTo(prefix));
                int draws = runtime.DrawCount;
                for (int i = 0; i < 3; i++)
                    Assert.That(runtime.Update(journal, null, OutputColumns, settings, 30d, true), Is.True);
                Assert.That(runtime.DrawCount, Is.EqualTo(draws), "Refreshing a settled sticker cannot repaint or darken it.");
                Assert.That(Read(runtime.PaintTexture), Is.EqualTo(pixels));
            }
        }

        [UnityTest]
        public IEnumerator ANewStickerFullyCoversTheOldColorWithoutPaintingUnpaidSurface()
        {
            RequireGraphics();
            var settings = Settings();
            using (var journal = Journal(settings))
            using (var runtime = new LabWatercolorStampRuntime())
            {
                int[] cells = Rectangle(16, 48, 8, 24);
                Append(journal, journal.BeginStroke(Stamp(Color.red, 1)), cells);
                yield return Render(runtime, journal, settings, true);
                var before = Read(runtime.PaintTexture);
                int center = CenterPixel(runtime.PaintTexture.width, 32, 16);
                Assert.That(before[center], Is.EqualTo(new Color32(255, 0, 0, 255)));
                Assert.That(journal.BeginStroke(Stamp(Color.blue, 2)), Is.Not.Zero);
                yield return Render(runtime, journal, settings, true);
                var after = Read(runtime.PaintTexture);
                Assert.That(after[center], Is.EqualTo(new Color32(0, 0, 255, 255)),
                    "The center of the new opaque sticker must replace red, not blend purple or retain a translucent lower layer.");
                Assert.That(after.Any(pixel => pixel.r > 200 && pixel.b == 0 && pixel.a == 255), Is.True,
                    "Red outside the new supported sticker must remain visible.");
                AssertUnpaidIsEmpty(after, runtime.PaintTexture.width, cells);
            }
        }

        [UnityTest]
        public IEnumerator ErasedStickerLayersStayGoneAfterRepaintResourceLossAndBoundedReplay()
        {
            RequireGraphics();
            var settings = Settings();
            using (var journal = Journal(settings))
            using (var clean = Journal(settings))
            using (var runtime = new LabWatercolorStampRuntime())
            using (var reference = new LabWatercolorStampRuntime())
            {
                int[] cells = Rectangle(20, 44, 9, 23);
                Append(journal, journal.BeginStroke(Stamp(Color.red, 1)), cells);
                Assert.That(clean.BeginStroke(Stamp(Color.red, 1)), Is.EqualTo(1));
                Assert.That(journal.BeginStroke(Stamp(Color.green, 2)), Is.EqualTo(2));
                Assert.That(clean.BeginStroke(Stamp(Color.green, 2)), Is.EqualTo(2));
                yield return Render(runtime, journal, settings, false);
                foreach (int cell in cells)
                    Assert.That(journal.AppendErase(cell, (uint)(journal.LastSequence + 1)), Is.True);
                yield return Render(runtime, journal, settings, true);
                Assert.That(Read(runtime.PaintTexture).All(pixel => pixel.Equals(new Color32())), Is.True,
                    "Drying must erase every accumulated color, not expose an older sticker.");
                Append(journal, journal.BeginStroke(Stamp(Color.blue, 3)), cells);
                Append(clean, clean.BeginStroke(Stamp(Color.blue, 3)), cells);
                yield return Render(runtime, journal, settings, false);
                yield return Render(reference, clean, settings, true);
                var expected = Read(reference.PaintTexture);
                Assert.That(Read(runtime.PaintTexture), Is.EqualTo(expected), "Old red/green cannot tint the blue repaint.");
                runtime.PaintTexture.Release();
                yield return Render(runtime, journal, settings, true);
                Assert.That(Read(runtime.PaintTexture), Is.EqualTo(expected), "GPU recreation must restore identical opaque sticker pixels.");
            }
        }

        [UnityTest]
        public IEnumerator RealFirstHitThreeRehitsAndTimedGrowthFillEveryPaidCellThroughCompletion()
        {
            RequireGraphics();
            var settings = Settings();
            var rules = new LabPaintGrowthSettings {
                Pattern = LabPaintPattern.Splatter, Resolution = 0,
                InitialPercent = 2f, RepeatPercent = 1.5f,
                GrowthPercentPerSecond = 100f, DryingEnabled = false
            };
            var surface = new LabPaintSurface(rules, 123u);
            surface.SetPaperStickerContour();
            using (var journal = Journal(settings))
            using (var runtime = new LabWatercolorStampRuntime())
            using (var replay = new LabWatercolorStampRuntime())
            {
                Assert.That(surface.AttachArtJournal(journal), Is.True);
                Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 1, 1), Is.True);
                for (int i = 0; i < 3; i++) {
                    Assert.That(surface.RegisterHit(Vector3.forward, i % 2 == 0 ? Color.blue : Color.green, 2, 2), Is.True);
                    surface.Advance(1d);
                }
                Assert.That(surface.CoveredCount, Is.EqualTo((int)decimal.Floor(.065m * surface.CellCount)));
                string before = SurfaceHash(surface); ulong sequence = journal.LastSequence;
                yield return Render(runtime, journal, settings, false);
                AssertEveryPaidCenterHasPigment(surface, runtime.PaintTexture);
                Assert.That(SurfaceHash(surface), Is.EqualTo(before));
                Assert.That(journal.LastSequence, Is.EqualTo(sequence));
                // Continue the same real connected-growth job rather than manufacturing
                // a first-stamp-only full map. This includes back/poles and tiny final holes.
                for (int hit = 0; hit < 100 && !surface.IsComplete; hit++) {
                    Assert.That(surface.RegisterHit(Vector3.forward, hit % 2 == 0 ? Color.red : Color.blue, 1, 1), Is.True);
                    surface.Advance(1d);
                }
                Assert.That(surface.IsComplete, Is.True);
                Assert.That(journal.Commit(surface.Revision, SurfaceHash(surface)), Is.True);
                Assert.That(journal.Seal(surface.Revision, SurfaceHash(surface)), Is.True);
                before = SurfaceHash(surface); sequence = journal.LastSequence;
                yield return Render(runtime, journal, settings, false);
                yield return Render(replay, journal, settings, true);
                var final = Read(runtime.PaintTexture);
                Assert.That(final.All(pixel => pixel.a == 255), Is.True,
                    "A completed actual surface must not retain transparent gaps from descriptor UVs or cell boundaries.");
                Assert.That(Read(replay.PaintTexture), Is.EqualTo(final));
                Assert.That(SurfaceHash(surface), Is.EqualTo(before));
                Assert.That(journal.LastSequence, Is.EqualTo(sequence));
                Assert.That(runtime.AppliedThrough, Is.EqualTo(sequence));
                Assert.That(runtime.RenderedThrough, Is.EqualTo(sequence));
            }
        }

        [UnityTest]
        public IEnumerator LosingOnlyTheTopStickerIdMapInvalidatesReadyAndReplaysTheSameEdgesWithinBudget()
        {
            RequireGraphics();
            var settings = Settings(); settings.paperSticker.edgeStrength = .08f;
            var field = typeof(LabWatercolorStampRuntime).GetField("topStickers",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "The edge path must retain its actual top-sticker identity map.");
            using (var journal = Journal(settings))
            using (var runtime = new LabWatercolorStampRuntime())
            {
                int[] cells = Rectangle(16, 48, 8, 24);
                Append(journal, journal.BeginStroke(Stamp(Color.red, 1)), cells);
                Assert.That(journal.BeginStroke(Stamp(Color.blue, 2)), Is.Not.Zero);
                var smaller = Stamp(Color.green, 3); smaller.radius = .45f;
                Assert.That(journal.BeginStroke(smaller), Is.Not.Zero);
                yield return Render(runtime, journal, settings, true);
                Assert.That(runtime.Ready, Is.True);
                var expected = Read(runtime.PaintTexture);
                var top = (RenderTexture)field.GetValue(runtime);
                Assert.That(top, Is.Not.Null);
                Assert.That(top.IsCreated(), Is.True, "This test must exercise the real auxiliary edge map.");
                top.Release();
                Assert.That(runtime.PaintTexture.IsCreated(), Is.True,
                    "Only the auxiliary map was lost; retaining the primary map is the regression boundary.");
                Assert.That(runtime.Ready, Is.False,
                    "An allocated primary image cannot prove edge rendering is ready after its identity map was lost.");

                int before = runtime.DrawCount;
                runtime.Update(journal, null, OutputColumns, settings, 30d, false);
                Assert.That(runtime.Error, Is.Null);
                Assert.That(runtime.DrawCount - before, Is.LessThanOrEqualTo(LabWatercolorStampRuntime.DrawBudget + 1),
                    "Bounded reconstruction must count the primary draw and the auxiliary ID draw rather than doubling its per-update budget.");
                yield return Render(runtime, journal, settings, true);
                top = (RenderTexture)field.GetValue(runtime);
                Assert.That(top != null && top.IsCreated(), Is.True);
                Assert.That(runtime.Ready, Is.True);
                Assert.That(Read(runtime.PaintTexture), Is.EqualTo(expected),
                    "Rebuilding only a lost edge-ID resource must replay the complete image and restore identical edge tones.");
                Assert.That(runtime.AppliedThrough, Is.EqualTo(journal.LastSequence));
                Assert.That(runtime.RenderedThrough, Is.EqualTo(journal.LastSequence));
            }
        }

        static LabWatercolorStampSettings Settings()
        {
            var value = new LabWatercolorStampSettings { mode = LabPaintStampMode.PaperSticker, revealSeconds = 0f };
            value.paperSticker.grainStrength = 0f; value.paperSticker.edgeStrength = 0f;
            return value;
        }
        static LabPaintArtJournal Journal(LabWatercolorStampSettings settings) => new LabPaintArtJournal(
            Path.Combine(Application.temporaryCachePath, "sticker-test-" + Guid.NewGuid().ToString("N")),
            "sticker-test", Columns, Rows, settings.Version);
        static LabPaintVisualStamp Stamp(Color color, uint seed) => new LabPaintVisualStamp {
            sequence = 1, center = Vector3.right, right = Vector3.forward, radius = .7f,
            color = color, painterSlot = 1, pattern = LabPaintPattern.Splatter, seed = seed
        };
        static int[] Rectangle(int minX, int maxX, int minY, int maxY) =>
            Enumerable.Range(minY, maxY - minY).SelectMany(y => Enumerable.Range(minX, maxX - minX).Select(x => y * Columns + x)).ToArray();
        static void Append(LabPaintArtJournal journal, uint stroke, int[] cells)
        {
            Assert.That(stroke, Is.Not.Zero);
            foreach (int cell in cells) Assert.That(journal.AppendPaint(stroke, cell, (uint)(journal.LastSequence + 1)), Is.True);
        }
        static IEnumerator Render(LabWatercolorStampRuntime runtime, LabPaintArtJournal journal, LabWatercolorStampSettings settings, bool complete)
        {
            for (int frame = 0; frame < 300; frame++) {
                runtime.Update(journal, null, OutputColumns, settings, 20d + frame / 60d, complete);
                Assert.That(runtime.Error, Is.Null);
                if (runtime.Ready) yield break;
                yield return null;
            }
            Assert.Fail("Opaque sticker journal did not finish within 300 frames.");
        }
        static bool Interior(bool[] paid, int cell)
        {
            int x = cell % Columns, y = cell / Columns;
            if (y == 0 || y == Rows - 1) return false;
            for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
                if (!paid[(y + oy) * Columns + (x + ox + Columns) % Columns]) return false;
            return true;
        }
        static int CenterPixel(int outputWidth, int x, int y)
        { int scale = outputWidth / Columns; return (y * scale + scale / 2) * outputWidth + x * scale + scale / 2; }
        static void AssertInterior(Color32[] pixels, int outputWidth, int[] cells, Func<Color32, bool> predicate)
        {
            var paid = new bool[Columns * Rows]; foreach (int cell in cells) paid[cell] = true;
            int tested = 0;
            foreach (int cell in cells) if (Interior(paid, cell)) {
                Assert.That(predicate(pixels[CenterPixel(outputWidth, cell % Columns, cell / Columns)]), Is.True, "Opaque cell " + cell);
                tested++;
            }
            Assert.That(tested, Is.GreaterThan(0));
        }
        static void AssertUnpaidIsEmpty(Color32[] pixels, int outputWidth, int[] cells)
        {
            var paid = new bool[Columns * Rows]; foreach (int cell in cells) paid[cell] = true;
            for (int cell = 0; cell < paid.Length; cell++) {
                int x = cell % Columns, y = cell / Columns; bool near = false;
                for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
                    if (y + oy >= 0 && y + oy < Rows && paid[(y + oy) * Columns + (x + ox + Columns) % Columns]) near = true;
                if (!near) Assert.That(pixels[CenterPixel(outputWidth, x, y)].a, Is.Zero, "Unpaid cell " + cell);
            }
        }
        static void AssertEveryPaidCenterHasPigment(LabPaintSurface surface, RenderTexture texture)
        {
            var logical = new Color32[surface.CellCount]; surface.CopyPixels(logical); var pixels = Read(texture);
            for (int cell = 0; cell < logical.Length; cell++) if (logical[cell].a != 0)
                Assert.That(pixels[CenterPixel(texture.width, cell % Columns, cell / Columns)].a, Is.GreaterThan(0), "Missing actual growth cell " + cell);
            AssertInterior(pixels, texture.width, Enumerable.Range(0, logical.Length).Where(cell => logical[cell].a != 0).ToArray(), c => c.a == 255);
        }
        static string SurfaceHash(LabPaintSurface surface)
        { var colors = new Color32[surface.CellCount]; var slots = new byte[surface.CellCount]; surface.CopyPixels(colors); surface.CopyPainterSlots(slots); return LabPaintPublisher.Hash(colors, slots); }
        static Color32[] Read(RenderTexture source)
        {
            var prior = RenderTexture.active; var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            try { RenderTexture.active = source; copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); copy.Apply(); return copy.GetPixels32(); }
            finally { RenderTexture.active = prior; Object.Destroy(copy); }
        }
        static void RequireGraphics()
        { if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a real GPU for opaque sticker replay."); }
    }
}
