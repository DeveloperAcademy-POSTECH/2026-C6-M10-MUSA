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
    // Explicit desktop evidence only. No fixture, shortened clock, simulated collision, or iOS input.
    public sealed class T09Probe : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-c6T09ProbeDirectory") < 0) return;
            var owner = FindAnyObjectByType<T09BattleController>();
            if (owner != null && owner.GetComponent<T09Probe>() == null) owner.gameObject.AddComponent<T09Probe>();
        }

        private T09BattleController controller;
        private string output, role, port, runtimeError;
        private bool ownsOutput;
        private int pointerSerial = -9000;
        private string lastCombined;
        private Report report;
        private readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
        private readonly List<Recovery> recoveries = new List<Recovery>();
        private readonly List<CombinationReply> combinations = new List<CombinationReply>();
        private readonly List<ResourceRequestReply> generations = new List<ResourceRequestReply>();
        private readonly List<ClockSample> clockSamples = new List<ClockSample>();
        private readonly List<DropGeometry> drops = new List<DropGeometry>();
        private readonly List<BoundaryLaunch> launches = new List<BoundaryLaunch>();
        private readonly List<AttackRequestReply> launchReplies = new List<AttackRequestReply>();
        private const int DropMoveSamples = 8;

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-c6T09ProbeDirectory") < 0) yield break;
            output = Arg(args, "-c6T09ProbeDirectory", null);
            role = Arg(args, "-c6T09Role", "solo");
            port = Arg(args, "-c6T09Port", "25109");
            report = new Report { startedAtUtc = DateTime.UtcNow.ToString("O"), role = role,
                mode = role == "clock" ? "DESKTOP_EXPLICIT_SOLO_DEFAULT_180_CLOCK" : "DESKTOP_EXPLICIT_SOLO_NORMAL_SEED_FULL_CENTER_DROP",
                sourceInput = "AUTOMATED",
                horizontalThresholdUse = "HISTORICAL_COMPARISON_ONLY_T09_TRANSFER_DISABLED",
                dropGeometry = role == "clock" ? "NOT_APPLICABLE_CLOCK_ONLY" : "SOURCE_CENTER_TO_TARGET_CENTER_8_INTERPOLATED_MOVES_THEN_UP",
                launchGeometry = role == "clock" ? "NOT_APPLICABLE_CLOCK_ONLY" : "NO_LAUNCH_IN_FORMER_BAND_THEN_CROSS_ACTUAL_LOWER_TOP_BOUNDARY_ONCE",
                buildGuid = Application.buildGUID, unity = Application.unityVersion, physicalDevice = false };
            string error = null;
            try
            {
                Require(!string.IsNullOrEmpty(output) && Path.IsPathRooted(output), "An absolute evidence directory is required.");
                Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Evidence directory must be empty.");
                Directory.CreateDirectory(output); ownsOutput = true;
                Require(role == "solo" || role == "clock", "Role must be solo or clock.");
                controller = GetComponent<T09BattleController>();
                Require(controller != null, "Saved BattleLoop controller is missing.");
                controller.Resource.RecoveryResolved += RecordRecovery;
                controller.Attack.RequestResolved += RecordLaunchReply;
                Application.logMessageReceived += ObserveLog;
                Application.runInBackground = true;
                Debug.Log("C6_T09_PROBE_BEGIN role=" + role + " sourceInput=AUTOMATED physicalDevice=false buildGuid=" + Application.buildGUID);
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
            // Failure cleanup never turns an incomplete run into a pass.
            Application.logMessageReceived -= ObserveLog;
            if (controller != null)
            {
                controller.Resource.RecoveryResolved -= RecordRecovery;
                controller.Attack.RequestResolved -= RecordLaunchReply;
                controller.EndDevelopmentTest();
            }
            report.status = error == null ? "PASS" : "FAIL"; report.error = error;
            report.finishedAtUtc = DateTime.UtcNow.ToString("O");
            report.checkpoints = checkpoints.ToArray(); report.recoveries = recoveries.ToArray();
            report.combinations = combinations.ToArray(); report.generations = generations.ToArray();
            report.clockSamples = clockSamples.ToArray(); report.drops = drops.ToArray();
            report.launches = launches.ToArray(); report.launchReplies = launchReplies.ToArray();
            if (ownsOutput) File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
            Debug.Log("C6_T09_PROBE_COMPLETE status=" + report.status + " role=" + role
                + " sourceInput=AUTOMATED physicalDevice=false buildGuid=" + Application.buildGUID + " error=" + error);
            yield return new WaitForSecondsRealtime(.5f);
            if (Array.IndexOf(args, "-c6T09ProbeQuit") >= 0) Application.Quit(error == null ? 0 : 1);
        }

        private IEnumerator Run()
        {
            for (int i = 0; i < 8; i++) yield return null;
            report.width = Screen.width; report.height = Screen.height; report.safeArea = Screen.safeArea;
            report.configDuration = controller.Layout.Config.BattleDurationSeconds;
            Require(controller.Views.Count == 0 && !controller.Attack.Connected && controller.Battle.Phase == BattlePhase.Boot,
                "Saved scene auto-started a battle or supplied orbs.");
            Require(report.configDuration == 180 && controller.Layout.Config.TeamHpDecayPerSecond == 1,
                "This evidence scenario requires the unchanged default 180-second clock and decay1.");
            yield return Capture("boot");
            controller.ConfigureDevelopmentSolo(true);
            Require(controller.StartDevelopmentHost(port), "Explicit development Host refused.");
            yield return WaitFor(() => controller.Battle.Connected && controller.Battle.Phase == BattlePhase.Ready, 25,
                "Explicit solo Host did not reach Ready.");
            report.localPlayer = controller.Attack.LocalPlayerId;
            Require(controller.Battle.Snapshot.developmentSolo && !controller.Battle.Snapshot.shortDuration
                && controller.Battle.Snapshot.participants == 1, "One-participant development mode was not explicit.");
            AssertEmptyReady();
            yield return Capture("ready-empty100");
            double waitingTime = controller.Battle.Snapshot.remaining;
            yield return new WaitForSecondsRealtime(1.1f);
            Require(controller.Battle.Snapshot.remaining == waitingTime && !controller.RequestGenerate(), "Ready clock or generation was active.");
            uint readyRound = controller.Battle.Snapshot.roundId;
            Require(controller.RequestHostStart(), "Explicit Host Start refused.");
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Playing && controller.CanInteract, 5, "Host Start did not enable Playing.");
            Require(controller.Battle.Snapshot.roundId > readyRound && controller.Views.Count == 0
                && controller.Resource.LocalPlayer.stamina == 100 && !controller.Resource.Snapshot.debugTestMode,
                "Start did not create a fresh normal round with empty inventory and full stamina.");
            report.playingRound = controller.Battle.Snapshot.roundId;
            report.authoritativeStartedAt = controller.Battle.Snapshot.startedAt;
            report.authoritativeDeadline = controller.Battle.Snapshot.deadline;
            yield return Capture("playing-empty100");
            if (role == "solo") yield return NormalVictory();
            else yield return FullClockDefeat();
            Require(controller.TouchBegins == 0 && controller.TouchLaunches == 0, "Automated pointer was misreported as physical touch.");
            report.touchBegins = controller.TouchBegins; report.touchLaunches = controller.TouchLaunches;
            controller.EndDevelopmentTest();
            yield return WaitFor(() => !controller.Attack.Connected && !controller.Battle.Connected
                && controller.Battle.Phase == BattlePhase.Boot && controller.Views.Count == 0, 5, "Explicit session end did not clean up.");
            Require(!controller.HasCombinationPending && !controller.Gestures.HasActivePointer && !controller.Gestures.HasPending,
                "Pointer or request remained after explicit close.");
            yield return Capture("closed-boot");
        }

        private IEnumerator NormalVictory()
        {
            for (int i = 0; i < 5; i++) yield return Generate();
            Require(generations.Count == 5 && controller.Views.Count == 5, "Five paid taps did not produce five individual Raw orbs.");
            yield return Capture("normal-paid-five");
            for (int i = 1; i <= 5; i++)
            {
                yield return MakeNormalCombination();
                yield return Capture("normal-combined-" + i);
                yield return FireConfirmed(lastCombined, 100 - i * 20, i);
                yield return Capture("actual-hit-" + i);
            }
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Victory, 5, "Five actual hits did not produce Victory.");
            Require(controller.Attack.Snapshot.roundHits == 5 && recoveries.Count == 5 && combinations.Count == 5,
                "Normal round did not finish with exactly five combinations, physical hits, and recovery events.");
            Require(recoveries.All(x => x.accepted && !x.duplicate && x.player == report.localPlayer && Math.Abs(x.added - 5) < .00001),
                "Each actual attacker hit, including the killing hit, must receive exactly5.");
            Require(!controller.Resource.Snapshot.debugTestMode && controller.Battle.Snapshot.remaining > 0,
                "Normal victory used a fixture or occurred after the deadline.");
            report.elapsedUntilResult = Time.realtimeSinceStartupAsDouble - report.authoritativeStartedAt;
            yield return VerifyFrozenResult("victory");
            yield return RetryAndFirstGenerate();
        }

        private IEnumerator FullClockDefeat()
        {
            Require(controller.Battle.Snapshot.duration == 180 && !controller.Battle.Snapshot.shortDuration,
                "Clock proof must use the complete default duration, without an override.");
            double stopAt = report.authoritativeDeadline + 15, nextSample = 0, nextProgress = 0;
            double previousRemaining = controller.Battle.Snapshot.remaining;
            while (controller.Battle.Phase == BattlePhase.Playing && Time.realtimeSinceStartupAsDouble < stopAt)
            {
                var snapshot = controller.Battle.Snapshot;
                Require(snapshot.remaining <= previousRemaining + .000001 && Math.Abs(snapshot.teamHp - snapshot.remaining) < .000001,
                    "Host Team HP increased or differed from the remaining clock.");
                previousRemaining = snapshot.remaining;
                Require(controller.Views.Count == 0 && controller.Attack.Snapshot.hp == 100
                    && controller.Attack.Snapshot.roundHits == 0 && controller.Resource.LocalPlayer.stamina == 100,
                    "Passive clock trial created gameplay or changed full stamina.");
                double now = Time.realtimeSinceStartupAsDouble;
                if (now >= nextSample)
                {
                    nextSample = now + 1;
                    clockSamples.Add(new ClockSample { observedAt = now, remaining = snapshot.remaining,
                        teamHp = snapshot.teamHp, phase = snapshot.phase, revision = snapshot.revision });
                }
                if (now >= nextProgress)
                {
                    nextProgress = now + 30;
                    Debug.Log("C6_T09_PROBE_CLOCK sourceInput=AUTOMATED physicalDevice=false elapsed="
                        + (now - report.authoritativeStartedAt).ToString("R") + " remaining=" + snapshot.remaining.ToString("R"));
                }
                yield return null;
            }
            report.elapsedUntilResult = Time.realtimeSinceStartupAsDouble - report.authoritativeStartedAt;
            Require(controller.Battle.Phase == BattlePhase.Defeat && report.elapsedUntilResult >= 180
                && controller.Battle.Snapshot.remaining == 0 && controller.Battle.Snapshot.teamHp == 0,
                "Actual 180-second elapsed time did not produce Defeat at zero.");
            Require(controller.Battle.Snapshot.observedMonsterHp == 100 && recoveries.Count == 0 && generations.Count == 0
                && combinations.Count == 0, "Clock-only Defeat included gameplay or forced-result activity.");
            clockSamples.Add(new ClockSample { observedAt = Time.realtimeSinceStartupAsDouble, remaining = 0,
                teamHp = 0, phase = controller.Battle.Snapshot.phase, revision = controller.Battle.Snapshot.revision });
            yield return VerifyFrozenResult("defeat-full180");
            yield return RetryAndFirstGenerate();
        }

        private IEnumerator VerifyFrozenResult(string label)
        {
            yield return null; yield return null;
            var before = Clone(controller.Battle.Snapshot);
            var stamina = controller.Resource.LocalPlayer.stamina;
            int hits = controller.Attack.Snapshot.roundHits, recoveryCount = recoveries.Count, views = controller.Views.Count;
            Require(controller.Hud.ResultOverlay.activeInHierarchy && !controller.CanInteract
                && !controller.Resource.CanGenerate && !controller.RequestGenerate(), "Result did not show its overlay and block generation/input.");
            Require(!controller.HasCombinationPending && !controller.Combination.HasPending && !controller.Resource.HasPending
                && !controller.Gestures.HasActivePointer && !controller.Gestures.HasPending && controller.Attack.ActiveProjectileCount == 0,
                "Terminal state retained pending input or live projectiles.");
            Require(controller.Hud.RetryButton.interactable && controller.Hud.LobbyButton.interactable,
                "Host result buttons were not available.");
            yield return Capture(label);
            yield return new WaitForSecondsRealtime(1.25f);
            var after = controller.Battle.Snapshot;
            Require(after.phase == before.phase && after.roundId == before.roundId && after.remaining == before.remaining
                && after.teamHp == before.teamHp && after.observedMonsterHp == before.observedMonsterHp
                && controller.Resource.LocalPlayer.stamina == stamina && controller.Attack.Snapshot.roundHits == hits
                && recoveries.Count == recoveryCount && controller.Views.Count == views,
                "Result clock, HP, stamina, inventory, or hit rewards continued changing.");
            Require(controller.Hud.ResultOverlay.activeInHierarchy, "Frozen result overlay disappeared.");
            yield return Capture(label + "-frozen");
        }

        private IEnumerator RetryAndFirstGenerate()
        {
            uint previous = controller.Battle.Snapshot.roundId;
            controller.RetryBattle();
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Ready && controller.Battle.Snapshot.roundId > previous, 5,
                "Host Retry did not create a new Ready round.");
            AssertEmptyReady();
            Require(controller.Attack.Snapshot.hp == 100 && controller.Attack.Snapshot.roundHits == 0
                && controller.Attack.ActiveProjectileCount == 0 && !controller.Hud.ResultOverlay.activeSelf,
                "Retry did not reset HP, hits, projectiles, or Result UI.");
            yield return Capture("retry-ready-empty100");
            double remaining = controller.Battle.Snapshot.remaining;
            yield return new WaitForSecondsRealtime(1.1f);
            Require(controller.Battle.Phase == BattlePhase.Ready && controller.Battle.Snapshot.remaining == remaining,
                "Retry automatically started the next clock.");
            uint retryRound = controller.Battle.Snapshot.roundId;
            Require(controller.RequestHostStart(), "Host could not explicitly restart after Retry.");
            yield return WaitFor(() => controller.Battle.Phase == BattlePhase.Playing && controller.CanInteract, 5, "Restart failed.");
            Require(controller.Battle.Snapshot.roundId > retryRound && controller.Views.Count == 0
                && controller.Resource.LocalPlayer.stamina == 100, "Restart did not create another empty/full round.");
            yield return Generate();
            var first = generations[generations.Count - 1];
            Require(Math.Abs(first.staminaBefore - 100) < .00001 && Math.Abs(first.staminaAfter - 80) < .00001
                && controller.Views.Count == 1 && controller.Resource.LocalPlayer.generatedTotal == 1,
                "First Generate after Retry was not exactly one Raw at cost20.");
            yield return Capture("restart-first-raw-cost20");
        }

        private void AssertEmptyReady()
        {
            Require(controller.Battle.Phase == BattlePhase.Ready && controller.Battle.CanStart && !controller.Battle.CanAct
                && controller.Views.Count == 0 && controller.Resource.LocalPlayer.stamina == 100
                && controller.Resource.LocalPlayer.generatedTotal == 0 && !controller.Resource.Snapshot.debugTestMode
                && !controller.Resource.HasPending && !controller.Combination.HasPending && !controller.HasCombinationPending,
                "Ready must contain empty normal inventory, stamina100, and no pending request.");
        }

        private OrbWire[] OwnRaw() => controller.Attack.Snapshot.orbs.Where(x => x.owner == report.localPlayer
            && x.kind == (int)OrbKind.Raw && x.state == (int)OrbAuthorityState.Idle && controller.Views.ContainsKey(x.id)).ToArray();

        private IEnumerator MakeNormalCombination()
        {
            double stopAt = Math.Min(Time.realtimeSinceStartupAsDouble + 65, report.authoritativeDeadline - 10);
            while (true)
            {
                Require(controller.Battle.CanAct && Time.realtimeSinceStartupAsDouble < stopAt,
                    "No usable normal Seed opposite pair before the bounded deadline.");
                var raw = OwnRaw();
                var pairs = (from a in raw from b in raw where a.id != b.id && a.polarity != b.polarity
                    orderby (controller.GetViewScreenPosition(a.id) - controller.GetViewScreenPosition(b.id)).sqrMagnitude,
                        a.id, b.id select new[] { a, b }).ToArray();
                foreach (var pair in pairs)
                {
                    if (!TryDropPoint(pair[0], pair[1], out var landing)) continue;
                    yield return Drop(pair[0], pair[1], landing); yield break;
                }
                Require(controller.Views.Count < controller.Layout.Config.OrbStorageLimit,
                    "Normal Seed inventory reached capacity without a usable opposite pair.");
                yield return WaitFor(() => controller.Resource.CanGenerate, 5, "Normal stamina did not recover one generation cost.");
                yield return Generate();
            }
        }

        private bool TryDropPoint(OrbWire source, OrbWire target, out Vector2 landing)
        {
            landing = controller.GetViewScreenPosition(target.id);
            // T09 allows full center-to-center combinations throughout its lower workspace,
            // including horizontal travel beyond the historical transfer threshold.
            if (!controller.Layout.BottomPixelRect.Contains(landing)) return false;
            Vector2 targetCenter = landing;
            var nearest = controller.Attack.Snapshot.orbs.Where(x => x.id != source.id && x.owner == report.localPlayer
                && x.state == (int)OrbAuthorityState.Idle && controller.Views.ContainsKey(x.id))
                .OrderBy(x => (controller.GetViewScreenPosition(x.id) - targetCenter).sqrMagnitude)
                .ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
            return nearest != null && nearest.id == target.id;
        }

        private IEnumerator Drop(OrbWire source, OrbWire target, Vector2 landing)
        {
            string previous = controller.Combination.LastResult?.requestId;
            int count = controller.Views.Count, pointer = --pointerSerial;
            Vector2 from = controller.GetViewScreenPosition(source.id);
            Require(controller.BeginPointer(pointer, from, false) && controller.Gestures.ActiveOrb?.OrbId == source.id,
                "Automated pointer could not select the intended Raw material.");
            Require(landing == controller.GetViewScreenPosition(target.id),
                "Normal drop must travel to the complete target center throughout the T09 workspace.");
            var geometry = new DropGeometry { sourceId = source.id, targetId = target.id, from = from,
                targetCenter = landing, release = landing, delta = landing - from, plannedMoveSamples = DropMoveSamples,
                horizontalSwipeFraction = controller.Layout.Config.HorizontalSwipeFraction,
                horizontalDominance = controller.Layout.Config.HorizontalDominance, horizontalTransferEnabled = false };
            drops.Add(geometry);
            for (int sample = 1; sample <= DropMoveSamples; sample++)
            {
                controller.MovePointer(pointer, Vector2.Lerp(from, landing, sample / (float)DropMoveSamples));
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
            controller.EndPointer(pointer, landing);
            yield return WaitFor(() => controller.Combination.LastResult != null && controller.Combination.LastResult.requestId != previous
                && !controller.Combination.HasPending, 8, "Drop did not obtain a known Host decision and full inventory.");
            var result = controller.Combination.LastResult;
            combinations.Add(Clone(result));
            Require(result.known && result.accepted && result.sourceOrbId == source.id && result.targetOrbId == target.id,
                "Normal opposite drop did not succeed against the intended target: " + result.reason);
            lastCombined = result.originalCombined.id;
            Require(lastCombined != source.id && lastCombined != target.id && result.originalCombined.kind == (int)OrbKind.Combined
                && result.originalCombined.polarity == (int)OrbPolarity.None && result.currentSource.state == (int)OrbAuthorityState.Consumed
                && result.currentTarget.state == (int)OrbAuthorityState.Consumed, "Host did not atomically consume two Raw and create one new Combined.");
            Require(Vector2.Distance(result.originalCombined.pos, (result.sourcePosition + result.targetPosition) * .5f) < .00001f,
                "Confirmed combination did not use the supplied midpoint.");
            yield return WaitFor(() => controller.Views.ContainsKey(lastCombined) && !controller.Views.ContainsKey(source.id)
                && !controller.Views.ContainsKey(target.id) && controller.Views.Count == count - 1, 5,
                "Confirmed combination inventory and two-dimensional views differ.");
        }

        private IEnumerator Generate()
        {
            Require(controller.RequestGenerate(), "Normal paid Generate send refused.");
            yield return WaitFor(() => !controller.Resource.HasPending, 8, "Generate was not acknowledged.");
            var reply = controller.Resource.LastResult;
            Require(reply != null && reply.known && reply.accepted && !reply.duplicate && reply.operation == (int)ResourceRequestKind.Generate
                && Math.Abs(reply.staminaBefore - reply.staminaAfter - 20) < .00001 && reply.confirmedOrb != null
                && reply.confirmedOrb.kind == (int)OrbKind.Raw && reply.confirmedOrb.owner == report.localPlayer,
                "Generate was not one confirmed normal Raw at atomic cost20.");
            generations.Add(Clone(reply));
            yield return WaitFor(() => controller.Views.ContainsKey(reply.confirmedOrb.id), 5, "Paid Raw did not reach confirmed inventory views.");
        }

        private IEnumerator FireConfirmed(string id, int expectedHp, int expectedHits)
        {
            // A paid Raw leaves room for the full +5; no resource amount or Seed is injected.
            if (controller.Resource.LocalPlayer.stamina > 85) yield return Generate();
            yield return WaitFor(() => controller.Views.ContainsKey(id), 5, "Actual Combined view missing before launch.");
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
            yield return WaitFor(() => controller.Attack.Snapshot.hp == expectedHp && controller.Attack.Snapshot.roundHits == expectedHits
                && recoveries.Count == expectedHits, 8, "Actual Combined Rigidbody did not apply one hit and one reward.");
            Require(!controller.Views.ContainsKey(id) && controller.Attack.Registry.TryGet(id, out var orb)
                && orb.AuthorityState == OrbAuthorityState.Consumed, "Physical hit did not consume the launched ID and remove its 2D view.");
            Require(recoveries[expectedHits - 1].accepted && !recoveries[expectedHits - 1].duplicate
                && Math.Abs(recoveries[expectedHits - 1].added - 5) < .00001, "Actual hit recovery was not exactly5.");
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
            checkpoints.Add(new Checkpoint { name = name, capturedAtUtc = DateTime.UtcNow.ToString("O"),
                realtime = Time.realtimeSinceStartupAsDouble, battle = Clone(controller.Battle.Snapshot),
                resource = Clone(controller.Resource.Snapshot), attack = Clone(controller.Attack.Snapshot), ownViews = controller.Views.Count,
                resultVisible = controller.Hud.ResultOverlay.activeInHierarchy, clockText = controller.Hud.ClockLabel.text,
                teamHpText = controller.Hud.TeamHpLabel.text, phaseText = controller.Hud.PhaseLabel.text,
                resultTitle = controller.Hud.ResultTitle.text, resultSummary = controller.Hud.ResultSummary.text });
            string path = Path.Combine(output, name + ".png");
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, texture.EncodeToPNG()); Destroy(texture);
            Require(new FileInfo(path).Length > 1000, "Screenshot was empty.");
        }
        private void RecordLaunchReply(AttackRequestReply value) => launchReplies.Add(Clone(value));
        private void RecordRecovery(ResourceRecoveryResult value) => recoveries.Add(new Recovery { player = value.PlayerId,
            added = value.Added, before = value.StaminaBefore, after = value.StaminaAfter, accepted = value.Accepted, duplicate = value.IsDuplicate });
        private void ObserveLog(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message; }
        private static IEnumerator WaitFor(Func<bool> ready, float seconds, string error)
        { double stopAt = Time.realtimeSinceStartupAsDouble + seconds; while (!ready() && Time.realtimeSinceStartupAsDouble < stopAt) yield return null; Require(ready(), error); }
        private static T Clone<T>(T value) where T : class => value == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private static void Require(bool okay, string error) { if (!okay) throw new InvalidOperationException(error); }
        private static string Arg(string[] args, string key, string fallback)
        { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        [Serializable] private sealed class Checkpoint
        { public string name, capturedAtUtc, clockText, teamHpText, phaseText, resultTitle, resultSummary; public double realtime;
            public BattleSnapshot battle; public ResourceSnapshot resource; public AttackSnapshot attack; public int ownViews; public bool resultVisible; }
        [Serializable] private sealed class Recovery { public ulong player; public double added, before, after; public bool accepted, duplicate; }
        [Serializable] private sealed class ClockSample { public double observedAt, remaining, teamHp; public string phase; public ulong revision; }
        [Serializable] private sealed class DropGeometry
        { public string sourceId, targetId; public Vector2 from, targetCenter, release, delta;
            public int plannedMoveSamples, completedMoveSamples; public float horizontalSwipeFraction, horizontalDominance;
            public bool noEarlyActionBeforeUp, horizontalTransferEnabled; }
        [Serializable] private sealed class BoundaryLaunch
        { public string orbId, acceptedRequestId; public Vector2 from, formerBandPoint, belowBoundary, acrossBoundary;
            public Rect lowerRect; public bool formerBandDidNotLaunch, belowBoundaryDidNotLaunch;
            public int acceptedUniqueRequests, roundHitsBefore, roundHitsAfter, hpBefore, hpAfter; }
        [Serializable] private sealed class Report
        { public string status, error, startedAtUtc, finishedAtUtc, role, mode, sourceInput, buildGuid, unity, dropGeometry, launchGeometry, horizontalThresholdUse;
            public bool physicalDevice; public int width, height, touchBegins, touchLaunches; public Rect safeArea; public ulong localPlayer;
            public uint playingRound; public double configDuration, authoritativeStartedAt, authoritativeDeadline, elapsedUntilResult;
            public Checkpoint[] checkpoints; public Recovery[] recoveries; public CombinationReply[] combinations;
            public ResourceRequestReply[] generations; public ClockSample[] clockSamples; public DropGeometry[] drops;
            public BoundaryLaunch[] launches; public AttackRequestReply[] launchReplies; }
#endif
    }
}
