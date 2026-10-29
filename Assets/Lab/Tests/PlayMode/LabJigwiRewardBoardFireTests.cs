using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Production owner-board presentation, synthetic accepted snapshots; no transport/device claims.</summary>
    public sealed class LabJigwiRewardBoardFireTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root;
        private LabConfig config;
        private LabNetwork network;
        private LabOrbBoard board;
        private Camera camera;
        private float timeScale;

        [SetUp]
        public void Prepare()
        {
            timeScale = Time.timeScale; Time.timeScale = 1f;
            root = new GameObject("Jigwi B reward board fixture");
            config = Object.Instantiate(Resources.Load<LabConfig>("LabConfig"));
            network = root.AddComponent<LabNetwork>(); network.enabled = false; network.config = config;
            var eye = new GameObject("Owner board camera"); eye.transform.SetParent(root.transform);
            camera = eye.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 5000f, -10f);
            var plate = new GameObject("Owner board"); plate.transform.SetParent(root.transform);
            board = plate.AddComponent<LabOrbBoard>(); board.Configure(config, camera, new Rect(-4f, 4998f, 8f, 4f));
            network.board = board;
            Assert.That(Resources.Load<LabJigwiRewardFireProfile>(LabJigwiRewardFireProfile.ResourcesPath).RimTexture, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = timeScale;
            Object.Destroy(root); Object.Destroy(config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SharedRewardLightsExistingAndArrivingOrbsButNotItemsWithoutChangingAOrPhysics()
        {
            LabOrbView yin = Orb("yin", LabOrbKind.Yin, .2f);
            LabOrbView combined = Orb("combined", LabOrbKind.Combined, .5f);
            LabOrbView bomb = Orb("bomb", LabOrbKind.Bomb, .7f);
            LabOrbView drum = Orb("drum", LabOrbKind.Drum, .8f);
            yin.SetJigwiABBurn(4f, true);
            float radius = yin.HitSphere.radius;
            Vector3 position = yin.transform.position, velocity = yin.Body.linearVelocity;
            Sample(20d);
            Assert.That(board.JigwiRewardFireActive, Is.True);
            AssertShown(yin); AssertShown(combined);
            Assert.That(bomb.JigwiRewardFlame, Is.Null); Assert.That(drum.JigwiRewardFlame, Is.Null);
            Assert.That(yin.JigwiABFlame.SecondsLeft, Is.EqualTo(4f), "B artwork must never renew or clear A's burn deadline.");
            Assert.That(yin.HitSphere.radius, Is.EqualTo(radius));
            Assert.That(yin.transform.position, Is.EqualTo(position));
            Assert.That(yin.Body.linearVelocity, Is.EqualTo(velocity));
            Assert.That(yin.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1));
            Assert.That(yin.GetComponentsInChildren<Rigidbody>(true), Has.Length.EqualTo(1));
            var received = Orb("received-after-reward", LabOrbKind.Yang, .35f);
            AssertShown(received);
            Sample(25d); // [resolvedAt, rewardUntil): exact end removes board fire, not the A deadline.
            Assert.That(board.JigwiRewardFireActive, Is.False);
            Assert.That(yin.JigwiRewardFlame.Shown, Is.False);
            Assert.That(received.JigwiRewardFlame.Shown, Is.False);
            Assert.That(yin.JigwiABFlame.Burning, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PredictionApprovalAndRejectionKeepFireOnTheVisibleIdentityOnly()
        {
            board.SetJigwiRewardFire(true);
            LabOrbView yin = Orb("a", LabOrbKind.Yin, .3f), yang = Orb("b", LabOrbKind.Yang, .6f);
            Assert.That(board.TryCreateCombinePreview("predicted", "a", "b", Vector2.one * .5f, Vector2.zero), Is.True);
            Assert.That(yin.JigwiRewardFlame.Shown, Is.False);
            Assert.That(yang.JigwiRewardFlame.Shown, Is.False);
            LabOrbView preview = Find("predicted"); AssertShown(preview);
            Assert.That(board.RejectCombinePreview("predicted"), Is.True);
            AssertShown(yin); AssertShown(yang);
            Assert.That(board.TryCreateCombinePreview("predicted-again", "a", "b", Vector2.one * .5f, Vector2.zero), Is.True);
            preview = Find("predicted-again");
            var flame = preview.JigwiRewardFlame;
            Assert.That(board.CommitCombinePreview("predicted-again", new LabOrbState
                { id = "approved", kind = LabOrbKind.Combined, element = LabOrbElement.Wood,
                    jigwiRadiusScale = 1f, jigwiABRadiusScale = 1f }), Is.True);
            yield return null;
            Assert.That(Find("approved"), Is.SameAs(preview));
            Assert.That(preview.JigwiRewardFlame, Is.SameAs(flame)); AssertShown(preview);
            Assert.That(board.BeginPendingLaunch("approved"), Is.True);
            Assert.That(flame.Shown, Is.False);
            Assert.That(board.RejectPendingLaunch("approved"), Is.True);
            AssertShown(preview);
        }

        [UnityTest]
        public IEnumerator BoardRingFollowsVisualHoverAndUsesOneHollowMeshWithHeldAnimation()
        {
            board.SetJigwiRewardFire(true);
            var orb = Orb("hover", LabOrbKind.Combined, .5f);
            var effect = orb.JigwiRewardFlame;
            Assert.That(effect.transform.parent, Is.SameAs(orb.Visual));
            Present(effect); float firstRadius = OuterRadius(effect);
            orb.Visual.localScale = Vector3.one * 1.4f;
            orb.Visual.localPosition = new Vector3(.2f, .3f, -.1f);
            Present(effect);
            Assert.That(OuterRadius(effect) / firstRadius, Is.EqualTo(1.4f).Within(.0001f));
            Vector3 centre = RingCentre(effect);
            Assert.That(Vector3.Distance(new Vector2(centre.x, centre.y),
                new Vector2(orb.Visual.position.x, orb.Visual.position.y)), Is.LessThan(.001f));
            Vector3[] vertices = effect.Mesh.vertices;
            for (int i = 0; i < vertices.Length; i += 2)
            {
                Vector3 inner = effect.transform.TransformPoint(vertices[i]);
                Assert.That(Vector3.Distance(inner, centre) / OuterRadius(effect),
                    Is.EqualTo(.98f / effect.Profile.RimScale).Within(.001f), "No triangles may fill the elemental centre.");
            }
            yield return null;
            board.SetJigwiRewardFire(true, true);
            float age = effect.AnimationTime; Mesh mesh = effect.Mesh;
            yield return null; yield return null;
            Assert.That(effect.AnimationTime, Is.EqualTo(age));
            board.SetJigwiRewardFire(true, true);
            Assert.That(effect.Mesh, Is.SameAs(mesh)); Assert.That(effect.AnimationTime, Is.EqualTo(age));
            board.SetJigwiRewardFire(true, false); yield return null;
            Assert.That(effect.AnimationTime, Is.GreaterThan(age));
            board.SetJigwiRewardFire(false); Present(effect);
            Assert.That(mesh.vertexCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PhaseExitMissingSnapshotAndBoardResetNeverCarryRewardToTheNextBoard()
        {
            var orb = Orb("old", LabOrbKind.Yin, .5f);
            Sample(21d); AssertShown(orb);
            Sample(21d, LabPhase.Idle);
            Assert.That(orb.JigwiRewardFlame.Shown, Is.False);
            Sample(21d); AssertShown(orb);
            SetLatest(null); Sync();
            Assert.That(orb.JigwiRewardFlame.Shown, Is.False);
            Sample(21d); board.Clear();
            Assert.That(board.JigwiRewardFireActive, Is.False);
            Assert.That(orb.JigwiRewardFlame.Shown, Is.False, "Clear removes fire immediately, before deferred object destruction.");
            LabOrbView fresh = Orb("new-board", LabOrbKind.Yin, .5f);
            Assert.That(fresh.JigwiRewardFlame, Is.Null);
            yield return null;
            Assert.That(orb == null, Is.True);
        }

        private LabOrbView Orb(string id, LabOrbKind kind, float x) => board.UpsertOrb(id, kind,
            new Vector2(x, .5f), Vector2.zero, element: LabOrbElement.Wood);
        private LabOrbView Find(string id) => Array.Find(board.GetComponentsInChildren<LabOrbView>(true), value => value.OrbId == id);
        private static void AssertShown(LabOrbView orb)
        {
            Assert.That(orb, Is.Not.Null); Assert.That(orb.JigwiRewardFlame, Is.Not.Null);
            Assert.That(orb.JigwiRewardFlame.Shown, Is.True);
        }
        private void Sample(double now, LabPhase phase = LabPhase.Playing)
        {
            // A paused accepted snapshot fixes an exact Host-clock sample for this boundary test.
            SetLatest(new LabSnapshot { phase = phase, serverTime = now, paused = true,
                jigwiAB = new LabJigwiABSnapshot { enabled = true, rounds = new[] { new LabJigwiABRound
                { kind = LabJigwiABKind.GroupFire, resolution = LabJigwiABResolution.Completed,
                    resolvedAt = 20d, rewardUntil = 25d } } } });
            Sync();
        }
        private void SetLatest(LabSnapshot value) => typeof(LabNetwork).GetField("latest", Private).SetValue(network, value);
        private void Sync() => typeof(LabNetwork).GetMethod("SyncJigwiRewardBoardPresentation", Private).Invoke(network, null);
        private void Present(LabJigwiRewardOrbFlame value) => typeof(LabJigwiRewardOrbFlame)
            .GetMethod("BeforeCamera", Private).Invoke(value, new object[] { camera });
        private static Vector3 RingCentre(LabJigwiRewardOrbFlame value) =>
            (value.transform.TransformPoint(value.Mesh.vertices[0]) + value.transform.TransformPoint(value.Mesh.vertices[48])) * .5f;
        private static float OuterRadius(LabJigwiRewardOrbFlame value) => Vector3.Distance(
            value.transform.TransformPoint(value.Mesh.vertices[1]), RingCentre(value));
    }
}
