using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using UnityEngine;

namespace C6.Prototype.Resources
{
    // Opt-in desktop evidence only. This component never runs in an ordinary app launch or on iOS.
    public sealed class T07Probe : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-c6T07ProbeDirectory") < 0) return;
            var owner = FindAnyObjectByType<T07ResourceController>();
            if (owner != null) owner.gameObject.AddComponent<T07Probe>();
        }
        private T07ResourceController controller;
        private string output, role, port;
        private bool network;
        private bool ownsOutput;
        private string runtimeError;
        private readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
        private readonly List<Recovery> recoveries = new List<Recovery>();
        private Report report;
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            output = Arg(args, "-c6T07ProbeDirectory", null);
            role = Arg(args, "-c6T07Role", "host");
            port = Arg(args, "-c6T07Port", "25077");
            network = Array.IndexOf(args, "-c6T07NetworkProbe") >= 0;
            report = new Report { startedAtUtc = DateTime.UtcNow.ToString("O"), role = role,
                mode = network ? "DESKTOP_TWO_PROCESS_DEBUG_POINTER" : "DESKTOP_HOST_DEBUG_POINTER",
                buildGuid = Application.buildGUID, unity = Application.unityVersion, physicalDevice = false };
            string error = null;
            try
            {
                Require(Path.IsPathRooted(output), "An absolute evidence directory is required.");
                Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Evidence directory must be empty.");
                Directory.CreateDirectory(output); ownsOutput = true;
                Require(role == "host" || role == "client" && network, "Invalid explicit probe role.");
                controller = GetComponent<T07ResourceController>();
                controller.Resource.RecoveryResolved += RecordRecovery;
                Application.logMessageReceived += ObserveLog;
                Application.runInBackground = true;
            }
            catch (Exception ex) { error = ex.ToString(); }
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && error == null)
            {
                object item = null; bool more = false;
                try
                {
                    if (runtimeError != null) throw new InvalidOperationException(runtimeError);
                    more = stack.Peek().MoveNext();
                    if (more) item = stack.Peek().Current; else stack.Pop();
                }
                catch (Exception ex) { error = ex.ToString(); }
                if (error != null) break;
                if (more && item is IEnumerator nested) stack.Push(nested);
                else if (more) yield return item;
            }
            report.status = error == null ? "PASS" : "FAIL"; report.error = error;
            report.finishedAtUtc = DateTime.UtcNow.ToString("O");
            report.checkpoints = checkpoints.ToArray(); report.recoveries = recoveries.ToArray();
            if (ownsOutput) File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
            Debug.Log("C6_T07_PROBE_COMPLETE status=" + report.status + " role=" + role + " error=" + error);
            Application.logMessageReceived -= ObserveLog;
            if (controller != null) { controller.Resource.RecoveryResolved -= RecordRecovery; controller.EndDevelopmentTest(); }
            yield return new WaitForSecondsRealtime(.5f);
            if (Array.IndexOf(args, "-c6T07ProbeQuit") >= 0) Application.Quit(error == null ? 0 : 1);
        }
        private IEnumerator Run()
        {
            for (int i = 0; i < 8; i++) yield return null;
            report.width = Screen.width; report.height = Screen.height; report.safeArea = Screen.safeArea;
            Require(controller.Views.Count == 0 && !controller.Attack.Connected, "Scene secretly started with a host or orbs.");
            bool started = role == "host" ? controller.StartDevelopmentHost(port) : controller.JoinDevelopmentHost("127.0.0.1", port);
            Require(started, "Explicit connection refused.");
            yield return WaitFor(() => controller.Resource.Connected, 20, "Resource session did not connect.");
            Require(controller.Resource.LocalPlayer.stamina == 100 && controller.Views.Count == 0, "Normal start must be 100 and zero orbs.");
            report.localPlayer = controller.Attack.LocalPlayerId;
            if (network) yield return WaitFor(() => controller.Resource.Snapshot.players.Length == 2, 25, "Two authenticated players missing.");
            yield return Capture("empty");
            for (int i = 0; i < 5; i++) yield return Generate();
            Require(controller.Views.Count == 5, "Five requests did not generate exactly five own Raw orbs.");
            Require(controller.Attack.Snapshot.orbs.Where(o => o.owner == report.localPlayer).All(o => o.kind == (int)OrbKind.Raw), "Normal generation created Combined.");
            Require(network || controller.Resource.LocalPlayer.stamina < 8, "Five immediate generation costs were not applied.");
            if (!network)
            {
                Require(controller.Resource.RequestGenerate(), "Explicit denied request did not reach Host.");
                Require(controller.Resource.LastResult != null && !controller.Resource.LastResult.accepted, "Sixth immediate generation was not rejected.");
                yield return Capture("five-generated");
                double before = controller.Resource.LocalPlayer.stamina;
                yield return new WaitForSecondsRealtime(3.05f);
                double restored = controller.Resource.LocalPlayer.stamina - before;
                Require(restored >= 19.9 && restored < 23, "Three-second continuous recovery differs from20.");
                yield return Capture("time-recovery");
                yield return Generate();
                Require(controller.Resource.BeginDebugFixtureRound(), "Explicit debug round refused.");
                yield return FillStorage(); yield return Capture("twenty-orbs");
                double capBefore = controller.Resource.LocalPlayer.stamina;
                Require(controller.Resource.RequestGenerate(), "Cap request did not reach Host.");
                Require(!controller.Resource.LastResult.accepted && controller.Resource.LastResult.reason == "STORAGE_FULL"
                    && controller.Resource.LastResult.staminaBefore == controller.Resource.LastResult.staminaAfter, "Storage reject charged resources.");
                Require(controller.Resource.BeginDebugFixtureRound(), "Second explicit debug round refused.");
                yield return Generate();
                yield return FireOne(80);
                Require(recoveries.Count == 1 && Math.Abs(recoveries[0].added - 5) < .00001, "Actual valid hit did not grant exactly5.");
                yield return Capture("hit-plus-five");
                Require(controller.Resource.ResetNormalRound(), "Reset refused.");
                Require(controller.Views.Count == 0 && controller.Resource.LocalPlayer.stamina == 100 && !controller.Resource.DebugTestMode,
                    "Reset did not restore normal empty100.");
                yield return Capture("reset-empty");
            }
            else yield return NetworkRun();
            Require(controller.TouchBegins == 0 && controller.TouchLaunches == 0, "Debug probe must not claim physical touch.");
            report.clientRigidbodyCount = role == "client" ? FindObjectsByType<Rigidbody>().Length : -1;
            Require(role != "client" || report.clientRigidbodyCount == 0, "Client simulated authoritative physics.");
        }
        private IEnumerator NetworkRun()
        {
            yield return WaitFor(() => controller.Resource.Snapshot.players.All(p => p.generatedTotal == 5), 20, "Both players did not generate5.");
            yield return Capture("normal-two-players");
            if (role == "host")
            {
                yield return new WaitForSecondsRealtime(1);
                Require(controller.Resource.BeginDebugFixtureRound(), "Host debug fixture refused.");
            }
            yield return WaitFor(() => controller.Resource.DebugTestMode && controller.Views.Count == 5, 20, "Peer debug fixture missing.");
            yield return FillStorage();
            yield return WaitFor(() => controller.Attack.Snapshot.orbs.Length == 40 && controller.Resource.Snapshot.players.All(p => p.storedOrbs == 20),
                20, "Expanded live inventory did not synchronize40orbs.");
            uint filledRound = controller.Resource.Snapshot.roundId;
            yield return Capture("forty-network-orbs");
            if (role == "host")
            {
                yield return new WaitForSecondsRealtime(2);
                Require(controller.Resource.BeginDebugFixtureRound(), "Fresh attack trial refused.");
            }
            yield return WaitFor(() => controller.Resource.Snapshot.roundId > filledRound && controller.Resource.DebugTestMode && controller.Views.Count == 5,
                20, "Fresh shared attack round missing.");
            for (int i = 0; i < 5; i++) yield return Generate();
            yield return WaitFor(() => controller.Resource.Snapshot.players.All(p => p.generatedTotal == 5), 20, "Both attacker resources did not spend100.");
            if (role == "host") yield return FireOne(80);
            else
            {
                yield return WaitFor(() => controller.Attack.Snapshot.hp == 80, 20, "Remote Host hit missing.");
                for (int hp = 60; hp >= 0; hp -= 20) yield return FireOne(hp);
            }
            yield return WaitFor(() => controller.Attack.Snapshot.hp == 0 && !controller.Resource.Snapshot.playing, 25, "Final target clear missing.");
            yield return new WaitForSecondsRealtime(.3f);
            if (role == "host")
                Require(recoveries.Count == 5 && recoveries.All(r => r.accepted && !r.duplicate && Math.Abs(r.added - 5) < .00001) && recoveries.Count(r => r.player == 0) == 1,
                    "Expected Host1+Client4 realhit bonuses of5 each.");
            yield return Capture("network-final");
            var frozen = controller.Resource.Snapshot.players.Select(p => p.stamina).ToArray();
            yield return new WaitForSecondsRealtime(1);
            Require(frozen.SequenceEqual(controller.Resource.Snapshot.players.Select(p => p.stamina)), "Regeneration continued after target cleared.");
            if (role == "host") yield return new WaitForSecondsRealtime(3);
        }
        private IEnumerator Generate()
        {
            Require(controller.Resource.RequestGenerate(), "Generate send refused.");
            yield return WaitFor(() => !controller.Resource.HasPending, 8, "Generate not acknowledged.");
            var r = controller.Resource.LastResult;
            Require(r != null && r.accepted && r.known && Math.Abs(r.staminaBefore - r.staminaAfter -20) < .00001,
                "Generation was not one accepted atomic cost20.");
            yield return null;
        }
        private IEnumerator FillStorage()
        {
            while (controller.Views.Count < 20)
            {
                Require(controller.Resource.RequestDebugCombined(), "Explicit debug fill refused.");
                yield return WaitFor(() => !controller.Resource.HasPending, 8, "Debug fill pending.");
                Require(controller.Resource.LastResult.accepted, "Debug fill rejected early.");
                yield return null;
            }
            Require(controller.Views.Keys.Select(controller.GetViewScreenPosition).Distinct().Count() == 20, "Orb display centers are not distinct.");
        }
        private IEnumerator FireOne(int expectedHp)
        {
            Require(controller.Resource.RequestDebugCombined(), "Explicit hit orb refused.");
            yield return WaitFor(() => !controller.Resource.HasPending, 8, "Hit orb not acknowledged.");
            string id = controller.Resource.LastResult.confirmedOrb.id;
            yield return WaitFor(() => controller.Views.ContainsKey(id), 5, "Confirmed hit orb view missing.");
            Vector2 start = controller.GetViewScreenPosition(id);
            var zone = OrbGestureEngine.AttackZone(controller.Layout.BottomPixelRect, controller.Layout.Config.AttackZoneHeightFraction);
            Vector2 to = new Vector2(start.x, zone.yMin + 3);
            int pointer = -7070 - expectedHp;
            Require(controller.BeginPointer(pointer, start, false), "Debug pointer could not select hit orb.");
            controller.MovePointer(pointer, to); controller.EndPointer(pointer, to);
            yield return WaitFor(() => controller.Attack.Snapshot.hp == expectedHp, 5, "Actual Rigidbody did not hit once.");
            Require(!controller.Views.ContainsKey(id), "Confirmed launch retained2D view.");
        }
        private IEnumerator Capture(string name)
        {
            yield return null; yield return new WaitForEndOfFrame();
            checkpoints.Add(new Checkpoint { name = name, resource = JsonUtility.FromJson<ResourceSnapshot>(JsonUtility.ToJson(controller.Resource.Snapshot)),
                attack = JsonUtility.FromJson<AttackSnapshot>(JsonUtility.ToJson(controller.Attack.Snapshot)), ownViews = controller.Views.Count });
            string path = Path.Combine(output, name + ".png");
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, texture.EncodeToPNG()); Destroy(texture);
            Require(new FileInfo(path).Length > 1000, "Screenshot empty.");
        }
        private void RecordRecovery(ResourceRecoveryResult r) => recoveries.Add(new Recovery
        { player = r.PlayerId, added = r.Added, before = r.StaminaBefore, after = r.StaminaAfter, accepted = r.Accepted, duplicate = r.IsDuplicate });
        private void ObserveLog(string text, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = text; }
        private static IEnumerator WaitFor(Func<bool> ready, float seconds, string error)
        { double deadline = Time.realtimeSinceStartupAsDouble + seconds; while (!ready() && Time.realtimeSinceStartupAsDouble < deadline) yield return null; Require(ready(), error); }
        private static void Require(bool okay, string error) { if (!okay) throw new InvalidOperationException(error); }
        private static string Arg(string[] a,string key,string fallback)
        { int i = Array.IndexOf(a,key); return i >= 0 && i+1 < a.Length ? a[i+1] : fallback; }
        [Serializable] private sealed class Checkpoint { public string name; public ResourceSnapshot resource; public AttackSnapshot attack; public int ownViews; }
        [Serializable] private sealed class Recovery { public ulong player; public double added,before,after; public bool accepted,duplicate; }
        [Serializable] private sealed class Report
        { public string status,error,startedAtUtc,finishedAtUtc,role,mode,buildGuid,unity; public bool physicalDevice; public int width,height,clientRigidbodyCount; public Rect safeArea; public ulong localPlayer; public Checkpoint[] checkpoints; public Recovery[] recoveries; }
#endif
    }
}
