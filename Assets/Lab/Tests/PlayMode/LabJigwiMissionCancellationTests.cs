using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Production network presentation and cancellation methods, isolated from real sockets/device transport.</summary>
    public sealed class LabJigwiMissionCancellationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private GameObject root;
        private Transform flights;
        private LabNetwork network;
        private LabConfig config;
        private LabTarget target;

        [SetUp]
        public void Prepare()
        {
            root = new GameObject("Jigwi mission cancellation fixture");
            config = Object.Instantiate(Resources.Load<LabConfig>("LabConfig"));
            network = root.AddComponent<LabNetwork>();
            network.enabled = false;
            network.config = config;
            var cameraObject = new GameObject("Owned board camera"); cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            var boardObject = new GameObject("Owned board"); boardObject.transform.SetParent(root.transform);
            network.board = boardObject.AddComponent<LabOrbBoard>();
            network.board.Configure(config, camera, new Rect(-4, -2, 8, 4));
            flights = new GameObject("Owned flights").transform;
            flights.SetParent(root.transform);
            Field("projectileRoot").SetValue(network, flights);
            Field("actionSession").SetValue(network, "mission");
            Field("actionRound").SetValue(network, 1u);
            Field("sessionId").SetValue(network, "mission");
            target = LabTarget.CreateCylinder(root.transform, new Vector3(0, 70, 20), .8f, 2f);
            network.target = target;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(root); Object.Destroy(config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailureCancelsHostPredictedAndDetachedHistoryWithoutAHitAndKeepsLaterShots()
        {
            int hits = 0;
            var old = Shot(10);
            Assert.That(old.Launch("old-flight", 0, Vector3.forward, config, target, _ => hits++), Is.True);
            var pending = Shot(0);
            pending.SetJigwiABReward(true);
            pending.LaunchVisual(Vector3.forward, config, target);
            var after = Shot(31);
            after.SetJigwiABReward(true);
            after.LaunchVisual(Vector3.forward, config, target);
            var oldRewardTail = DetachedRewardTail(9);
            var newRewardTail = DetachedRewardTail(31);
            var oldTail = new GameObject("Old detached trail"); oldTail.transform.SetParent(flights);
            oldTail.AddComponent<LabProjectileRevisionStamp>().Revision = 9;
            var newTail = new GameObject("Later detached trail"); newTail.transform.SetParent(flights);
            newTail.AddComponent<LabProjectileRevisionStamp>().Revision = 31;
            var predictions = (Dictionary<long, LabProjectile>)Field("predictedShots").GetValue(network);
            predictions[5] = pending;
            predictions[7] = after;
            var presented = (HashSet<long>)Field("presentedShots").GetValue(network);
            presented.Add(5); presented.Add(7);
            var coordinator = (LabLocalActionCoordinator)Field("localActions").GetValue(network);
            coordinator.SetActive("mission", 1, 0);
            Assert.That(coordinator.TryRegister(7, new[] { "new-flight" }, null, 0d, out string reason), Is.True, reason);
            object laterRequest = Packet("new-flight", 7, 31);
            laterRequest.GetType().GetField("type").SetValue(laterRequest, "launch");
            ((System.Collections.IDictionary)Field("actionRequests").GetValue(network)).Add(7L, laterRequest);

            Assert.That(Purge(30), Is.True);
            Assert.That(old.gameObject.activeSelf, Is.False);
            Assert.That(pending.gameObject.activeSelf, Is.False);
            Assert.That(oldTail.activeSelf, Is.False);
            Assert.That(after.gameObject.activeSelf, Is.True);
            Assert.That(newTail.activeSelf, Is.True);
            Assert.That(oldRewardTail.gameObject.activeSelf, Is.False);
            Assert.That(oldRewardTail.PathCount, Is.Zero, "The failed mission immediately clears its detached dedicated flame mesh.");
            Assert.That(newRewardTail.gameObject.activeSelf, Is.True);
            Assert.That(newRewardTail.GetComponent<LabProjectileRevisionStamp>().Revision, Is.EqualTo(31UL));
            Assert.That(after.JigwiRewardFireTrail.IsEmitting, Is.True,
                "The post-fence approved flight keeps its dedicated layer while its action receipt is pending.");
            Assert.That(predictions.Keys, Is.EquivalentTo(new[] { 7L }),
                "A newer approved shot awaiting its action receipt must survive preview rollback as well.");
            Assert.That(presented.Contains(7), Is.True);
            Assert.That(((HashSet<long>)Field("presentedShots").GetValue(network)).Contains(5), Is.False);
            Assert.That(hits, Is.Zero, "Cancellation must not call the hit/miss resolver.");
            Assert.That(Purge(30), Is.False, "The same failed mission is applied once.");
            yield return null;
            Assert.That(old == null && pending == null && oldTail == null && oldRewardTail == null, Is.True);
            Assert.That(hits, Is.Zero);
        }

        [Test]
        public void DelayedProjectileNoticesCannotResurrectClearedFlightsButNewApprovalsStillWork()
        {
            Assert.That(Purge(30), Is.True);
            Assert.That(Presented(Packet("old", 5, 10)), Is.True);
            Assert.That(Presented(Packet("pending", 5, 0)), Is.True);
            Assert.That(Presented(Packet("new", 5, 31)), Is.False);
            Assert.That(Presented(Packet("new", 5, 31)), Is.True, "A valid later shot still deduplicates.");
            object stale = Packet("wrong-round", 6, 40);
            stale.GetType().GetField("round").SetValue(stale, 2u);
            Assert.That(Presented(stale), Is.True);
        }

        [Test]
        public void HostProjectileCreationUsesTheCapturedRewardAndAcceptedRevision()
        {
            var model = new LabModel();
            model.Begin("mission", new ulong[] { 0, 10 }, 0d, config);
            Field("model").SetValue(network, model);
            Field("latest").SetValue(network, model.Snapshot(0d));
            var orb = new LabOrbState { id = "rewarded-flight", owner = 0, kind = LabOrbKind.Combined,
                element = LabOrbElement.Wood, inFlight = true, revision = 42,
                jigwiABReward = true, jigwiABRewardSizeScale = 1.3f, jigwiABRewardPaintScale = 1.5f };
            Method("SpawnHostProjectile").Invoke(network, new object[] { orb, new Vector2(0, 2.8f), .5f, 1L });
            LabProjectile shot = flights.GetComponentInChildren<LabProjectile>();
            Assert.That(shot, Is.Not.Null);
            Assert.That(shot.ProjectileId, Is.EqualTo(orb.id));
            Assert.That(shot.AuthorityRevision, Is.EqualTo(42UL));
            Assert.That(shot.RadiusScale, Is.EqualTo(1.3f));
            Assert.That(shot.JigwiABRewardActive, Is.True);
            Assert.That(shot.ElementFlightTrail.Element, Is.EqualTo(LabOrbElement.Wood));
            Assert.That(shot.JigwiRewardFireTrail, Is.Not.Null);
            Assert.That(shot.JigwiRewardFireTrail.IsEmitting, Is.True);
            Assert.That(typeof(LabProjectile).GetField("element", Private).GetValue(shot), Is.EqualTo(LabOrbElement.Wood));
            Assert.That(shot.InitialVelocity, Is.Not.EqualTo(Vector3.zero));
        }

        private LabJigwiRewardFireTrail DetachedRewardTail(ulong revision)
        {
            LabProjectile shot = Shot(revision);
            shot.SetJigwiABReward(true);
            Assert.That(shot.LaunchVisual(Vector3.forward, config, target), Is.True);
            LabJigwiRewardFireTrail tail = shot.JigwiRewardFireTrail;
            Assert.That(tail, Is.Not.Null);
            shot.SendMessage("FinishVisual"); // Existing replica completion, without synthesizing a physical hit.
            Assert.That(tail.transform.parent, Is.EqualTo(flights));
            Assert.That(tail.GetComponent<LabProjectileRevisionStamp>().Revision, Is.EqualTo(revision));
            return tail;
        }

        private LabProjectile Shot(ulong revision)
        {
            var shot = LabProjectile.Create(new Vector3(0, 70, 0), config, flights, LabOrbElement.Water);
            shot.AuthorityRevision = revision;
            return shot;
        }

        private bool Purge(ulong revision) => (bool)Method("ApplyJigwiMissionFailure").Invoke(network,
            new object[] { "mission", 1u, "jigwi-ab/mission/1/1", revision, 35d });
        private bool Presented(object packet) => (bool)Method("HasPresentedProjectile").Invoke(network, new[] { packet });
        private static object Packet(string id, long request, ulong revision)
        {
            Type type = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
            object packet = Activator.CreateInstance(type);
            foreach (var value in new Dictionary<string, object> { { "sessionId", "mission" }, { "round", 1u },
                { "firstId", id }, { "requestId", request }, { "modelRevision", revision }, { "owner", 0UL } })
                type.GetField(value.Key).SetValue(packet, value.Value);
            return packet;
        }
        private static FieldInfo Field(string name) => typeof(LabNetwork).GetField(name, Private);
        private static MethodInfo Method(string name) => typeof(LabNetwork).GetMethod(name, Private);
    }
}
