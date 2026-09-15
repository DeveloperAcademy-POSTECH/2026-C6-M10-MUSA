using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using C6.Prototype.Resources;
using UnityEngine;

namespace C6.Prototype.Combination
{
    // Opt-in desktop evidence only. This component never runs in an ordinary app launch or on iOS.
    public sealed class T08Probe : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-c6T08ProbeDirectory") < 0) return;
            var owner = FindAnyObjectByType<T08CombinationController>();
            if (owner != null && owner.GetComponent<T08Probe>() == null) owner.gameObject.AddComponent<T08Probe>();
        }
        private T08CombinationController controller;
        private string output, role, port;
        private bool network;
        private bool ownsOutput;
        private string runtimeError;
        private readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
        private readonly List<Recovery> recoveries = new List<Recovery>();
        private Report report;
        private readonly List<CombinationReply> combinations = new List<CombinationReply>();
        private int pointerSerial = -8000;
        private string lastCombined;
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-c6T08ProbeDirectory") < 0) yield break;
            output = Arg(args, "-c6T08ProbeDirectory", null);
            role = Arg(args, "-c6T08Role", "host");
            port = Arg(args, "-c6T08Port", "25091");
            network = Array.IndexOf(args, "-c6T08NetworkProbe") >= 0;
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
                controller = GetComponent<T08CombinationController>();
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
            report.checkpoints = checkpoints.ToArray(); report.recoveries = recoveries.ToArray(); report.combinations = combinations.ToArray();
            if (ownsOutput) File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
            Debug.Log("C6_T08_PROBE_COMPLETE status=" + report.status + " role=" + role + " error=" + error);
            Application.logMessageReceived -= ObserveLog;
            if (controller != null) { controller.Resource.RecoveryResolved -= RecordRecovery; controller.EndDevelopmentTest(); }
            yield return new WaitForSecondsRealtime(.5f);
            if (Array.IndexOf(args, "-c6T08ProbeQuit") >= 0) Application.Quit(error == null ? 0 : 1);
        }
        private IEnumerator Run()
        {
            for (int i = 0; i < 8; i++) yield return null;
            report.width = Screen.width; report.height = Screen.height; report.safeArea = Screen.safeArea;
            Require(controller.Views.Count == 0 && !controller.Attack.Connected, "Scene auto-started or supplied orbs.");
            bool started = role == "host" ? controller.StartDevelopmentHost(port) : controller.JoinDevelopmentHost("127.0.0.1", port);
            Require(started, "Explicit connection refused.");
            yield return WaitFor(() => controller.Combination.Connected && controller.Resource.Connected, 25, "Services did not connect.");
            Require(controller.Resource.LocalPlayer.stamina == 100 && controller.Views.Count == 0, "Normal start must be empty100.");
            report.localPlayer = controller.Attack.LocalPlayerId;
            if (network) yield return WaitFor(() => controller.Resource.Snapshot.players.Length == 2, 25, "Two authenticated players missing.");
            yield return Capture("empty");
            for (int i = 0; i < 5; i++) yield return Generate();
            yield return Capture("paid-raw-five");
            yield return MakeNormalCombination();
            yield return Capture("normal-combined");
            if (network)
            {
                if (role == "host") yield return FireConfirmed(lastCombined, 80);
                else
                {
                    yield return WaitFor(() => controller.Attack.Snapshot.hp == 80, 25, "Host first actual hit missing.");
                    yield return FireConfirmed(lastCombined, 60);
                }
                yield return WaitFor(() => controller.Attack.Snapshot.hp == 60, 25, "Two first actual hits missing.");
                yield return MakeNormalCombination();
                if (role == "host") yield return FireConfirmed(lastCombined, 40);
                else
                {
                    yield return WaitFor(() => controller.Attack.Snapshot.hp == 40, 45, "Host second hit missing.");
                    yield return FireConfirmed(lastCombined, 20);
                }
                yield return WaitFor(() => controller.Attack.Snapshot.hp == 20, 45, "Four actual combination attacks missing.");
                yield return new WaitForSecondsRealtime(.5f);
                if (role == "host") Require(recoveries.Count == 4 && recoveries.All(x => x.accepted && !x.duplicate && Math.Abs(x.added-5) < .00001)
                    && recoveries.Count(x => x.player == 0) == 2, "Expected Host2 Client2 actual bonuses5.");
                Require(role != "client" || recoveries.Count == 0, "Client authored a hit bonus.");
                yield return Capture("network-final");
                yield return new WaitForSecondsRealtime(role == "host" ? 4 : 1);
            }
            else
            {
                yield return FireConfirmed(lastCombined, 80);
                Require(recoveries.Count == 1 && Math.Abs(recoveries[0].added-5) < .00001, "Real hit bonus was not5.");
                yield return Capture("normal-hit-plus-five");
                Require(controller.Resource.BeginMixedDebugFixtureRound(), "Explicit mixed fixture refused.");
                yield return WaitFor(() => controller.Views.Count == 5, 5, "Mixed five missing.");
                yield return Capture("debug-mixed-five");
                var yin = OwnRaw().Where(x => x.polarity == (int)OrbPolarity.Yin).OrderBy(x => x.pos.x).ToArray();
                double before = controller.Resource.LocalPlayer.stamina;
                yield return Drop(yin[0], yin[1], false);
                Require(controller.Views.Count == 5 && controller.Resource.LocalPlayer.stamina >= before, "Rejected drop lost material or stamina.");
                yield return Capture("same-polarity-rejected");
                // Select the first column's opposite pair; reverse direction is covered here and in automated tests.
                var yang = OwnRaw().Where(x=>x.polarity == (int)OrbPolarity.Yang).OrderBy(x=>x.pos.x).First();
                var other = OwnRaw().Where(x=>x.polarity == (int)OrbPolarity.Yin).OrderBy(x=>x.pos.x).First();
                yield return Drop(yang, other, true);
                yield return Capture("debug-reverse-combined");
                Require(controller.Resource.ResetNormalRound(), "Normal reset refused.");
                yield return WaitFor(() => controller.Views.Count == 0 && controller.Resource.LocalPlayer.stamina == 100, 5, "Empty100 reset failed.");
                yield return Capture("reset-empty");
            }
            Require(controller.TouchBegins == 0 && controller.TouchLaunches == 0, "Automated pointer cannot count as physical touch.");
            report.clientRigidbodyCount = role == "client" ? FindObjectsByType<Rigidbody>().Length : -1;
            Require(role != "client" || report.clientRigidbodyCount == 0, "Client simulated authoritative physics.");
        }
        private OrbWire[] OwnRaw() => controller.Attack.Snapshot.orbs.Where(x => x.owner == report.localPlayer
            && x.kind == (int)OrbKind.Raw && x.state == (int)OrbAuthorityState.Idle && controller.Views.ContainsKey(x.id)).ToArray();
        private IEnumerator MakeNormalCombination()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 65;
            while (true)
            {
                var raw = OwnRaw();
                var pairs = (from a in raw from b in raw where a.polarity != b.polarity
                    let delta = controller.GetViewScreenPosition(b.id)-controller.GetViewScreenPosition(a.id)
                    let landing = delta.normalized * Mathf.Max(0, delta.magnitude-Screen.width*.06f)
                    where Mathf.Abs(landing.x) < Screen.width*.18f || Mathf.Abs(landing.x) < Mathf.Abs(landing.y)*1.25f
                    orderby delta.sqrMagnitude select new [] {a,b}).FirstOrDefault();
                if (pairs != null) { yield return Drop(pairs[0],pairs[1],true); yield break; }
                Require(Time.realtimeSinceStartupAsDouble < deadline && controller.Views.Count < 20, "No usable normal Seed opposite pair before bounded deadline/cap.");
                yield return WaitFor(() => controller.Resource.CanGenerate, 5, "Generation did not recover20.");
                yield return Generate();
            }
        }
        private IEnumerator Drop(OrbWire source, OrbWire target, bool accepted)
        {
            string previous = controller.Combination.LastResult?.requestId;
            Vector2 from = controller.GetViewScreenPosition(source.id), targetCenter = controller.GetViewScreenPosition(target.id);
            Vector2 delta = targetCenter-from;
            Vector2 to = targetCenter - delta.normalized * Mathf.Min(Screen.width*.06f, delta.magnitude*.25f);
            int pointer = --pointerSerial;
            int count = controller.Views.Count;
            Require(controller.BeginPointer(pointer, from, false), "Debug pointer could not select source.");
            controller.MovePointer(pointer, to);
            // Contact/move alone must not create a combination.
            Require(controller.Combination.LastResult?.requestId == previous, "Contact auto-combined before pointer Up.");
            controller.EndPointer(pointer, to);
            yield return WaitFor(() => controller.Combination.LastResult != null && controller.Combination.LastResult.requestId != previous
                && !controller.Combination.HasPending, 8, "Drop did not obtain a known Host decision.");
            var result = controller.Combination.LastResult;
            combinations.Add(JsonUtility.FromJson<CombinationReply>(JsonUtility.ToJson(result)));
            Require(result.known && result.accepted == accepted, "Unexpected drop decision: "+result.reason);
            if (!accepted)
            {
                Require(result.currentSource != null && result.currentTarget != null && result.currentSource.state == (int)OrbAuthorityState.Idle
                    && result.currentTarget.state == (int)OrbAuthorityState.Idle && controller.Views.Count == count, "Denied drop lost its materials.");
                yield break;
            }
            lastCombined = result.originalCombined.id;
            Require(lastCombined != source.id && lastCombined != target.id && result.originalCombined.kind == (int)OrbKind.Combined
                && result.originalCombined.polarity == (int)OrbPolarity.None, "Success did not produce new Combined None.");
            Require(result.currentSource.state == (int)OrbAuthorityState.Consumed && result.currentTarget.state == (int)OrbAuthorityState.Consumed,
                "Host did not atomically consume both materials.");
            Require(Vector2.Distance(result.originalCombined.pos,(result.sourcePosition+result.targetPosition)*.5f) < .00001f, "Result midpoint differs.");
            yield return WaitFor(() => controller.Views.ContainsKey(lastCombined) && !controller.Views.ContainsKey(source.id)
                && !controller.Views.ContainsKey(target.id) && controller.Views.Count == count-1, 5, "Confirmed inventory/2D views differ.");
            // Explicit transport-level replay of the exact successful request; no replacement materials.
            var replay = new CombinationRequest(result.sessionId,result.roundId,result.requestId,source.id,target.id,result.sequence,result.sourcePosition,result.targetPosition);
            Require(controller.Combination.Submit(replay), "Exact replay send refused.");
            yield return WaitFor(() => !controller.Combination.HasPending, 8, "Replay was not acknowledged.");
            Require(controller.Combination.LastResult.accepted && controller.Combination.LastResult.duplicate
                && controller.Combination.LastResult.originalCombined.id == lastCombined && controller.Views.Count == count-1,
                "Replay created a second result or lost original receipt.");
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
        private IEnumerator FireConfirmed(string id, int expectedHp)
        {
            if (controller.Resource.LocalPlayer.stamina > 85) yield return Generate();
            yield return WaitFor(() => controller.Views.ContainsKey(id), 5, "Combined view missing.");
            Vector2 start = controller.GetViewScreenPosition(id);
            var zone = OrbGestureEngine.AttackZone(controller.Layout.BottomPixelRect,controller.Layout.Config.AttackZoneHeightFraction);
            Vector2 to = new Vector2(start.x,zone.yMin+3);
            int pointer = --pointerSerial;
            Require(controller.BeginPointer(pointer,start,false), "Cannot select actual Combined for launch.");
            controller.MovePointer(pointer,to); controller.EndPointer(pointer,to);
            yield return WaitFor(() => controller.Attack.Snapshot.hp == expectedHp, 8, "Actual Combined Rigidbody did not hit once.");
            Require(!controller.Views.ContainsKey(id), "Launched Combined retained its 2D view.");
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
        { public string status,error,startedAtUtc,finishedAtUtc,role,mode,buildGuid,unity; public bool physicalDevice; public int width,height,clientRigidbodyCount; public Rect safeArea; public ulong localPlayer; public Checkpoint[] checkpoints; public Recovery[] recoveries; public CombinationReply[] combinations; }
#endif
    }
}
