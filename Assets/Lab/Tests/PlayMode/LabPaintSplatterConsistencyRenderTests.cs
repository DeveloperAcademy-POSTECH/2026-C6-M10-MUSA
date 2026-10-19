using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// Deterministic surface fixtures rendered through the saved shell and production shader.
    /// These are not projectile, session, movement or device play tests. Their full maps expose
    /// hidden-side fragmentation which a front-view screenshot alone cannot establish.
    /// </summary>
    public sealed class LabPaintSplatterConsistencyRenderTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool Baseline => Environment.GetEnvironmentVariable("C6_PAINT_VISUAL_BASELINE") == "1";
        private static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Logs/PaintSplatterConsistency"));

        private static LabPaintGrowthSettings Settings() => new LabPaintGrowthSettings {
            Resolution = 2, Pattern = LabPaintPattern.Splatter, InitialPercent = 2f, RepeatPercent = 4f,
            GrowthPercentPerSecond = 6f, ClusterCompactness = .85f, ClusterIrregularity = .25f,
            Raggedness = .55f, FillChancePercent = 60f, VerticalWeight = 1.35f
        };

        [UnityTearDown]
        public IEnumerator Unload()
        {
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Splatter consistency cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator SavedDefaultOneAndHalfPercentAddsSmallerBurstsWithoutChangingTheFirstSplatter()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device: no saved-shell verification was performed.");
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint != null && paint.IsReady, Is.True);
            var source = Resources.Load<LabConfig>("LabConfig");
            var settings = source.PaintGrowthSettings;
            Assert.That(settings.Resolution, Is.EqualTo(2));
            Assert.That(settings.InitialPercent, Is.EqualTo(2f));
            Assert.That(settings.RepeatPercent, Is.EqualTo(1.5f));
            var surface = new LabPaintSurface(settings, 123);
            var evidence = new Evidence { fixture = "saved-default-first-2-repeat-1_5-percent", baseline = false };
            surface.RegisterHit(Vector3.forward, ColorFor(1), 1, 1);
            Assert.That(surface.CoveredCount, Is.EqualTo(655));
            using (var rig = new ShellRig(paint))
            {
                rig.Capture(surface, "default1_5-first", false, true);
                for (int hit = 1; hit <= 3; hit++)
                {
                    Color32[] before = Pixels(surface);
                    byte[] owners = Owners(surface);
                    int painter = hit + 1;
                    surface.RegisterHit(Vector3.forward, ColorFor(painter), (ulong)painter, painter);
                    surface.Advance(1d);
                    Color32[] after = Pixels(surface);
                    int[] added = Added(before, after);
                    Assert.That(added.Length, Is.InRange(491, 492));
                    Assert.That(surface.CoveredCount, Is.EqualTo((int)decimal.Floor((2m + hit * 1.5m) * surface.CellCount / 100m)));
                    Assert.That(added.All(i => after[i].Equals((Color32)ColorFor(painter))
                        && surface.GetPainterSlot(i) == painter), Is.True);
                    AssertOldPaintUnchanged(surface, before, owners);
                    Assert.That(surface.PendingCoverage, Is.LessThan(1d / surface.CellCount));
                    evidence.shapes.Add(Measure(surface, added, 123, hit, "repeat-default1_5"));
                    rig.Capture(surface, $"default1_5-repeat{hit}", false, true);
                }
            }
            WriteEvidence("default1_5-evidence.json", evidence);
        }

        [UnityTest]
        public IEnumerator SavedShellShowsTheSameSplatterFamilyForTwoAndFourPercentHits()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device: no saved-shell or shader verification was performed.");
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint != null && paint.IsReady, Is.True, paint == null ? "No live paint binding" : paint.InitializationError);
            var evidence = new Evidence { fixture = "two-and-four-percent-first-and-three-repeats", baseline = Baseline };
            using (var rig = new ShellRig(paint))
            {
                foreach (uint seed in new uint[] { 7, 123, 777 })
                {
                    var surface = new LabPaintSurface(Settings(), seed);
                    var geometricControl = new LabPaintSurface(Settings(), seed);
                    foreach (LabPaintSurface current in new[] { surface, geometricControl })
                        Assert.That(current.RegisterHit(Vector3.forward, ColorFor(1), 1, 1), Is.True);
                    Color32[] first = Pixels(surface);
                    Assert.That(first, Is.EqualTo(Pixels(geometricControl)));
                    Assert.That(surface.CoveredCount, Is.EqualTo(655), "The first 2% splatter retains its actual integer area.");
                    evidence.shapes.Add(Measure(surface, Enumerable.Range(0, first.Length).Where(i => first[i].a != 0).ToArray(),
                        seed, 0, "first-splatter"));
                    rig.Capture(surface, $"seed{seed}-first", false, true);

                    // A selector-only control: both surfaces have exactly the same saved first
                    // SPLATTER pixels, RNG, supplies and anchor inputs. Only later growth's
                    // pattern family differs. This must distinguish mask-shaped repeat growth
                    // from #208's pattern-agnostic smooth geometry, without a golden map hash.
                    typeof(LabPaintSurface).GetField("pattern", Private).SetValue(geometricControl, LabPaintPattern.Legacy);
                    for (int hit = 1; hit <= 3; hit++)
                    {
                        Color32[] before = Pixels(surface);
                        byte[] oldOwners = Owners(surface);
                        int painter = hit + 1;
                        foreach (LabPaintSurface current in new[] { surface, geometricControl })
                        {
                            Assert.That(current.RegisterHit(Vector3.forward, ColorFor(painter), (ulong)painter, painter), Is.True);
                            current.Advance(1d);
                        }
                        Color32[] after = Pixels(surface);
                        AssertOldPaintUnchanged(surface, before, oldOwners);
                        int[] added = Added(before, after);
                        Assert.That(surface.CoveredCount, Is.EqualTo((int)Math.Floor((2m + hit * 4m) * surface.CellCount / 100m)));
                        Assert.That(surface.CoveredCount, Is.EqualTo(geometricControl.CoveredCount));
                        Assert.That(surface.PendingCoverage, Is.LessThan(1d / surface.CellCount),
                            "Only legitimate sub-cell credit may remain after the supplied integer area is spent.");
                        Assert.That(added.All(i => after[i].Equals((Color32)ColorFor(painter)) && surface.GetPainterSlot(i) == painter), Is.True);
                        Shape shape = Measure(surface, added, seed, hit, "repeat-splatter");
                        evidence.shapes.Add(shape);
                        rig.Capture(surface, $"seed{seed}-repeat{hit}", false, true);
                        WriteEvidence("first-repeat-evidence.json", evidence);
                        if (!Baseline && hit == 1)
                        {
                            Assert.That(shape.largestFraction, Is.GreaterThanOrEqualTo(.9f), "A first unobstructed repeat stays predominantly one connected cluster.");
                            Assert.That(shape.rmsOverDiskRadius, Is.LessThan(1.6f), "Mask detail must not distribute the new supply around the whole old perimeter.");
                            int changed = after.Zip(Pixels(geometricControl), (a, b) => a.Equals(b) ? 0 : 1).Sum();
                            Assert.That(changed, Is.GreaterThan(64), "The selected SPLATTER must affect repeat geometry, rather than retaining the pattern-agnostic smooth disk.");
                        }
                        if (seed == 123 && hit == 3)
                        {
                            rig.Capture(surface, "seed123-repeat3-player-patterns-on", true, true);
                            rig.Capture(geometricControl, "seed123-repeat3-geometric-control", false, true);
                        }
                    }
                }
            }
            WriteEvidence("first-repeat-evidence.json", evidence);
        }

        [UnityTest]
        public IEnumerator VariedWhiteAndPaintedHitsRenderFiveColorsThroughTheFinalActualCell()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device: no saved-shell or shader verification was performed.");
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint != null && paint.IsReady, Is.True, paint == null ? "No live paint binding" : paint.InitializationError);
            var surface = new LabPaintSurface(Settings(), 211);
            var evidence = new Evidence { fixture = "varied-local-directions-five-colors-2-and-4-percent", baseline = Baseline };
            int nextMilestone = 1, whiteHits = 0, repeats = 0;
            using (var rig = new ShellRig(paint))
            {
                for (int hit = 0; hit < 64 && !surface.IsComplete; hit++)
                {
                    Color32[] before = Pixels(surface);
                    byte[] oldOwners = Owners(surface);
                    // Synthetic directions cover the sphere and alternate white and painted
                    // branches intentionally; they are not recorded physical impacts.
                    bool painted = hit % 3 == 1 && surface.CoveredCount > 0;
                    Vector3 desired = Quaternion.Euler(hit * 17f, hit * 137.50776f, 0) * Vector3.forward;
                    int target = NearestCell(surface, before, desired, painted);
                    int painter = hit % 5 + 1;
                    if (painted) repeats++; else whiteHits++;
                    Assert.That(surface.RegisterHit(Direction(surface, target), ColorFor(painter), (ulong)painter, painter), Is.True);
                    int immediate = surface.CoveredCount;
                    surface.Advance(1d);
                    AssertOldPaintUnchanged(surface, before, oldOwners);
                    Color32[] after = Pixels(surface);
                    int[] added = Added(before, after);
                    Assert.That(added.Length, Is.GreaterThan(0));
                    Assert.That(after.Count(p => p.a != 0), Is.EqualTo(surface.CoveredCount));
                    Assert.That(after.Where(p => p.a != 0).All(p => p.a == 255), Is.True);
                    // Fractional credit from earlier supplies can finish a cell here; assert
                    // each committed color and P pair stays atomic, rather than assuming
                    // every new cell must belong to this single synthetic event.
                    foreach (int cell in added)
                    {
                        int slot = surface.GetPainterSlot(cell);
                        Assert.That(slot, Is.InRange(1, 5));
                        Assert.That(after[cell], Is.EqualTo((Color32)ColorFor(slot)));
                    }
                    Shape shape = Measure(surface, added, 211, hit + 1, painted ? "painted-repeat" : "white-first");
                    shape.immediateCells = immediate - before.Count(p => p.a != 0);
                    evidence.shapes.Add(shape);
                    WriteEvidence("mixed-hit-evidence.json", evidence);
                    if (hit < 6) rig.Capture(surface, $"mixed-hit{hit + 1:00}-{shape.kind}", false, false);
                    while (nextMilestone <= 4 && surface.Coverage >= nextMilestone * .25f)
                    {
                        rig.Capture(surface, $"mixed-coverage{nextMilestone * 25:000}", false, true);
                        rig.Capture(surface, $"mixed-coverage{nextMilestone * 25:000}-player-patterns-on", true, true);
                        nextMilestone++;
                    }
                }
                Assert.That(whiteHits, Is.GreaterThan(3));
                Assert.That(repeats, Is.GreaterThan(3));
                Assert.That(surface.IsComplete, Is.True, "Varied directions must reach the last actual blank cell without a viewer-only completion shortcut.");
                Assert.That(surface.CoveredCount, Is.EqualTo(surface.CellCount));
                Assert.That(surface.PendingCoverage, Is.Zero);
                Assert.That(Owners(surface).Distinct().OrderBy(x => x), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));
                for (int slot = 1; slot <= 5; slot++)
                    Assert.That(Owners(surface).Count(p => p == slot), Is.GreaterThan(100), "Each participant must have a real visible contribution, rather than only pending credit.");
            }
            WriteEvidence("mixed-hit-evidence.json", evidence);
        }

        [UnityTest]
        public IEnumerator ANewOverlappingSplatterDoesNotSpendAnOlderPendingColorsClusterElsewhere()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device: no saved-shell or shader verification was performed.");
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint != null && paint.IsReady, Is.True, paint == null ? "No live paint binding" : paint.InitializationError);
            var surface = new LabPaintSurface(Settings(), 211);
            surface.RegisterHit(Vector3.forward, ColorFor(1), 1, 1);
            Color32[] original = Pixels(surface); byte[] originalOwners = Owners(surface);
            // Pick a white neighbor of the actual hit's connected component, rather than
            // a detached first-impact satellite. The following first footprint must merge
            // into the component which already owns the unspent repeat supply.
            int center = NearestCell(surface, original, Vector3.forward, true);
            var component = new HashSet<int> { center }; var queue = new Queue<int>(); queue.Enqueue(center);
            while (queue.Count > 0)
                foreach (int next in Neighbors(surface, queue.Dequeue()))
                    if (original[next].a != 0 && component.Add(next)) queue.Enqueue(next);
            int target = -1; float best = float.NegativeInfinity;
            foreach (int cell in component)
                foreach (int next in Neighbors(surface, cell)) if (original[next].a == 0)
                {
                    float dot = Vector3.Dot(Vector3.forward, Direction(surface, next));
                    if (dot > best) { best = dot; target = next; }
                }
            Assert.That(target, Is.GreaterThanOrEqualTo(0));
            Assert.That(surface.RegisterHit(Vector3.forward, ColorFor(2), 2, 2), Is.True);
            Assert.That(surface.RegisterHit(Direction(surface, target), ColorFor(3), 3, 3), Is.True);
            int repeatPixelsImmediately = Owners(surface).Count(p => p == 2);
            using (var rig = new ShellRig(paint))
            {
                rig.Capture(surface, "queued-repeat-new-white-immediate", false, true);
                surface.Advance(1d);
                rig.Capture(surface, "queued-repeat-new-white-after-growth", false, true);
            }
            AssertOldPaintUnchanged(surface, original, originalOwners);
            Assert.That(surface.PendingCoverage, Is.LessThan(1d / surface.CellCount),
                "Unspent fractional ink must survive without painting an unfunded cell.");
            Assert.That(Owners(surface).Count(p => p == 2), Is.GreaterThanOrEqualTo(1310));
            Assert.That(Owners(surface).Count(p => p == 3), Is.GreaterThan(0));
            TestContext.WriteLine($"SPLATTER_QUEUED_MERGE baseline={Baseline} olderRepeatPixelsBeforeAdvance={repeatPixelsImmediately} covered={surface.CoveredCount}");
            if (!Baseline)
                Assert.That(repeatPixelsImmediately, Is.Zero, "A fresh SPLATTER cannot spend the older repeat's color/ownership on its unrelated immediate footprint.");
        }

        private static Color ColorFor(int slot) => LabPaintPalette.ColorFor((LabOrbElement)slot);
        private static Color32[] Pixels(LabPaintSurface surface) { var value = new Color32[surface.CellCount]; surface.CopyPixels(value); return value; }
        private static byte[] Owners(LabPaintSurface surface) { var value = new byte[surface.CellCount]; surface.CopyPainterSlots(value); return value; }
        private static int[] Added(Color32[] before, Color32[] after) => Enumerable.Range(0, before.Length).Where(i => before[i].a == 0 && after[i].a != 0).ToArray();
        private static void AssertOldPaintUnchanged(LabPaintSurface surface, Color32[] before, byte[] owners)
        {
            Color32[] after = Pixels(surface);
            for (int i = 0; i < before.Length; i++) if (before[i].a != 0)
            { Assert.That(after[i], Is.EqualTo(before[i]), "Existing pigment was overwritten at cell " + i); Assert.That(surface.GetPainterSlot(i), Is.EqualTo(owners[i])); }
        }
        private static int NearestCell(LabPaintSurface s, Color32[] map, Vector3 desired, bool painted)
        {
            int selected = -1; float best = float.NegativeInfinity;
            for (int i = 0; i < map.Length; i++) if ((map[i].a != 0) == painted)
            { float dot = Vector3.Dot(desired, Direction(s, i)); if (dot > best) { best = dot; selected = i; } }
            Assert.That(selected, Is.GreaterThanOrEqualTo(0)); return selected;
        }
        private static Vector3 Direction(LabPaintSurface s, int cell)
        {
            float y = 1f - (cell / s.Columns + .5f) * 2f / s.Rows;
            float angle = ((cell % s.Columns + .5f) / s.Columns - .5f) * Mathf.PI * 2f;
            float ring = Mathf.Sqrt(1f - y * y); return new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring);
        }
        private static IEnumerable<int> Neighbors(LabPaintSurface s, int cell)
        {
            int x = cell % s.Columns, y = cell / s.Columns;
            yield return y * s.Columns + (x + s.Columns - 1) % s.Columns;
            yield return y * s.Columns + (x + 1) % s.Columns;
            if (y > 0) yield return cell - s.Columns;
            if (y + 1 < s.Rows) yield return cell + s.Columns;
        }
        private static Shape Measure(LabPaintSurface surface, int[] cells, uint seed, int hit, string kind)
        {
            var set = new HashSet<int>(cells); var remaining = new HashSet<int>(cells);
            var queue = new Queue<int>(); int components = 0, largest = 0, perimeter = 0;
            foreach (int cell in cells) perimeter += Neighbors(surface, cell).Count(n => !set.Contains(n));
            while (remaining.Count > 0)
            {
                int first = remaining.First(); remaining.Remove(first); queue.Enqueue(first); int count = 0; components++;
                while (queue.Count > 0) { int cell = queue.Dequeue(); count++; foreach (int next in Neighbors(surface, cell)) if (remaining.Remove(next)) queue.Enqueue(next); }
                largest = Math.Max(largest, count);
            }
            Vector3 center = Vector3.zero; foreach (int cell in cells) center += Direction(surface, cell); center.Normalize();
            double rms = cells.Length == 0 ? 0 : Math.Sqrt(cells.Average(i => { double arc = Math.Acos(Math.Max(-1d, Math.Min(1d, Vector3.Dot(center, Direction(surface, i))))); return arc * arc; }));
            double radius = Math.Acos(1d - 2d * cells.Length / surface.CellCount);
            return new Shape { seed = seed, hit = hit, kind = kind, cells = cells.Length, components = components,
                largestFraction = cells.Length == 0 ? 0 : largest / (float)cells.Length, perimeter = perimeter,
                perimeterOverSqrtArea = cells.Length == 0 ? 0 : perimeter / Mathf.Sqrt(cells.Length),
                rmsOverDiskRadius = radius == 0 ? 0 : (float)(rms / radius), covered = surface.CoveredCount };
        }
        private static void WriteEvidence(string name, Evidence value)
        { Directory.CreateDirectory(DirectoryPath); File.WriteAllText(Path.Combine(DirectoryPath, name), JsonUtility.ToJson(value, true)); }

        [Serializable] private sealed class Evidence { public string fixture; public bool baseline; public List<Shape> shapes = new List<Shape>(); }
        [Serializable] private sealed class Shape
        {
            public uint seed; public int hit, cells, components, perimeter, covered, immediateCells;
            public string kind; public float largestFraction, rmsOverDiskRadius, perimeterOverSqrtArea;
        }

        private sealed class ShellRig : IDisposable
        {
            private readonly GameObject root;
            private readonly Renderer renderer;
            private readonly Camera camera;
            private readonly Vector3 center;
            private readonly RenderTexture output;
            private readonly Texture2D readback, colorMap, painterMap;
            public ShellRig(LabGourdPaint source)
            {
                root = new GameObject("Isolated saved shell splatter consistency fixture"); root.transform.position = Vector3.one * 1000f;
                var shell = new GameObject("Saved production shell"); shell.layer = 30; shell.transform.SetParent(root.transform, false);
                Mesh mesh = source.Shell.GetComponent<MeshFilter>().sharedMesh;
                shell.AddComponent<MeshFilter>().sharedMesh = mesh; center = mesh.bounds.center;
                renderer = shell.AddComponent<MeshRenderer>(); renderer.sharedMaterial = source.paintMaterial;
                var cameraObject = new GameObject("Surface fixture camera"); cameraObject.transform.SetParent(root.transform, false);
                camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.y) * 1.1f;
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.06f, .07f, .09f, 1); camera.nearClipPlane = .01f; camera.farClipPlane = 10f;
                camera.allowHDR = false; camera.allowMSAA = false;
                output = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Assert.That(output.Create(), Is.True); camera.targetTexture = output;
                readback = new Texture2D(640, 640, TextureFormat.RGBA32, false, true);
                colorMap = new Texture2D(256, 128, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
                painterMap = new Texture2D(256, 128, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            }
            public void Capture(LabPaintSurface surface, string name, bool patterns, bool allSides)
            {
                colorMap.SetPixels32(Pixels(surface)); colorMap.Apply(false, false);
                byte[] owners = Owners(surface); var ownerPixels = new Color32[owners.Length];
                for (int i = 0; i < owners.Length; i++) ownerPixels[i] = new Color32(owners[i], 0, 0, 255);
                painterMap.SetPixels32(ownerPixels); painterMap.Apply(false, false);
                var block = new MaterialPropertyBlock(); block.SetTexture("_PaintMap", colorMap); block.SetTexture("_PainterMap", painterMap);
                block.SetFloat("_PaintMapReady", 1); block.SetFloat("_PlayerPaintPatternsEnabled", patterns ? 1 : 0);
                // Isolate geometry and ownership. Grain changes pigment brightness, not cell boundaries.
                block.SetFloat("_PaintGrainEnabled", 0); renderer.SetPropertyBlock(block);
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllBytes(Path.Combine(DirectoryPath, name + "-raw-color-map.png"), colorMap.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(DirectoryPath, name + "-raw-painter-map.png"), painterMap.EncodeToPNG());
                foreach (int yaw in allSides ? new[] { 0, 90, 180, 270 } : new[] { 0 })
                {
                    camera.transform.localPosition = center + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 3f;
                    camera.transform.LookAt(root.transform.TransformPoint(center)); camera.Render();
                    RenderTexture previous = RenderTexture.active;
                    try
                    {
                        RenderTexture.active = output; readback.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); readback.Apply(false, false);
                        Color32[] pixels = readback.GetPixels32();
                        Assert.That(pixels.Count(p => p.r > 50 || p.g > 50 || p.b > 50), Is.GreaterThan(10000), "The capture must contain the saved shell, not only a background.");
                        File.WriteAllBytes(Path.Combine(DirectoryPath, name + $"-yaw{yaw:000}.png"), readback.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; }
                }
            }
            public void Dispose()
            {
                camera.targetTexture = null; output.Release(); Object.Destroy(root); Object.Destroy(output);
                Object.Destroy(readback); Object.Destroy(colorMap); Object.Destroy(painterMap);
            }
        }
    }
}
