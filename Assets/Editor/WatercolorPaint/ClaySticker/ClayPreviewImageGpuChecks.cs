using System;
using System.Collections.Generic;
using System.Linq;
using Hapio.Experiments;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Real pixels for imported preview images, isolated from every game Scene and saved sample.</summary>
    public static class ClayPreviewImageGpuChecks
    {
        [Serializable] public sealed class Report
        {
            public string unity, device;
            public int checksPassed, capturedFrames;
            public string[] checks;
        }
        [MenuItem("C6 Lab/Experiments/Clay Images/Run Image GPU Checks")]
        static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            Need(!EditorApplication.isPlaying && !EditorApplication.isCompiling, "Stop Play mode and finish compilation first.");
            Need(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null
                && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat), "An actual float-capable GPU is required.");
            var report = new Report { unity = Application.unityVersion, device = SystemInfo.graphicsDeviceType.ToString() };
            var checks = new List<string>();
            string[] paths = {
                "Assets/Lab/Resources/GourdPaint/ClaySticker/ClayStickerSurface.shader",
                "Assets/Experiments/ClayPreview/Runtime/Shaders/RoundedClayStickerSurface.shader",
                "Assets/Experiments/ClayPreview/Runtime/Shaders/StickerCoatingSurface.shader" };
            foreach (string path in paths) CheckSurface(Load(path), report, checks);
            CheckBackground(Load("Assets/Experiments/ClayPreview/Runtime/Shaders/ClayPreviewBackground.shader"), report, checks);
            report.checks = checks.ToArray(); report.checksPassed = checks.Count;
            return JsonUtility.ToJson(report);
        }

        static void CheckSurface(Shader shader, Report report, List<string> checks)
        {
            Texture2D color = null, height = null, neutral = null, pattern = null;
            try
            {
                color = Constant(64, 32, TextureFormat.RGBAHalf, new Color(.25f, .44f, .16f, 1f));
                height = Constant(64, 32, TextureFormat.RGBAHalf, new Color(.25f, .5f, .5f, 1f));
                neutral = Constant(32, 32, TextureFormat.RGBAHalf, new Color(.5f, .5f, .5f, 1f));
                pattern = Constant(32, 32, TextureFormat.RGBAHalf, Color.clear);
                var pixels = new Color[32 * 32];
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    float value = .5f + .45f * Mathf.Sin((x + y * .35f) * Mathf.PI / 8f);
                    pixels[y * 32 + x] = new Color(value, value, value, 1f);
                }
                pattern.SetPixels(pixels); pattern.Apply(false, false);
                Color[] originalColor = color.GetPixels(), originalHeight = height.GetPixels();
                using (var preview = new RoundedClaySurfaceChecks.FloatPreview(shader, color, height))
                {
                    preview.SetCaptureSize(256);
                    ClayPreviewImagePresentation.ApplyStickerTexture(preview.Material, null, true, 4, 1, 1);
                    Need(preview.Material.GetFloat("_ClayPreviewTextureEnabled") == 0f, "Null texture must force the original path.");
                    Color[] baseline = Render(preview, report);
                    ClayPreviewImagePresentation.ApplyStickerTexture(preview.Material, neutral, true, 4, 1, 1);
                    Need(Difference(baseline, Render(preview, report)) <= .00001f, "A neutral .5 image changed the artwork: " + shader.name);
                    checks.Add(shader.name + ": exact neutral texture is invisible even at full strengths");

                    ClayPreviewImagePresentation.ApplyStickerTexture(preview.Material, pattern, true, 4, 1, 0);
                    Need(Difference(baseline, Render(preview, report)) > .002f, "Imported pigment texture had no visible GPU effect: " + shader.name);
                    ClayPreviewImagePresentation.ApplyStickerTexture(preview.Material, pattern, true, 4, 0, 1);
                    Need(Difference(baseline, Render(preview, report)) > .00001f, "Imported micro-normal had no visible GPU effect: " + shader.name);
                    checks.Add(shader.name + ": color and bounded micro-normal each change actual pixels independently");

                    float before = preview.Material.GetFloat("_ClayPreviewTextureScale");
                    bool rejected = false;
                    try { ClayPreviewImagePresentation.ApplyStickerTexture(preview.Material, neutral, true, float.NaN, 1, 1); }
                    catch (ArgumentOutOfRangeException) { rejected = true; }
                    Need(rejected && preview.Material.GetFloat("_ClayPreviewTextureScale") == before
                        && preview.Material.GetTexture("_ClayPreviewTexture") == pattern, "Invalid settings partially changed the material.");
                    ClayPreviewImagePresentation.ApplyStickerTexture(preview.Material, null, true, 4, 1, 1);
                    Need(Difference(baseline, Render(preview, report)) <= .00001f, "Removing the image did not restore the original pixels.");
                    Need(preview.Material.GetTexture("_PaintMap") == color && preview.Material.GetTexture("_HeightMap") == height
                        && color.GetPixels().SequenceEqual(originalColor) && height.GetPixels().SequenceEqual(originalHeight),
                        "The visual override changed or replaced a frozen paint/height map.");
                    checks.Add(shader.name + ": invalid input is atomic; null restores original output and frozen maps");
                }
                height.SetPixels(Enumerable.Repeat(new Color(.25f, 0, 0, 0), 64 * 32).ToArray()); height.Apply(false, false);
                using (var preview = new RoundedClaySurfaceChecks.FloatPreview(shader, color, height))
                {
                    preview.SetCaptureSize(256);
                    Color[] baseline = Render(preview, report);
                    ClayPreviewImagePresentation.ApplyStickerTexture(preview.Material, pattern, true, 12, 1, 1);
                    Need(Difference(baseline, Render(preview, report)) <= .00001f,
                        "Imported texture leaked onto completed base with no actual sticker coverage: " + shader.name);
                    checks.Add(shader.name + ": completion-only A-zero base receives neither color texture nor micro-normal");
                }
            }
            finally { foreach (var texture in new[] { color, height, neutral, pattern }) if (texture != null) Object.DestroyImmediate(texture); }
        }

        static void CheckBackground(Shader shader, Report report, List<string> checks)
        {
            Scene scene = default; RenderTexture target = null; Texture2D image = null; ClayPreviewImagePresentation helper = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var root = new GameObject("Imported background isolated check") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                var cameraObject = new GameObject("Preview camera"); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.cameraType = CameraType.Preview; camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = 1f; camera.aspect = 1f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 20f; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.07f, .11f, .15f, 1f); camera.cullingMask = 1 << 30; camera.allowMSAA = false;
                target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                    { hideFlags = HideFlags.HideAndDontSave };
                Need(target.Create(), "Cannot create the background float target."); camera.targetTexture = target;
                helper = new ClayPreviewImagePresentation(root.transform, camera, shader);
                image = Constant(8, 4, TextureFormat.RGBAHalf, new Color(.28f, .53f, .81f, 1f));
                Color[] empty = Capture(camera, target, report);
                helper.SetBackground(image, false);
                Color[] fit = Capture(camera, target, report);
                Need(Delta(fit[8 * 256 + 8], empty[8 * 256 + 8]) <= .00001f
                    && Delta(fit[128 * 256 + 128], empty[128 * 256 + 128]) > .1f, "Fit did not preserve the image aspect and letterbox.");
                helper.SetBackground(image, true);
                Color[] fill = Capture(camera, target, report);
                Need(Delta(fill[8 * 256 + 8], empty[8 * 256 + 8]) > .1f, "Fill did not cover the viewport.");
                var light = new GameObject("Unrelated light"); light.transform.SetParent(root.transform, false);
                Light source = light.AddComponent<Light>(); source.type = LightType.Directional; source.intensity = 20f;
                Need(Difference(fill, Capture(camera, target, report)) <= .00001f, "The unlit background changed with the scene key light.");
                camera.orthographicSize = 2f; camera.transform.SetPositionAndRotation(new Vector3(2, 3, -1), Quaternion.Euler(20, 45, 0));
                helper.UpdateLayout();
                Need(Difference(fill, Capture(camera, target, report)) <= .00001f, "Background shifted after camera layout/zoom change.");
                checks.Add("background Fit/Fill use a camera-fixed unlit quad, independent of model or light");
                image.SetPixels(Enumerable.Repeat(new Color(.28f, .53f, .81f, 0f), 8 * 4).ToArray()); image.Apply(false, false);
                Need(Difference(empty, Capture(camera, target, report)) <= .00001f, "PNG alpha did not reveal the flat preview clear color.");
                MeshRenderer owned = helper.BackgroundRenderer;
                helper.Dispose(); helper.Dispose();
                Need(owned == null && image != null && image.width == 8, "Disposal must release only the owned background, never the imported image.");
                checks.Add("background transparency and idempotent resource disposal preserve the imported texture");
            }
            finally
            {
                helper?.Dispose();
                if (target != null) { if (RenderTexture.active == target) RenderTexture.active = null; target.Release(); Object.DestroyImmediate(target); }
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (image != null) Object.DestroyImmediate(image);
            }
        }

        static Color[] Render(RoundedClaySurfaceChecks.FloatPreview preview, Report report)
        { report.capturedFrames++; return preview.Render(.014f, .35f, .15f, 1f); }
        static Color[] Capture(Camera camera, RenderTexture target, Report report)
        {
            RenderTexture previous = RenderTexture.active; Texture2D copy = null;
            try
            {
                camera.Render(); RenderTexture.active = target;
                copy = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
                copy.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); copy.Apply(false, false);
                report.capturedFrames++; Color[] pixels = copy.GetPixels();
                foreach (Color color in pixels) Need(float.IsFinite(color.r) && float.IsFinite(color.g) && float.IsFinite(color.b) && float.IsFinite(color.a), "The background produced a nonfinite pixel.");
                return pixels;
            }
            finally { RenderTexture.active = previous; if (copy != null) Object.DestroyImmediate(copy); }
        }
        static Texture2D Constant(int width, int height, TextureFormat format, Color color)
        {
            var texture = new Texture2D(width, height, format, false, true)
            { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            texture.SetPixels(Enumerable.Repeat(color, width * height).ToArray()); texture.Apply(false, false); return texture;
        }
        static Shader Load(string path)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Need(shader != null && shader.isSupported, "Missing or unsupported shader: " + path);
            Need(!ShaderUtil.GetShaderMessages(shader).Any(x => x.severity.ToString() == "Error"), "Shader compile error: " + path); return shader;
        }
        static float Delta(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r-b.r), Mathf.Max(Mathf.Abs(a.g-b.g), Mathf.Max(Mathf.Abs(a.b-b.b), Mathf.Abs(a.a-b.a))));
        static float Difference(Color[] a, Color[] b) { Need(a.Length == b.Length, "Capture sizes differ."); float result = 0f; for (int i=0;i<a.Length;i++) result=Mathf.Max(result,Delta(a[i],b[i])); return result; }
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException("Clay preview image GPU check: " + message); }
    }
}
