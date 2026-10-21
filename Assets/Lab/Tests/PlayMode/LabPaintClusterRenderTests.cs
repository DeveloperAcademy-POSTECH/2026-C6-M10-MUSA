using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Saved shell/shader comparison fixtures, separate from real projectile/network validation.</summary>
    public sealed class LabPaintClusterRenderTests
    {
        private static readonly Color Earlier = new Color(32f / 255f, 49f / 255f, 79f / 255f);
        private static readonly Color Latest = new Color(43f / 255f, 95f / 255f, 174f / 255f);

        [UnityTearDown]
        public IEnumerator Unload()
        {
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Cluster render cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator SavedShellDisplaysCurrentClustersWithoutRecoloringEarlierCells()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A real graphics device is required; no shader or image verification was performed.");
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            foreach (LabPaintPattern pattern in new[] { LabPaintPattern.Legacy, LabPaintPattern.Splatter })
            {
                var surface = new LabPaintSurface(new LabPaintGrowthSettings {
                    Resolution = 2, Pattern = pattern, Style = 1, Raggedness = 0,
                    InitialPercent = pattern == LabPaintPattern.Legacy ? 6.25f : 2f,
                    RepeatPercent = pattern == LabPaintPattern.Legacy ? 3.125f : 4f,
                    GrowthPercentPerSecond = 100f, ClusterCompactness = .85f, ClusterIrregularity = .25f
                }, 123);
                Assert.That(surface.RegisterHit(Vector3.forward, Earlier, 1, 1), Is.True);
                Color32[] initial = Pixels(surface);
                int covered = surface.CoveredCount;
                using (var rig = new SavedShellRig(paint))
                {
                    Color32[] firstImage = rig.Capture(surface, pattern + "-initial.png");
                    Color32[] grownImage = firstImage;
                    for (int hit = 1; hit <= 3; hit++)
                    {
                        Assert.That(surface.RegisterHit(Vector3.forward, hit == 3 ? Latest : Earlier,
                            (ulong)(hit + 1), hit + 1), Is.True);
                        surface.Advance(1d);
                        Assert.That(surface.CoveredCount, Is.GreaterThan(covered));
                        covered = surface.CoveredCount;
                        Color32[] current = Pixels(surface);
                        for (int i = 0; i < initial.Length; i++)
                            if (initial[i].a != 0) Assert.That(current[i], Is.EqualTo(initial[i]));
                        Assert.That(surface.PendingCoverage, Is.LessThan(1d / surface.CellCount));
                        grownImage = rig.Capture(surface, pattern + "-cluster-after" + hit + ".png");
                    }
                    Assert.That(Pixels(surface), Is.Not.EqualTo(initial));
                    int changed = 0;
                    for (int i = 0; i < firstImage.Length; i++)
                        if (!firstImage[i].Equals(grownImage[i])) changed++;
                    Assert.That(changed, Is.GreaterThan(200), "The production shader must display the added cluster.");
                    var owners = new byte[surface.CellCount]; surface.CopyPainterSlots(owners);
                    Assert.That(owners, Does.Contain((byte)4));
                }
            }
        }

        private static Color32[] Pixels(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount]; surface.CopyPixels(pixels);return pixels;
        }

        private sealed class SavedShellRig : IDisposable
        {
            private readonly GameObject root;
            private readonly Renderer renderer;
            private readonly Camera camera;
            private readonly RenderTexture renderTexture;
            private readonly Texture2D readback, colorMap, painterMap;
            public SavedShellRig(LabGourdPaint source)
            {
                root = new GameObject("Isolated saved gourd cluster comparison");
                root.transform.position = new Vector3(1000, 1000, 1000);
                var shell = new GameObject("Saved shell"); shell.layer = 30; shell.transform.SetParent(root.transform, false);
                Mesh mesh = source.Shell.GetComponent<MeshFilter>().sharedMesh;
                shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = shell.AddComponent<MeshRenderer>(); renderer.sharedMaterial = source.paintMaterial;
                var cameraObject = new GameObject("Comparison camera");cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition = mesh.bounds.center + Vector3.forward * 3f;
                cameraObject.transform.localRotation = Quaternion.Euler(0, 180, 0);
                camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.y) * 1.1f;
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.06f, .07f, .09f, 0);
                camera.nearClipPlane = .01f; camera.farClipPlane = 10f; camera.allowHDR = false; camera.allowMSAA = false;
                renderTexture = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Assert.That(renderTexture.Create(), Is.True); camera.targetTexture = renderTexture;
                readback = new Texture2D(640, 640, TextureFormat.RGBA32, false, true);
                colorMap = new Texture2D(256, 128, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point,
                    wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
                painterMap = new Texture2D(256, 128, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point,
                    wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            }
            public Color32[] Capture(LabPaintSurface surface, string name)
            {
                colorMap.SetPixels32(Pixels(surface)); colorMap.Apply(false, false);
                var owners = new byte[surface.CellCount];surface.CopyPainterSlots(owners);
                var ownerPixels = new Color32[surface.CellCount];
                for (int i = 0; i < owners.Length; i++) ownerPixels[i] = new Color32(owners[i], 0, 0, 255);
                painterMap.SetPixels32(ownerPixels);painterMap.Apply(false, false);
                var block = new MaterialPropertyBlock();block.SetTexture("_PaintMap", colorMap);
                block.SetTexture("_PainterMap", painterMap);block.SetFloat("_PaintMapReady", 1);
                // Disable only visual overlays in this comparison; color/owner maps are intact.
                block.SetFloat("_PlayerPaintPatternsEnabled", 0);block.SetFloat("_PaintGrainEnabled", 0);
                renderer.SetPropertyBlock(block);camera.Render();
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = renderTexture;readback.ReadPixels(new Rect(0,0,640,640),0,0);readback.Apply(false,false);
                    string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/PaintClusters"));
                    Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,name),readback.EncodeToPNG());
                    return readback.GetPixels32();
                }
                finally { RenderTexture.active = previous; }
            }
            public void Dispose()
            {
                camera.targetTexture = null;renderTexture.Release();Object.Destroy(root);Object.Destroy(renderTexture);
                Object.Destroy(readback);Object.Destroy(colorMap);Object.Destroy(painterMap);
            }
        }
    }
}
