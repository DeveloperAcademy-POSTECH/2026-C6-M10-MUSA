using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace C6.Prototype.Presentation
{
    /// <summary>Explicit development-player render evidence; never starts network or gameplay work.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplitScreenLayout))]
    public sealed class T04Capture : MonoBehaviour
    {
        [SerializeField] private SplitScreenLayout layout;

        public void Configure(SplitScreenLayout splitLayout) => layout = splitLayout;

#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        private const float SaveTimeoutSeconds = 10f;

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int captureIndex = Array.IndexOf(args, "-c6CaptureDirectory");
            if (captureIndex < 0)
                yield break;
            bool quit = Array.IndexOf(args, "-c6CaptureQuit") >= 0;
            string pngPath = null;
            string jsonPath = null;
            string error = null;
            try
            {
                if (captureIndex + 1 >= args.Length || !Path.IsPathRooted(args[captureIndex + 1]))
                    throw new ArgumentException("-c6CaptureDirectory requires an absolute directory.");
                if (Array.LastIndexOf(args, "-c6CaptureDirectory") != captureIndex)
                    throw new ArgumentException("Only one capture directory may be specified.");
                string directory = Path.GetFullPath(args[captureIndex + 1]);
                pngPath = Path.Combine(directory, "render.png");
                jsonPath = Path.Combine(directory, "render.json");
                if (File.Exists(pngPath) || File.Exists(jsonPath))
                    throw new IOException("Capture artifacts already exist; refusing to overwrite them.");
                Directory.CreateDirectory(directory);
                if (layout == null)
                    layout = GetComponent<SplitScreenLayout>();
                if (layout == null || layout.BattleCamera == null || layout.OrbCamera == null)
                    throw new InvalidOperationException("Capture requires the configured two-camera layout.");
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }
            if (error != null)
            {
                Finish(false, error, quit);
                yield break;
            }

            for (int frame = 0; frame < 8; frame++)
                yield return null;
            yield return new WaitForEndOfFrame();

            var record = MakeRecord();
            try
            {
                // Recheck after the settle frames, before the screenshot API writes anything.
                if (File.Exists(pngPath) || File.Exists(jsonPath))
                    throw new IOException("Capture artifacts appeared while waiting; refusing to overwrite them.");
                ScreenCapture.CaptureScreenshot(pngPath);
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }
            if (error != null)
            {
                Finish(false, error, quit);
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + SaveTimeoutSeconds;
            bool saved = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (IsCompletePng(pngPath))
                {
                    saved = true;
                    break;
                }
                yield return new WaitForSecondsRealtime(0.1f);
            }

            record.result = saved ? "PASS" : "FAIL";
            record.detail = saved ? "ScreenCapture PNG saved with complete header and IEND chunk."
                : "PNG save did not complete within 10 seconds.";
            try
            {
                if (saved)
                    record.pngBytes = new FileInfo(pngPath).Length;
                using (var stream = new FileStream(jsonPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.Write(JsonUtility.ToJson(record, true));
            }
            catch (Exception exception)
            {
                error = "Could not save evidence JSON: " + exception.Message;
            }
            if (error != null)
            {
                Finish(false, error, quit);
                yield break;
            }
            Finish(saved, record.detail, quit);
        }

        private CaptureRecord MakeRecord()
        {
            var hud = GetComponent<T04Hud>();
            return new CaptureRecord
            {
                task = "T04",
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                applicationVersion = Application.version,
                buildGuid = Application.buildGUID,
                developmentBuild = Debug.isDebugBuild,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                safeArea = Screen.safeArea,
                battleCamera = CameraRecord.From(layout.BattleCamera),
                orbCamera = CameraRecord.From(layout.OrbCamera),
                activeCanvasCount = CountActive<Canvas>(),
                activeAudioListenerCount = CountActive<AudioListener>(),
                activeEventSystemCount = CountActive<EventSystem>(),
                hudCanvasPresent = hud != null && hud.Canvas != null && hud.Canvas.isActiveAndEnabled,
                hudCanvasRenderMode = hud != null && hud.Canvas != null ? hud.Canvas.renderMode.ToString() : "Missing"
            };
        }

        private static int CountActive<T>() where T : Behaviour
        {
            int count = 0;
            foreach (var component in FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (component.isActiveAndEnabled)
                    count++;
            return count;
        }

        private static bool IsCompletePng(string path)
        {
            try
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (file.Length < 20)
                        return false;
                    byte[] header = new byte[8];
                    if (file.Read(header, 0, 8) != 8 || header[0] != 137 || header[1] != 80 ||
                        header[2] != 78 || header[3] != 71 || header[4] != 13 || header[5] != 10 ||
                        header[6] != 26 || header[7] != 10)
                        return false;
                    file.Seek(-12, SeekOrigin.End);
                    byte[] end = new byte[12];
                    return file.Read(end, 0, 12) == 12 && end[0] == 0 && end[1] == 0 && end[2] == 0 &&
                           end[3] == 0 && end[4] == 73 && end[5] == 69 && end[6] == 78 && end[7] == 68 &&
                           end[8] == 174 && end[9] == 66 && end[10] == 96 && end[11] == 130;
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static void Finish(bool passed, string detail, bool quit)
        {
            if (passed)
                Debug.Log("C6_T04_CAPTURE PASS " + detail);
            else
                Debug.LogError("C6_T04_CAPTURE FAIL " + detail);
            if (quit)
                Application.Quit(passed ? 0 : 1);
        }

        [Serializable]
        private sealed class CaptureRecord
        {
            public string task;
            public string capturedAtUtc;
            public string applicationVersion;
            public string buildGuid;
            public bool developmentBuild;
            public int screenWidth;
            public int screenHeight;
            public Rect safeArea;
            public CameraRecord battleCamera;
            public CameraRecord orbCamera;
            public int activeCanvasCount;
            public int activeAudioListenerCount;
            public int activeEventSystemCount;
            public bool hudCanvasPresent;
            public string hudCanvasRenderMode;
            public long pngBytes;
            public string result;
            public string detail;
        }

        [Serializable]
        private sealed class CameraRecord
        {
            public string name;
            public Rect rect;
            public Rect pixelRect;
            public bool targetTextureAssigned;

            public static CameraRecord From(Camera camera) => new CameraRecord
            {
                name = camera.name,
                rect = camera.rect,
                pixelRect = camera.pixelRect,
                targetTextureAssigned = camera.targetTexture != null
            };
        }
#endif
    }
}
