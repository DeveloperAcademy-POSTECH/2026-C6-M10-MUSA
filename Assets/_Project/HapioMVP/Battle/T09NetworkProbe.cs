using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Combination;
using C6.Prototype.Orbs;
using C6.Prototype.Resources;
using UnityEngine;

namespace C6.Prototype.Battle
{
    // Explicit desktop evidence only. No ordinary-launch, iOS, discovery, or Ready-handshake automation.
    public sealed class T09NetworkProbe : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-c6T09NetworkProbeDirectory") < 0) return;
            var owner = FindAnyObjectByType<T09BattleController>();
            if (owner != null && owner.GetComponent<T09NetworkProbe>() == null) owner.gameObject.AddComponent<T09NetworkProbe>();
        }

        private T09BattleController controller;
        private string output, role, port, runtimeError;
        private bool ownsOutput;
        private int pointerSerial = -19000;
        private readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
        private readonly List<Recovery> recoveries = new List<Recovery>();
        private readonly List<CombinationReply> combinations = new List<CombinationReply>();
        private readonly List<ResourceRequestReply> generations = new List<ResourceRequestReply>();
        private readonly List<DropGeometry> drops = new List<DropGeometry>();
        private readonly List<BoundaryLaunch> launches = new List<BoundaryLaunch>();
        private readonly List<AttackRequestReply> launchReplies = new List<AttackRequestReply>();
        private const int DropMoveSamples = 8;
        private Report report;
        private string nextCombined;

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-c6T09NetworkProbeDirectory") < 0) yield break;
            output = Arg(args, "-c6T09NetworkProbeDirectory", null);
            role = Arg(args, "-c6T09NetworkRole", "host");
            port = Arg(args, "-c6T09NetworkPort", "25102");
            report = new Report { startedAtUtc = DateTime.UtcNow.ToString("O"), role = role,
                mode = "DESKTOP_TWO_PROCESS_NORMAL_SEED_FULL_CENTER_DROP_DEBUG_POINTER", buildGuid = Application.buildGUID,
                dropGeometry = "SOURCE_CENTER_TO_TARGET_CENTER_8_INTERPOLATED_MOVES_THEN_UP",
                launchGeometry = "NO_LAUNCH_IN_FORMER_BAND_THEN_CROSS_ACTUAL_LOWER_TOP_BOUNDARY_ONCE",
                unity = Application.unityVersion, physicalDevice = false,
                horizontalThresholdUse = "HISTORICAL_COMPARISON_ONLY_T09_TRANSFER_DISABLED" };
            string error = null;
            try
            {
                Require(Path.IsPathRooted(output), "An absolute evidence directory is required.");
                Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Evidence directory must be empty.");
                Require(role == "host" || role == "client", "Expected explicit host or client role.");
                Directory.CreateDirectory(output); ownsOutput = true;
                controller = GetComponent<T09BattleController>();
                controller.Resource.RecoveryResolved += RecordRecovery;
                controller.Attack.RequestResolved += RecordLaunchReply;
                Application.logMessageReceived += ObserveLog;
                Application.runInBackground = true;
            }
            catch (Exception exception) { error = exception.ToString(); }
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
                catch (Exception exception) { error = exception.ToString(); }
                if (error != null) break;
                if (more && item is IEnumerator nested) stack.Push(nested);
                else if (more) yield return item;
            }
            report.status = error == null ? "PASS" : "FAIL";
            report.error = error; report.finishedAtUtc = DateTime.UtcNow.ToString("O");
            report.checkpoints = checkpoints.ToArray(); report.recoveries = recoveries.ToArray();
            report.combinations = combinations.ToArray(); report.generations = generations.ToArray(); report.drops = drops.ToArray();
            report.launches = launches.ToArray(); report.launchReplies = launchReplies.ToArray();
            if (controller != null && report.finalBattle == null)
            {
                // Failure fallback only; successful final state was frozen before either peer leaves.
                report.finalBattle = Copy(controller.Battle.Snapshot); report.finalResource = Copy(controller.Resource.Snapshot);
                report.finalAttack = Copy(controller.Attack.Snapshot);
            }
            if (ownsOutput) File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
            Debug.Log($"C6_T09_NETWORK_PROBE_COMPLETE status={report.status} role={role} error={error}");
            Application.logMessageReceived -= ObserveLog;
            if (controller != null) { controller.Resource.RecoveryResolved -= RecordRecovery; controller.Attack.RequestResolved -= RecordLaunchReply; controller.EndDevelopmentTest(); }
            yield return new WaitForSecondsRealtime(.5f);
            if (Array.IndexOf(args, "-c6T09NetworkProbeQuit") >= 0) Application.Quit(error == null ? 0 : 1);
        }

        private IEnumerator Run()
        {
            for (int index = 0; index < 8; index++) yield return null;
            report.width = Screen.width; report.height = Screen.height; report.safeArea = Screen.safeArea;
            Require(!controller.Attack.Connected && controller.Views.Count == 0, "Scene auto-started or pre-generated inventory.");
            Require(!controller.DevelopmentSolo, "Default two-player mode was replaced by solo.");
            Require(controller.Layout.Config.BattleDurationSeconds == 180, "This network probe requires the unchanged180-second configuration.");
            bool started = role == "host" ? controller.StartDevelopmentHost(port) : controller.JoinDevelopmentHost("127.0.0.1", port);
            Require(started, "Explicit direct-IP connection refused.");
            yield return WaitFor(() => controller.Battle.Connected && controller.Battle.Snapshot != null, 25, "Battle services did not bind.");
            report.localPlayer = controller.Attack.LocalPlayerId;
            if (role == "host")
            {
                // Launch Client only after this file appears. It records default single-Host admission.
                Require(controller.Battle.Snapshot.participants == 1 && controller.Battle.Phase == BattlePhase.Lobby,
                    "Start the client after the host lobby-ready evidence file; one-player Lobby must be observed.");
                Require(!controller.Battle.CanStart && !controller.RequestHostStart() && !controller.Resource.RequestGenerate(),
                    "Single Host started or generated without explicit solo mode.");
                Require(controller.Views.Count == 0 && controller.Resource.LocalPlayer.stamina == 100 && !controller.Resource.Snapshot.playing,
                    "Lobby did not preserve empty100 with recovery stopped.");
                yield return Capture("single-host-lobby");
                File.WriteAllText(Path.Combine(output, "host-lobby-ready.json"), JsonUtility.ToJson(controller.Battle.Snapshot, true));
            }
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Ready && controller.Battle.Snapshot.participants == 2,
                35, "Two authenticated participants did not become Ready.");
            Require(!controller.Battle.Snapshot.developmentSolo && !controller.Battle.Snapshot.shortDuration
                && controller.Battle.Snapshot.duration == 180 && !controller.Battle.CanAct && !controller.Resource.Snapshot.playing,
                "Ready must retain normal two-player180 seconds without gameplay.");
            uint readyRound = controller.Battle.Snapshot.roundId;
            yield return Capture("two-participants-ready");
            if (role == "host")
            {
                // Allow the client to capture Ready before the explicit Host Start action.
                yield return new WaitForSecondsRealtime(1);
                Require(controller.RequestHostStart(), "Host Start rejected Ready.");
            }
            else Require(!controller.RequestHostStart() && !controller.Battle.RetryHost() && !controller.Battle.ReturnLobby(),
                "Client authored a Host-only start/reset.");
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Playing && controller.Resource.Snapshot.playing, 15, "Explicit Host Start did not reach Playing.");
            Require(controller.Battle.Snapshot.roundId > readyRound && controller.Views.Count == 0
                && controller.Resource.LocalPlayer.stamina == 100 && !controller.Resource.Snapshot.debugTestMode,
                "Host Start did not confirm a new empty100 normal round.");
            report.playingSession = controller.Battle.Snapshot.sessionId; report.playingRound = controller.Battle.Snapshot.roundId;
            yield return Capture("playing-empty-full");
            for (int index = 0; index < 5; index++) yield return Generate();
            yield return Capture("paid-five-raw");
            yield return MakeNormalCombination();
            yield return Capture("first-normal-combined");
            if (role == "host") yield return FireConfirmed(nextCombined, 80);
            else
            {
                yield return WaitFor(() => controller.Attack.Snapshot.hp == 80, 35, "Host first hit missing.");
                yield return FireConfirmed(nextCombined, 60);
            }
            yield return WaitFor(() => controller.Attack.Snapshot.hp == 60, 35, "First Host/Client attacks missing.");
            yield return MakeNormalCombination();
            if (role == "host") yield return FireConfirmed(nextCombined, 40);
            else
            {
                yield return WaitFor(() => controller.Attack.Snapshot.hp == 40, 65, "Host second hit missing.");
                yield return FireConfirmed(nextCombined, 20);
            }
            yield return WaitFor(() => controller.Attack.Snapshot.hp == 20, 65, "Second Host/Client attacks missing.");
            if (role == "host")
            {
                yield return MakeNormalCombination();
                yield return FireConfirmed(nextCombined, 0);
            }
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Victory && controller.Attack.Snapshot.hp == 0
                && !controller.Resource.Snapshot.playing && controller.Attack.ActiveProjectileCount == 0,
                70, "Five real combination hits did not confirm Victory with frozen resources.");
            Require(controller.Battle.Snapshot.sessionId == report.playingSession && controller.Battle.Snapshot.roundId == report.playingRound,
                "The battle reset while completing its five actual hits.");
            Require(controller.Battle.Snapshot.remaining > 0 && controller.Battle.Snapshot.teamHp > 0
                && controller.Battle.Snapshot.observedMonsterHp == 0 && controller.Battle.Snapshot.duration == 180,
                "Victory values do not match the actual completed battle.");
            Require(!controller.Battle.CanAct && !controller.Resource.RequestGenerate() && !controller.Resource.HasPending
                && !controller.Combination.HasPending && !controller.HasCombinationPending,
                "Confirmed result did not stop actions and clear pending state.");
            Require(controller.Hud.ResultOverlay.activeInHierarchy, "Result panel missing.");
            if (role == "host")
                Require(recoveries.Count == 5 && recoveries.Count(value => value.player == report.localPlayer) == 3
                    && recoveries.Count(value => value.player != report.localPlayer) == 2
                    && recoveries.All(value => value.accepted && !value.duplicate && Math.Abs(value.added - 5) < .00001),
                    "Expected five Host-authored bonuses5: Host3 / Client2, including final kill.");
            else Require(recoveries.Count == 0, "Client authored a recovery.");
            var frozenBattle = Copy(controller.Battle.Snapshot);
            double frozenStamina = controller.Resource.LocalPlayer.stamina;
            int frozenHits = controller.Attack.Snapshot.totalHits;
            yield return new WaitForSecondsRealtime(1.2f);
            Require(controller.Battle.Phase == BattlePhase.Victory && controller.Battle.Snapshot.remaining == frozenBattle.remaining
                && controller.Battle.Snapshot.teamHp == frozenBattle.teamHp && controller.Battle.Snapshot.observedMonsterHp == 0
                && controller.Resource.LocalPlayer.stamina == frozenStamina && controller.Attack.Snapshot.totalHits == frozenHits,
                "Result clock/resources/hits changed after confirmation.");
            Require(!controller.Resource.Snapshot.debugTestMode && generations.All(value => value.accepted && value.operation == (int)ResourceRequestKind.Generate),
                "A development fixture replaced normal seed generation.");
            Require(combinations.Count == (role == "host" ? 3 : 2), "Unexpected actual combination count.");
            Require(controller.TouchBegins == 0 && controller.TouchLaunches == 0, "Debug pointers must not count as physical touch.");
            report.clientRigidbodyCount = role == "client" ? FindObjectsByType<Rigidbody>().Length : -1;
            Require(role != "client" || report.clientRigidbodyCount == 0, "Client simulated authoritative physics.");
            yield return Capture("victory-frozen-final");
            yield return new WaitForSecondsRealtime(role == "host" ? 4 : 1.5f);
        }

        private OrbWire[] OwnRaw() => controller.Attack.Snapshot.orbs.Where(value => value.owner == report.localPlayer
            && value.kind == (int)OrbKind.Raw && value.state == (int)OrbAuthorityState.Idle && controller.Views.ContainsKey(value.id)).ToArray();
        private IEnumerator MakeNormalCombination()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 65;
            while (true)
            {
                var raw = OwnRaw();
                var pair = (from a in raw from b in raw where a.id != b.id && a.polarity != b.polarity
                    let targetCenter = controller.GetViewScreenPosition(b.id)
                    let delta = targetCenter - controller.GetViewScreenPosition(a.id)
                    where controller.Layout.BottomPixelRect.Contains(targetCenter)
                    orderby delta.sqrMagnitude, a.id, b.id select new[] { a, b }).FirstOrDefault();
                if (pair != null) { yield return Drop(pair[0], pair[1]); yield break; }
                Require(controller.Battle.Phase == BattlePhase.Playing && Time.realtimeSinceStartupAsDouble < deadline
                    && controller.Views.Count < controller.Layout.Config.OrbStorageLimit,
                    "Normal Seed did not produce a usable opposite pair before its deadline/cap; no fixture was substituted.");
                yield return WaitFor(() => controller.Resource.CanGenerate, 5, "Normal stamina did not recover20 for the next seed draw.");
                yield return Generate();
            }
        }
        private IEnumerator Drop(OrbWire source, OrbWire target)
        {
            string previous = controller.Combination.LastResult?.requestId;
            Vector2 from = controller.GetViewScreenPosition(source.id), targetCenter = controller.GetViewScreenPosition(target.id);
            // T09 allows full center-to-center combinations throughout its lower workspace,
            // including horizontal travel beyond the historical transfer threshold.
            Vector2 to = targetCenter;
            int pointer = --pointerSerial; int count = controller.Views.Count;
            Require(controller.BeginPointer(pointer, from, false) && controller.Gestures.ActiveOrb?.OrbId == source.id,
                "Debug pointer could not select the intended normal Raw source.");
            var geometry = new DropGeometry { sourceId = source.id, targetId = target.id, from = from,
                targetCenter = targetCenter, release = to, delta = to - from, plannedMoveSamples = DropMoveSamples,
                horizontalSwipeFraction = controller.Layout.Config.HorizontalSwipeFraction,
                horizontalDominance = controller.Layout.Config.HorizontalDominance, horizontalTransferEnabled = false };
            drops.Add(geometry);
            for (int sample = 1; sample <= DropMoveSamples; sample++)
            {
                controller.MovePointer(pointer, Vector2.Lerp(from, to, sample / (float)DropMoveSamples));
                geometry.completedMoveSamples = sample;
                Require(controller.Combination.LastResult?.requestId == previous && !controller.Combination.HasPending
                    && !controller.HasCombinationPending && !controller.Gestures.HasPending
                    && controller.Gestures.ActiveOrb?.OrbId == source.id,
                    "Interpolated center drop combined, transferred, or ended before actual pointer Up.");
                yield return null;
                Require(controller.Combination.LastResult?.requestId == previous && !controller.Combination.HasPending
                    && !controller.HasCombinationPending && !controller.Gestures.HasPending
                    && controller.Gestures.ActiveOrb?.OrbId == source.id,
                    "Contact caused an early action while waiting for pointer Up.");
            }
            geometry.noEarlyActionBeforeUp = true;
            controller.EndPointer(pointer, to);
            yield return WaitFor(() => controller.Combination.LastResult != null && controller.Combination.LastResult.requestId != previous
                && !controller.Combination.HasPending && !controller.HasCombinationPending, 8, "Drop did not obtain confirmed inventory.");
            var reply = controller.Combination.LastResult; combinations.Add(Copy(reply));
            Require(reply.known && reply.accepted && !reply.duplicate && reply.sourceOrbId == source.id && reply.targetOrbId == target.id
                && reply.currentSource.state == (int)OrbAuthorityState.Consumed
                && reply.currentTarget.state == (int)OrbAuthorityState.Consumed, "Host did not consume both Raw materials once: " + reply.reason);
            nextCombined = reply.originalCombined.id;
            Require(nextCombined != source.id && nextCombined != target.id && reply.originalCombined.kind == (int)OrbKind.Combined
                && reply.originalCombined.polarity == (int)OrbPolarity.None, "New Combined identity/polarity invalid.");
            Require(Vector2.Distance(reply.originalCombined.pos, (reply.sourcePosition + reply.targetPosition) * .5f) < .00001f,
                "Combined did not use the actual Drop midpoint.");
            yield return WaitFor(() => controller.Views.ContainsKey(nextCombined) && !controller.Views.ContainsKey(source.id)
                && !controller.Views.ContainsKey(target.id) && controller.Views.Count == count - 1, 5, "New confirmed inventory and local views differ.");
        }
        private IEnumerator Generate()
        {
            Require(controller.Resource.RequestGenerate(), "Normal Generate send refused.");
            yield return WaitFor(() => !controller.Resource.HasPending, 8, "Normal Generate did not receive Host confirmation.");
            var result = controller.Resource.LastResult;
            Require(result != null && result.known && result.accepted && result.operation == (int)ResourceRequestKind.Generate
                && Math.Abs(result.staminaBefore - result.staminaAfter - 20) < .00001, "Generate did not atomically charge20 for one Raw.");
            generations.Add(Copy(result)); yield return null;
        }
        private IEnumerator FireConfirmed(string id, int expectedHp)
        {
            // Keep room for the exact +5 bonus without replacing the normal generation cost or seed.
            if (controller.Resource.LocalPlayer.stamina > 85) yield return Generate();
            yield return WaitFor(() => controller.Views.ContainsKey(id), 5, "Actual Combined view is missing.");
            Vector2 from = controller.GetViewScreenPosition(id);
            Rect lower = controller.Layout.BottomPixelRect;
            Rect formerBand = OrbGestureEngine.AttackZone(lower, controller.Layout.Config.AttackZoneHeightFraction);
            Vector2 formerBandPoint = new Vector2(from.x, formerBand.center.y);
            Vector2 belowBoundary = new Vector2(from.x, lower.yMax - 2f);
            Vector2 acrossBoundary = new Vector2(from.x, lower.yMax + 3f);
            int hitsBefore = controller.Attack.Snapshot.roundHits;
            int hpBefore = controller.Attack.Snapshot.hp;
            int repliesBefore = launchReplies.Count;
            Require(expectedHp == hpBefore - 20 && lower.Contains(formerBandPoint) && lower.Contains(belowBoundary),
                "Boundary launch setup must start with one expected hit and both waiting points inside the lower area.");
            var geometry = new BoundaryLaunch { orbId = id, from = from, lowerRect = lower,
                formerBandPoint = formerBandPoint, belowBoundary = belowBoundary, acrossBoundary = acrossBoundary,
                roundHitsBefore = hitsBefore, hpBefore = hpBefore };
            launches.Add(geometry);
            int pointer = --pointerSerial;
            Require(controller.BeginPointer(pointer, from, false) && controller.Gestures.ActiveOrb?.OrbId == id,
                "Automated pointer could not select the intended Combined for boundary launch.");
            controller.MovePointer(pointer, formerBandPoint);
            yield return new WaitForSecondsRealtime(.15f);
            AssertHeldWithoutLaunch(id, pointer, repliesBefore, hitsBefore, hpBefore);
            geometry.formerBandDidNotLaunch = true;
            if (launches.Count == 1) yield return Capture("held-combined-inside-former-band");
            AssertHeldWithoutLaunch(id, pointer, repliesBefore, hitsBefore, hpBefore);
            controller.MovePointer(pointer, belowBoundary);
            yield return new WaitForSecondsRealtime(.1f);
            AssertHeldWithoutLaunch(id, pointer, repliesBefore, hitsBefore, hpBefore);
            geometry.belowBoundaryDidNotLaunch = true;
            // Extra movement and Up after crossing must not submit another launch for this ID.
            controller.MovePointer(pointer, acrossBoundary);
            controller.MovePointer(pointer, new Vector2(from.x, lower.yMax + 8f));
            controller.EndPointer(pointer, new Vector2(from.x, lower.yMax + 8f));
            yield return WaitFor(() => controller.Attack.Snapshot.hp == expectedHp, 8, "Combined Rigidbody did not produce the expected actual hit.");
            Require(!controller.Views.ContainsKey(id), "Launched Combined retained its2D view.");
            var accepted = launchReplies.Where(value => value.orbId == id && value.known && value.accepted && !value.duplicate)
                .Select(value => value.requestId).Distinct(StringComparer.Ordinal).ToArray();
            Require(accepted.Length == 1 && controller.Attack.Snapshot.roundHits == hitsBefore + 1,
                "Crossing the actual boundary and later Move/Up must confirm one launch request and one real hit.");
            geometry.acceptedUniqueRequests = accepted.Length; geometry.acceptedRequestId = accepted[0];
            geometry.roundHitsAfter = controller.Attack.Snapshot.roundHits; geometry.hpAfter = controller.Attack.Snapshot.hp;
        }
        private void AssertHeldWithoutLaunch(string id, int pointer, int repliesBefore, int hitsBefore, int hpBefore)
        {
            var wire = controller.Attack.Snapshot.orbs.FirstOrDefault(value => value.id == id);
            Require(controller.Gestures.ActivePointerId == pointer && controller.Gestures.ActiveOrb?.OrbId == id
                && !controller.Gestures.HasPending && controller.Views.ContainsKey(id)
                && wire != null && wire.state == (int)OrbAuthorityState.Idle
                && controller.Attack.ActiveProjectileCount == 0 && launchReplies.Count == repliesBefore
                && controller.Attack.Snapshot.roundHits == hitsBefore && controller.Attack.Snapshot.hp == hpBefore,
                "A held Combined launched or stopped being held before crossing the actual lower/top boundary.");
        }

        private IEnumerator Capture(string name)
        {
            yield return null; yield return new WaitForEndOfFrame();
            var checkpoint = new Checkpoint { name = name, battle = Copy(controller.Battle.Snapshot), resource = Copy(controller.Resource.Snapshot),
                attack = Copy(controller.Attack.Snapshot), ownViews = controller.Views.Count };
            checkpoints.Add(checkpoint);
            if (name == "victory-frozen-final")
            {
                // Peer shutdown is transport cleanup. Freeze gameplay evidence while both peers
                // are still connected, instead of reading cleared authorities after the keepalive wait.
                report.finalBattle = checkpoint.battle; report.finalResource = checkpoint.resource; report.finalAttack = checkpoint.attack;
                report.finalSnapshotCheckpoint = name;
                report.finalSnapshotCapturedAtUtc = DateTime.UtcNow.ToString("O");
            }
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            string path = Path.Combine(output, name + ".png");
            File.WriteAllBytes(path, image.EncodeToPNG()); Destroy(image);
            Require(new FileInfo(path).Length > 1000, "Screenshot is empty.");
        }
        private void RecordLaunchReply(AttackRequestReply value) => launchReplies.Add(Copy(value));
        private void RecordRecovery(ResourceRecoveryResult value) => recoveries.Add(new Recovery
        { player = value.PlayerId, added = value.Added, before = value.StaminaBefore, after = value.StaminaAfter, accepted = value.Accepted, duplicate = value.IsDuplicate });
        private void ObserveLog(string text, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = text; }
        private static T Copy<T>(T value) where T : class => value == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private static IEnumerator WaitFor(Func<bool> ready, float seconds, string error)
        { double deadline = Time.realtimeSinceStartupAsDouble + seconds; while (!ready() && Time.realtimeSinceStartupAsDouble < deadline) yield return null; Require(ready(), error); }
        private static void Require(bool okay, string error) { if (!okay) throw new InvalidOperationException(error); }
        private static string Arg(string[] args, string key, string fallback)
        { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
        [Serializable] private sealed class Recovery { public ulong player; public double added, before, after; public bool accepted, duplicate; }
        [Serializable] private sealed class Checkpoint
        { public string name; public BattleSnapshot battle; public ResourceSnapshot resource; public AttackSnapshot attack; public int ownViews; }
        [Serializable] private sealed class DropGeometry
        { public string sourceId, targetId; public Vector2 from, targetCenter, release, delta;
            public int plannedMoveSamples, completedMoveSamples; public float horizontalSwipeFraction, horizontalDominance;
            public bool noEarlyActionBeforeUp, horizontalTransferEnabled; }
        [Serializable] private sealed class BoundaryLaunch
        { public string orbId, acceptedRequestId; public Vector2 from, formerBandPoint, belowBoundary, acrossBoundary;
            public Rect lowerRect; public bool formerBandDidNotLaunch, belowBoundaryDidNotLaunch;
            public int acceptedUniqueRequests, roundHitsBefore, roundHitsAfter, hpBefore, hpAfter; }
        [Serializable] private sealed class Report
        {
            public string status, error, startedAtUtc, finishedAtUtc, role, mode, buildGuid, unity, playingSession, dropGeometry, launchGeometry, horizontalThresholdUse;
            public string finalSnapshotCheckpoint, finalSnapshotCapturedAtUtc;
            public bool physicalDevice; public int width, height, clientRigidbodyCount; public Rect safeArea;
            public ulong localPlayer; public uint playingRound;
            public Checkpoint[] checkpoints; public Recovery[] recoveries; public CombinationReply[] combinations; public ResourceRequestReply[] generations;
            public DropGeometry[] drops; public BoundaryLaunch[] launches; public AttackRequestReply[] launchReplies;
            public BattleSnapshot finalBattle; public ResourceSnapshot finalResource; public AttackSnapshot finalAttack;
        }
#endif
    }
}
