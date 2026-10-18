using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Explicit local evidence export, never invoked by gameplay or on import.</summary>
    public static class LabPaintStyleComparisonValidation
    {
        public static void ExportValidation(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new ArgumentException("Choose an absolute evidence output directory.", nameof(directory));
            SceneState before = SceneState.Capture();
            Directory.CreateDirectory(directory);
            var entries = new List<Entry>();
            using (var rig = new LabPaintStudyRenderer())
            {
                var masks = new LabPaintStudyMask[3];
                for (int style = 0; style < 3; style++) masks[style] = LabPaintStudyRenderer.LoadMask((LabPaintStudyStyle)style);
                foreach (LabPaintStudyStage stage in Enum.GetValues(typeof(LabPaintStudyStage)))
                {
                    rig.ClearMapCache();
                    var images = new Texture2D[3];
                    Texture2D sheet = null;
                    try
                    {
                        for (int style = 0; style < 3; style++)
                        {
                            var kind = (LabPaintStudyStyle)style;
                            LabPaintStudyMap map = LabPaintStyleStudy.Build(masks[style], kind, stage,
                                LabPaintStudyRenderer.MapColumns, LabPaintStudyRenderer.MapRows, LabPaintStudyRenderer.Seed);
                            images[style] = rig.Render(map, true);
                            string filename = kind.ToString().ToLowerInvariant() + "-" + stage.ToString().ToLowerInvariant() + ".png";
                            byte[] png = images[style].EncodeToPNG();
                            File.WriteAllBytes(Path.Combine(directory, filename), png);
                            entries.Add(new Entry
                            {
                                style = kind.ToString(), stage = stage.ToString(), filename = filename,
                                coveredTexels = map.CoveredCount, coverage = map.Coverage,
                                mapSha256 = Hash(map.Pixels), pngSha256 = Hash(png)
                            });
                        }
                        sheet = LabPaintStudyRenderer.Combine(images);
                        File.WriteAllBytes(Path.Combine(directory, "comparison-" + stage.ToString().ToLowerInvariant() + ".png"),
                            sheet.EncodeToPNG());
                    }
                    finally
                    {
                        if (sheet != null) Object.DestroyImmediate(sheet);
                        foreach (Texture2D image in images) if (image != null) Object.DestroyImmediate(image);
                    }
                }
            }
            SceneState after = SceneState.Capture();
            if (!before.EqualsState(after))
                throw new InvalidOperationException("The paint comparison changed the active or loaded scene state.");
            var evidence = new Evidence
            {
                recordedUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                classification = "Editor fixture renders. No gameplay hits, growth, network, goal or device validation.",
                prefab = LabPaintStudyRenderer.PrefabPath, shader = LabPaintStudyRenderer.ShaderPath,
                seed = LabPaintStudyRenderer.Seed, columns = LabPaintStudyRenderer.MapColumns,
                rows = LabPaintStudyRenderer.MapRows, renderSize = LabPaintStudyRenderer.RenderSize,
                comparisonOrder = "Left Broad / Middle Splatter / Right Flow", internalGrain = true,
                yaw = 0f, zoom = 1f, activeScenePreserved = true,
                paletteSrgb = PaletteHex(), entries = entries.ToArray()
            };
            File.WriteAllText(Path.Combine(directory, "paint-style-evidence.json"), JsonUtility.ToJson(evidence, true));
            Debug.Log("PAINT_STYLE_COMPARISON_EXPORTED count=" + entries.Count + " directory=" + directory);
        }

        private static string[] PaletteHex()
        {
            var colors = new string[5];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = "#" + ColorUtility.ToHtmlStringRGB(LabPaintStyleStudy.PaletteColor(i));
            return colors;
        }

        private static string Hash(Color32[] pixels)
        {
            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].r;
                bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b;
                bytes[i * 4 + 3] = pixels[i].a;
            }
            return Hash(bytes);
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        [Serializable]
        private sealed class Entry
        {
            public string style, stage, filename, mapSha256, pngSha256;
            public int coveredTexels;
            public float coverage;
        }

        [Serializable]
        private sealed class Evidence
        {
            public string recordedUtc, unityVersion, graphicsDevice, classification, prefab, shader, comparisonOrder;
            public string[] paletteSrgb;
            public uint seed;
            public int columns, rows, renderSize;
            public bool internalGrain, activeScenePreserved;
            public float yaw, zoom;
            public Entry[] entries;
        }

        private sealed class SceneState
        {
            private Scene active;
            private Scene[] handles;
            private bool[] dirty;

            public static SceneState Capture()
            {
                var state = new SceneState
                {
                    active = SceneManager.GetActiveScene(),
                    handles = new Scene[SceneManager.sceneCount], dirty = new bool[SceneManager.sceneCount]
                };
                for (int i = 0; i < state.handles.Length; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    state.handles[i] = scene;
                    state.dirty[i] = scene.isDirty;
                }
                return state;
            }

            public bool EqualsState(SceneState other)
            {
                if (active != other.active || handles.Length != other.handles.Length) return false;
                for (int i = 0; i < handles.Length; i++)
                    if (handles[i] != other.handles[i] || dirty[i] != other.dirty[i]) return false;
                return true;
            }
        }
    }
}
