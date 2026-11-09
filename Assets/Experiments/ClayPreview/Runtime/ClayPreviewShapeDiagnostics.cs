using System;
using System.Collections.Generic;
using System.IO;
using C6Lab;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hapio.Experiments
{
    public enum ClayPreviewShapeScenario { Full67 = 0, FirstHit = 1, FirstHitThreeRepeats = 2 }

    /// <summary>Cell-centre diagnostics in the same sphere projection as the compositor; never a game mask.</summary>
    public sealed class ClayPreviewShapeDiagnostics : IDisposable
    {
        public const int DiagnosticSeed = 123;
        public const float InitialPercent = 2f, RepeatPercent = 1.5f;
        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public int PaintedCells { get; private set; }
        public int MaximumSupportCells { get; private set; }
        public int FootprintSamples { get; private set; }
        public int OutsideSupportSamples { get; private set; }
        public bool Complete => PaintedCells == Columns * Rows;
        public bool HasFootprint { get; private set; }
        public Texture2D SupportTexture { get; private set; }
        public Texture2D FootprintTexture { get; private set; }
        // Exact source token components, including the non-HEX quantized DarkRed value.
        // Hapio.tokens.json: 5OrbsColor/TreeGreen/500***, WaterBlue/500***;
        // TextColor/DarkRed/500***, SecondaryLightIvory/500***.
        static readonly Color32 Actual = new Color(.18431373f, .41176471f, .27450982f, 1);
        static readonly Color32 Halo = new Color(.05490196f, .38039216f, .57254905f, 1);
        static readonly Color32 Outside = new Color(.688176095f, .278579950f, .218751281f, 1);
        static readonly Color32 Empty = new Color(.90980393f, .82352942f, .72549021f, 1);
        readonly struct Draw
        {
            public readonly LabPaintVisualStamp Stamp;
            public readonly int[] Erased;
            public Draw(LabPaintVisualStamp stamp) { Stamp = stamp; Erased = null; }
            public Draw(int[] erased) { Stamp = default; Erased = erased; }
        }

        public static LabPaintArtJournal CreateScenarioJournal(ClayPreviewShapeScenario scenario, string styleVersion, Color32 color)
        {
            if (scenario != ClayPreviewShapeScenario.FirstHit && scenario != ClayPreviewShapeScenario.FirstHitThreeRepeats)
                throw new ArgumentOutOfRangeException(nameof(scenario));
            // This is an explicit isolated rule fixture, not an edited prefix of the frozen 67-hit artwork.
            var surface = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 2,
                InitialPercent = InitialPercent, RepeatPercent = RepeatPercent, DryingEnabled = false }, DiagnosticSeed);
            string path = Path.Combine(Application.temporaryCachePath, "hapio-shape-" + Guid.NewGuid().ToString("N") + ".journal");
            var journal = new LabPaintArtJournal(path, "shape-diagnostic-" + scenario, surface.Columns, surface.Rows, styleVersion);
            try
            {
                Need(journal.Error == null && surface.AttachArtJournal(journal), journal.Error ?? "Could not attach the isolated shape journal.");
                int count = scenario == ClayPreviewShapeScenario.FirstHit ? 1 : 4;
                for (int hit = 0; hit < count; hit++)
                {
                    Need(surface.RegisterHit(Vector3.forward, color, 1, 1), "The fixed diagnostic hit was rejected.");
                    surface.Advance(60);
                    Need(journal.Commit(surface.Revision, SurfaceHash(surface)), journal.Error ?? "Could not commit a diagnostic hit.");
                }
                Need(surface.PaintCount == count && !surface.IsComplete, "The isolated scenario must remain partial and retain its actual hit count.");
                Need(journal.Seal(surface.Revision, SurfaceHash(surface)), journal.Error ?? "Could not seal the isolated shape journal.");
                return journal;
            }
            catch { journal.Dispose(); throw; }
        }

        public static ClayPreviewShapeDiagnostics Create(LabPaintArtJournal source, Texture2D brush)
        {
            if (source == null || source.Error != null) throw new ArgumentException("A readable journal is required.");
            var result = new ClayPreviewShapeDiagnostics { Columns = source.Columns, Rows = source.Rows };
            try
            {
                var painted = new bool[source.Columns * source.Rows];
                var descriptors = new Dictionary<uint, LabPaintVisualStamp>();
                var draws = new List<Draw>(); var pendingCells = new HashSet<int>();
                var operations = new List<LabPaintArtOperation>(256);
                LabPaintArtOperationKind pendingKind = 0; uint pendingStroke = 0, highestFirstStroke = 0;
                ulong after = 0;
                void Flush()
                {
                    if (pendingCells.Count == 0) return;
                    if (pendingKind == LabPaintArtOperationKind.EraseCells) draws.Add(new Draw(new List<int>(pendingCells).ToArray()));
                    else
                    {
                        LabPaintVisualStamp shape = descriptors[pendingStroke];
                        if (pendingStroke <= highestFirstStroke)
                        {
                            Vector3 center = Vector3.zero;
                            foreach (int cell in pendingCells) center += CellDirection(cell, source.Columns, source.Rows);
                            center = center.sqrMagnitude > .000001f ? center.normalized : shape.center;
                            Vector3 right = Vector3.ProjectOnPlane(shape.right, center);
                            if (right.sqrMagnitude < .000001f) right = Vector3.Cross(center, Mathf.Abs(center.y) < .9f ? Vector3.up : Vector3.right);
                            shape.center = center; shape.right = right.normalized;
                            shape.radius = Mathf.Clamp(Mathf.Sqrt(pendingCells.Count / (float)painted.Length * 4 * Mathf.PI / .48f) * .5f, .018f, 1.3f);
                        }
                        highestFirstStroke = Math.Max(highestFirstStroke, pendingStroke);
                        shape.radius = Mathf.Min(shape.radius, 1.3f);
                        draws.Add(new Draw(shape));
                    }
                    pendingCells.Clear(); pendingStroke = 0;
                }
                while (after < source.LastSequence)
                {
                    Need(source.ReadOperations(after, 256, operations) && operations.Count > 0, source.Error ?? "Could not read the shape journal.");
                    foreach (var operation in operations)
                    {
                        bool cell = operation.Kind == LabPaintArtOperationKind.PaintCells || operation.Kind == LabPaintArtOperationKind.EraseCells;
                        bool continues = cell && pendingCells.Count > 0 && pendingKind == operation.Kind
                            && (operation.Kind == LabPaintArtOperationKind.EraseCells || operation.StrokeId == pendingStroke);
                        if (pendingCells.Count > 0 && !continues) Flush();
                        if (operation.Kind == LabPaintArtOperationKind.StrokeBegin) descriptors[operation.StrokeId] = operation.Stamp;
                        if (cell)
                        {
                            pendingKind = operation.Kind; pendingStroke = operation.StrokeId;
                            foreach (int index in operation.Cells)
                            {
                                painted[index] = operation.Kind == LabPaintArtOperationKind.PaintCells;
                                pendingCells.Add(index);
                            }
                        }
                    }
                    after = operations[operations.Count - 1].Sequence;
                }
                Flush();
                bool[] support = CalculateMaximumSupport(painted, source.Columns, source.Rows);
                var footprint = new float[painted.Length];
                result.HasFootprint = brush != null;
                if (brush != null)
                {
                    Color32[] mask = ReadBrush(brush);
                    foreach (var draw in draws)
                    {
                        if (draw.Erased != null) { foreach (int cell in draw.Erased) footprint[cell] = 0; continue; }
                        for (int cell = 0; cell < footprint.Length; cell++)
                        {
                            float incoming = ProjectCoverage(draw.Stamp, CellDirection(cell, source.Columns, source.Rows), mask, brush.width, brush.height);
                            footprint[cell] = incoming + footprint[cell] * (1 - incoming);
                        }
                    }
                }
                int left = source.Columns, bottom = source.Rows, rightBound = -1, top = -1;
                for (int cell = 0; cell < painted.Length; cell++)
                {
                    if (painted[cell]) result.PaintedCells++;
                    if (support[cell]) result.MaximumSupportCells++;
                    if (footprint[cell] > .02f) { result.FootprintSamples++; if (!support[cell]) result.OutsideSupportSamples++; }
                    if (support[cell] || footprint[cell] > .02f)
                    {
                        left = Math.Min(left, cell % source.Columns); rightBound = Math.Max(rightBound, cell % source.Columns);
                        bottom = Math.Min(bottom, cell / source.Columns); top = Math.Max(top, cell / source.Columns);
                    }
                }
                // Same crop for both maps. A seam-crossing footprint intentionally shows the full wrapped sheet.
                left = Math.Max(0, left - 3); rightBound = Math.Min(source.Columns - 1, rightBound + 3);
                bottom = Math.Max(0, bottom - 3); top = Math.Min(source.Rows - 1, top + 3);
                int width = Math.Max(1, rightBound - left + 1), height = Math.Max(1, top - bottom + 1);
                var supportedPixels = new Color32[width * height]; var footprintPixels = new Color32[width * height];
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    int cell = (bottom + y) * source.Columns + left + x, at = y * width + x;
                    supportedPixels[at] = painted[cell] ? Actual : support[cell] ? Halo : Empty;
                    footprintPixels[at] = footprint[cell] > .02f ? support[cell] || result.Complete ? Halo : Outside : Empty;
                }
                result.SupportTexture = Texture("Actual cells and maximum two-cell support", width, height, supportedPixels);
                result.FootprintTexture = Texture("Projected footprint: permitted and clipped", width, height, footprintPixels);
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        public static bool[] CalculateMaximumSupport(bool[] painted, int columns, int rows)
        {
            if (painted == null || columns < 1 || rows < 1 || painted.Length != columns * rows) throw new ArgumentException("Invalid logical map dimensions.");
            var support = new bool[painted.Length]; int radius = LabClayStickerRuntime.SupportHaloCells;
            for (int cell = 0; cell < painted.Length; cell++) if (painted[cell])
                for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = ((cell % columns + dx) % columns + columns) % columns;
                    int y = Mathf.Clamp(cell / columns + dy, 0, rows - 1);
                    support[y * columns + x] = true;
                }
            return support;
        }

        public static Vector3 CellDirection(int cell, int columns, int rows)
        {
            float u = (cell % columns + .5f) / columns, v = (cell / columns + .5f) / rows;
            float y = 1 - v * 2, angle = (u - .5f) * Mathf.PI * 2, ring = Mathf.Sqrt(Mathf.Max(0, 1 - y * y));
            return new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring);
        }

        static float ProjectCoverage(LabPaintVisualStamp shape, Vector3 direction, Color32[] pixels, int width, int height)
        {
            float cosine = Mathf.Clamp(Vector3.Dot(direction, shape.center), -1, 1);
            if (cosine <= 0 || shape.radius <= 0) return 0;
            float sine = Mathf.Sqrt(Mathf.Max(0, 1 - cosine * cosine));
            float factor = sine > .000001f ? Mathf.Acos(cosine) / sine : 1;
            float u = .5f + Vector3.Dot(direction, shape.right) * factor / (2 * shape.radius);
            float v = .5f + Vector3.Dot(direction, shape.Up) * factor / (2 * shape.radius);
            if (u < 0 || v < 0 || u > 1 || v > 1) return 0;
            // Texture bilinear coordinates address centres at (x+.5)/size and clamp at the edge.
            float px = u * width - .5f, py = v * height - .5f;
            int x = Mathf.FloorToInt(px), y = Mathf.FloorToInt(py); float fx = px - x, fy = py - y;
            float At(int ix, int iy) => pixels[Mathf.Clamp(iy, 0, height - 1) * width + Mathf.Clamp(ix, 0, width - 1)].r / 255f;
            float coverage = Mathf.Lerp(Mathf.Lerp(At(x, y), At(x + 1, y), fx), Mathf.Lerp(At(x, y + 1), At(x + 1, y + 1), fx), fy);
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .88f, coverage));
        }

        static Color32[] ReadBrush(Texture2D brush)
        {
            if (brush.isReadable) return brush.GetPixels32();
            RenderTexture old = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(brush.width, brush.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D readable = null;
            try
            {
                Graphics.Blit(brush, temporary); RenderTexture.active = temporary;
                readable = new Texture2D(brush.width, brush.height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, brush.width, brush.height), 0, 0); readable.Apply(); return readable.GetPixels32();
            }
            finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(temporary); Destroy(readable); }
        }
        static string SurfaceHash(LabPaintSurface surface)
        {
            var cells = new Color32[surface.CellCount]; surface.CopyPixels(cells);
            var bytes = new byte[cells.Length * 4];
            for (int i = 0; i < cells.Length; i++) { int j = i * 4; bytes[j] = cells[i].r; bytes[j + 1] = cells[i].g; bytes[j + 2] = cells[i].b; bytes[j + 3] = cells[i].a; }
            return LabPaintVisualStamp.Hash(bytes);
        }
        static Texture2D Texture(string name, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = name, hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }
        public void Dispose() { Destroy(SupportTexture); Destroy(FootprintTexture); SupportTexture = FootprintTexture = null; }
        static void Destroy(Object value) { if (value != null) { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); } }
        static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    }
}
