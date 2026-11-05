using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Real GPU replay checks; these do not claim device/network performance validation.</summary>
    public sealed class LabWatercolorStampRuntimeTests
    {
        [UnityTest]
        public IEnumerator OrderedMulticolorLayersReplayIdenticallyAndDoNotDarkenOnRefresh()
        {
            RequireGraphics();
            var settings = new LabWatercolorStampSettings { revealSeconds = 0f };
            using (var journal = Journal(settings))
            using (var immediate = new LabWatercolorStampRuntime())
            using (var bounded = new LabWatercolorStampRuntime())
            {
                SeedRectangle(journal, Color.red, 1, 22, 30, 10, 22);
                SeedRectangle(journal, Color.blue, 2, 30, 38, 10, 22);
                SeedRectangle(journal, Color.green, 3, 22, 38, 22, 26);
                yield return Render(immediate, journal, settings, true);
                yield return Render(bounded, journal, settings, false);
                Color32[] expected = Read(immediate.PaintTexture);
                Assert.That(Read(bounded.PaintTexture), Is.EqualTo(expected), "Journal batching cannot change source-over density.");
                Assert.That(expected.Any(c => c.a > 0 && c.r > 5 && c.b > 5), Is.True,
                    "A blue brush crossing prior red paint retains both color layers.");
                ulong rendered = immediate.RenderedThrough;
                int draws = immediate.DrawCount;
                for (int i = 0; i < 4; i++) Assert.That(immediate.Update(journal, null, 1024, settings, 5d, true), Is.True);
                Assert.That(Read(immediate.PaintTexture), Is.EqualTo(expected));
                Assert.That(immediate.DrawCount, Is.EqualTo(draws));
                Assert.That(immediate.RenderedThrough, Is.EqualTo(rendered));
            }
        }

        [UnityTest]
        public IEnumerator DryingDeletesEveryOldLayerBeforeRepaintingAndGpuLossRestoresExactly()
        {
            RequireGraphics();
            var settings = new LabWatercolorStampSettings { revealSeconds = 0f };
            using (var journal = Journal(settings))
            using (var runtime = new LabWatercolorStampRuntime())
            {
                uint red = journal.BeginStroke(Stamp(Color.red, 1));
                int[] cells = Rectangle(24, 40, 12, 20);
                uint revision = 0;
                foreach (int c in cells) Assert.That(journal.AppendPaint(red, c, ++revision), Is.True);
                uint green = journal.BeginStroke(Stamp(Color.green, 2));
                // Green overlaps red without changing gameplay coverage.
                Assert.That(green, Is.Not.EqualTo(0));
                yield return Render(runtime, journal, settings, true);
                Assert.That(Read(runtime.PaintTexture).Any(c => c.r > 0 && c.g > 0), Is.True);
                foreach (int c in cells) Assert.That(journal.AppendErase(c, ++revision), Is.True);
                yield return Render(runtime, journal, settings, true);
                Assert.That(Read(runtime.PaintTexture).All(c => c.r == 0 && c.g == 0 && c.b == 0 && c.a == 0), Is.True);
                uint blue = journal.BeginStroke(Stamp(Color.blue, 3));
                foreach (int c in cells) Assert.That(journal.AppendPaint(blue, c, ++revision), Is.True);
                yield return Render(runtime, journal, settings, true);
                Color32[] expected = Read(runtime.PaintTexture);
                Assert.That(expected.Where(c => c.a > 0).All(c => c.r == 0 && c.g == 0 && c.b > 0), Is.True,
                    "Erased lower layers must not resurrect under fresh blue.");
                runtime.PaintTexture.Release();
                Assert.That(runtime.Ready, Is.False);
                yield return Render(runtime, journal, settings, true);
                Assert.That(Read(runtime.PaintTexture), Is.EqualTo(expected), "Resource loss replays all chronological operations.");
            }
        }

        [UnityTest]
        public IEnumerator OldStrokeCanGrowAfterMoreThan128StrokesWithoutRetainingAllDescriptorsInRam()
        {
            RequireGraphics();
            var settings = new LabWatercolorStampSettings { revealSeconds = 0f };
            using (var journal = Journal(settings))
            using (var runtime = new LabWatercolorStampRuntime())
            using (var replay = new LabWatercolorStampRuntime())
            {
                uint old = journal.BeginStroke(Stamp(Color.blue, 1));
                Assert.That(journal.AppendPaint(old, 16 * 64 + 32, 1), Is.True);
                for (int i = 0; i < 132; i++)
                {
                    LabPaintVisualStamp s = Stamp(Color.red, (uint)(i + 2));
                    s.center = Vector3.left;
                    journal.BeginStroke(s);
                }
                Assert.That(journal.AppendPaint(old, 16 * 64 + 33, 2), Is.True);
                yield return Render(runtime, journal, settings, false);
                Assert.That(runtime.CachedDescriptorCount, Is.LessThanOrEqualTo(128));
                yield return Render(replay, journal, settings, true);
                Assert.That(Read(runtime.PaintTexture), Is.EqualTo(Read(replay.PaintTexture)));
                Assert.That(runtime.AppliedThrough, Is.EqualTo(journal.LastSequence));
                Assert.That(runtime.RenderedThrough, Is.EqualTo(journal.LastSequence));
            }
        }

        [UnityTest]
        public IEnumerator RevealUsesPresentationTimeAndCaptureSettlesToTheSameArtwork()
        {
            RequireGraphics();
            var settings = new LabWatercolorStampSettings { revealSeconds = .4f };
            using (var journal = Journal(settings))
            using (var live = new LabWatercolorStampRuntime())
            using (var reference = new LabWatercolorStampRuntime())
            {
                yield return Render(live, journal, settings, true);
                SeedRectangle(journal, Color.blue, 1, 24, 40, 12, 20);
                live.Update(journal, null, 1024, settings, 10d);
                Color32[] first = Read(live.PaintTexture);
                live.Update(journal, null, 1024, settings, 10d);
                Assert.That(Read(live.PaintTexture), Is.EqualTo(first), "A held presentation clock cannot progress or add pigment.");
                Assert.That(journal.AppendPaint(1, 20 * 64 + 40, (uint)(journal.LastSequence + 1)), Is.True);
                live.Update(journal, null, 1024, settings, 10.2d);
                Assert.That(Read(live.PaintTexture), Is.Not.EqualTo(first));
                yield return Render(live, journal, settings, true);
                yield return Render(reference, journal, settings, true);
                Assert.That(Read(live.PaintTexture), Is.EqualTo(Read(reference.PaintTexture)));
                Assert.That(live.IsAnimating, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator CapturedBrushVersionSurvivesCloningAndRejectsADifferentInstalledBrushPack()
        {
            RequireGraphics();
            var captured = LabWatercolorStampSettings.CaptureDefault();
            string before = captured.Version;
            var clone = captured.Clone();
            Assert.That(clone.Version, Is.EqualTo(before));
            var pin = typeof(LabWatercolorStampSettings).GetField("pinnedBrushVersion", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pin, Is.Not.Null);
            // Simulate a round pinned to another package without changing any project asset.
            pin.SetValue(clone, new string('a', 64));
            Assert.That(clone.SanitizedCopy().Version, Is.EqualTo(clone.Version));
            Assert.That(captured.Version, Is.EqualTo(before), "A captured copy never mutates its source or the authoring asset.");
            using (var journal = Journal(clone))
            using (var runtime = new LabWatercolorStampRuntime())
            {
                Assert.That(runtime.Update(journal, null, 1024, clone, 0d, true), Is.False);
                Assert.That(runtime.Error, Does.Contain("brush data changed during this round"));
                Assert.That(runtime.AppliedThrough, Is.Zero);
                Assert.That(runtime.PaintTexture, Is.Null, "Incompatible brush data cannot be mixed into an existing round image.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChangedV2OpticsCannotReinterpretTheAlreadyCapturedRound()
        {
            RequireGraphics();
            var captured = new LabWatercolorStampSettings().Clone();
            using (var journal = Journal(captured))
            {
                SeedRectangle(journal, LabPaintPalette.ColorFor(LabOrbElement.Water), 1, 24, 40, 12, 20);
                ulong before = journal.LastSequence;
                foreach (string field in new[] { "pigmentContrast", "pigmentSaturation", "granulationStrength", "granulationScale", "rimBreakup" })
                {
                    var changed = captured.Clone();
                    var member = typeof(LabWatercolorStampSettings).GetField(field);
                    member.SetValue(changed, member.GetCustomAttribute<UnityEngine.RangeAttribute>().max);
                    using (var runtime = new LabWatercolorStampRuntime())
                    {
                        Assert.That(runtime.Update(journal, null, 1024, changed, 0d, true), Is.False, field);
                        Assert.That(runtime.Error, Does.Contain("version differs"), field);
                        Assert.That(runtime.AppliedThrough, Is.Zero);
                        Assert.That(runtime.PaintTexture, Is.Null);
                    }
                }
                Assert.That(journal.LastSequence, Is.EqualTo(before), "A failed visual configuration cannot append or change authority operations.");
            }
            yield return null;
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator EditorStudyInjectionCannotChangeTheDefaultShaderReplayOrOwnItsBrushAssets()
        {
            RequireGraphics();
            var settings = new LabWatercolorStampSettings { revealSeconds = 0f };
            string version = settings.Version;
            Shader shader = Resources.Load<Shader>(LabWatercolorStampRuntime.ShaderResource);
            Texture2D[] supplied = new[] { "Round", "Growth", "Overlap" }
                .Select(name => Resources.Load<Texture2D>("GourdPaint/WatercolorBrushes/" + name)).ToArray();
            Texture2D originalRound = supplied[0];
            int bindings = 0;
            using (var journal = Journal(settings))
            using (var normal = new LabWatercolorStampRuntime())
            using (var study = new LabWatercolorStampRuntime(shader, supplied, block => bindings++, "test-neutral-editor-study-v1"))
            {
                supplied[0] = null; // The study owns its array copy, not the supplied asset or array.
                SeedRectangle(journal, Color.red, 11, 23, 33, 10, 22);
                SeedRectangle(journal, Color.blue, 12, 28, 39, 22, 27);
                yield return Render(normal, journal, settings, true);
                yield return Render(study, journal, settings, true);
                Assert.That(study.Error, Is.Null);
                Assert.That(bindings, Is.GreaterThan(0), "The explicit comparison hook must actually execute.");
                Assert.That(Read(study.PaintTexture), Is.EqualTo(Read(normal.PaintTexture)),
                    "A neutral Editor study must preserve every premultiplied output byte of the shipped/default path.");
                Assert.That(normal.StudyIdentity, Is.Null);
                Assert.That(study.StudyIdentity, Is.EqualTo("test-neutral-editor-study-v1"));
                Assert.That(journal.StyleVersion, Is.EqualTo(version), "Study identity cannot rewrite the source ledger's round version.");
                Assert.That(settings.Version, Is.EqualTo(version));
            }
            Assert.That(originalRound != null, Is.True, "Disposing a comparison never destroys a supplied shared brush asset.");
        }

        [UnityTest]
        public IEnumerator EditorV3CapsEraseRepaintRecoverAndReplayWithoutTintDrift()
        {
            RequireGraphics();
            Color32[] lowerCap = null;
            foreach (float cap in new[] { .75f, .85f })
            using (var runtime = V3StudyRuntime(cap, out LabWatercolorStampSettings settings))
            using (var reference = V3StudyRuntime(cap, out _))
            using (var journal = Journal(settings))
            using (var clean = Journal(settings))
            {
                Color red = LabPaintPalette.ColorFor(LabOrbElement.Fire);
                Color green = LabPaintPalette.ColorFor(LabOrbElement.Wood);
                Color blue = LabPaintPalette.ColorFor(LabOrbElement.Water);
                int[] cells = Rectangle(24, 40, 12, 20);
                SeedRectangle(journal, red, 11, 24, 40, 12, 20);
                Assert.That(clean.BeginStroke(Stamp(red, 11)), Is.EqualTo(1u));
                // More than DrawBudget strokes exercise genuinely bounded replay.
                // The clean reference reserves identical descriptor IDs without paint.
                for (uint i = 0; i < 10; i++)
                {
                    LabPaintVisualStamp stamp = Stamp(i % 2 == 0 ? green : red, 12 + i);
                    uint stroke = journal.BeginStroke(stamp);
                    Assert.That(stroke, Is.Not.Zero);
                    Assert.That(clean.BeginStroke(stamp), Is.EqualTo(stroke));
                }
                yield return Render(runtime, journal, settings, false);
                Color32[] layered = Read(runtime.PaintTexture);
                Assert.That(layered.Any(c => c.a > 0), Is.True, "V3 must render actual baked pigment.");
                Assert.That(runtime.DrawCount, Is.GreaterThan(LabWatercolorStampRuntime.DrawBudget));
                yield return Render(reference, journal, settings, true);
                Assert.That(Read(reference.PaintTexture), Is.EqualTo(layered),
                    "V3 multicolor layers must match across complete and bounded draw batches.");
                foreach (int cell in cells)
                    Assert.That(journal.AppendErase(cell, (uint)(journal.LastSequence + 1)), Is.True);
                yield return Render(runtime, journal, settings, true);
                Assert.That(Read(runtime.PaintTexture).All(c => c.Equals(new Color32())), Is.True,
                    "V3 erase removes every accumulated lower layer at cap " + cap);

                SeedRectangle(journal, blue, 71, 24, 40, 12, 20);
                SeedRectangle(clean, blue, 71, 24, 40, 12, 20);
                yield return Render(runtime, journal, settings, false);
                yield return Render(reference, clean, settings, true);
                Color32[] expected = Read(reference.PaintTexture);
                Assert.That(expected.Any(c => c.a > 0 && c.b > c.r), Is.True);
                Assert.That(Read(runtime.PaintTexture), Is.EqualTo(expected),
                    "Erased red/green must not tint fresh V3 blue at cap " + cap);
                if (lowerCap == null) lowerCap = expected;
                else Assert.That(expected, Is.Not.EqualTo(lowerCap), "Both V3 opacity caps must actually bind into GPU output.");

                // Switching the reference journal forces a complete chronological replay.
                yield return Render(reference, journal, settings, true);
                Assert.That(Read(reference.PaintTexture), Is.EqualTo(expected), "V3 full and incremental replay bytes must agree.");
                runtime.PaintTexture.Release();
                Assert.That(runtime.Ready, Is.False);
                yield return Render(runtime, journal, settings, true);
                Assert.That(Read(runtime.PaintTexture), Is.EqualTo(expected), "V3 GPU resource loss restores the exact artwork.");
                int draws = runtime.DrawCount;
                for (int i = 0; i < 4; i++)
                    Assert.That(runtime.Update(journal, null, 1024, settings, 30d + i, true), Is.True);
                Assert.That(Read(runtime.PaintTexture), Is.EqualTo(expected), "Refreshing cannot accumulate pigment or drift tint.");
                Assert.That(runtime.DrawCount, Is.EqualTo(draws));
                Assert.That(runtime.AppliedThrough, Is.EqualTo(journal.LastSequence));
                Assert.That(runtime.RenderedThrough, Is.EqualTo(journal.LastSequence));
            }
        }

        private static LabWatercolorStampRuntime V3StudyRuntime(float cap, out LabWatercolorStampSettings settings)
        {
            const string directory = "Assets/Editor/WatercolorPaint/V3/";
            Object profile = UnityEditor.AssetDatabase.LoadMainAssetAtPath(directory + "Data/V3StudyProfile.asset");
            Shader shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(directory + "Shaders/WatercolorStampV3.shader");
            Assert.That(profile, Is.Not.Null, "The actual baked V3 profile is required; this test cannot use a substitute.");
            Assert.That(shader, Is.Not.Null, "The actual V3 GPU shader is required.");
            Assert.That(shader.name, Is.EqualTo("Hidden/C6Lab/WatercolorBrushStampsV3"));
            MethodInfo capture = profile.GetType().GetMethod("Snapshot", new[] { typeof(float) });
            Assert.That(capture, Is.Not.Null);
            object snapshot = capture.Invoke(profile, new object[] { cap });
            Assert.That(snapshot, Is.Not.Null);
            Type type = snapshot.GetType();
            PropertyInfo baseSettings = type.GetProperty("BaseStampSettings");
            PropertyInfo brushAssets = type.GetProperty("Brushes");
            PropertyInfo version = type.GetProperty("Version");
            MethodInfo apply = type.GetMethod("ApplyStamp", new[] { typeof(MaterialPropertyBlock) });
            Assert.That(baseSettings, Is.Not.Null); Assert.That(brushAssets, Is.Not.Null);
            Assert.That(version, Is.Not.Null); Assert.That(apply, Is.Not.Null);
            settings = (LabWatercolorStampSettings)baseSettings.GetValue(snapshot);
            Texture2D[] brushes = (Texture2D[])brushAssets.GetValue(snapshot);
            Assert.That(settings, Is.Not.Null);
            Assert.That(brushes, Has.Length.EqualTo(3));
            Assert.That(brushes.All(texture => texture != null), Is.True, "All three actual V3 baked brushes are required.");
            Assert.That(brushes.All(texture => UnityEditor.AssetDatabase.GetAssetPath(texture).StartsWith(directory)), Is.True);
            var bind = (Action<MaterialPropertyBlock>)Delegate.CreateDelegate(typeof(Action<MaterialPropertyBlock>), snapshot, apply);
            return new LabWatercolorStampRuntime(shader, brushes, bind, (string)version.GetValue(snapshot));
        }
#endif

        private static IEnumerator Render(LabWatercolorStampRuntime runtime, LabPaintArtJournal journal,
            LabWatercolorStampSettings settings, bool complete)
        {
            for (int i = 0; i < 200; i++)
            {
                runtime.Update(journal, null, 1024, settings, 20d + i / 60d, complete);
                Assert.That(runtime.Error, Is.Null);
                if (runtime.Ready) yield break;
                yield return null;
            }
            Assert.Fail("Watercolor journal did not finish GPU replay within 200 frames.");
        }
        private static LabPaintArtJournal Journal(LabWatercolorStampSettings settings)
            => new LabPaintArtJournal(Path.Combine(Application.temporaryCachePath, "wc-test-" + Guid.NewGuid().ToString("N")),
                Guid.NewGuid().ToString("N"), 64, 32, settings.Version);
        private static LabPaintVisualStamp Stamp(Color color, uint seed) => new LabPaintVisualStamp
        {
            sequence = 1, center = Vector3.right, right = Vector3.forward, radius = 1.2f,
            color = color, painterSlot = 1, pattern = LabPaintPattern.Splatter, seed = seed
        };
        private static void SeedRectangle(LabPaintArtJournal journal, Color color, uint seed,
            int minX, int maxX, int minY, int maxY)
        {
            uint id = journal.BeginStroke(Stamp(color, seed));
            Assert.That(id, Is.Not.EqualTo(0));
            foreach (int cell in Rectangle(minX, maxX, minY, maxY))
                Assert.That(journal.AppendPaint(id, cell, (uint)(journal.LastSequence + 1)), Is.True);
        }
        private static int[] Rectangle(int minX, int maxX, int minY, int maxY)
            => Enumerable.Range(minY, maxY - minY).SelectMany(y => Enumerable.Range(minX, maxX - minX).Select(x => y * 64 + x)).ToArray();
        private static Color32[] Read(RenderTexture source)
        {
            var old = RenderTexture.active;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = source; copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); copy.Apply();
                return copy.GetPixels32();
            }
            finally { RenderTexture.active = old; Object.Destroy(copy); }
        }
        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Requires a real graphics device for watercolor GPU replay.");
            Assert.That(Resources.Load<Shader>(LabWatercolorStampRuntime.ShaderResource), Is.Not.Null);
            foreach (string name in new[] { "Round", "Growth", "Overlap" })
                Assert.That(Resources.Load<Texture2D>("GourdPaint/WatercolorBrushes/" + name), Is.Not.Null);
        }
    }
}
