using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEngine;

namespace Hapio.EditorTools
{
    /// <summary>Editor-only export of the current presentation, including unfinished paint.</summary>
    public static class CurrentGourdPngExport
    {
        public const int Resolution = 4096;
        public const string MenuPath = "Hapio/Export Current Gourd PNG (4096)...";

        [MenuItem(MenuPath, false, 210)]
        private static void ExportFromMenu()
        {
            try
            {
                LabGourdPaint source = FindCurrentPaint();
                ValidateSource(source);
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/GourdExports"));
                Directory.CreateDirectory(directory);
                string path = EditorUtility.SaveFilePanel("Export current gourd — transparent 4096 x 4096 PNG",
                    directory, "gourd-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), "png");
                if (string.IsNullOrEmpty(path)) return;
                Export(source, path);
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Gourd PNG export", exception.Message, "OK");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanExport() => !EditorApplication.isCompiling && !EditorApplication.isUpdating;

        public static LabGourdPaint FindCurrentPaint()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Start Play Mode and enter a battle first. You may pause the Editor before exporting.");

            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                LabGourdPaint selectedPaint = selected.GetComponentInParent<LabGourdPaint>();
                if (selectedPaint == null) selectedPaint = selected.GetComponentInChildren<LabGourdPaint>();
                if (selectedPaint != null && selectedPaint.IsReady) return selectedPaint;
            }

            LabGourdPaint found = null;
            foreach (LabGourdPaint paint in UnityEngine.Object.FindObjectsByType<LabGourdPaint>())
            {
                if (!paint.IsReady) continue;
                if (found != null)
                    throw new InvalidOperationException("More than one current gourd exists. Select the desired target in the Hierarchy, then export again.");
                found = paint;
            }
            if (found == null)
                throw new InvalidOperationException("No ready in-game gourd was found. Enter a battle and wait for its model and paint to appear.");
            return found;
        }

        public static void Export(LabGourdPaint source, string path)
        {
            ValidateSource(source);
            if (string.IsNullOrWhiteSpace(path) || !string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a PNG destination.", nameof(path));

            // Visual refresh only. No Victory/100% requirement and no change to the Host's paint map.
            source.PrepareVisualCapture();
            Camera reference = FindBattleCamera(source);
            Texture2D image = null;
            try
            {
                image = GourdPngCapture.Capture(source.VisualInstance, reference, Resolution);
                byte[] bytes = image.EncodeToPNG();
                if (bytes == null || bytes.Length == 0) throw new InvalidOperationException("PNG encoding failed.");
                string fullPath = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllBytes(fullPath, bytes);
                Debug.Log("HAPIO_GOURD_PNG_EXPORTED size=4096x4096 transparent=true path=" + fullPath);
            }
            finally
            {
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static Camera FindBattleCamera(LabGourdPaint source)
        {
            if (source.network != null && source.network.BattleCamera != null)
                return source.network.BattleCamera;
            foreach (LabSeatCamera rig in UnityEngine.Object.FindObjectsByType<LabSeatCamera>())
                if (rig.network == source.network && rig.battleCamera != null) return rig.battleCamera;
            foreach (LabHud hud in UnityEngine.Object.FindObjectsByType<LabHud>())
                if (hud.network == source.network && hud.battleCamera != null) return hud.battleCamera;
            if (Camera.main != null) return Camera.main;
            throw new InvalidOperationException("The battle camera could not be found. Wait for the battle view to finish loading.");
        }

        private static void ValidateSource(LabGourdPaint source)
        {
            if (source == null || !source.IsReady || source.VisualInstance == null)
                throw new InvalidOperationException("The current gourd presentation is not ready.");
            Renderer shell = source.Shell != null ? source.Shell.GetComponent<Renderer>() : null;
            if (shell != null && shell.forceRenderingOff)
                throw new InvalidOperationException("The original gourd is hidden during the completion effect. Export before that effect, or wait until the battle gourd is visible again.");
        }
    }
}
