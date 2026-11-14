using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// Exercises the real Network receipt/board handlers and a physical board with synthetic packets.
    /// No socket, remote device, latency target or hotspot success is claimed by these fixtures.
    /// </summary>
    public sealed class LabLocalBoardNetworkIntegrationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Session = "local-board-network-fixture";
        private GameObject root;
        private LabConfig config;
        private LabOrbBoard board;
        private LabNetwork network;
        private LabSnapshot initial;
        private ulong owner;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Local board Network fixture");
            config = ScriptableObject.CreateInstance<LabConfig>();
            var cameraObject = new GameObject("Input camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(root.transform, false);
            board = boardObject.AddComponent<LabOrbBoard>();
            board.Configure(config, camera, new Rect(-4f, -2f, 8f, 4f));
            network = root.AddComponent<LabNetwork>();
            network.enabled = false;
            network.config = config;
            network.board = board;
            // With no transport this is a client-only fixture. Keep its actual local identity.
            owner = network.LocalPlayerId;
            initial = new LabSnapshot
            {
                sessionId = Session, round = 1u, phase = LabPhase.Playing, serverTime = 1d,
                remaining = 179d, hp = 800, paintCoverage = .25f, paintRevision = 1u,
                paintColumns = 64, paintRows = 32, paintCoveredCount = 512, paintCellCount = 2048,
                players = new[]
                {
                    new LabPlayerState { id = owner, seat = 0, personalHp = 300, stamina = 80f },
                    new LabPlayerState { id = 2ul, seat = 1, personalHp = 300, stamina = 100f }
                },
                orbs = new[] { Raw("yin", LabOrbKind.Yin, .4f), Raw("yang", LabOrbKind.Yang, .6f) }
            };
            Set("sessionId", Session);
            Set("latest", initial);
            Invoke("EnsureActionRound");
            Invoke("SyncBoard");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (network != null) network.Stop();
            if (root != null) UnityEngine.Object.Destroy(root);
            if (config != null) UnityEngine.Object.Destroy(config);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DelayedCombinationApprovalShowsNeutralAndPromotesTheSameBodyToTheApprovedSprite()
        {
            UseSavedOrbAppearance();
            object combine = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(combine, "provisionalId");
            LabOrbView pending = FindOrb(temporary);
            Rigidbody body = pending.Body;
            SphereCollider collider = pending.HitSphere;
            body.linearVelocity = new Vector3(.7f, .2f, 0f);
            for (int frame = 0; frame < 3; frame++)
            {
                Assert.That(board.Count, Is.EqualTo(1), "The combination responds before a receipt arrives.");
                Assert.That(board.IsProvisional(temporary), Is.True);
                Assert.That(pending.Element, Is.EqualTo(LabOrbElement.None), "The Client must not choose the Host's element.");
                Assert.That(pending.Visual.GetComponentsInChildren<LabOrbSpriteArt>(), Is.Empty);
                var surface = pending.Visual.Find("Replacement Visual/Sphere").GetComponent<MeshRenderer>();
                Assert.That(surface.gameObject.activeInHierarchy, Is.True);
                var properties = new MaterialPropertyBlock();
                surface.GetPropertyBlock(properties);
                Assert.That(properties.GetColor("_BaseColor"),
                    Is.EqualTo(LabOrbAppearance.DefaultElementColor(LabOrbElement.None)),
                    "The saved purple Combined material must not appear while waiting for approval.");
                yield return null;
            }
            Vector3 position = body.position, velocity = body.linearVelocity;
            LabActionReceipt receipt = CombinedReceipt(1, temporary, 1);
            Invoke("ReceiveActionReceipt", receipt);
            LabOrbView approved = FindOrb("combined");
            Assert.That(approved, Is.SameAs(pending));
            Assert.That(approved.Body, Is.SameAs(body));
            Assert.That(approved.HitSphere, Is.SameAs(collider));
            Assert.That(body.position, Is.EqualTo(position));
            Assert.That(body.linearVelocity, Is.EqualTo(velocity));
            Assert.That(approved.Element, Is.EqualTo(LabOrbElement.Fire));
            Assert.That(approved.Visual.GetComponentInChildren<LabOrbSpriteArt>()
                .GetComponent<SpriteRenderer>().sprite,
                Is.SameAs(config.OrbAppearance.SpriteFor(LabOrbKind.Combined, LabOrbElement.Fire)));
            Assert.That(approved.Visual.GetComponentsInChildren<MeshRenderer>(), Is.Empty);
            Invoke("ReceiveActionReceipt", receipt.Copy());
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(FindOrb("combined"), Is.SameAs(pending));
            Assert.That(board.IsProvisional(temporary), Is.False);
        }

        [Test]
        public void CombinationReceiptUpdatesTheBoardWhileANewerPaintSnapshotIsStillWaiting()
        {
            var waiting = new LabSnapshot { sessionId = Session, round = 1u, phase = LabPhase.Victory,
                paintRevision = 5u, paintCoverage = 1f };
            Set("pendingPaintSnapshot", waiting);
            Set("awaitingPaintSession", Session);
            Set("awaitingPaintRound", 1u);
            int combinedFeedback = 0;
            network.LocalCombined += () => combinedFeedback++;
            object combine = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(combine, "provisionalId");
            Assert.That(board.IsProvisional(temporary), Is.True);

            LabActionReceipt receipt = CombinedReceipt(1, temporary, 1);
            Invoke("ReceiveActionReceipt", receipt);
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(network.Snapshot.orbs.Select(orb => orb.id), Is.EqualTo(new[] { "combined" }));
            Assert.That(board.IsProvisional(temporary), Is.False);
            Assert.That(Get<LabSnapshot>("pendingPaintSnapshot"), Is.SameAs(waiting));
            Assert.That(network.Snapshot.paintRevision, Is.EqualTo(1u));
            Assert.That(network.Snapshot.paintCoverage, Is.EqualTo(.25f));
            Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(combinedFeedback, Is.EqualTo(1));
            Invoke("ReceiveActionReceipt", receipt.Copy());
            Assert.That(combinedFeedback, Is.EqualTo(1), "A repeated receipt must not replay feedback.");
            Assert.That(board.Count, Is.EqualTo(1));
        }

        [Test]
        public void RejectedCombinationRollsBackItsUnsentDependentLaunchAndRestoresMaterials()
        {
            UseSavedOrbAppearance();
            LabOrbView yin = FindOrb("yin"), yang = FindOrb("yang");
            object combine = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(combine, "provisionalId");
            Submit("launch", 2, temporary);
            LabLocalActionCoordinator coordinator = Get<LabLocalActionCoordinator>("localActions");
            Assert.That(coordinator.TryGetPending(2, out var before), Is.True);
            Assert.That(before.state, Is.EqualTo(LabPendingActionState.WaitingForDependencies));
            Assert.That(board.Count, Is.Zero);

            Invoke("ReceiveActionReceipt", new LabActionReceipt
            {
                sessionId = Session, round = 1u, sender = owner, requestId = 1, sequence = 1,
                accepted = false, reason = "NOT_OWNED_IDLE", serverTime = 2d,
                states = initial.orbs, players = initial.players, provisionalId = temporary
            });
            Assert.That(coordinator.TryGetPending(2, out var after), Is.True);
            Assert.That(after.state, Is.EqualTo(LabPendingActionState.Rejected));
            Assert.That(board.Count, Is.EqualTo(2));
            Assert.That(board.IsSnapshotSuppressed("yin") || board.IsSnapshotSuppressed("yang"), Is.False);
            Assert.That(board.IsProvisional(temporary), Is.False);
            Assert.That(network.Snapshot.orbs.Select(orb => orb.id), Is.EquivalentTo(new[] { "yin", "yang" }));
            Assert.That(FindOrb("yin"), Is.SameAs(yin));
            Assert.That(FindOrb("yang"), Is.SameAs(yang));
            foreach (LabOrbView restored in new[] { yin, yang })
            {
                Assert.That(restored.gameObject.activeInHierarchy, Is.True);
                Assert.That(restored.Visual.GetComponentInChildren<LabOrbSpriteArt>()
                    .GetComponent<SpriteRenderer>().sprite,
                    Is.SameAs(config.OrbAppearance.SpriteFor(restored.Kind)));
                Assert.That(restored.Visual.GetComponentsInChildren<MeshRenderer>(), Is.Empty);
                Assert.That(restored.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            }
        }

        [Test]
        public void AGivenUpRecoveryRollsBackItsViewsAndAContiguousLateReceiptStillLands()
        {
            UseSavedOrbAppearance();
            LabOrbView yin = FindOrb("yin"), yang = FindOrb("yang");
            string temporary = AbandonCombinationWithDependentLaunch();
            Assert.That(board.Count, Is.EqualTo(2), "Both raw orbs are back on the board.");
            Assert.That(board.IsProvisional(temporary), Is.False);
            Assert.That(board.IsSnapshotSuppressed("yin") || board.IsSnapshotSuppressed("yang"), Is.False);
            Assert.That(FindOrb("yin"), Is.SameAs(yin));
            Assert.That(FindOrb("yang"), Is.SameAs(yang));
            foreach (LabOrbView restored in new[] { yin, yang })
            {
                Assert.That(restored.gameObject.activeInHierarchy, Is.True);
                Assert.That(restored.Visual.GetComponentInChildren<LabOrbSpriteArt>()
                    .GetComponent<SpriteRenderer>().sprite, Is.SameAs(config.OrbAppearance.SpriteFor(restored.Kind)));
            }
            Assert.That(Get<bool>("boardCheckpointWanted"), Is.True, "The Host's board is asked for to settle it.");

            // The Host did commit it; its receipt is the next stream sequence, so the board can take it.
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("C6_LAB_ACTION_LATE_RECEIPT request=1 .*applied=True"));
            Invoke("ReceiveActionReceipt", CombinedReceipt(1, temporary, 1));
            Assert.That(network.Snapshot.orbs.Select(orb => orb.id), Is.EqualTo(new[] { "combined" }));
            Assert.That(Get<LabBoardReplica>("boardReplica").NeedsCheckpoint, Is.False);
            Assert.That(board.IsProvisional(temporary), Is.False);
            LabLocalActionCoordinator coordinator = Get<LabLocalActionCoordinator>("localActions");
            Assert.That(coordinator.TryGetPending(1, out var parent), Is.True);
            Assert.That(parent.state, Is.EqualTo(LabPendingActionState.Rejected), "The given-up action stays given up.");
            Assert.That(coordinator.TryGetPending(2, out var launch), Is.True);
            Assert.That(launch.state, Is.EqualTo(LabPendingActionState.Rejected));
        }

        [Test]
        public void ALateReceiptAfterAGapLeavesTheGivenUpActionToTheCheckpoint()
        {
            string temporary = AbandonCombinationWithDependentLaunch();
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("C6_LAB_ACTION_LATE_RECEIPT request=1 .*applied=False"));
            Invoke("ReceiveActionReceipt", CombinedReceipt(1, temporary, 3));
            Assert.That(Get<LabBoardReplica>("boardReplica").NeedsCheckpoint, Is.True);
            Assert.That(board.InteractionEnabled, Is.False);
            Assert.That(network.Snapshot.orbs.Select(orb => orb.id), Is.EquivalentTo(new[] { "yin", "yang" }));
            LabBoardFrame checkpoint = EmptyCheckpoint(4);
            checkpoint.states = new[] { Combined() };
            Invoke("ReceiveBoardFrame", checkpoint);
            Assert.That(Get<LabBoardReplica>("boardReplica").NeedsCheckpoint, Is.False);
            Assert.That(network.Snapshot.orbs.Select(orb => orb.id), Is.EqualTo(new[] { "combined" }));
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(board.InteractionEnabled, Is.True);
        }

        /// <summary>A combination (with a launch of its output) whose receipt never came, given up at 30 s.</summary>
        private string AbandonCombinationWithDependentLaunch()
        {
            object combine = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(combine, "provisionalId");
            Submit("launch", 2, temporary);
            Assert.That(board.IsProvisional(temporary), Is.True);
            LabLocalActionCoordinator coordinator = Get<LabLocalActionCoordinator>("localActions");
            // The fixture has no transport, so drive the real coordinator through its sends by hand.
            for (int attempt = 0; attempt < 6; attempt++) Assert.That(coordinator.MarkSent(1, 1d), Is.True);
            const double recoveryAt = 1000d;
            coordinator.GetDueRequestIds(recoveryAt, true);
            Assert.That(coordinator.GetRecoveryRequiredRequestIds(), Is.EqualTo(new long[] { 1 }));
            Invoke("ExpireActionRecovery", recoveryAt + 29.999);
            Assert.That(board.IsProvisional(temporary), Is.True, "Still recovering at 29.999 s.");
            Assert.That(network.AbandonedActions, Is.Zero);
            LogAssert.Expect(LogType.Warning, "C6_LAB_ACTION_ABANDONED request=1 dependents=1");
            Invoke("ExpireActionRecovery", recoveryAt + LabLocalActionCoordinator.RecoveryAbandonSeconds);
            Assert.That(network.AbandonedActions, Is.EqualTo(1));
            Assert.That(coordinator.GetRecoveryRequiredRequestIds(), Is.Empty);
            return temporary;
        }

        [Test]
        public void ABoardCheckpointBeforeTheReceiptCannotDuplicateAProvisionalBodyOrItsDependentLaunch()
        {
            object combine = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(combine, "provisionalId");
            Submit("launch", 2, temporary);
            LabOrbState combined = Combined();
            Invoke("ReceiveBoardFrame", new LabBoardFrame
            {
                sessionId = Session, round = 1u, sequence = 2, localOwner = owner, checkpoint = true,
                phase = LabPhase.Playing, serverTime = 2d, remaining = 178d,
                states = new[] { combined }, players = initial.players
            });
            Invoke("ReceiveActionReceipt", CombinedReceipt(1, temporary, 1));
            Assert.That(board.IsSnapshotSuppressed("combined"), Is.True,
                "The subsequent local launch remains hidden after the combination is promoted.");
            Assert.That(board.IsProvisional(temporary), Is.False);

            LabOrbState flying = Combined();
            flying.inFlight = true;
            Invoke("ReceiveActionReceipt", new LabActionReceipt
            {
                sessionId = Session, round = 1u, sender = owner, requestId = 2, sequence = 3,
                accepted = true, reason = "LAUNCHED", serverTime = 2.1d,
                states = new[] { flying }, removedIds = new[] { "combined" }, players = initial.players
            });
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.TryGetMotion("combined", out _, out _), Is.False);
            Assert.That(board.TryGetMotion("yin", out _, out _), Is.False);
            Assert.That(board.TryGetMotion("yang", out _, out _), Is.False);
            Assert.That(network.Snapshot.orbs.Any(orb => orb.owner == owner && !orb.inFlight), Is.False);
        }

        [Test]
        public void ANewRoundDiscardsOldProvisionalBodiesAndRejectsTheirLateReceipt()
        {
            object combine = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(combine, "provisionalId");
            Set("latest", new LabSnapshot { sessionId = Session, round = 2u, phase = LabPhase.Playing,
                remaining = 180d, hp = 800, players = initial.players });
            Invoke("EnsureActionRound");
            Invoke("SyncBoard");
            Invoke("ReceiveActionReceipt", CombinedReceipt(1, temporary, 1));
            Assert.That(network.Snapshot.round, Is.EqualTo(2u));
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.IsProvisional(temporary), Is.False);
            Assert.That(board.TryGetMotion("yin", out _, out _), Is.False);
            Assert.That(board.TryGetMotion("yang", out _, out _), Is.False);
        }

        [Test]
        public void AHistoricalCombinationReceiptCannotResurrectAnOutputMissingFromTheNewerLedger()
        {
            object combine = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(combine, "provisionalId");
            Invoke("ReceiveBoardFrame", EmptyCheckpoint(2));
            Invoke("ReceiveActionReceipt", CombinedReceipt(1, temporary, 1));
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.OrbIds, Is.Empty);
            Assert.That(network.Snapshot.orbs, Is.Empty);
        }

        [Test]
        public void AHistoricalLaunchRejectionCannotRestoreAnOrbAlreadyGoneInTheNewerLedger()
        {
            LabOrbState combined = Combined();
            initial.orbs = new[] { combined };
            Invoke("SyncBoard");
            Submit("launch", 1, "combined");
            Assert.That(board.IsSnapshotSuppressed("combined"), Is.True);
            Invoke("ReceiveBoardFrame", EmptyCheckpoint(2));
            Invoke("ReceiveActionReceipt", new LabActionReceipt
            {
                sessionId = Session, round = 1u, sender = owner, requestId = 1, sequence = 1,
                accepted = false, reason = "NOT_OWNED_IDLE", serverTime = 2d,
                states = new[] { combined }, players = initial.players
            });
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.OrbIds, Is.Empty);
            Assert.That(network.Snapshot.orbs, Is.Empty);
        }

        [Test]
        public void CapturingAFastBoardFrameRetainsTheCurrentNonzeroHangingTargetPose()
        {
            LabTarget target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 0f, 5f), .8f, 2f);
            LabHangingTarget hanging = target.gameObject.AddComponent<LabHangingTarget>();
            hanging.ConfigureSpin(80f, 1.2f, 720f);
            hanging.ConfigureRopeMotion(.2f, 3f, .18f, 45f, 3f, .5f);
            hanging.ReconcileYaw(35f, 90f, true);
            hanging.ReconcileRope(.7f, .15f, .4f, true);
            network.target = target;
            var actionSnapshot = new LabSnapshot();

            Invoke("CaptureTargetPose", actionSnapshot);

            Assert.That(actionSnapshot.targetYawDegrees, Is.EqualTo(35f).Within(.00001f));
            Assert.That(actionSnapshot.targetYawSpeedDegreesPerSecond, Is.EqualTo(90f).Within(.00001f));
            Assert.That(actionSnapshot.targetRopeCycleElapsed, Is.EqualTo(.7f).Within(.00001f));
            Assert.That(actionSnapshot.targetRopeHitOffset, Is.EqualTo(.15f).Within(.00001f));
            Assert.That(actionSnapshot.targetRopeHitVelocity, Is.EqualTo(.4f).Within(.00001f));
        }

        [Test]
        public void RecoveryResendsOnlyTheFrozenRequestAndIgnoresStaleOrCompletedRetryNotices()
        {
            object original = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(original, "provisionalId");
            object retry = NewPacket("action_retry", 1);
            retry.GetType().GetField("sessionId").SetValue(retry, Session);
            retry.GetType().GetField("round").SetValue(retry, 1u);
            LabLocalActionCoordinator coordinator = Get<LabLocalActionCoordinator>("localActions");
            Assert.That((bool)Invoke("ReceiveActionRetry", retry), Is.False,
                "A request that has not exhausted retries is not a recovery request.");

            object wire = JsonUtility.FromJson(JsonUtility.ToJson(original), original.GetType());
            wire.GetType().GetField("ownershipVersion").SetValue(wire, 7ul);
            wire.GetType().GetField("x").SetValue(wire, .8f);
            Get<IDictionary>("actionWireRequests").Add(1L, wire);
            string frozenPayload = JsonUtility.ToJson(wire);
            for (int attempt = 0; attempt < 6; attempt++)
                Assert.That(coordinator.MarkSent(1, attempt * 3d), Is.True);
            Assert.That(coordinator.GetDueRequestIds(100d, true), Is.Empty);
            Assert.That(coordinator.TryGetPending(1, out var waiting), Is.True);
            Assert.That(waiting.state, Is.EqualTo(LabPendingActionState.RecoveryRequired));

            initial.orbs[0].ownershipVersion = 9ul;
            initial.orbs[0].x = .1f;
            Assert.That((bool)Invoke("ReceiveActionRetry", (object)null), Is.False);
            retry.GetType().GetField("round").SetValue(retry, 2u);
            Assert.That((bool)Invoke("ReceiveActionRetry", retry), Is.False);
            retry.GetType().GetField("round").SetValue(retry, 1u);
            retry.GetType().GetField("sessionId").SetValue(retry, "old-session");
            Assert.That((bool)Invoke("ReceiveActionRetry", retry), Is.False);
            retry.GetType().GetField("sessionId").SetValue(retry, Session);
            Assert.That(network.ActionRequestTransmissions, Is.Zero);

            Assert.That((bool)Invoke("ReceiveActionRetry", retry), Is.True);
            Assert.That(network.ActionRequestTransmissions, Is.EqualTo(1));
            Assert.That(Get<IDictionary>("actionWireRequests")[1L], Is.SameAs(wire));
            Assert.That(JsonUtility.ToJson(wire), Is.EqualTo(frozenPayload),
                "Recovery must not recapture the newer position or ownership version.");
            Invoke("ReceiveActionReceipt", CombinedReceipt(1, temporary, 1));
            Assert.That((bool)Invoke("ReceiveActionRetry", retry), Is.False);
            Assert.That(network.ActionRequestTransmissions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ABoardSequenceGapBlocksNewCommandsUntilCheckpointWithoutStoppingPassivePhysics()
        {
            LabOrbView moving = board.GetComponentsInChildren<LabOrbView>(true).Single(view => view.OrbId == "yang");
            moving.Body.linearVelocity = new Vector3(1.5f, 0f, 0f);
            float previousX = moving.Body.position.x;
            var gap = EmptyCheckpoint(3);
            gap.checkpoint = false;
            Invoke("ReceiveBoardFrame", gap);
            Assert.That(Get<LabBoardReplica>("boardReplica").NeedsCheckpoint, Is.True);
            Assert.That(board.InteractionEnabled, Is.False);
            LabLocalActionCoordinator coordinator = Get<LabLocalActionCoordinator>("localActions");
            Submit("combine", 1, "yin", "yang");
            Assert.That(coordinator.Count, Is.Zero, "No new critical command may register against a missing ledger baseline.");
            Assert.That(board.Count, Is.EqualTo(2));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(moving.Body.position.x, Is.GreaterThan(previousX), "Only input is gated, not free local physics.");

            LabBoardFrame checkpoint = EmptyCheckpoint(4);
            checkpoint.states = initial.orbs;
            Invoke("ReceiveBoardFrame", checkpoint);
            Assert.That(Get<LabBoardReplica>("boardReplica").NeedsCheckpoint, Is.False);
            Assert.That(board.InteractionEnabled, Is.True);
            Submit("combine", 2, "yin", "yang");
            Assert.That(coordinator.Count, Is.EqualTo(1));
            Assert.That(board.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GenerationPreviewWaitsForTheHostsCorrectedRawReceipt()
        {
            float[] values = config.CaptureDeveloperValues();
            values[5] = 1f; values[51] = 0f;
            values[225] = 1f; values[226] = 0f; values[227] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            double began = Time.realtimeSinceStartupAsDouble;
            var model = new LabModel();
            model.BeginSolo(Session, owner, began, config);
            initial = model.Snapshot(began);
            Set("model", model);
            Set("latest", initial);
            Invoke("SyncBoard");
            LabOrbState drum = initial.drums.First();
            LabOrbKind previous = LabOrbKind.Combined;
            for (int i = 0; i < 8; i++)
            {
                long requestId = 700 + i;
                object packet = Submit("generate", requestId, null, drum.id);
                packet.GetType().GetField("ownershipVersion").SetValue(packet, drum.ownershipVersion);
                Assert.That(model.CopyPlayers().Single().generatedTotal, Is.EqualTo(i),
                    "A Client preview must not decide or record the next raw polarity.");
                if (i == 3)
                {
                    packet.GetType().GetField("ownershipVersion").SetValue(packet, drum.ownershipVersion + 1ul);
                    var rejected = (LabActionReceipt)Invoke("ExecuteBoardAction", owner, packet);
                    Assert.That(rejected.accepted, Is.False, "The Host checks drum authority before generation.");
                    Assert.That(model.CopyPlayers().Single().generatedTotal, Is.EqualTo(i));
                    rejected.sessionId = Session; rejected.round = 1u;
                    rejected.sender = owner; rejected.requestId = requestId;
                    Invoke("ReceiveActionReceipt", rejected);
                    requestId += 1000;
                    packet = Submit("generate", requestId, null, drum.id);
                    packet.GetType().GetField("ownershipVersion").SetValue(packet, drum.ownershipVersion);
                }
                var receipt = (LabActionReceipt)Invoke("ExecuteBoardAction", owner, packet);
                Assert.That(receipt.accepted, Is.True, receipt.reason);
                LabOrbState generated = receipt.states.Single(state => state.id == receipt.outputId);
                Assert.That(generated.kind.IsRaw(), Is.True);
                Assert.That(generated.kind, Is.Not.EqualTo(previous), "Maximum one is enforced by the Host rule path.");
                previous = generated.kind;
                receipt.sessionId = Session; receipt.round = 1u;
                receipt.sender = owner; receipt.requestId = requestId;
                Invoke("ReceiveActionReceipt", receipt);
                Assert.That(FindOrb(generated.id).Kind, Is.EqualTo(generated.kind),
                    "The real receipt promotes the preview to the Host's exact raw result.");
                Assert.That(network.Snapshot.orbs.Single(state => state.id == generated.id).kind, Is.EqualTo(generated.kind));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnActionTriggeredExpiryVanishesBeforeItsReceiptRemovesTheSourceBody()
        {
            float[] values = config.CaptureDeveloperValues();
            values[5] = 1f;
            values[10] = .1f;
            values[39] = values[40] = 50f;
            for (int i = 41; i <= 44; i++) values[i] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            double began = Time.realtimeSinceStartupAsDouble;
            var model = new LabModel();
            model.BeginSolo(Session, owner, began, config);
            Assert.That(model.TryGenerate(owner, "expiry-source", began, out var expiring, out error), Is.True, error);
            initial = model.Snapshot(began);
            Set("model", model);
            Set("latest", initial);
            Invoke("SyncBoard");
            Assert.That(board.TryGetMotion(expiring.id, out _, out _), Is.True);
            var vanished = new List<string>();
            var notices = new List<string>();
            board.OrbVanished += id => vanished.Add(id);
            network.OrbExpired += id => notices.Add(id);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(board.TryGetMotion(expiring.id, out _, out _), Is.True,
                "A local life countdown must not authoritatively remove an orb before the Host advances.");
            LabOrbState drum = initial.drums.Single();
            object request = Submit("generate", 500, null, drum.id);
            request.GetType().GetField("ownershipVersion").SetValue(request, drum.ownershipVersion);
            request.GetType().GetField("configRevision").SetValue(request, Get<int>("configRevision"));
            request.GetType().GetField("configFingerprint").SetValue(request, Get<string>("configFingerprint"));

            // Execute the actual Host rule handler without sockets. TryGenerate advances the real
            // model clock, so the earlier orb expires while this command is being processed.
            var receipt = (LabActionReceipt)Invoke("ExecuteBoardAction", owner, request);

            Assert.That(receipt.accepted, Is.True, receipt.reason);
            Assert.That(receipt.removedIds, Does.Contain(expiring.id));
            Assert.That(vanished, Is.EqualTo(new[] { expiring.id }),
                "The expiry must be announced before its action receipt is delivered, while Vanish can still find the body.");
            Assert.That(notices, Is.EqualTo(new[] { expiring.id }));
            Assert.That(board.GetComponentsInChildren<LabOrbVanishSmoke>(true).Length, Is.EqualTo(1));
            Assert.That(board.TryGetMotion(expiring.id, out _, out _), Is.False);
            receipt.sessionId = Session;
            receipt.round = 1u;
            receipt.sender = owner;
            receipt.requestId = 500;
            Invoke("ReceiveActionReceipt", receipt);
            Assert.That(vanished.Count, Is.EqualTo(1), "The later ledger removal must not create another expiry effect.");
            Assert.That(network.Snapshot.orbs.Any(orb => orb.id == expiring.id), Is.False);
            Assert.That(network.Snapshot.orbs.Any(orb => orb.id == receipt.outputId), Is.True);
        }

        [Test]
        public void AnOldRoundExpiryNoticeCannotVanishANewRoundOrbAndCurrentNoticePrecedesCheckpointRemoval()
        {
            initial.round = 2u;
            Set("latest", initial);
            Invoke("EnsureActionRound");
            Invoke("SyncBoard");
            var vanished = new List<string>();
            var notices = new List<string>();
            board.OrbVanished += id => vanished.Add(id);
            network.OrbExpired += id => notices.Add(id);
            ReceiveControl("orbExpired", "yin", Session, 1u);
            ReceiveControl("orbExpired", "yin", "previous-room", 2u);
            Assert.That(vanished, Is.Empty);
            Assert.That(notices, Is.Empty);
            Assert.That(board.TryGetMotion("yin", out _, out _), Is.True,
                "An old round/session cannot expire a current orb even if its ID is reused.");

            ReceiveControl("orbExpired", "yin", Session, 2u);

            Assert.That(vanished, Is.EqualTo(new[] { "yin" }));
            Assert.That(notices, Is.EqualTo(new[] { "yin" }));
            Assert.That(board.GetComponentsInChildren<LabOrbVanishSmoke>(true).Length, Is.EqualTo(1));
            Assert.That(board.TryGetMotion("yin", out _, out _), Is.False);
            LabBoardFrame checkpoint = EmptyCheckpoint(1);
            checkpoint.round = 2u;
            checkpoint.states = initial.orbs.Where(orb => orb.id == "yang").ToArray();
            Invoke("ReceiveBoardFrame", checkpoint);
            Assert.That(network.Snapshot.orbs.Select(orb => orb.id), Is.EqualTo(new[] { "yang" }));
            Assert.That(vanished, Is.EqualTo(new[] { "yin" }), "The owner-board removal is not a second expiry.");
            Assert.That(board.GetComponentsInChildren<LabOrbVanishSmoke>(true).Length, Is.EqualTo(1));
        }

        [TestCase("combine")]
        [TestCase("launch")]
        [TestCase("transfer")]
        public void AConsumedOrTransferredOrbDoesNotPlayAnExpirySmokeOrExpiryNotice(string action)
        {
            int vanished = 0, notices = 0;
            board.OrbVanished += _ => vanished++;
            network.OrbExpired += _ => notices++;
            if (action == "combine")
            {
                object request = Submit("combine", 1, "yin", "yang");
                Invoke("ReceiveActionReceipt", CombinedReceipt(1, PacketField<string>(request, "provisionalId"), 1));
                Assert.That(board.TryGetMotion("yin", out _, out _) || board.TryGetMotion("yang", out _, out _), Is.False);
                Assert.That(board.TryGetMotion("combined", out _, out _), Is.True);
            }
            else
            {
                initial.orbs = new[] { Combined() };
                Invoke("SyncBoard");
                Submit(action, 1, "combined");
                LabOrbState result = Combined();
                if (action == "launch") result.inFlight = true;
                else { result.owner = 2ul; result.ownershipVersion++; }
                Invoke("ReceiveActionReceipt", new LabActionReceipt
                {
                    sessionId = Session, round = 1u, sender = owner, requestId = 1, sequence = 1,
                    accepted = true, reason = action == "launch" ? "LAUNCHED" : "TRANSFERRED", serverTime = 2d,
                    states = new[] { result }, removedIds = new[] { "combined" }, players = initial.players
                });
                Assert.That(board.TryGetMotion("combined", out _, out _), Is.False);
            }
            Assert.That(vanished, Is.Zero, "Only a Host lifetime-expiry notice may invoke Vanish.");
            Assert.That(notices, Is.Zero);
            Assert.That(board.GetComponentsInChildren<LabOrbVanishSmoke>(true), Is.Empty);
        }

        [Test]
        public void MyPresentedHitIsCorrectedByTheHostImpactWithoutPlayingItTwice()
        {
            LabHangingTarget hanging = AddHangingTarget();
            int hits = 0, misses = 0;
            network.LocalThrowHit += _ => hits++;
            network.LocalThrowMissed += () => misses++;
            Vector3 point = network.target.Hitbox.bounds.center + Vector3.forward * .5f + Vector3.right * .4f,
                velocity = Vector3.back * 9f;
            float yawBefore = hanging.YawSpeedDegreesPerSecond;
            PresentContact(7, "orb-7", LabProjectileHitKind.Monster, point, velocity);
            Assert.That(hits, Is.EqualTo(1), "The hit sound and vibration play at contact (#459).");
            float presentedYaw = hanging.YawSpeedDegreesPerSecond - yawBefore;
            Assert.That(Mathf.Abs(presentedYaw), Is.GreaterThan(.1f), "An off-axis hit spins the gourd at contact.");
            float bounce = hanging.VerticalVelocity;
            Assert.That(bounce, Is.GreaterThan(0f), "The gourd is kicked at contact.");

            ReceiveImpact("orb-7", point, velocity, presentedYaw + 30f);
            Assert.That(hits, Is.EqualTo(1), "The Host's confirmation does not replay the hit.");
            Assert.That(hanging.VerticalVelocity, Is.EqualTo(bounce).Within(1e-4f), "Same velocity: no second kick.");
            Assert.That(hanging.YawSpeedDegreesPerSecond - yawBefore, Is.EqualTo(presentedYaw + 30f).Within(.01f),
                "Only the spin difference is applied.");
            Assert.That(misses, Is.Zero);
        }

        [Test]
        public void MyPresentedHitIsTakenBackWhenTheHostDecidesAMiss()
        {
            LabHangingTarget hanging = AddHangingTarget();
            int hits = 0, misses = 0;
            network.LocalThrowHit += _ => hits++;
            network.LocalThrowMissed += () => misses++;
            PresentContact(8, "orb-8", LabProjectileHitKind.Monster,
                network.target.Hitbox.bounds.center + Vector3.forward * .5f + Vector3.right * .4f, Vector3.back * 9f);
            Assert.That(Mathf.Abs(hanging.YawSpeedDegreesPerSecond), Is.GreaterThan(.1f));
            ReceiveMiss("orb-8");
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(misses, Is.EqualTo(1), "The miss sound plays when the Host disagrees.");
            Assert.That(hanging.VerticalVelocity, Is.EqualTo(0f).Within(1e-4f), "The presented kick is removed.");
            Assert.That(hanging.YawSpeedDegreesPerSecond, Is.EqualTo(0f).Within(.01f));
        }

        [Test]
        public void APresentedMissIsReplayedInFullWhenTheHostDecidesAHitAndAMissIsNotPlayedTwice()
        {
            AddHangingTarget();
            int hits = 0, misses = 0;
            network.LocalThrowHit += _ => hits++;
            network.LocalThrowMissed += () => misses++;
            PresentContact(9, "orb-9", LabProjectileHitKind.Miss, Vector3.zero, Vector3.back * 9f);
            Assert.That(misses, Is.EqualTo(1));
            ReceiveImpact("orb-9", network.target.Hitbox.bounds.center, Vector3.back * 9f, 0f);
            Assert.That(hits, Is.EqualTo(1), "The Host's hit plays as before.");

            PresentContact(10, "orb-10", LabProjectileHitKind.Miss, Vector3.zero, Vector3.back * 9f);
            ReceiveMiss("orb-10");
            Assert.That(misses, Is.EqualTo(2), "The confirmed miss is not played a second time.");
        }

        [Test]
        public void AHitTheHostNeverAnswersIsTakenBackByTime()
        {
            LabHangingTarget hanging = AddHangingTarget();
            PresentContact(12, "orb-12", LabProjectileHitKind.Monster,
                network.target.Hitbox.bounds.center + Vector3.forward * .5f + Vector3.right * .4f, Vector3.back * 9f);
            Assert.That(hanging.VerticalVelocity, Is.GreaterThan(0f));
            int hits = 0;
            network.LocalThrowHit += _ => hits++;
            double now = network.BattlePresentationTime;
            Invoke("ExpirePresentedHits", now + .5d);
            Assert.That(hanging.VerticalVelocity, Is.GreaterThan(0f), "Still waiting for the Host.");
            Invoke("ExpirePresentedHits", now + 10d);
            Assert.That(hanging.VerticalVelocity, Is.EqualTo(0f).Within(1e-4f), "A voided contact is never answered; time takes it back.");
            Assert.That(hanging.YawSpeedDegreesPerSecond, Is.EqualTo(0f).Within(.01f));

            // The verdict was only late: the gourd is kicked by the Host's values, without a second hit sound.
            ReceiveImpact("orb-12", network.target.Hitbox.bounds.center + Vector3.forward * .5f, Vector3.back * 9f, 0f);
            Assert.That(hits, Is.Zero, "The hit was presented once already.");
            Assert.That(hanging.VerticalVelocity, Is.GreaterThan(0f), "The Host's impact still moves the gourd.");
        }

        [Test]
        public void ANearlyDecidedJigwiCurtainWaitsForTheHost()
        {
            AddHangingTarget();
            int hits = 0;
            network.LocalThrowHit += _ => hits++;
            initial.jigwiAB = new LabJigwiABSnapshot { enabled = true, startedAt = 0d, cutoffAt = 100d, rounds = new[]
            {
                new LabJigwiABRound { id = "curtain", kind = LabJigwiABKind.GroupFire, warningAt = .5d, activeAt = 1.5d, endsAt = 20d }
            } };
            PresentContact(11, "orb-11", LabProjectileHitKind.Monster, network.target.Hitbox.bounds.center, Vector3.back * 9f);
            Assert.That(hits, Is.Zero, "The Host may block it at the curtain; nothing is presented early.");
            // An A round still running while the next B warns (#388 54/84 s) must not hide the B.
            initial.jigwiAB.rounds = new[]
            {
                new LabJigwiABRound { id = "a", kind = LabJigwiABKind.PersonalBurn, warningAt = 0d, activeAt = .2d, endsAt = 1.6d },
                new LabJigwiABRound { id = "b-soon", kind = LabJigwiABKind.GroupFire, warningAt = 1.5d, activeAt = 2.5d, endsAt = 12d }
            };
            PresentContact(12, "orb-12", LabProjectileHitKind.Monster, network.target.Hitbox.bounds.center, Vector3.back * 9f);
            Assert.That(hits, Is.Zero, "The B warning starts within the margin; the Host may meet the shot behind the curtain.");
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        [Category("GPU")]
        public void OrdinaryContactUsesElementVfxWhileTalismanKeepsItsExistingPresentation(bool talisman, bool received)
        {
            AddHangingTarget();
            network.impactFeedback = root.AddComponent<LabImpactFeedback>();
            var rootsBefore = root.scene.GetRootGameObjects();
            try
            {
                var pool = LabFiveElementVfx.ForScene(root.scene);
                pool.ManualClock = true;
                pool.ClearAll();
                Vector3 point = network.target.Hitbox.transform.TransformPoint(Vector3.right * .6f);
                Vector3 velocity = Vector3.left * 9f;
                int talismanEvents = 0;
                network.TalismanHit += _ => talismanEvents++;
                if (received)
                {
                    object packet = NewPacket("targetImpact", 0, "element-contact");
                    Type packetType = packet.GetType();
                    Vector3 local = network.target.Hitbox.transform.InverseTransformPoint(point);
                    packetType.GetField("owner").SetValue(packet, owner);
                    packetType.GetField("element").SetValue(packet, (int)LabOrbElement.Water);
                    packetType.GetField("impactValue").SetValue(packet, 20);
                    packetType.GetField("paintX").SetValue(packet, 1f);
                    packetType.GetField("x").SetValue(packet, local.x);
                    packetType.GetField("y").SetValue(packet, local.y);
                    packetType.GetField("z").SetValue(packet, local.z);
                    packetType.GetField("wx").SetValue(packet, velocity.x);
                    packetType.GetField("talisman").SetValue(packet, talisman);
                    ReceivePacket(packet, Session, 1u);
                    ReceivePacket(packet, Session, 1u);
                    Assert.That(talismanEvents, Is.EqualTo(talisman ? 1 : 0),
                        "The approved talisman splash stays on its existing once-only event path.");
                }
                else
                {
                    Get<LabLocalHitPresentation>("localHits").Register(60, "element-contact");
                    Invoke("OnPredictedShotContact", 60L,
                        new LabProjectileResult(null, 0ul, LabProjectileHitKind.Monster, point, .5f, 1f, 3f,
                            velocity, hitNormal: Vector3.right, visualDiameter: .33f),
                        LabOrbElement.Water, 20, 1f, false, false, talisman, false); // #713 optional soot, explicit for reflection.
                }
                Assert.That(pool.ActiveImpactCount, Is.EqualTo(talisman ? 0 : 1),
                    "Special talisman water effects must not also acquire an ordinary element impact.");
            }
            finally
            {
                // These synchronous handlers create detached burst roots and scene services.
                foreach (GameObject created in root.scene.GetRootGameObjects())
                    if (!rootsBefore.Contains(created)) UnityEngine.Object.DestroyImmediate(created);
            }
        }

        private LabHangingTarget AddHangingTarget()
        {
            LabTarget target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 500f, 0f), .6f, 2.7f);
            LabHangingTarget hanging = target.gameObject.AddComponent<LabHangingTarget>();
            network.target = target;
            // Snapshot handling activates the impact gate for the round; this fixture does it directly.
            Get<LabImpactReceiptGate>("impactReceipts").SetActive(Session, 1u);
            return hanging;
        }

        private void PresentContact(long requestId, string projectileId, LabProjectileHitKind kind, Vector3 point, Vector3 velocity)
        {
            Get<LabLocalHitPresentation>("localHits").Register(requestId, projectileId);
            Invoke("OnPredictedShotContact", requestId,
                // Reflection supplies #713's optional soot flag explicitly; these remain ordinary shots.
                new LabProjectileResult(null, 0ul, kind, point, .5f, 1f, 3f, velocity), LabOrbElement.None, 20, 1f, false, false, false, false);
        }

        private void ReceiveImpact(string projectileId, Vector3 worldPoint, Vector3 velocity, float spin)
        {
            object packet = NewPacket("targetImpact", 0, projectileId);
            Vector3 local = network.target.Hitbox.transform.InverseTransformPoint(worldPoint);
            Type type = packet.GetType();
            type.GetField("owner").SetValue(packet, owner);
            type.GetField("element").SetValue(packet, (int)LabOrbElement.None);
            type.GetField("impactValue").SetValue(packet, 20);
            type.GetField("x").SetValue(packet, local.x); type.GetField("y").SetValue(packet, local.y); type.GetField("z").SetValue(packet, local.z);
            type.GetField("wx").SetValue(packet, velocity.x); type.GetField("wy").SetValue(packet, velocity.y); type.GetField("wz").SetValue(packet, velocity.z);
            type.GetField("spinImpulse").SetValue(packet, spin);
            ReceivePacket(packet, Session, 1u);
        }

        private void ReceiveMiss(string projectileId)
        {
            object packet = NewPacket("targetMiss", 0, projectileId);
            packet.GetType().GetField("owner").SetValue(packet, owner);
            ReceivePacket(packet, Session, 1u);
        }

        private void ReceiveControl(string type, string orbId, string session, uint round)
            => ReceivePacket(NewPacket(type, 0, orbId), session, round);

        private void ReceivePacket(object packet, string session, uint round)
        {
            packet.GetType().GetField("sessionId").SetValue(packet, session);
            packet.GetType().GetField("round").SetValue(packet, round);
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
            var wire = new byte[payload.Length + 8];
            Buffer.BlockCopy(BitConverter.GetBytes(3), 0, wire, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, wire, 4, 4);
            Buffer.BlockCopy(payload, 0, wire, 8, payload.Length);
            // The fixture intentionally references only the game assembly. Reflection constructs
            // the real NGO reader and invokes its actual authenticated receive path without a socket.
            MethodInfo receive = typeof(LabNetwork).GetMethod("Receive", Private);
            Type readerType = receive.GetParameters()[1].ParameterType;
            ConstructorInfo constructor = readerType.GetConstructors().Single(candidate =>
                candidate.GetParameters().Length == 4 && candidate.GetParameters()[0].ParameterType == typeof(byte[]));
            Type allocatorType = constructor.GetParameters()[1].ParameterType;
            object reader = constructor.Invoke(new[] { (object)wire, Enum.Parse(allocatorType, "Temp"), -1, 0 });
            try { receive.Invoke(network, new[] { (object)0ul, reader }); }
            finally { ((IDisposable)reader).Dispose(); }
        }

        private LabBoardFrame EmptyCheckpoint(long sequence) => new LabBoardFrame
        {
            sessionId = Session, round = 1u, sequence = sequence, localOwner = owner,
            checkpoint = true, phase = LabPhase.Playing, serverTime = 3d, remaining = 177d,
            players = initial.players
        };

        private LabOrbState Raw(string id, LabOrbKind kind, float x) => new LabOrbState
        { id = id, owner = owner, kind = kind, x = x, y = .5f, ownershipVersion = 1ul };

        private LabOrbState Combined() => new LabOrbState
        { id = "combined", owner = owner, kind = LabOrbKind.Combined, element = LabOrbElement.Fire,
            x = .5f, y = .5f, ownershipVersion = 1ul };

        private LabActionReceipt CombinedReceipt(long request, string temporary, long sequence) => new LabActionReceipt
        {
            sessionId = Session, round = 1u, sender = owner, requestId = request, sequence = sequence,
            accepted = true, reason = "COMBINED", serverTime = 2d,
            provisionalId = temporary, outputId = "combined", states = new[] { Combined() },
            removedIds = new[] { "yin", "yang" }, players = initial.players
        };

        private void UseSavedOrbAppearance()
        {
            LabConfig saved = Resources.Load<LabConfig>("LabConfig");
            typeof(LabConfig).GetField("orbAppearance", Private).SetValue(config, saved.OrbAppearance);
            board.Configure(config, root.GetComponentInChildren<Camera>(), new Rect(-4f, -2f, 8f, 4f));
        }

        private LabOrbView FindOrb(string id) => board.GetComponentsInChildren<LabOrbView>(true)
            .Single(view => view.OrbId == id && view.gameObject.activeInHierarchy);

        private object Submit(string type, long request, string first, string second = null)
        {
            object packet = NewPacket(type, request, first, second);
            Invoke("SubmitBoardAction", packet);
            return packet;
        }

        private static object NewPacket(string type, long request, string first = null, string second = null)
        {
            Type packetType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
            object packet = Activator.CreateInstance(packetType, true);
            packetType.GetField("type").SetValue(packet, type);
            packetType.GetField("requestId").SetValue(packet, request);
            packetType.GetField("firstId").SetValue(packet, first);
            packetType.GetField("secondId").SetValue(packet, second);
            packetType.GetField("x").SetValue(packet, .5f);
            packetType.GetField("y").SetValue(packet, .5f);
            return packet;
        }

        private static T PacketField<T>(object packet, string name) => (T)packet.GetType().GetField(name).GetValue(packet);
        private void Set(string field, object value) => typeof(LabNetwork).GetField(field, Private).SetValue(network, value);
        private T Get<T>(string field) => (T)typeof(LabNetwork).GetField(field, Private).GetValue(network);
        private object Invoke(string method, params object[] arguments) => typeof(LabNetwork)
            .GetMethod(method, Private).Invoke(network, arguments);
    }
}
