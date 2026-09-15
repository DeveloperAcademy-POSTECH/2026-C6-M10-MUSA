using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace C6.Prototype.Orbs
{
    /// <summary>Explicit development-player evidence, never a normal game or physical-finger test.</summary>
    [DisallowMultipleComponent]
    public sealed class T05Probe : MonoBehaviour
    {
        [SerializeField] private T05OrbController controller;
        public void Configure(T05OrbController owner) => controller = owner;
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-c6T05ProbeDirectory");
            if (index < 0) yield break;
            bool quit = Array.IndexOf(args, "-c6T05ProbeQuit") >= 0;
            string output = null;
            var report = new ProbeReport { buildGuid = Application.buildGUID, startedAtUtc = DateTime.UtcNow.ToString("O") };
            string error = null;
            try
            {
                if (index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                    throw new ArgumentException("Probe directory must be absolute.");
                output = Path.GetFullPath(args[index + 1]);
                if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                    throw new IOException("Use a new probe directory to preserve prior evidence.");
                Directory.CreateDirectory(output);
                if (controller == null) controller = GetComponent<T05OrbController>();
                if (controller == null) throw new InvalidOperationException("Missing T05 controller.");
            }
            catch (Exception exception) { error = exception.Message; }
            if (error != null) { Finish(false, error, quit); yield break; }
            // Only this explicitly requested capture run continues when its window loses focus.
            Application.runInBackground = true;
            for (int i = 0; i < 8; i++) yield return null;
            int portIndex = Array.IndexOf(args, "-c6T05ProbePort");
            string port = portIndex >= 0 && portIndex + 1 < args.Length ? args[portIndex + 1] : "25015";
            controller.StartDevelopmentHost(port);
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!controller.IsHostReady && Time.realtimeSinceStartup < deadline) yield return null;
            if (!controller.IsHostReady)
            {
                SaveFailure(output, report, "Actual development Host did not start: " + controller.Session.Message, quit);
                yield break;
            }
            for (int i = 0; i < 8; i++) yield return null;
            report.screenWidth = Screen.width; report.screenHeight = Screen.height; report.safeArea = Screen.safeArea;
            report.sessionId = controller.Registry.SessionId;
            report.actualHost = controller.Session.OwnedManager.IsHost;
            report.ownerPlayerId = controller.Session.OwnedManager.LocalClientId;
            report.fixture = Snapshot();
            report.upperFraction = controller.Layout.Config.UpperFraction;
            report.horizontalSwipeFraction = controller.Layout.Config.HorizontalSwipeFraction;
            report.horizontalDominance = controller.Layout.Config.HorizontalDominance;
            report.combinationRadiusFraction = controller.Layout.Config.CombinationRadiusFraction;
            report.attackZoneHeightFraction = controller.Layout.Config.AttackZoneHeightFraction;
            report.lowerViewport = controller.Layout.BottomPixelRect;
            report.attackZone = OrbGestureEngine.AttackZone(report.lowerViewport, report.attackZoneHeightFraction);
            report.canvasCount = FindObjectsByType<Canvas>().Length;
            report.audioListenerCount = FindObjectsByType<AudioListener>().Length;
            report.eventSystemCount = FindObjectsByType<EventSystem>().Length;
            report.rigidbodyCount = FindObjectsByType<Rigidbody>().Length;
            yield return Capture(Path.Combine(output, "fixture.png"));
            try
            {
                Require(report.fixture.Length == 3 && report.actualHost, "Host fixture missing");
                var raw = controller.Registry.Snapshot().First(x => x.Polarity == OrbPolarity.Yin);
                var start = controller.GetViewScreenPosition(raw.OrbId);
                var above = new Vector2(start.x, report.lowerViewport.yMax + 20f);
                Require(controller.BeginPointer(101, start, false), "Raw begin failed");
                controller.MovePointer(101, above); controller.EndPointer(101, above);
                report.rawAttackRejectedWithoutRequest = controller.RequestCount == 0 && !controller.Registry.IsPending(raw.OrbId) &&
                    controller.Registry.TryGet(raw.OrbId, out var preserved) && preserved.AuthorityState == OrbAuthorityState.Idle;
                Require(report.rawAttackRejectedWithoutRequest, "Raw incorrectly requested a launch");

                controller.ResetDevelopmentFixture();
                raw = controller.Registry.Snapshot().First(x => x.Polarity == OrbPolarity.Yin);
                var yang = controller.Registry.Snapshot().First(x => x.Polarity == OrbPolarity.Yang);
                start = controller.GetViewScreenPosition(raw.OrbId);
                var drop = controller.GetViewScreenPosition(yang.OrbId);
                Require(controller.BeginPointer(102, start, false), "Drop begin failed");
                controller.MovePointer(102, drop);
                report.contactWithoutDropDoesNothing = controller.RequestCount == 0;
                controller.EndPointer(102, drop);
                report.combineReservedOnlyOnUp = controller.AcceptedReservations == 1 &&
                    controller.LatestDecision.Value.Kind == OrbActionKind.Combine &&
                    controller.Registry.IsPending(raw.OrbId) && controller.Registry.IsPending(yang.OrbId) &&
                    controller.Registry.Snapshot().Count == 3;
                Require(report.contactWithoutDropDoesNothing && report.combineReservedOnlyOnUp, "Drop reservation failed");

                controller.ResetDevelopmentFixture();
                var combined = controller.Registry.Snapshot().First(x => x.Kind == OrbKind.Combined);
                start = controller.GetViewScreenPosition(combined.OrbId);
                var corner = new Vector2(-Screen.width * .1f, report.lowerViewport.yMax + 10f);
                Require(controller.BeginPointer(103, start, false), "Combined begin failed");
                controller.MovePointer(103, corner);
                controller.MovePointer(103, start); controller.EndPointer(103, corner);
                report.zoneWinsAndReservesOnce = controller.AcceptedReservations == 1 && controller.RequestCount == 1 &&
                    controller.LatestDecision.Value.Kind == OrbActionKind.Launch && controller.Registry.IsPending(combined.OrbId);
                report.authorityUnchanged = controller.Registry.Snapshot().All(x => x.AuthorityState == OrbAuthorityState.Idle && x.OwnerPlayerId == report.ownerPlayerId) &&
                    controller.Registry.Snapshot().Count == 3 && FindObjectsByType<Rigidbody>().Length == 0;
                Require(report.zoneWinsAndReservesOnce && report.authorityUnchanged, "A reservation became a gameplay transition");
                report.reservedFixture = Snapshot();
                report.finalRoundId = controller.Registry.RoundId;
            }
            catch (Exception exception) { error = exception.Message; }
            if (error != null) { SaveFailure(output, report, error, quit); yield break; }
            for (int i = 0; i < 3; i++) yield return null;
            yield return Capture(Path.Combine(output, "reserved.png"));
            report.pngsComplete = CompletePng(Path.Combine(output, "fixture.png")) && CompletePng(Path.Combine(output, "reserved.png"));
            controller.EndDevelopmentTest();
            yield return null;
            report.endCleanup = !controller.Registry.HasSession && controller.Views.Count == 0 && !controller.Gestures.HasActivePointer;
            report.result = report.pngsComplete && report.endCleanup ? "PASS" : "FAIL";
            report.detail = "Actual Mac development player; pointer-service calls are debug injection, not physical Touch. PNG save success is separate from visual review.";
            using (var stream = new FileStream(Path.Combine(output, "probe.json"), FileMode.CreateNew))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
            Finish(report.result == "PASS", report.detail, quit);
        }

        private IEnumerator Capture(string path)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!CompletePng(path) && Time.realtimeSinceStartup < deadline) yield return null;
        }
        private OrbEvidence[] Snapshot() => controller.Registry.Snapshot().Select(x => new OrbEvidence
        {
            orbId = x.OrbId, kind = x.Kind.ToString(), polarity = x.Polarity.ToString(),
            owner = x.OwnerPlayerId, state = x.AuthorityState.ToString(), position = x.NormalizedPosition,
            pending = controller.Registry.IsPending(x.OrbId)
        }).ToArray();
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static bool CompletePng(string path)
        {
            try
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (file.Length < 20) return false;
                    var expected = new byte[] { 137,80,78,71,13,10,26,10 };
                    for (int i = 0; i < expected.Length; i++) if (file.ReadByte() != expected[i]) return false;
                    file.Seek(-12, SeekOrigin.End);
                    var ending = new byte[] { 0,0,0,0,73,69,78,68,174,66,96,130 };
                    for (int i = 0; i < ending.Length; i++) if (file.ReadByte() != ending[i]) return false;
                    return true;
                }
            }
            catch (IOException) { return false; }
        }
        private void SaveFailure(string output, ProbeReport report, string error, bool quit)
        {
            report.result = "FAIL"; report.detail = error;
            File.WriteAllText(Path.Combine(output, "probe.json"), JsonUtility.ToJson(report, true));
            if (controller != null) controller.EndDevelopmentTest();
            Finish(false, error, quit);
        }
        private static void Finish(bool passed, string detail, bool quit)
        {
            if (passed) Debug.Log("C6_T05_PROBE PASS " + detail); else Debug.LogError("C6_T05_PROBE FAIL " + detail);
            if (quit) Application.Quit(passed ? 0 : 1);
        }
        [Serializable] private sealed class OrbEvidence
        {
            public string orbId, kind, polarity, state;
            public ulong owner;
            public Vector2 position;
            public bool pending;
        }
        [Serializable] private sealed class ProbeReport
        {
            public string task = "T05", buildNumber = "7", mode = "DEV_HOST_FIXTURE", buildGuid, startedAtUtc, sessionId, result, detail;
            public int screenWidth, screenHeight, canvasCount, audioListenerCount, eventSystemCount, rigidbodyCount;
            public Rect safeArea, lowerViewport, attackZone;
            public ulong ownerPlayerId;
            public uint finalRoundId;
            public float upperFraction, horizontalSwipeFraction, horizontalDominance, combinationRadiusFraction, attackZoneHeightFraction;
            public bool actualHost, rawAttackRejectedWithoutRequest, contactWithoutDropDoesNothing, combineReservedOnlyOnUp,
                zoneWinsAndReservesOnce, authorityUnchanged, pngsComplete, endCleanup;
            public OrbEvidence[] fixture, reservedFixture;
        }
#endif
    }
}
