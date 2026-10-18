using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Explicit native-resolution Host-rule/render evidence; never runs on import or in a player.</summary>
    public static class LabPaintPatternValidation
    {
        public static void ExportValidation(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new ArgumentException("Choose an absolute evidence directory.", nameof(directory));
            Directory.CreateDirectory(directory);
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            if (source == null) throw new InvalidOperationException("Saved LabConfig is missing.");
            string sourceBefore = EditorJsonUtility.ToJson(source);
            var records = new List<Entry>();
            var elements = new[] { LabOrbElement.Fire, LabOrbElement.Metal, LabOrbElement.Water,
                LabOrbElement.Wood, LabOrbElement.Earth };
            var hit = new Vector3(0f, .20f, 1f).normalized;
            var surfaces = new LabPaintSurface[4];
            using (var rig = new LabPaintStudyRenderer())
            {
                for (int pattern = 0; pattern < surfaces.Length; pattern++)
                {
                    LabPaintGrowthSettings settings = source.PaintGrowthSettings;
                    settings.Pattern = (LabPaintPattern)pattern;
                    settings.Style = 0;
                    settings.Resolution = 2;
                    settings.InitialPercent = 6f;
                    settings.RepeatPercent = 4f;
                    settings.GrowthPercentPerSecond = 6f;
                    surfaces[pattern] = new LabPaintSurface(settings, 200u);
                    surfaces[pattern].SetFlowDirection(Vector3.down);
                    Require(surfaces[pattern].RegisterHit(hit, LabPaintPalette.ColorFor(elements[0]), 1), "Initial hit rejected.");
                }
                Capture("initial", surfaces, rig, directory, records);
                for (int pattern = 0; pattern < surfaces.Length; pattern++)
                {
                    for (int color = 1; color < elements.Length; color++)
                        Require(surfaces[pattern].RegisterHit(hit, LabPaintPalette.ColorFor(elements[color]), (ulong)(color + 1)),
                            "Repeated hit rejected.");
                    surfaces[pattern].Advance(4d);
                }
                Capture("grown", surfaces, rig, directory, records);
                for (int pattern = 0; pattern < surfaces.Length; pattern++)
                {
                    for (int supply = 0; supply < 30 && !surfaces[pattern].IsComplete; supply++)
                    {
                        Require(surfaces[pattern].RegisterHit(hit, LabPaintPalette.ColorFor(elements[supply % elements.Length]), 1),
                            "Completion supply rejected.");
                        surfaces[pattern].Advance(100d);
                    }
                    Require(surfaces[pattern].IsComplete, "Actual growth failed to reach the last empty cell.");
                }
                Capture("complete", surfaces, rig, directory, records);
            }
            Require(EditorJsonUtility.ToJson(source) == sourceBefore, "Runtime validation modified saved Config.");
            var evidence = new Evidence
            {
                recordedUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
                graphics = SystemInfo.graphicsDeviceType.ToString(),
                classification = "Production RegisterHit/Advance fixture on saved gourd. No projectile, network or device claim.",
                order = "Legacy / Broad / Splatter / Flow", columns = surfaces[0].Columns, rows = surfaces[0].Rows,
                seed = 200, initialPercent = 6f, repeatPercent = 4f, growthPercentPerSecond = 6f,
                sourceDefaultPattern = source.PaintPattern.ToString(), sourceConfigUnchanged = true,
                filter = "Point", grain = false, entries = records.ToArray()
            };
            File.WriteAllText(Path.Combine(directory, "runtime-pattern-evidence.json"), JsonUtility.ToJson(evidence, true));
            Debug.Log("PAINT_RUNTIME_PATTERN_VALIDATION_OK renders=" + records.Count + " default=" + source.PaintPattern);
        }

        private static void Capture(string stage, LabPaintSurface[] surfaces, LabPaintStudyRenderer rig,
            string directory, List<Entry> entries)
        {
            var images = new Texture2D[surfaces.Length];
            Texture2D sheet = null;
            try
            {
                for (int pattern = 0; pattern < surfaces.Length; pattern++)
                {
                    LabPaintSurface surface = surfaces[pattern];
                    images[pattern] = rig.RenderSurface(surface, false);
                    string file = ((LabPaintPattern)pattern).ToString().ToLowerInvariant() + "-" + stage + ".png";
                    byte[] png = images[pattern].EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(directory, file), png);
                    var pixels = new Color32[surface.CellCount];
                    surface.CopyPixels(pixels);
                    entries.Add(new Entry
                    {
                        pattern = ((LabPaintPattern)pattern).ToString(), stage = stage, filename = file,
                        covered = surface.CoveredCount, total = surface.CellCount, regions = surface.RegionCount,
                        hits = surface.PaintCount, revision = surface.Revision,
                        mapSha256 = HashPixels(pixels), pngSha256 = Hash(png)
                    });
                }
                sheet = LabPaintStudyRenderer.Combine(images);
                File.WriteAllBytes(Path.Combine(directory, "runtime-patterns-" + stage + ".png"), sheet.EncodeToPNG());
            }
            finally
            {
                if (sheet != null) Object.DestroyImmediate(sheet);
                foreach (Texture2D image in images) if (image != null) Object.DestroyImmediate(image);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static string HashPixels(Color32[] pixels)
        {
            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].r; bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b; bytes[i * 4 + 3] = pixels[i].a;
            }
            return Hash(bytes);
        }

        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        [Serializable] private sealed class Entry
        {
            public string pattern, stage, filename, mapSha256, pngSha256;
            public int covered, total, regions, hits;
            public uint revision;
        }

        [Serializable] private sealed class Evidence
        {
            public string recordedUtc, unityVersion, graphics, classification, order, sourceDefaultPattern, filter;
            public int columns, rows, seed;
            public float initialPercent, repeatPercent, growthPercentPerSecond;
            public bool sourceConfigUnchanged, grain;
            public Entry[] entries;
        }
    }
}
