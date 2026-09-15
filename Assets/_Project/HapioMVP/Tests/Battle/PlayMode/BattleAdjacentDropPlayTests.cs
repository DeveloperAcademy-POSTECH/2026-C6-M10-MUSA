using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Battle.Tests
{
    /// <summary>Real scene/controller/NGO with explicit Raw geometry fixtures; no normal Seed or physical Touch claim.</summary>
    public sealed class BattleAdjacentDropPlayTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/BattleLoop.unity";
        private T09BattleController controller;
        private bool previousRunInBackground;
        private int pointer;

        [UnitySetUp]
        public IEnumerator OpenSavedSceneAndStartExplicitSolo()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
#endif
            yield return null; yield return null;
            controller = Object.FindAnyObjectByType<T09BattleController>(); pointer = 9400;
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Layout.Config.HorizontalSwipeFraction, Is.EqualTo(.18f));
            Assert.That(controller.Layout.Config.HorizontalDominance, Is.EqualTo(1.25f));
            Assert.That(controller.Layout.Config.CombinationRadiusFraction, Is.EqualTo(.08f));
            controller.ConfigureDevelopmentSolo(true);
            Assert.That(controller.StartDevelopmentHost("25119"), Is.True);
            yield return WaitFor(() => controller.Battle.CanStart, "The test Host did not reach explicit solo Ready.");
            Assert.That(controller.RequestHostStart(), Is.True);
            yield return WaitFor(() => controller.CanInteract, "The test Host did not start Playing.");
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator CloseOnlyTheTestSceneConnection()
        {
            try
            {
                if (controller != null) controller.EndDevelopmentTest();
                yield return WaitFor(() => Object.FindObjectsByType<NetworkManager>()
                    .All(manager => !manager.IsListening && !manager.ShutdownInProgress), "The test connection did not stop.");
                var loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.isLoaded)
                {
                    SceneManager.SetActiveScene(SceneManager.CreateScene("T09 Adjacent Drop Cleanup"));
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                yield return null; yield return null;
                Assert.That(Object.FindObjectsByType<T09BattleController>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<NetworkManager>(), Is.Empty);
                Assert.That(Object.FindObjectsByType<OrbView>(), Is.Empty);
            }
            finally { Application.runInBackground = previousRunInBackground; }
        }

        [UnityTest]
        public IEnumerator AdjacentCenterDropsAcrossEveryColumnPairInBothDirectionsCombineOnlyOnRelease()
        {
            for (int column = 0; column < 4; column++)
            for (int direction = -1; direction <= 1; direction += 2)
            {
                yield return FreshRound();
                yield return AdjacentDrop(column, direction, false);
            }
            Debug.Log("C6_T09_ADJACENT_DROP setup=RAW_GEOMETRY_FIXTURE cases=8 sourceGrab=CENTER end=TARGET_CENTER moveSamples=6 transferDecisions=0 combinedOnUp=true configThresholdsUnchanged=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator NearOuterColliderEdgeGrabsReachAdjacentTargetCentersWithoutTransfer()
        {
            for (int column = 0; column < 4; column++)
            for (int direction = -1; direction <= 1; direction += 2)
            {
                yield return FreshRound();
                yield return AdjacentDrop(column, direction, true);
            }
            Debug.Log("C6_T09_ADJACENT_DROP setup=RAW_GEOMETRY_FIXTURE cases=8 sourceGrab=95_PERCENT_OUTER_COLLIDER_RADIUS end=TARGET_CENTER moveSamples=6 transferDecisions=0 combinedOnUp=true physicalDevice=false");
        }

        [UnityTest]
        public IEnumerator FullWorkspaceHorizontalRawDragCombinesAtTheFarTargetOnlyOnRelease()
        {
            for (int direction = -1; direction <= 1; direction += 2)
            {
                yield return FreshRound();
                var pair = SupplyPair(new Vector2(direction > 0 ? .1f : .9f, .625f),
                    new Vector2(direction > 0 ? .9f : .1f, .625f));
                yield return null; yield return null;
                var replies = new List<AttackRequestReply>();
                controller.Attack.RequestResolved += replies.Add;
                try
                {
                    Vector2 start = controller.GetViewScreenPosition(pair[0].OrbId);
                    Vector2 end = controller.GetViewScreenPosition(pair[1].OrbId);
                    Assert.That(Mathf.Abs(end.x - start.x),
                        Is.GreaterThan(Screen.width * controller.Layout.Config.HorizontalSwipeFraction),
                        "This regression must exercise a drag longer than the preserved global Transfer threshold.");
                    int id = ++pointer;
                    Assert.That(controller.BeginPointer(id, start, false), Is.True);
                    for (int step = 1; step <= 12; step++)
                    {
                        controller.MovePointer(id, Vector2.Lerp(start, end, step / 12f));
                        yield return null;
                        Assert.That(controller.Gestures.ActivePointerId, Is.EqualTo(id));
                        Assert.That(controller.Gestures.HasPending || controller.HasCombinationPending, Is.False);
                        Assert.That(controller.Combination.LastResult, Is.Null, "Overlap while held cannot combine.");
                    }
                    Assert.That(replies, Is.Empty, "T09 full-workspace drag must not request the unimplemented Transfer action.");
                    controller.EndPointer(id, end);
                    yield return WaitFor(() => controller.Combination.LastResult != null && !controller.HasCombinationPending,
                        "Host did not resolve the full-workspace drop.");
                    var reply = controller.Combination.LastResult;
                    Assert.That(reply.known && reply.accepted, Is.True, reply.reason);
                    Assert.That(reply.sourceOrbId, Is.EqualTo(pair[0].OrbId));
                    Assert.That(reply.targetOrbId, Is.EqualTo(pair[1].OrbId));
                    Assert.That(reply.currentSource.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
                    Assert.That(reply.currentTarget.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
                    Assert.That(reply.originalCombined.id, Is.Not.EqualTo(pair[0].OrbId).And.Not.EqualTo(pair[1].OrbId));
                    Assert.That(controller.Views.Count, Is.EqualTo(1));
                    Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
                    Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.Zero);
                    Assert.That(replies, Is.Empty);
                    Assert.That(controller.Gestures.HasPending || controller.Gestures.HasActivePointer, Is.False);
                }
                finally { controller.Attack.RequestResolved -= replies.Add; }
            }
            Debug.Log("C6_T09_ADJACENT_DROP setup=RAW_GEOMETRY_FIXTURE cases=2 movement=FULL_WORKSPACE moveSamples=12 transferRequests=0 combinedOnUp=true physicalDevice=false");
        }

        private IEnumerator AdjacentDrop(int column, int direction, bool edgeGrab)
        {
            var pair = SupplyAdjacentPair(column, direction);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); Physics2D.SyncTransforms();
            var source = controller.Views[pair[0].OrbId];
            Vector2 sourceCenter = controller.GetViewScreenPosition(pair[0].OrbId);
            Vector2 targetCenter = controller.GetViewScreenPosition(pair[1].OrbId);
            Vector2 colliderEdge = controller.Layout.OrbCamera.WorldToScreenPoint(
                source.transform.TransformPoint(Vector3.right * source.Collider.radius));
            float radius = Vector2.Distance(sourceCenter, colliderEdge);
            Assert.That(targetCenter.y, Is.EqualTo(sourceCenter.y).Within(.01f));
            Vector2 start = sourceCenter - Vector2.right * direction * (edgeGrab ? radius * .95f : 0f);
            int id = ++pointer;
            Assert.That(controller.BeginPointer(id, start, false), Is.True);
            Assert.That(controller.Gestures.ActiveOrb.OrbId, Is.EqualTo(pair[0].OrbId));
            for (int step = 1; step <= 6; step++)
            {
                controller.MovePointer(id, Vector2.Lerp(start, targetCenter, step / 6f));
                yield return null;
                Assert.That(controller.Gestures.ActivePointerId, Is.EqualTo(id), "An ordinary adjacent drag lost its pointer before release.");
                Assert.That(controller.Gestures.HasPending || controller.HasCombinationPending, Is.False);
                Assert.That(controller.Combination.LastResult, Is.Null, "Overlap while held cannot automatically combine.");
            }
            controller.EndPointer(id, targetCenter);
            yield return WaitFor(() => controller.Combination.LastResult != null && !controller.HasCombinationPending,
                "Host did not resolve the real controller drop.");
            var reply = controller.Combination.LastResult;
            Assert.That(reply.known && reply.accepted, Is.True, reply.reason);
            Assert.That(controller.OrbGridScreenRect.center.x,
                Is.EqualTo(controller.Hud.OrbWorkspaceScreenRect.center.x).Within(.01f));
            Assert.That(reply.sourceOrbId, Is.EqualTo(pair[0].OrbId));
            Assert.That(reply.targetOrbId, Is.EqualTo(pair[1].OrbId));
            Assert.That(reply.originalCombined.id, Is.Not.EqualTo(pair[0].OrbId).And.Not.EqualTo(pair[1].OrbId));
            Assert.That(reply.currentSource.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
            Assert.That(reply.currentTarget.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
            Assert.That(controller.Views.Count, Is.EqualTo(1));
            Assert.That(controller.Attack.Snapshot.hp, Is.EqualTo(100));
            Assert.That(controller.Resource.LocalPlayer.generatedTotal, Is.Zero,
                "Explicit geometry fixtures must not be counted as normal paid Seed generation.");
            Assert.That(controller.Gestures.HasPending || controller.Gestures.HasActivePointer, Is.False);
        }

        private OrbRecord[] SupplyAdjacentPair(int leftColumn, int direction)
        {
            float left = (leftColumn + .5f) / 5f;
            float right = (leftColumn + 1.5f) / 5f;
            return SupplyPair(new Vector2(direction > 0 ? left : right, .625f),
                new Vector2(direction > 0 ? right : left, .625f));
        }

        private OrbRecord[] SupplyPair(Vector2 sourcePosition, Vector2 targetPosition)
        {
            Assert.That(controller.Attack.Registry.Snapshot(), Is.Empty);
            ulong owner = controller.Attack.LocalPlayerId;
            var source = controller.Attack.Registry.RegisterDevelopmentOrb(owner, OrbKind.Raw, OrbPolarity.Yin, sourcePosition);
            var target = controller.Attack.Registry.RegisterDevelopmentOrb(owner, OrbKind.Raw, OrbPolarity.Yang, targetPosition);
            controller.Attack.PublishInventoryChange("t09-explicit-raw-geometry-fixture");
            Assert.That(source.NormalizedPosition, Is.EqualTo(sourcePosition));
            Assert.That(target.NormalizedPosition, Is.EqualTo(targetPosition));
            return new[] { source, target };
        }

        private IEnumerator FreshRound()
        {
            Assert.That(controller.Battle.RetryHost(), Is.True);
            yield return WaitFor(() => controller.Battle.CanStart, "Fixture reset did not return Ready.");
            Assert.That(controller.RequestHostStart(), Is.True);
            yield return WaitFor(() => controller.CanInteract, "Fixture reset did not start Playing.");
            yield return null; yield return null;
            Assert.That(controller.Combination.LastResult, Is.Null);
            Assert.That(controller.Views, Is.Empty);
        }

        private static IEnumerator WaitFor(Func<bool> condition, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
