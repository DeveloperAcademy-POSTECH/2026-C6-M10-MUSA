using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>GPU appearance invariants; these are not real-device or networking performance tests.</summary>
    public sealed class LabWatercolorPaintRuntimeTests
    {
        [UnityTest]
        public IEnumerator SettledPaintKeepsAllFiveElementHuesAndAuthoritativeBytes()
        {
            RequireGraphics();
            foreach (LabOrbElement element in new[] { LabOrbElement.Fire, LabOrbElement.Water,
                LabOrbElement.Wood, LabOrbElement.Metal, LabOrbElement.Earth })
            {
                using (var fixture = new Fixture(element))
                {
                    string authority = State(fixture.Surface);
                    AssertReady(fixture.Update(0d, true), fixture.Runtime);
                    Assert.That(fixture.Runtime.IsAnimating, Is.False, "A restored map is already settled.");
                    AssertAppearance(Read(fixture.Baseline.PaintTexture), Read(fixture.Runtime.PaintTexture));
                    Assert.That(State(fixture.Surface), Is.EqualTo(authority));
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator LiveAppendUsesElapsedTimeAndRepeatedRefreshDoesNotAccumulateAgain()
        {
            RequireGraphics();
            using (var fixture = new Fixture())
            {
                AssertReady(fixture.Update(0d, true), fixture.Runtime);
                fixture.AddRepeat();
                string authority = State(fixture.Surface);
                AssertReady(fixture.Update(1d), fixture.Runtime);
                Assert.That(fixture.Runtime.IsAnimating, Is.True);
                Color32[] start = Read(fixture.Runtime.PaintTexture);
                int startedCommits = fixture.Runtime.CommittedStampCount;
                AssertReady(fixture.Update(1d), fixture.Runtime);
                Assert.That(Read(fixture.Runtime.PaintTexture), Is.EqualTo(start), "The same time cannot darken a mark twice.");
                Assert.That(fixture.Runtime.CommittedStampCount, Is.EqualTo(startedCommits));

                AssertReady(fixture.Update(1d + fixture.Settings.spreadSeconds * .5d), fixture.Runtime);
                Color32[] middle = Read(fixture.Runtime.PaintTexture);
                Assert.That(middle.SequenceEqual(start), Is.False, "The fixed-footprint reveal must visibly progress.");
                AssertReady(fixture.Update(1d + fixture.Settings.spreadSeconds + .01d), fixture.Runtime);
                Color32[] final = Read(fixture.Runtime.PaintTexture);
                Assert.That(fixture.Runtime.IsAnimating, Is.False);
                Assert.That(final.SequenceEqual(middle), Is.False);
                AssertAppearance(Read(fixture.Baseline.PaintTexture), final);
                Assert.That(fixture.Runtime.CommittedStampCount, Is.EqualTo(fixture.Surface.CopyVisualStamps().Length));
                int reconciles = fixture.Runtime.ReconcileCount;
                int commits = fixture.Runtime.CommittedStampCount;
                AssertReady(fixture.Update(3d), fixture.Runtime);
                Assert.That(Read(fixture.Runtime.PaintTexture), Is.EqualTo(final));
                Assert.That(fixture.Runtime.ReconcileCount, Is.EqualTo(reconciles), "Settled, unchanged paint does not redraw.");
                Assert.That(fixture.Runtime.CommittedStampCount, Is.EqualTo(commits));
                Assert.That(State(fixture.Surface), Is.EqualTo(authority));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DifferentFramePartitionsAndCaptureFlushReachTheSameFinalImage()
        {
            RequireGraphics();
            using (var fixture = new Fixture())
            using (var coarse = new LabWatercolorPaintRuntime())
            using (var fine = new LabWatercolorPaintRuntime())
            using (var capture = new LabWatercolorPaintRuntime())
            {
                foreach (var runtime in new[] { coarse, fine, capture })
                    AssertReady(fixture.Update(runtime, 0d, true), runtime);
                fixture.AddRepeat();
                string authority = State(fixture.Surface);
                foreach (var runtime in new[] { coarse, fine, capture })
                    AssertReady(fixture.Update(runtime, 10d), runtime);
                for (int i = 1; i <= 13; i++) AssertReady(fixture.Update(coarse, 10d + i / 30d), coarse);
                for (int i = 1; i <= 26; i++) AssertReady(fixture.Update(fine, 10d + i / 60d), fine);
                AssertReady(fixture.Update(capture, 10.05d, true), capture);
                Color32[] expected = Read(coarse.PaintTexture);
                Assert.That(Read(fine.PaintTexture), Is.EqualTo(expected), "Final pigment must not depend on animation sample count.");
                Assert.That(Read(capture.PaintTexture), Is.EqualTo(expected), "Result/PNG capture flushes to the same settled result.");
                Assert.That(coarse.IsAnimating || fine.IsAnimating || capture.IsAnimating, Is.False);
                Assert.That(State(fixture.Surface), Is.EqualTo(authority));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ErasureAndTerritoryOverwriteUseOnlyTheCurrentAuthority()
        {
            RequireGraphics();
            using (var fixture = new Fixture())
            {
                AssertReady(fixture.Update(0d, true), fixture.Runtime);
                LabPaintVisualStamp[] oldBlue = fixture.Surface.CopyVisualStamps();
                var red = Enumerable.Repeat((Color32)LabPaintPalette.ColorFor(LabOrbElement.Fire), fixture.Surface.CellCount).ToArray();
                var redOwners = Enumerable.Repeat((byte)3, fixture.Surface.CellCount).ToArray();
                Assert.That(fixture.Surface.TryReplacePixels(fixture.Surface.Columns, fixture.Surface.Rows,
                    fixture.Surface.Revision + 1, red, redOwners, fixture.Surface.PaintCount + 1), Is.True);
                Assert.That(fixture.Surface.ReplaceVisualStamps(oldBlue), Is.True);
                fixture.SyncBaseline();
                string redState = State(fixture.Surface);
                AssertReady(fixture.Update(1d, true), fixture.Runtime);
                Color32[] overwritten = Read(fixture.Runtime.PaintTexture);
                Assert.That(overwritten.All(p => p.a > 0), Is.True, "100% gameplay coverage keeps visible pigment everywhere.");
                AssertAppearance(Read(fixture.Baseline.PaintTexture), overwritten);
                Assert.That(Read(fixture.Baseline.PainterTexture).All(p => p.r == 3), Is.True);
                Assert.That(State(fixture.Surface), Is.EqualTo(redState));

                Assert.That(fixture.Surface.TryReplacePixels(fixture.Surface.Columns, fixture.Surface.Rows,
                    fixture.Surface.Revision + 1, new Color32[fixture.Surface.CellCount],
                    new byte[fixture.Surface.CellCount], fixture.Surface.PaintCount), Is.True);
                Assert.That(fixture.Surface.ReplaceVisualStamps(oldBlue), Is.True);
                fixture.SyncBaseline();
                string erasedState = State(fixture.Surface);
                AssertReady(fixture.Update(2d, true), fixture.Runtime);
                Assert.That(Read(fixture.Runtime.PaintTexture).All(p => p.a == 0 && p.r == 0 && p.g == 0 && p.b == 0), Is.True,
                    "Retained descriptors cannot create pigment after authoritative erosion.");
                Assert.That(State(fixture.Surface), Is.EqualTo(erasedState));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullCheckpointIsBoundedAndRecordEvictionRebuildsWithoutStaleOptics()
        {
            RequireGraphics();
            using (var fixture = new Fixture())
            using (var fresh = new LabWatercolorPaintRuntime())
            {
                Color32 blue = LabPaintPalette.ColorFor(LabOrbElement.Water);
                var records = new LabPaintVisualStamp[LabPaintVisualStamp.MaximumCount];
                for (int i = 0; i < records.Length; i++) records[i] = new LabPaintVisualStamp {
                    sequence = (uint)(i + 1), center = i == 0 ? Vector3.forward : Vector3.back,
                    right = Vector3.right, radius = i == 0 ? .25f : .05f,
                    color = blue, painterSlot = 1, pattern = LabPaintPattern.Splatter, seed = (uint)i
                };
                Assert.That(fixture.Surface.ReplaceVisualStamps(records), Is.True);
                fixture.SyncBaseline();
                Assert.That(fixture.Update(0d), Is.False);
                Assert.That(fixture.Runtime.HasPendingWork, Is.True);
                int firstBatch = fixture.Runtime.CommittedStampCount;
                Assert.That(firstBatch, Is.LessThanOrEqualTo(LabHighResolutionPaintRuntime.StampPassBudget));
                Assert.That(fixture.Update(0d), Is.False);
                Assert.That(fixture.Runtime.CommittedStampCount, Is.EqualTo(firstBatch), "One frame cannot bypass the descriptor budget.");
                for (int frame = 0; frame < 20 && !fixture.Runtime.Ready; frame++)
                { yield return null; fixture.Update((frame + 1) / 60d); }
                Assert.That(fixture.Runtime.Ready, Is.True, fixture.Runtime.Error);
                Assert.That(fixture.Runtime.IsAnimating, Is.False, "Historical checkpoint marks do not restart a spread.");
                Assert.That(fixture.Runtime.CommittedStampCount, Is.EqualTo(records.Length));

                LabPaintVisualStamp[] shifted = records.Skip(1).Concat(new[] { new LabPaintVisualStamp {
                    sequence = 129, center = Vector3.back, right = Vector3.right, radius = .05f,
                    color = blue, painterSlot = 1, pattern = LabPaintPattern.Splatter, seed = 129
                } }).ToArray();
                Assert.That(fixture.Surface.ReplaceVisualStamps(shifted), Is.True);
                fixture.SyncBaseline();
                string authority = State(fixture.Surface);
                AssertReady(fixture.Update(2d, true), fixture.Runtime);
                AssertReady(fixture.Update(fresh, 2d, true), fresh);
                Assert.That(Read(fixture.Runtime.PaintTexture), Is.EqualTo(Read(fresh.PaintTexture)),
                    "An evicted front mark must not survive in the old optical cache.");
                Assert.That(fixture.Runtime.IsAnimating, Is.False);
                Assert.That(State(fixture.Surface), Is.EqualTo(authority));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DescriptorBurstRespectsTheActiveCapAndConvergesToASettledCheckpoint()
        {
            RequireGraphics();
            using (var fixture = new Fixture())
            using (var checkpoint = new LabWatercolorPaintRuntime())
            {
                fixture.Settings.maxActiveStains = 2;
                AssertReady(fixture.Update(0d, true), fixture.Runtime);
                LabPaintVisualStamp[] original = fixture.Surface.CopyVisualStamps();
                var burst = new LabPaintVisualStamp[original.Length + 16];
                Array.Copy(original, burst, original.Length);
                uint previous = original[original.Length - 1].sequence;
                for (int i = 0; i < 16; i++)
                {
                    LabPaintVisualStamp stamp = original[0];
                    stamp.sequence = previous + (uint)i + 1;
                    stamp.radius = .15f + i * .003f;
                    stamp.seed = 369u + (uint)i;
                    burst[original.Length + i] = stamp;
                }
                Assert.That(fixture.Surface.ReplaceVisualStamps(burst), Is.True);
                fixture.SyncBaseline();
                string authority = State(fixture.Surface);

                Assert.That(fixture.Update(1d), Is.False,
                    "A 16-descriptor burst is split across the per-frame replay budget.");
                Assert.That(fixture.Runtime.Error, Is.Null);
                Assert.That(fixture.Runtime.HasPendingWork, Is.True);
                Assert.That(fixture.Runtime.ActiveStampCount, Is.LessThanOrEqualTo(2));
                int firstCommits = fixture.Runtime.CommittedStampCount;
                int firstActive = fixture.Runtime.ActiveStampCount;
                Assert.That(firstCommits + firstActive - original.Length,
                    Is.LessThanOrEqualTo(LabWatercolorPaintRuntime.StampPassBudget));
                Assert.That(fixture.Update(1d), Is.False);
                Assert.That(fixture.Runtime.CommittedStampCount, Is.EqualTo(firstCommits));
                Assert.That(fixture.Runtime.ActiveStampCount, Is.EqualTo(firstActive),
                    "Repeated refresh in the same frame must not admit more burst work.");

                for (int frame = 0; frame < 6 && fixture.Runtime.HasPendingWork; frame++)
                {
                    yield return null;
                    fixture.Update(1d + (frame + 1) / 60d);
                    Assert.That(fixture.Runtime.Error, Is.Null);
                    Assert.That(fixture.Runtime.ActiveStampCount, Is.LessThanOrEqualTo(2));
                }
                Assert.That(fixture.Runtime.HasPendingWork, Is.False);
                Assert.That(fixture.Runtime.Ready, Is.True, fixture.Runtime.Error);
                AssertReady(fixture.Update(2d, true), fixture.Runtime);
                AssertReady(fixture.Update(checkpoint, 2d, true), checkpoint);
                Assert.That(fixture.Runtime.IsAnimating, Is.False);
                Assert.That(fixture.Runtime.CommittedStampCount, Is.EqualTo(burst.Length));
                Assert.That(Read(fixture.Runtime.PaintTexture), Is.EqualTo(Read(checkpoint.PaintTexture)),
                    "Capping active spread may shorten an effect, but cannot change final pigment.");
                Assert.That(State(fixture.Surface), Is.EqualTo(authority),
                    "This is a presentation-only descriptor burst, not a simulated fever gameplay round.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator QualityChangesAndLostOutputRecreateResourcesAndDisposeCannotReopenThem()
        {
            RequireGraphics();
            using (var fixture = new Fixture())
            {
                AssertReady(fixture.Update(0d, true), fixture.Runtime);
                string authority = State(fixture.Surface);
                RenderTexture first = fixture.Runtime.PaintTexture;
                first.Release();
                Assert.That(fixture.Runtime.Ready, Is.False);
                AssertReady(fixture.Update(1d, true), fixture.Runtime);
                Assert.That(fixture.Runtime.PaintTexture, Is.Not.SameAs(first));
                Assert.That(fixture.Runtime.IsAnimating, Is.False);
                fixture.SyncBaseline(2048);
                AssertReady(fixture.Update(2d, true), fixture.Runtime);
                RenderTexture large = fixture.Runtime.PaintTexture;
                Assert.That(large.width, Is.EqualTo(2048)); Assert.That(large.height, Is.EqualTo(1024));
                fixture.Runtime.Dispose();
                Assert.That(fixture.Runtime.Ready, Is.False);
                Assert.That(large == null || !large.IsCreated(), Is.True);
                Assert.That(fixture.Update(3d, true), Is.False);
                Assert.That(State(fixture.Surface), Is.EqualTo(authority));
            }
            yield return null;
        }

        private sealed class Fixture : IDisposable
        {
            public readonly LabPaintSurface Surface;
            public readonly LabHighResolutionPaintRuntime Baseline = new LabHighResolutionPaintRuntime();
            public readonly LabWatercolorPaintRuntime Runtime = new LabWatercolorPaintRuntime();
            public readonly LabWatercolorPaintSettings Settings = new LabWatercolorPaintSettings();
            private readonly Texture2D colors, owners;
            private readonly Color color;

            public Fixture(LabOrbElement element = LabOrbElement.Water)
            {
                color = LabPaintPalette.ColorFor(element);
                Surface = new LabPaintSurface(new LabPaintGrowthSettings {
                    Resolution = 0, Pattern = LabPaintPattern.Splatter, InitialPercent = 5f,
                    RepeatPercent = 3f, GrowthPercentPerSecond = 6f, DryingEnabled = false
                }, 369u);
                Assert.That(Surface.RegisterHit(Vector3.forward, color, 1, 1), Is.True);
                colors = Map(Surface.Columns, Surface.Rows);
                owners = Map(Surface.Columns, Surface.Rows);
                SyncBaseline();
            }
            public void AddRepeat()
            {
                int before = Surface.CopyVisualStamps().Length;
                Assert.That(Surface.RegisterHit(Vector3.forward, color, 1, 1), Is.True);
                Surface.Advance(1d);
                Assert.That(Surface.CopyVisualStamps().Length, Is.GreaterThan(before));
                SyncBaseline();
            }
            public void SyncBaseline(int columns = 1024)
            {
                var pixels = new Color32[Surface.CellCount]; var slots = new byte[Surface.CellCount];
                Surface.CopyPixels(pixels); colors.SetPixels32(pixels); colors.Apply(false, false);
                Surface.CopyPainterSlots(slots);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(slots[i], 0, 0, 255);
                owners.SetPixels32(pixels); owners.Apply(false, false);
                Assert.That(Baseline.Update(Surface, colors, owners, columns, true), Is.True, Baseline.Error);
            }
            public bool Update(double now, bool complete = false) => Update(Runtime, now, complete);
            public bool Update(LabWatercolorPaintRuntime runtime, double now, bool complete = false) =>
                runtime.Update(Surface, Baseline.PaintTexture, Baseline.PainterTexture, Settings, now, complete);
            public void Dispose()
            { Runtime.Dispose(); Baseline.Dispose(); Object.Destroy(colors); Object.Destroy(owners); }
        }

        private static Texture2D Map(int width, int height) => new Texture2D(width, height, TextureFormat.RGBA32, false, true) {
            filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp
        };
        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supports2DArrayTextures)
                Assert.Ignore("A real graphics device with texture-array support is required.");
            foreach (string resource in new[] { LabHighResolutionPaintRuntime.ShaderResource, "GourdPaint/WatercolorRuntime" })
            {
                Shader shader = Resources.Load<Shader>(resource);
                Assert.That(shader, Is.Not.Null, resource); Assert.That(shader.isSupported, Is.True, resource);
            }
        }
        private static void AssertReady(bool result, LabWatercolorPaintRuntime runtime)
        { Assert.That(result, Is.True, runtime.Error); Assert.That(runtime.Ready, Is.True, runtime.Error); }
        private static string State(LabPaintSurface surface)
        {
            var colors = new Color32[surface.CellCount]; var painters = new byte[surface.CellCount];
            surface.CopyPixels(colors); surface.CopyPainterSlots(painters);
            return LabPaintPublisher.Hash(colors, painters) + ":" + surface.Revision + ":" + surface.VisualVersion
                + ":" + surface.CoveredCount + ":" + surface.PaintCount + ":" + surface.PendingCoverage
                + ":" + LabPaintVisualStamp.Hash(LabPaintVisualStamp.Encode(surface.CopyVisualStamps()));
        }
        private static void AssertAppearance(Color32[] current, Color32[] candidate)
        {
            Assert.That(candidate.Length, Is.EqualTo(current.Length));
            int supportErrors = 0, premultErrors = 0, hueErrors = 0, hueSamples = 0, translucent = 0;
            for (int i = 0; i < current.Length; i++)
            {
                Color32 source = current[i], p = candidate[i];
                if ((source.a == 0) != (p.a == 0)) supportErrors++;
                if (p.r > p.a + 1 || p.g > p.a + 1 || p.b > p.a + 1) premultErrors++;
                if (p.a > 0 && p.a < 255) translucent++;
                if (p.a <= 32 || source.a <= 64) continue;
                float max = Math.Max(p.r, Math.Max(p.g, p.b));
                float sourceMax = Math.Max(source.r, Math.Max(source.g, source.b));
                if (max < 8 || sourceMax < 8) continue;
                hueSamples++;
                if (Math.Abs(p.r / max - source.r / sourceMax) >= .09f
                    || Math.Abs(p.g / max - source.g / sourceMax) >= .09f
                    || Math.Abs(p.b / max - source.b / sourceMax) >= .09f) hueErrors++;
            }
            Assert.That(supportErrors, Is.Zero, "A settled appearance cannot change the baseline optical support.");
            Assert.That(premultErrors, Is.Zero); Assert.That(hueErrors, Is.Zero, "Optical layers cannot mix in another element's hue.");
            Assert.That(hueSamples, Is.GreaterThan(100)); Assert.That(translucent, Is.GreaterThan(100));
        }
        private static Color32[] Read(RenderTexture texture)
        {
            Assert.That(texture != null && texture.IsCreated(), Is.True);
            RenderTexture previous = RenderTexture.active;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = texture;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply(false, false);
                return copy.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.Destroy(copy); }
        }
    }
}
