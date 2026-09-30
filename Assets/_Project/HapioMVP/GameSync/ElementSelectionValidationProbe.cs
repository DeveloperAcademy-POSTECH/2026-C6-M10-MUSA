using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
using C6.Prototype.Lobby;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using C6.Prototype.Resources;
using UnityEngine;

namespace C6.Prototype.GameSync
{
    /// <summary>
    /// An explicitly launched Mac development diagnostic, never normal gameplay or physical Touch evidence.
    /// The runtime fixture changes only this room's duration / monster HP / attack delay, not saved Config.
    /// </summary>
    [DefaultExecutionOrder(350), DisallowMultipleComponent]
    public sealed class ElementSelectionValidationProbe : MonoBehaviour
    {
        private T10GameSession game;
        private T09BattleController controller;
        private string output, shared, port, runtimeError, runId;
        private int index, count, pointer = -56000;
        private bool active, ownsOutput, observing, ownAssertionsCompleted;
        private ulong[] roster;
        private Report report;
        private readonly List<Assertion> assertions = new List<Assertion>();
        private readonly List<Proof> proofs = new List<Proof>();
        private readonly List<RoundObservation> rounds = new List<RoundObservation>();
        private readonly HashSet<string> proofKeys = new HashSet<string>(StringComparer.Ordinal);
        private static double Now => Time.realtimeSinceStartupAsDouble;
        private bool Host => index == 0;
        private bool IsLastPeer => index == count - 1;
        private OrbElement OwnElement => LobbyProtocol.Elements[index];
        private static bool Enabled(string[] args) => !Application.isEditor && Debug.isDebugBuild
            && Application.platform == RuntimePlatform.OSXPlayer && Array.IndexOf(args, "-c6Element56") >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOnlyForExplicitDiagnostic()
        {
            if (!Enabled(Environment.GetCommandLineArgs())) return;
            var owner = FindAnyObjectByType<T10GameSession>();
            if (owner != null && owner.GetComponent<ElementSelectionValidationProbe>() == null)
                owner.gameObject.AddComponent<ElementSelectionValidationProbe>();
        }

        private void Update()
        {
            if (!active || game == null) return;
            GatherProofs();
            if (!Host && FindObjectsByType<HostProjectile3D>().Length > 0)
                runtimeError = "Client created an authoritative HostProjectile3D.";
        }

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!Enabled(args)) yield break;
            string error = null;
            double deadline = Now + 150;
            report = new Report { startedAtUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                buildGuid = Application.buildGUID, device = SystemInfo.deviceModel };
            try
            {
                index = int.Parse(Arg(args, "-c6Element56Index", "0"));
                count = int.Parse(Arg(args, "-c6Element56Count", "3"));
                port = Arg(args, "-c6Element56Port", "25356");
                output = Arg(args, "-c6Element56Output", null);
                Require((count == 3 || count == 5) && index >= 0 && index < count, "Use 3 or 5 participants with a valid index.");
                Require(!string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output)
                    && (!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any()), "Fresh absolute per-player output required.");
                Require(DirectConnectionValidation.TryParsePort(port, out _), "Invalid diagnostic port.");
                Directory.CreateDirectory(output); ownsOutput = true; shared = Directory.GetParent(output).FullName;
                report.index = index; report.participants = count; report.output = output;
                game = GetComponent<T10GameSession>(); controller = game?.Controller;
                Require(game != null && controller != null && game.ElementSelectionEnabled && game.MaximumParticipants == 5
                    && game.ContinuousTransfersEnabled && controller.ReleaseThrowsEnabled && controller.OrbPhysicsEnabled
                    && game.Lobby.ElementSelectionEnabled && game.Lobby.ProtocolVersion == LobbyProtocol.ElementSelectionVersion,
                    "The saved scene must enable #56, continuous transfers, release throws, and orb physics.");
                report.buildIdentifier = game.BuildIdentifier;
                Application.runInBackground = true; Application.targetFrameRate = 60;
                Application.logMessageReceived += Observe; observing = true;
                controller.Resource.RecoveryResolved += Recovery; active = true;
                if (Host)
                {
                    Require(!Exists("run"), "Diagnostic markers already exist; choose a fresh parent directory.");
                    runId = Guid.NewGuid().ToString("N");
                    Write("run", new Marker { runId = runId });
                }
            }
            catch (Exception exception) { error = exception.ToString(); }

            // Manually advance nested coroutines so any failed assertion, timeout, or callback
            // is recorded and the diagnostic-owned connection is cleaned up before exiting.
            var stack = new Stack<IEnumerator>();
            if (error == null) stack.Push(Run());
            while (stack.Count > 0 && error == null)
            {
                object next = null; bool more = false;
                try
                {
                    Require(Now < deadline, "Overall diagnostic deadline (150 seconds).");
                    CheckExternalFailure();
                    more = stack.Peek().MoveNext();
                    if (more) next = stack.Peek().Current; else stack.Pop();
                }
                catch (Exception exception) { error = exception.ToString(); }
                if (error != null) break;
                if (more && next is IEnumerator nested) stack.Push(nested);
                else if (more) yield return next;
            }

            active = false;
            if (observing) { Application.logMessageReceived -= Observe; observing = false; }
            if (controller != null) controller.Resource.RecoveryResolved -= Recovery;
            report.status = error == null ? "PASS" : "FAIL"; report.error = error; report.runId = runId;
            report.assertions = assertions.ToArray(); report.proofs = proofs.ToArray(); report.rounds = rounds.ToArray();
            report.finishedAtUtc = DateTime.UtcNow.ToString("O");
            if (game?.Snapshot != null) report.finalGame = JsonUtility.FromJson<GameSnapshot>(JsonUtility.ToJson(game.Snapshot));
            if (ownsOutput)
            {
                if (error != null && !Exists("failed-" + index)) Write("failed-" + index, new Marker { runId = runId, detail = error });
                File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
            }
            // Only this explicit app-owned diagnostic room is ended. Evidence files are retained.
            if (game != null) game.Leave();
            Debug.Log("C6_ELEMENT56_PROBE_COMPLETE index=" + index + " status=" + report.status + " error=" + error);
            yield return new WaitForSecondsRealtime(.5f);
            Application.Quit(error == null ? 0 : 1);
        }

        private IEnumerator Run()
        {
            for (int frame = 0; frame < 8; frame++) yield return null;
            if (!Host)
            {
                yield return Wait(() => Exists("run"), 15, "Host run identity missing.");
                runId = Read<Marker>("run").runId;
                Require(!string.IsNullOrEmpty(runId), "Invalid run identity.");
                yield return Wait(() => Exists("host-open") && (index == 1 || Exists("joined-" + (index - 1))), 35,
                    "Serialized admission predecessor missing.");
            }
            if (Host)
            {
                LobbyHostConfig fixture = game.Lobby.CaptureRoomDefaults();
                Require(fixture != null, "Host defaults missing.");
                fixture.duration = 15; fixture.monsterHp = fixture.monsterHp2 = fixture.monsterHp3 = fixture.monsterHp4 = fixture.monsterHp5 = 20;
                fixture.monsterAttackFirstDelay = 60;
                report.runtimeFixture = JsonUtility.ToJson(fixture);
                Check("fixture-config-valid", LobbyHostConfig.TryRead(report.runtimeFixture, out _), "Runtime-only duration15 / HP20 / threat delay60.");
                Require(game.Lobby.CreateRoom("C6 explicit E56 " + count, port, fixture), "CreateRoom refused.");
                yield return Wait(() => game.Lobby.InitialStateReady, 15, "Host configuration snapshot missing.");
                Write("host-open", new Marker { runId = runId });
            }
            else
            {
                Require(game.Lobby.JoinDirect(Arg(Environment.GetCommandLineArgs(), "-c6Element56Host", "127.0.0.1"), port), "JoinDirect refused.");
                yield return Wait(() => game.Lobby.InitialStateReady, 15, "Client configuration acknowledgement missing.");
            }
            Check("new-room-none", game.Lobby.LocalPlayer.selectedElement == OrbElement.None, "Initial selection is None.");
            Check("none-ready-blocked", !game.Lobby.CanReady && !game.Lobby.ToggleReady(), "Session Ready blocks before selection.");
            Write("joined-" + index, new Marker { runId = runId });
            yield return Wait(() => game.Lobby.Snapshot?.ParticipantCount == count, 35, "Expected participant roster missing.");
            roster = game.Lobby.Snapshot.OrderedPlayers.Select(player => player.clientId).ToArray();
            report.roster = roster; report.localPlayer = game.Lobby.Connection.LocalClientId ?? ulong.MaxValue;
            Check("identity-admission-order", report.localPlayer == roster[index], "Diagnostic admission order preserves P index.");
            yield return Barrier("all-joined");

            if (Host)
            {
                Require(game.Lobby.SelectElement(OrbElement.Fire), "Host Fire choice refused.");
                Require(game.Lobby.ToggleReady(), "Host Ready after its choice refused.");
                Write("duplicate-ready", new Marker { runId = runId });
            }
            yield return Wait(() => Exists("duplicate-ready") && game.Lobby.Snapshot.p1.selectedElement == OrbElement.Fire
                && game.Lobby.Snapshot.p1.ready, 8, "Host choice / Ready not shared.");
            if (index == 1)
            {
                // Deliberately bypass only the local occupied-button check. This still uses the
                // session's ordinary pending ID/sequence, authenticated wire, and Host receipt.
                MethodInfo submit = typeof(T10LobbySession).GetMethod("Submit", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(submit != null && (bool)submit.Invoke(game.Lobby, new object[] { LobbyProtocol.SelectElement, false, OrbElement.Fire }),
                    "Explicit duplicate choice request was not sent.");
                Check("pending-select-blocked", !game.Lobby.CanSelectElement && !game.Lobby.SelectElement(OrbElement.Water),
                    "A second ordinary choice cannot be sent while the diagnostic request waits.");
                yield return Wait(() => !game.Lobby.HasPending && game.Lobby.LastReply != null, 8, "Duplicate selection receipt missing.");
                Check("host-duplicate-rejected", !game.Lobby.LastReply.accepted && game.Lobby.LastReply.reason == "ELEMENT_ALREADY_SELECTED"
                    && game.Lobby.LocalPlayer.selectedElement == OrbElement.None && game.Lobby.Snapshot.p1.ready,
                    "Authenticated Host rejects duplicate, preserving existing Fire occupancy and Host Ready.");
                Write("duplicate-done", new Marker { runId = runId });
            }
            yield return Wait(() => Exists("duplicate-done"), 8, "Duplicate check missing.");
            yield return Barrier("duplicate");
            Require(game.Lobby.SelectElement(OwnElement), "Unique element selection refused.");
            yield return Wait(() => !game.Lobby.HasPending && game.Lobby.LocalPlayer.selectedElement == OwnElement, 8, "Own choice not confirmed.");
            yield return Wait(AllInitialChoices, 8, "Unique choices not shared.");
            Check("choice-change-clears-every-ready", game.Lobby.Snapshot.OrderedPlayers.All(player => !player.ready), "Every successful choice change revokes Ready.");
            if (Host) yield return Capture("selected-lobby");
            yield return Barrier("selected");
            game.DevelopmentHoldInitialAck = IsLastPeer;
            Require(game.Lobby.ToggleReady(), "Ready with selected element refused.");
            yield return Wait(() => game.Lobby.Snapshot.canStart, 8, "All selected participants Ready missing.");
            yield return Barrier("first-lobby-ready");
            if (Host) Require(game.Lobby.StartMatch(), "First Host start refused.");
            yield return CaptureSharedReady("first-ready", 1);
            game.DevelopmentHoldInitialAck = false;
            yield return Wait(() => game.InitialConfirmed && controller.CanInteract && game.Snapshot.battle.phase == "Playing", 10, "First game ACK / Playing missing.");
            Check("initial-empty-full", game.Snapshot.attack.orbs.Length == 0 && game.Snapshot.resources.players.All(player => player.stamina == 100)
                && game.Snapshot.attack.hp == 20, "Runtime fixture starts empty / full stamina / HP20 after ACK.");
            yield return Barrier("first-playing");
            yield return GenerateAndCheck(2, "first-generate");
            yield return Barrier("generated");

            if (Host)
            {
                var registry = controller.Attack.Registry;
                var yin = registry.RegisterDevelopmentOrb(roster[0], OrbKind.Raw, OrbPolarity.Yin, new Vector2(.48f,.45f), OrbElement.Fire);
                var yang = registry.RegisterDevelopmentOrb(roster[0], OrbKind.Raw, OrbPolarity.Yang, new Vector2(.52f,.45f), OrbElement.Fire);
                var request = new OrbActionRequest(game.Snapshot.sessionId, game.Snapshot.roundId, Guid.NewGuid().ToString("N"),
                    yin.OrbId, yang.OrbId, OrbActionKind.Combine, 1, yin.NormalizedPosition);
                var held = registry.Reserve(roster[0], request);
                OrbRecord combined = null;
                Require(held.Accepted && registry.TryCompleteReservedCombination(held.Reservation, yang.NormalizedPosition, out _, out _, out combined),
                    "Explicit matching-Combined fixture could not be committed.");
                OrbElements.CombinedElements(combined.OrbId, out var first, out var second);
                Require(first == OrbElement.Fire && second == OrbElement.Fire, "Fixture Combined element is not Fire.");
                controller.Attack.PublishInventoryChange("element56-explicit-fire-materials-normal-combine");
                ulong receiver = RightNeighbour(roster[0]);
                Write("combined", new OrbIdentity { runId = runId, id = combined.OrbId, owner = roster[0], receiver = receiver });
                report.fixtureMethod = "Explicit Fire Yin/Yang registry fixtures -> normal Reserve/CompleteCombination -> normal transfer request";
            }
            yield return Wait(() => Exists("combined"), 6, "Fixture identity missing.");
            OrbIdentity identity = Read<OrbIdentity>("combined");
            yield return Wait(() => Orb(identity.id) != null, 6, "Combined snapshot missing.");
            yield return Barrier("combined-visible");
            if (Host)
            {
                var transfer = new OrbActionRequest(game.Snapshot.sessionId, game.Snapshot.roundId, Guid.NewGuid().ToString("N"),
                    identity.id, null, OrbActionKind.TransferRight, Orb(identity.id).sequence + 1, new Vector2(1,.45f),
                    transferMotion: new OrbTransferMotion(new Vector2(.04f,0), controller.Attack.MotionServerTime));
                Require(controller.Attack.Submit(transfer), "Normal transfer request refused.");
                yield return Wait(() => controller.Attack.LastResult?.requestId == transfer.RequestId && controller.Attack.LastResult.known, 6, "Transfer receipt missing.");
                Check("seat-right-transfer-approved", controller.Attack.LastResult.accepted, controller.Attack.LastResult.reason);
            }
            yield return Wait(() => Orb(identity.id)?.owner == identity.receiver && Orb(identity.id).transferCount == 1, 6, "Selected seat neighbour did not receive Combined.");
            Check("delivered-id-element-preserved", OrbElements.TryDecodeCombinedId(identity.id, out var yinElement, out var yangElement)
                && yinElement == OrbElement.Fire && yangElement == OrbElement.Fire && Orb(identity.id).state == (int)OrbAuthorityState.Idle,
                "Transfer preserves ID / Fire / Idle, independent of recipient selection.");
            yield return Barrier("transferred");
            if (report.localPlayer == identity.receiver)
            {
                yield return Wait(() => controller.Views.ContainsKey(identity.id), 6, "Recipient view missing.");
                Vector2 original = controller.GetViewScreenPosition(identity.id);
                double timeBefore = game.DisplayRemaining;
                yield return Swipe(identity.id);
                Check("wrong-element-popup-and-restore", controller.Hud.ElementWarningVisible && !controller.Gestures.HasPending
                    && Vector2.Distance(original,controller.GetViewScreenPosition(identity.id)) < 2f,
                    "Synthetic controller swipe opens actual warning overlay and restores drag start.");
                yield return Capture("wrong-element-warning");
                controller.Hud.ElementWarningConfirm.onClick.Invoke();
                Check("warning-confirm-closes", !controller.Hud.ElementWarningVisible, "Actual warning confirm button closes the overlay.");
                var launch = new OrbActionRequest(game.Snapshot.sessionId, game.Snapshot.roundId, Guid.NewGuid().ToString("N"),
                    identity.id, null, OrbActionKind.Launch, Orb(identity.id).sequence + 1, new Vector2(.5f,1),
                    new OrbThrowInput(new Vector2(0,.15f),.1f));
                Require(controller.Attack.Submit(launch), "Direct authenticated attack request refused by the local transport.");
                yield return Wait(() => controller.Attack.LastResult?.requestId == launch.RequestId && controller.Attack.LastResult.known, 6, "Host mismatch reply missing.");
                Check("host-wrong-element-rejected", !controller.Attack.LastResult.accepted && controller.Attack.LastResult.reason == "SELECTED_ELEMENT_MISMATCH",
                    "The authenticated current owner fails the Host check even when local controller checking is bypassed.");
                yield return new WaitForSecondsRealtime(.5f);
                Check("warning-does-not-stop-clock", game.DisplayRemaining < timeBefore - .2, "Combat clock continues during warning and rejection.");
                Write("wrong-launch-done", new Marker { runId = runId });
            }
            yield return Wait(() => Exists("wrong-launch-done"), 8, "Recipient rejection check missing.");
            Check("reject-preserves-no-damage-no-projectile", Orb(identity.id).owner == identity.receiver && Orb(identity.id).state == (int)OrbAuthorityState.Idle
                && game.Snapshot.attack.hp == 20 && game.Snapshot.attack.roundHits == 0 && game.Snapshot.attack.projectiles.Length == 0
                && report.acceptedHitRecoveries == 0, "Same orb remains Idle / owned; no projectile, hit, damage, or accepted hit-recovery event.");
            if (Host) Check("reject-no-registry-reservation", !controller.Attack.Registry.IsPending(identity.id), "Rejected attack reserves no orb.");
            yield return Barrier("rejected");

            yield return Wait(() => game.Snapshot.battle.phase == "Defeat", 22, "Runtime 15-second result missing.");
            Check("fixture-timeout-result", game.Snapshot.attack.hp == 20 && game.DisplayRemaining == 0, "Short runtime fixture reaches shared Defeat with HP unchanged.");
            yield return Barrier("first-result");
            uint oldRound = game.Snapshot.roundId;
            game.DevelopmentHoldInitialAck = IsLastPeer;
            if (Host) game.Retry();
            yield return CaptureSharedReady("retry-ready", oldRound + 1);
            game.DevelopmentHoldInitialAck = false;
            yield return Wait(() => game.InitialConfirmed && game.Snapshot.battle.phase == "Ready", 10, "Retry state ACK missing.");
            Check("retry-selected-empty-full", game.Snapshot.OrderedPlayers.Select(player => player.selectedElement).SequenceEqual(LobbyProtocol.Elements.Take(count))
                && game.Snapshot.attack.orbs.Length == 0 && game.Snapshot.resources.players.All(player => player.stamina == 100),
                "Retry retains choices and prepares the empty/full next round before start.");
            if (Host) Check("retry-start-permitted-after-acks", game.CanStart, "Host can start only after all fresh state hashes were confirmed.");
            yield return Barrier("retry-confirmed");
            uint retryRound = game.Snapshot.roundId;
            if (Host)
            {
                yield return null;
                Check("ready-room-controls-connected", controller.Hud.ReadyStartButton != null && controller.Hud.ReadyStartButton.interactable
                    && controller.Hud.ReadyLobbyButton != null && controller.Hud.ReadyLobbyButton.interactable,
                    "Saved Ready controls become available after all ACKs; the actual Lobby button is used next.");
                controller.Hud.ReadyLobbyButton.onClick.Invoke();
            }
            yield return Wait(() => !game.Attached && game.Lobby.Snapshot?.phase == LobbyProtocol.Lobby, 8, "Actual same-room lobby return missing.");
            Check("same-room-return-selection-retained", game.Lobby.Connected && game.Lobby.LocalPlayer.selectedElement == OwnElement
                && game.Lobby.Snapshot.OrderedPlayers.All(player => !player.ready) && game.Lobby.Snapshot.start == null,
                "Connection / P identity / approved choice survive; Ready clears and the battle contract detaches.");
            Check("same-player-identity-return", game.Lobby.Connection.LocalClientId == report.localPlayer
                && game.Lobby.Snapshot.OrderedPlayers.Select(player => player.clientId).SequenceEqual(roster), "Same NGO player IDs and admission order.");
            yield return Barrier("returned");
            if (Host)
            {
                Require(game.Lobby.ClearElement(), "Host Clear selection refused.");
                Check("clear-none-ready-blocked", game.Lobby.LocalPlayer.selectedElement == OrbElement.None && !game.Lobby.CanReady,
                    "The approved Clear button operation releases Fire and blocks Ready.");
                Write("cleared", new Marker { runId = runId });
            }
            yield return Wait(() => Exists("cleared") && game.Lobby.Snapshot.p1.selectedElement == OrbElement.None, 8, "Cleared selection not shared.");
            if (index == 1)
            {
                Require(game.Lobby.SelectElement(OrbElement.Fire), "Peer Fire choice after Clear refused.");
                yield return Wait(() => !game.Lobby.HasPending && game.Lobby.LocalPlayer.selectedElement == OrbElement.Fire, 8, "Peer swap approval missing.");
                Write("peer-swapped", new Marker { runId = runId });
            }
            yield return Wait(() => Exists("peer-swapped") && game.Lobby.Snapshot.Find(roster[1]).selectedElement == OrbElement.Fire, 8, "Peer swap not shared.");
            if (Host) Require(game.Lobby.SelectElement(OrbElement.Water), "Host Water choice after swap refused.");
            yield return Wait(SwappedChoices, 8, "Final swap choices not shared.");
            Check("five-player-swap-with-clear", game.Lobby.Snapshot.OrderedPlayers.All(player => !player.ready)
                && game.Lobby.Snapshot.OrderedPlayers.Select(player => player.selectedElement).Distinct().Count() == count,
                "Clear enables a unique P1/P2 swap, including the fully occupied five-player case.");
            yield return Barrier("swapped");
            game.DevelopmentHoldInitialAck = IsLastPeer;
            Require(game.Lobby.ToggleReady(), "Ready after swap refused.");
            yield return Wait(() => game.Lobby.Snapshot.canStart, 8, "Second all Ready missing.");
            yield return Barrier("second-lobby-ready");
            if (Host) Require(game.Lobby.StartMatch(), "Second start refused.");
            yield return CaptureSharedReady("second-ready", retryRound + 1);
            game.DevelopmentHoldInitialAck = false;
            yield return Wait(() => game.InitialConfirmed && controller.CanInteract, 10, "New selection start ACK missing.");
            OrbElement afterSwap = index == 0 ? OrbElement.Water : index == 1 ? OrbElement.Fire : OwnElement;
            Check("new-round-above-retry", game.Snapshot.roundId > retryRound && controller.SelectedElement == afterSwap,
                "Same-room start uses a fresh round and applies the updated approved attack selection.");
            yield return GenerateAndCheck(1, "after-swap-generate");
            ownAssertionsCompleted = true;
            yield return Barrier("complete");
        }

        private IEnumerator GenerateAndCheck(int quantity, string key)
        {
            for (int ordinal = 0; ordinal < quantity; ordinal++)
            {
                yield return Wait(() => controller.Resource.CanGenerate, 5, "Generation gate not open.");
                Require(controller.RequestGenerate(), "Ordinary resource generation refused.");
                yield return Wait(() => !controller.Resource.HasPending && controller.Resource.LastResult?.known == true, 6, "Generation receipt missing.");
                ResourceRequestReply reply = controller.Resource.LastResult;
                var selected = new HashSet<OrbElement>(game.Snapshot.OrderedPlayers.Select(player => player.selectedElement));
                Check(key + "-" + ordinal, reply.accepted && reply.confirmedOrb != null && selected.Contains(reply.confirmedOrb.rawElement)
                    && Math.Abs(reply.staminaBefore - reply.staminaAfter - game.Snapshot.resources.generateCost) < .0001,
                    "Host-created Raw belongs to a currently selected element and spends the normal configured cost.");
            }
            // Host receipts can resolve synchronously within this coroutine, while the shared
            // game aggregate is committed later in LateUpdate. Wait for that actual aggregate;
            // do not treat the receipt alone as evidence that every peer received the inventory.
            Func<bool> inventoryConfirmed = () => game.Snapshot?.attack?.orbs != null
                && game.Snapshot.attack.orbs.Count(orb => orb.owner == report.localPlayer && orb.kind == (int)OrbKind.Raw
                    && orb.state == (int)OrbAuthorityState.Idle
                    && game.Snapshot.OrderedPlayers.Any(player => player.selectedElement == orb.rawElement)) == quantity;
            yield return Wait(inventoryConfirmed, 6, key + " confirmed aggregate Raw inventory did not reach the exact requested count.");
            Check(key + "-inventory", inventoryConfirmed(),
                "Exactly the requested number of Raw orbs from current room selections exists after aggregate commit.");
        }

        private IEnumerator CaptureSharedReady(string key, uint expectedRound)
        {
            yield return Wait(() => game.Snapshot?.roundId == expectedRound && game.Snapshot.battle.phase == "Ready", 8, key + " missing.");
            if (Host)
            {
                Check(key + "-start-blocked", !game.CanStart && !game.InitialConfirmed, "The last peer deliberately holds its initial state ACK.");
                Write(key + "-reference", new ReadyReference { runId = runId, round = game.Snapshot.roundId, revision = game.Snapshot.revision,
                    hash = GameWire.CanonicalHash(game.Snapshot), seats = (ulong[])game.Snapshot.roundSeatOrder.Clone(),
                    selected = game.Snapshot.OrderedPlayers.Select(player => player.selectedElement).ToArray() });
            }
            yield return Wait(() => Exists(key + "-reference"), 6, key + " reference missing.");
            ReadyReference reference = Read<ReadyReference>(key + "-reference");
            yield return Wait(() => game.Proofs.Any(proof => proof.round == reference.round && proof.revision == reference.revision && proof.hash == reference.hash),
                6, key + " exact Host hash was not observed locally.");
            Check(key + "-shared-seats-and-choices", game.Snapshot.roundSeatOrder.SequenceEqual(reference.seats)
                && game.Snapshot.OrderedPlayers.Select(player => player.selectedElement).SequenceEqual(reference.selected)
                && RoundSeatLayout.Valid(reference.seats, roster), "Every player applies the same seat permutation and selections before ACK.");
            int seat = Array.IndexOf(reference.seats, report.localPlayer);
            Check(key + "-seat-consumers", controller.ApprovedSeatNumber == seat + 1
                && controller.SelectedElement == reference.selected[index], "Controller uses the approved seat and identity-ordered selected element.");
            rounds.Add(new RoundObservation { stage = key, round = reference.round, hash = reference.hash, revision = reference.revision,
                seats = reference.seats, selected = reference.selected, localSeat = seat + 1,
                left = reference.seats[(seat + count - 1) % count], right = reference.seats[(seat + 1) % count],
                canonicalHashObserved = true, observedBeforeAck = true });
            if (Host && key == "first-ready") yield return Capture("initial-ready-before-ack");
            yield return Barrier(key + "-applied");
        }

        private IEnumerator Swipe(string id)
        {
            Physics2D.SyncTransforms();
            int held = --pointer;
            Require(controller.BeginPointer(held, controller.GetViewScreenPosition(id), false), "Synthetic throw grab refused.");
            var start = new Vector2(Screen.width * .5f, controller.Layout.BottomPixelRect.yMax - Screen.width * .025f);
            controller.MovePointer(held, start);
            yield return new WaitForSecondsRealtime(.16f);
            double began = Time.unscaledTimeAsDouble; Vector2 end = start;
            float speed = Mathf.Min(controller.Layout.Config.ThrowMaxInputSpeed, Mathf.Max(1.25f, controller.Layout.Config.ThrowMinUpSpeed + .25f));
            do
            {
                yield return null;
                end = start + Vector2.up * (Screen.width * (float)(Time.unscaledTimeAsDouble - began) * speed);
                controller.MovePointer(held, end);
            }
            while (Time.unscaledTimeAsDouble - began < .1);
            Require(controller.ThrowArmed && !controller.Gestures.HasPending, "Synthetic throw did not arm.");
            controller.EndPointer(held, end);
        }

        private bool AllInitialChoices() => game.Lobby.Snapshot.OrderedPlayers.Select(player => player.selectedElement).SequenceEqual(LobbyProtocol.Elements.Take(count));
        private bool SwappedChoices() => game.Lobby.Snapshot.p1.selectedElement == OrbElement.Water
            && game.Lobby.Snapshot.Find(roster[1]).selectedElement == OrbElement.Fire
            && game.Lobby.Snapshot.OrderedPlayers.Skip(2).Select(player => player.selectedElement).SequenceEqual(LobbyProtocol.Elements.Skip(2).Take(count - 2));
        private ulong RightNeighbour(ulong owner)
        { var seats = game.Snapshot.roundSeatOrder; return seats[(Array.IndexOf(seats, owner) + 1) % seats.Length]; }
        private OrbWire Orb(string id) => game.Snapshot?.attack?.orbs?.FirstOrDefault(orb => orb.id == id);
        private IEnumerator Capture(string name)
        {
            yield return null; yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG()); }
            finally { Destroy(image); }
        }
        private IEnumerator Barrier(string stage)
        {
            Write(stage + "-" + index, new Marker { runId = runId });
            yield return Wait(() => Enumerable.Range(0, count).All(player => Exists(stage + "-" + player)), 8, "Barrier " + stage + " missing.");
        }
        private static IEnumerator Wait(Func<bool> predicate, float seconds, string reason)
        { double until = Now + seconds; while (!predicate() && Now < until) yield return null; Require(predicate(), reason); }
        private void GatherProofs()
        {
            foreach (var observation in game.Proofs)
            {
                string key = observation.round + "/" + observation.revision;
                if (proofKeys.Add(key)) proofs.Add(new Proof { session = game.Snapshot?.sessionId, round = observation.round,
                    revision = observation.revision, hash = observation.hash, phase = observation.phase });
            }
        }
        private void Recovery(ResourceRecoveryResult value) { if (value?.Accepted == true) report.acceptedHitRecoveries++; }
        private void Observe(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Assert || type == LogType.Error) runtimeError = message; }
        private void CheckExternalFailure()
        {
            if (!string.IsNullOrEmpty(runtimeError)) throw new InvalidOperationException(runtimeError);
            if (game != null && !string.IsNullOrEmpty(game.Error))
            {
                // Once every process has written its final successful barrier, a faster peer
                // can finish its owned-room cleanup before this coroutine resumes. Suppress
                // only that expected disconnection, never an earlier or unrelated game error.
                bool completedCleanup = ownAssertionsCompleted && shared != null
                    && Enumerable.Range(0, count).All(player => Exists("complete-" + player))
                    && (game.Error == "PARTICIPANT_DISCONNECTED" || game.Error == "CONNECTION_ENDED");
                if (!completedCleanup) throw new InvalidOperationException("Game session error: " + game.Error);
                report.expectedDiagnosticShutdown = game.Error;
            }
            if (shared == null) return;
            for (int player = 0; player < count; player++)
                if (Exists("failed-" + player)) throw new InvalidOperationException("Peer " + player + " failed: " + Read<Marker>("failed-" + player).detail);
        }
        private void Check(string id, bool condition, string detail)
        { assertions.Add(new Assertion { id = id, passed = condition, method = "MAC_DEVELOPMENT_SYNTHETIC", detail = detail }); Require(condition, id + ": " + detail); }
        private bool Exists(string name) => shared != null && File.Exists(Path.Combine(shared, "ce56-" + name + ".json"));
        private T Read<T>(string name) => JsonUtility.FromJson<T>(File.ReadAllText(Path.Combine(shared, "ce56-" + name + ".json")));
        private void Write<T>(string name, T value)
        {
            string destination = Path.Combine(shared, "ce56-" + name + ".json"), temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            try { File.WriteAllText(temporary, JsonUtility.ToJson(value, true)); File.Move(temporary, destination); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static string Arg(string[] args, string key, string fallback)
        { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : fallback; }

        [Serializable] private sealed class Marker { public string runId, detail; }
        [Serializable] private sealed class OrbIdentity { public string runId, id; public ulong owner, receiver; }
        [Serializable] private sealed class ReadyReference
        { public string runId, hash; public uint round; public ulong revision; public ulong[] seats; public OrbElement[] selected; }
        [Serializable] private sealed class Assertion { public string id, method, detail; public bool passed; }
        [Serializable] private sealed class Proof { public string session, hash, phase; public uint round; public ulong revision; }
        [Serializable] private sealed class RoundObservation
        { public string stage, hash; public uint round; public ulong revision; public ulong[] seats; public OrbElement[] selected; public int localSeat; public ulong left, right; public bool canonicalHashObserved, observedBeforeAck; }
        [Serializable] private sealed class Report
        {
            public string status, error, runId, startedAtUtc, finishedAtUtc, buildGuid, unity, buildIdentifier, device, output, runtimeFixture, fixtureMethod, expectedDiagnosticShutdown;
            public string scope = "EXPLICIT_MAC_DIRECT_IP_3_OR_5_PROCESS_HOST_CLIENT_SYNTHETIC_POINTER_RUNTIME15SEC_HP20_FIXTURES";
            public string normalDuration180Validation = "NOT_RUN";
            public string iOSPhysicalDeviceValidation = "NOT_RUN";
            public bool physicalTouch, bonjourValidated;
            public int index, participants, acceptedHitRecoveries;
            public ulong localPlayer; public ulong[] roster;
            public Assertion[] assertions; public Proof[] proofs; public RoundObservation[] rounds; public GameSnapshot finalGame;
        }
    }
}
