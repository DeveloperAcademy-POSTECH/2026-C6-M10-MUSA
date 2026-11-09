using System;
using System.IO;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace C6Lab.Editor
{
    /// <summary>Graphics-enabled batch Play Mode check. Run without -quit; the callback exits after verification.</summary>
    [InitializeOnLoad]
    public static class LabFiveElementsPlaybackCheck
    {
        private const string Key = "C6Lab.FiveElements.Verify";
        private static int firstFrame = -1, samples;
        private static double started;
        private static string firstError;
        private static ProfilerRecorder alloc;
        private static int baselineHits, fpsPass = 60, lastSampleFrame = -1;
        private static readonly FrameTiming[] timing = new FrameTiming[1];

        [Serializable]
        private sealed class Evidence
        {
            public string unity, graphicsApi, graphicsDevice, scene;
            public int firstFrame, lastFrame, naturalHits, sampleFrames, recoveredImpacts;
            public int sixtyFpsClayFrame, thirtyFpsClayFrame;
            public bool duplicateHitSuppressed, staleLeaseIsolated, movingMarkFollowed, destroyedTargetReclaimed;
            public bool hudPauseMonoResumeSubmitted, clayOnlyImpacts;
            public int[] changedPixelsByLane;
            public int claySettlingChangedPixels, drawCallsPeak = -1, trianglesPeak = -1;
            public long editorGcBytesPeak = -1;
            public double cpuFrameMsPeak = -1, gpuFrameMsPeak = -1;
            public string timingScope = "Whole Editor frames, including startup. Zero/unsupported draw counters are -1. GPU timings from a batch graphics context cannot establish device performance.";
        }
        private static Evidence result;

        static LabFiveElementsPlaybackCheck()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
            started = EditorApplication.timeSinceStartup;
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run this verifier in an isolated graphics-enabled Unity batch process.");
            LabFiveElementsDemoBuilder.Prepare();
            string folder = Path.GetFullPath(Arg("-fiveElementsOutput") ?? "Temp/FiveElementsCheck");
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(LabFiveElementsDemoBuilder.ScenePath, OpenSceneMode.Single);
            SessionState.SetString(Key + ".out", folder);
            SessionState.SetBool(Key, true);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.EnterPlaymode();
        }

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (firstError == null && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
                firstError = message + "\n" + stack;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            try
            {
                if (firstError != null) throw new Exception(firstError);
                if (EditorApplication.timeSinceStartup - started > 150)
                    throw new TimeoutException("No advancing game frames or five natural contacts were observed within 150 seconds.");
                if (!EditorApplication.isPlaying) return;
                var demo = UnityEngine.Object.FindAnyObjectByType<LabFiveElementsDemo>();
                if (demo == null || demo.DemoCamera == null) return;
                if (firstFrame < 0)
                {
                    firstFrame = Time.frameCount;
                    baselineHits = demo.VolleyHits;
                    result = new Evidence {
                        firstFrame = firstFrame, unity = Application.unityVersion,
                        scene = demo.gameObject.scene.path, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                        graphicsDevice = SystemInfo.graphicsDeviceName
                    };
                    alloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                    return;
                }
                if (Time.frameCount == lastSampleFrame) return;
                lastSampleFrame = Time.frameCount;
                samples++;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                {
                    if (timing[0].cpuFrameTime > 0) result.cpuFrameMsPeak = Math.Max(result.cpuFrameMsPeak, timing[0].cpuFrameTime);
                    if (timing[0].gpuFrameTime > 0) result.gpuFrameMsPeak = Math.Max(result.gpuFrameMsPeak, timing[0].gpuFrameTime);
                }
                if (alloc.Valid && alloc.Count > 0) result.editorGcBytesPeak = Math.Max(result.editorGcBytesPeak, alloc.LastValue);
                if (UnityStats.drawCalls > 0) result.drawCallsPeak = Math.Max(result.drawCallsPeak, UnityStats.drawCalls);
                if (UnityStats.triangles > 0) result.trianglesPeak = Math.Max(result.trianglesPeak, UnityStats.triangles);
                if (demo.VolleyHits == 0) baselineHits = 0;
                if (baselineHits != 0 || demo.VolleyHits != 5 || Time.frameCount < firstFrame + 5 ||
                    demo.PlaybackTime < LabFiveElementsDemo.FlightSeconds + .45f) return;
                if (fpsPass == 60)
                {
                    result.sixtyFpsClayFrame = Time.frameCount;
                    fpsPass = 30;
                    demo.SetFrameRate(30);
                    demo.Replay();
                    return;
                }
                result.thirtyFpsClayFrame = Time.frameCount;
                result.naturalHits = demo.VolleyHits;
                result.lastFrame = Time.frameCount;
                result.sampleFrames = samples;
                CheckDemo(demo);
                LabFiveElementCostSample.Measure(demo, Output);
                string json = JsonUtility.ToJson(result, true);
                File.WriteAllText(Path.Combine(Output, "verification.json"), json);
                Debug.Log("FIVE_ELEMENTS_PLAYBACK_PASS " + json);
                Exit(0);
            }
            catch (Exception e)
            {
                string reason = e.ToString();
                File.WriteAllText(Path.Combine(Output, "verification-failure.txt"), reason);
                Debug.LogError("FIVE_ELEMENTS_PLAYBACK_FAIL " + reason);
                Exit(1);
            }
        }

        private static string Output => SessionState.GetString(Key + ".out", "Temp");
        private static void Require(bool value, string reason)
        {
            if (!value) throw new InvalidOperationException(reason);
        }

        private static void CheckDemo(LabFiveElementsDemo demo)
        {
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "A real graphics device is required.");
            CheckHud(demo);
            demo.SetPaused(true);
            demo.Seek(.45f);
            Require(demo.ActiveFlights == 5 && demo.ActiveImpacts == 0, "Five flying clay bodies were expected.");
            Capture(demo, "01-flight", 1400, 520);

            demo.Seek(LabFiveElementsDemo.FlightSeconds + .38f);
            Require(demo.ActiveFlights == 0 && demo.ActiveImpacts == 5 && demo.VolleyHits == 5, "Missing active impacts.");
            var clay = Capture(demo, "02-clay-spread", 1400, 520);
            CheckClayOnly(demo);
            demo.Seek(LabFiveElementsDemo.CycleSeconds);
            result.recoveredImpacts = demo.ActiveImpacts;
            Require(demo.ActiveImpacts == 0, "An effect survived its configured mark lifetime.");
            var empty = Capture(demo, null, 1400, 520);
            result.changedPixelsByLane = new int[5];
            for (int i = 0; i < 5; i++)
            {
                result.changedPixelsByLane[i] = Difference(clay, empty, 1400, i * 280, (i + 1) * 280);
                Require(result.changedPixelsByLane[i] > 50, "No rendered clay spread or drops in lane " + i);
            }
            demo.SelectLane(4);
            demo.Seek(LabFiveElementsDemo.FlightSeconds + .04f);
            var squash = Capture(demo, "03-contact-squash", 740, 500);
            demo.Seek(LabFiveElementsDemo.FlightSeconds + .75f);
            var settled = Capture(demo, "04-settled-clay", 740, 500);
            result.claySettlingChangedPixels = Difference(squash, settled, 740, 0, 740);
            Require(result.claySettlingChangedPixels > 80, "The initial squash and settled clay have the same appearance.");

            demo.SelectLane(-1);
            demo.SetFrameRate(30);
            demo.SetComparison(true, true, true, false, true);
            demo.SetSurface(LabDemoSurface.MovingWall);
            demo.Seek(LabFiveElementsDemo.FlightSeconds + .30f);
            Require(demo.VolleyHits == 5 && demo.ActiveImpacts == 5, "Moving-wall strong-hit volley failed.");
            Capture(demo, "05-monochrome-moving-wall", 1400, 520);
            demo.SetSurface(LabDemoSurface.Wall);
            demo.SetComparison(false, false, false, true, false);
            demo.Seek(LabFiveElementsDemo.FlightSeconds + .31f);
            Capture(demo, "06-game-distance-portrait", 390, 455);
            Capture(demo, "07-game-distance-landscape", 840, 300);
            CheckLeases(demo);
            Require(firstError == null, firstError);
        }

        private static void CheckClayOnly(LabFiveElementsDemo demo)
        {
            var pool = LabFiveElementVfx.ForScene(demo.gameObject.scene);
            int visibleMarks = 0;
            foreach (Transform child in pool.transform)
            {
                if (!child.name.StartsWith("Impact ", StringComparison.Ordinal)) continue;
                Require(child.Find("Element release") == null, "An elemental release layer survived in a clay impact.");
                Transform mark = child.Find("Wet surface mark");
                if (mark != null && mark.gameObject.activeInHierarchy && mark.GetComponent<MeshRenderer>().enabled)
                    visibleMarks++;
            }
            foreach (var renderer in pool.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    Require(material == null || material.shader.name != "C6Lab/Five Elements/Release",
                        "The removed elemental explosion shader is still used by a pooled renderer.");
            Require(visibleMarks == 5, "All five contacts must keep visible clay after their squash.");
            result.clayOnlyImpacts = true;
        }

        private static void CheckHud(LabFiveElementsDemo demo)
        {
            var hud = demo.GetComponent<UIDocument>().rootVisualElement;
            Button play = hud.Q<Button>("play"), color = hud.Q<Button>("color");
            Require(play?.panel != null && color?.panel != null, "The comparison HUD is not attached to a live UI Toolkit panel.");
            float instant = demo.PlaybackTime;
            Submit(play);
            Require(demo.Paused && play.text == "재생", "UI Toolkit did not deliver the pause button's submit event.");
            Submit(color);
            Require(color.text == "단색" && demo.Paused && Mathf.Abs(demo.PlaybackTime - instant) < .0001f,
                "The color button failed to switch to monochrome at the paused playhead.");
            Submit(color);
            Require(color.text == "컬러" && Mathf.Abs(demo.PlaybackTime - instant) < .0001f,
                "The color button failed to restore the palette at the same playhead.");
            Submit(play);
            Require(!demo.Paused && play.text == "일시정지", "UI Toolkit did not deliver the resume button's submit event.");
            result.hudPauseMonoResumeSubmitted = true;
        }

        private static void Submit(Button button)
        {
            button.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        private static void CheckLeases(LabFiveElementsDemo demo)
        {
            var pool = LabFiveElementVfx.ForScene(demo.gameObject.scene);
            pool.ClearAll();
            var target = new GameObject("Transient validation target");
            try
            {
                var a = pool.BeginFlight(LabOrbElement.Earth, Vector3.zero, Vector3.forward, .33f);
                Require(a.IsActive && a.Hit(Vector3.zero, Vector3.back, Vector3.forward, target.transform, leaveMark: false),
                    "A real flight failed to create its first impact.");
                result.duplicateHitSuppressed = !a.Hit(Vector3.zero, Vector3.back, Vector3.forward, target.transform) &&
                    pool.ActiveImpactCount == 1 && pool.ActiveFlightCount == 0;
                Require(result.duplicateHitSuppressed, "A repeated contact created another impact.");

                // Reusing the same flight slot must not let its earlier owner move or cancel the new throw.
                var b = pool.BeginFlight(LabOrbElement.Fire, Vector3.right, Vector3.up, .22f);
                a.Cancel();
                a.Move(Vector3.left * 50f, Vector3.left);
                result.staleLeaseIsolated = b.IsActive && !a.IsActive && pool.ActiveImpactCount == 1;
                Require(result.staleLeaseIsolated, "A returned flight interfered with a reused slot or its mark.");
                b.Cancel();

                Transform anchor = pool.transform.Find("Impact 0");
                Require(anchor != null && anchor.gameObject.activeInHierarchy, "Could not inspect the active clay impact transform.");
                var mark = anchor.Find("Wet surface mark").GetComponent<MeshRenderer>();
                Vector3 before = anchor.position;
                target.transform.position = new Vector3(.13f, .24f, .32f);
                pool.Advance(.16f);
                Require(mark.enabled && mark.gameObject.activeInHierarchy,
                    "The Battle contact path must retain brief clay after its squash even without a long-lived mark.");
                result.movingMarkFollowed = (anchor.position - before - target.transform.position).sqrMagnitude < .00001f;
                Require(result.movingMarkFollowed, "A mark detached from its moving rigid target.");
                UnityEngine.Object.DestroyImmediate(target);
                pool.Advance(0f);
                result.destroyedTargetReclaimed = pool.ActiveImpactCount == 0;
                Require(result.destroyedTargetReclaimed, "The destroyed target left a floating mark.");
            }
            finally
            {
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                pool.ClearAll();
            }
        }

        private static Color32[] Capture(LabFiveElementsDemo demo, string file, int width, int height)
        {
            var camera = demo.DemoCamera;
            var oldRect = camera.rect;
            float oldSize = camera.orthographicSize;
            var prev = RenderTexture.active;
            RenderTexture rt = null;
            Texture2D copy = null;
            try
            {
                camera.rect = new Rect(0, 0, 1, 1);
                demo.SetLens((float)width / height);
                rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                var request = new RenderPipeline.StandardRequest { destination = rt };
                Require(RenderPipeline.SupportsRenderRequest(camera, request), "The current renderer cannot capture this camera.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = rt;
                copy = new Texture2D(width, height, TextureFormat.RGB24, false);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                copy.Apply(false, false);
                if (file != null) File.WriteAllBytes(Path.Combine(Output, file + ".png"), copy.EncodeToPNG());
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = prev;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
                camera.rect = oldRect;
                camera.orthographicSize = oldSize;
            }
        }

        private static int Difference(Color32[] a, Color32[] b, int width, int xmin, int xmax)
        {
            int changed = 0;
            for (int row = 0; row < a.Length; row += width)
                for (int x = xmin; x < xmax; x++)
                {
                    var p = a[row + x]; var q = b[row + x];
                    if (Math.Abs(p.r - q.r) + Math.Abs(p.g - q.g) + Math.Abs(p.b - q.b) > 30) changed++;
                }
            return changed;
        }

        private static void Exit(int status)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            if (alloc.Valid) alloc.Dispose();
            EditorApplication.Exit(status);
        }
    }
}
