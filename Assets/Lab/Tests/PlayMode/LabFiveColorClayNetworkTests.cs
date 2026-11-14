using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>One real saved-scene Solo Host plus authenticated in-process client packet fixtures.</summary>
    [Category("GPU")]
    public sealed class LabFiveColorClayNetworkTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Session = "five-color-clay-client";
        private readonly LabSavedSceneNetworkFixture saved = new LabSavedSceneNetworkFixture();
        private LabNetwork network;
        private LabFiveElementVfx pool;
        private GameObject root;
        private LabConfig tuning;
        private Scene clientScene;
        private Scene previousScene;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (root != null)
            {
                if (network != null) network.Stop();
                Object.Destroy(root);
                if (pool != null) Object.Destroy(pool.gameObject);
            }
            if (tuning != null) Object.Destroy(tuning);
            yield return saved.Cleanup();
            yield return null;
            if (clientScene.IsValid() && clientScene.isLoaded)
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                yield return SceneManager.UnloadSceneAsync(clientScene);
            }
        }

        [UnityTest]
        public IEnumerator SavedSoloHostLaunchesAnActualNoneFiveColorRewardAndPresentsOneClayContact()
        {
            yield return saved.Load();
            var developer = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
            network = developer.network;
            saved.UseAvailablePort(network);
            Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
            yield return null;
            Assert.That(network.IsHost, Is.True);
            AssertNoLegacyFiveColorBursts();
            Assert.That(network.VfxLabGiveFiveColorOrbs(1), Is.EqualTo(1));
            yield return null; // #719 draws its allowed reward-spawn dust before testing the contact.
            var rewardSpawnDust = new HashSet<LabFiveColorSparkle>(Object.FindObjectsByType<LabFiveColorSparkle>(FindObjectsSortMode.None)
                .Where(item => item.name == "Five Color Dust Burst"));
            LabOrbState orb = network.Snapshot.orbs.Single(item => item.fiveColor);
            Assert.That(orb.element, Is.EqualTo(LabOrbElement.None), "Exercise the real reward, not an elemental stand-in.");
            Assert.That(orb.kind, Is.EqualTo(LabOrbKind.Combined));
            pool = LabFiveElementVfx.ForScene(network.projectileRoot.gameObject.scene);
            pool.ManualClock = true;
            int hits = 0, receipts = 0, rewardHits = 0;
            network.LocalThrowHit += _ => hits++;
            network.LocalFiveColorHit += () => rewardHits++;
            Invoke("OnThrowRequested", new LabOrbThrowGesture(orb.id, new Vector2(0f, 2.8f), .5f, .5f));
            LabProjectile shot = network.projectileRoot.GetComponentsInChildren<LabProjectile>()
                .Single(item => item.ProjectileId == orb.id);
            Assert.That(shot.ClayFlightActive, Is.True);
            Assert.That(shot.VisualMount.gameObject.activeSelf, Is.False);
            Assert.That(shot.GetComponent<SphereCollider>().radius,
                Is.EqualTo(network.config.ThrowRadius * shot.SizeScale * shot.RadiusScale).Within(.00001f));
            Assert.That(shot.GetComponentInChildren<LabFiveColorSparkle>(), Is.Not.Null);
            Assert.That(pool.transform.Find("Flight 0/Wet clay").GetComponent<MeshRenderer>().sharedMaterial,
                Is.SameAs(Resources.Load<Material>("FiveElements/OrbSurface/Monochrome")));
            LabProjectileResult contact = default;
            FieldInfo callbackField = typeof(LabProjectile).GetField("onResolved", Private);
            var original = (Action<LabProjectileResult>)callbackField.GetValue(shot);
            callbackField.SetValue(shot, (Action<LabProjectileResult>)(result =>
            {
                receipts++;
                contact = result;
                original(result);
            }));
            for (int step = 0; step < 120 && receipts == 0; step++) yield return new WaitForFixedUpdate();
            Assert.That(receipts, Is.EqualTo(1), "The actual saved-profile physics throw must resolve.");
            Assert.That(contact.Hit, Is.True, "The legal centre flick must reach the real saved gourd hitbox.");
            Assert.That(contact.VisualDiameter, Is.GreaterThan(0f));
            Assert.That(contact.HitNormal.sqrMagnitude, Is.EqualTo(1f).Within(.002f));
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(network.LastLocalHitFiveColor, Is.True);
            Assert.That(pool.ActiveFlightCount, Is.Zero);
            Assert.That(pool.ActiveImpactCount, Is.EqualTo(1));
            AssertTint(pool.transform.Find("Impact 0/Contact squash").GetComponent<MeshRenderer>(), contact.FiveColorAge);
            Assert.That(rewardHits, Is.EqualTo(1), "The thrower's dedicated sound and haptic event still fires.");
            AssertNoLegacyFiveColorBursts(rewardSpawnDust);
            Invoke("OnProjectileResolved", contact);
            for (int step = 0; step < 3; step++) yield return new WaitForFixedUpdate();
            Assert.That(receipts, Is.EqualTo(1));
            Assert.That(hits, Is.EqualTo(1), "A repeated Host callback cannot confirm the consumed reward twice.");
            Assert.That(rewardHits, Is.EqualTo(1));
            Assert.That(pool.ActiveImpactCount, Is.EqualTo(1));
            AssertNoLegacyFiveColorBursts(rewardSpawnDust);
        }

        [UnityTest]
        public IEnumerator PredictedAndReplicaPathsUseFiveColorClayAndPosePayloadsWithoutReplayingContacts()
        {
            PrepareClient();
            yield return null; // Let the cylinder's presentation primitive colliders finish removal.
            object request = Packet("launch", "predicted-five", network.LocalPlayerId, 19);
            Put(request, "x", .5f); Put(request, "vy", 2.8f);
            Invoke("BeginPredictedShot", request);
            LabProjectile predicted = Get<Dictionary<long, LabProjectile>>("predictedShots")[19];
            Assert.That(predicted.ClayFlightActive, Is.True);
            Assert.That(predicted.GetComponentInChildren<LabFiveColorSparkle>(), Is.Not.Null);
            Assert.That(predicted.VisualMount.gameObject.activeSelf, Is.False);
            Vector3 before = predicted.transform.position;
            object approval = ProjectilePacket("predicted-five", network.LocalPlayerId, 19, before);
            Put(approval, "fiveColor", false);
            Receive(approval);
            Assert.That(predicted.ClayFlightActive, Is.False, "Host reconciliation can remove an incorrect predicted flag.");
            Put(approval, "fiveColor", true);
            Receive(approval);
            Assert.That(predicted.ClayFlightActive, Is.True, "The same body can reconcile back to approved five-color clay.");
            Assert.That(predicted.transform.position, Is.EqualTo(before), "Appearance reconciliation cannot rewind flight.");
            Assert.That(network.projectileRoot.GetComponentsInChildren<LabProjectile>().Length, Is.EqualTo(1));
            int hits = 0, rewardHits = 0;
            network.LocalThrowHit += _ => hits++;
            network.LocalFiveColorHit += () => rewardHits++;
            Transform hitbox = network.target.Hitbox.transform;
            Vector3 point = hitbox.TransformPoint(new Vector3(0f, 0f, .8f));
            Vector3 normal = hitbox.forward, incoming = -normal * 10f;
            var contact = new LabProjectileResult(null, 0, LabProjectileHitKind.Monster, point,
                .3f, 0f, 1f, incoming, hitNormal: normal, visualDiameter: .27f);
            // #713 added an optional soot flag; reflection must still supply its ninth argument.
            Invoke("OnPredictedShotContact", 19L, contact, LabOrbElement.None, 20, 1f, true, false, false, false);
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(pool.ActiveImpactCount, Is.EqualTo(1));
            Assert.That(rewardHits, Is.Zero, "Dedicated reward feedback waits for Host confirmation.");
            AssertNoLegacyFiveColorBursts();
            object confirmation = ImpactPacket("predicted-five", network.LocalPlayerId, point, normal, .27f);
            Receive(confirmation);
            Receive(confirmation);
            Assert.That(hits, Is.EqualTo(1), "The Host verdict corrects a presented hit without replaying it.");
            Assert.That(rewardHits, Is.EqualTo(1), "Repeated confirmation keeps reward sound and haptics single-shot.");
            Assert.That(pool.ActiveImpactCount, Is.EqualTo(1));
            AssertNoLegacyFiveColorBursts();
            predicted.gameObject.SetActive(false);

            object replicaPacket = ProjectilePacket("remote-five", 2UL, 20, new Vector3(0f, 81f, 3f));
            Receive(replicaPacket);
            LabProjectile replica = network.projectileRoot.GetComponentsInChildren<LabProjectile>()
                .Single(item => item.gameObject.activeSelf);
            Assert.That(replica.ClayFlightActive, Is.True);
            Assert.That(replica.GetComponent<SphereCollider>().isTrigger, Is.True);
            Assert.That(replica.GetComponentInChildren<LabFiveColorSparkle>(), Is.Not.Null);
            Receive(replicaPacket);
            Assert.That(network.projectileRoot.GetComponentsInChildren<LabProjectile>().Count(item => item.gameObject.activeSelf),
                Is.EqualTo(1), "A repeated projectile notice cannot acquire another lease.");
            Vector3 remoteNormal = new Vector3(.3f, .8f, .5f).normalized;
            object impact = ImpactPacket("remote-five", 2UL, point, remoteNormal, .19f);
            Receive(impact);
            Assert.That(pool.ActiveImpactCount, Is.EqualTo(2));
            Transform remoteImpact = pool.transform.Find("Impact 1");
            Assert.That(remoteImpact.localScale.x, Is.EqualTo(.19f).Within(.00001f),
                "The receiver uses the encoded physical diameter instead of the fallback full shot size.");
            Assert.That(Vector3.Dot(remoteImpact.up, remoteNormal), Is.GreaterThan(.999f),
                "The receiver transforms the encoded local surface normal into the real contact plane.");
            Assert.That(remoteImpact.Find("Contact squash").GetComponent<MeshRenderer>().sharedMaterial,
                Is.SameAs(Resources.Load<Material>("FiveElements/OrbSurface/Monochrome")));
            AssertTint(remoteImpact.Find("Contact squash").GetComponent<MeshRenderer>(), .63f);
            Receive(impact);
            Assert.That(pool.ActiveImpactCount, Is.EqualTo(2));
            Assert.That(hits, Is.EqualTo(1), "Remote visual contact never awards a local gameplay hit.");
            Assert.That(rewardHits, Is.EqualTo(1), "A remote throw must not trigger my reward sound or haptics.");
            AssertNoLegacyFiveColorBursts();
            Assert.That(network.Snapshot.hp, Is.EqualTo(800));
            Assert.That(network.Snapshot.paintCoveredCount, Is.Zero, "Client feedback never performs Host paint adjudication.");
        }

        private static void AssertNoLegacyFiveColorBursts(ISet<LabFiveColorSparkle> existingRewardDust = null)
        {
            Assert.That(Object.FindObjectsByType<LabFiveColorSparkle>(FindObjectsSortMode.None)
                .Where(item => (item.name == "Five Color Sparkle Burst" || item.name == "Five Color Dust Burst")
                    && !(item.name == "Five Color Dust Burst" && existingRewardDust != null
                        && existingRewardDust.Contains(item))),
                Is.Empty, "Five-color contacts must not create a legacy burst; only the exact pre-contact #719 reward dust may remain.");
        }

        private void PrepareClient()
        {
            previousScene = SceneManager.GetActiveScene();
            clientScene = SceneManager.CreateScene("Five color clay client fixture");
            SceneManager.SetActiveScene(clientScene); // Own any unexpected standalone effects as well as the fixture root.
            root = new GameObject("Five color clay client fixture");
            SceneManager.MoveGameObjectToScene(root, clientScene);
            tuning = Object.Instantiate(Resources.Load<LabConfig>("LabConfig"));
            network = root.AddComponent<LabNetwork>();
            network.enabled = false;
            network.config = tuning;
            network.target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 81f, 0f), .8f, 2f);
            network.target.transform.rotation = Quaternion.Euler(8f, 23f, 5f);
            network.projectileRoot = new GameObject("Owned projectiles").transform;
            network.projectileRoot.SetParent(root.transform, false);
            network.impactFeedback = root.AddComponent<LabImpactFeedback>();
            ulong owner = network.LocalPlayerId;
            Set("sessionId", Session);
            Set("latest", new LabSnapshot
            {
                sessionId = Session, round = 1u, phase = LabPhase.Playing, serverTime = 1d,
                remaining = 179d, hp = 800,
                players = new[] { new LabPlayerState { id = owner, seat = 0, stamina = 80f },
                    new LabPlayerState { id = 2UL, seat = 1, stamina = 80f } },
                orbs = new[] { new LabOrbState { id = "predicted-five", owner = owner,
                    kind = LabOrbKind.Combined, element = LabOrbElement.None, fiveColor = true,
                    ownershipVersion = 1UL, x = .5f, y = .5f } }
            });
            Invoke("EnsureActionRound");
            Get<LabImpactReceiptGate>("impactReceipts").SetActive(Session, 1u);
            pool = LabFiveElementVfx.ForScene(clientScene);
            pool.ManualClock = true;
        }

        private object ProjectilePacket(string id, ulong owner, long request, Vector3 position)
        {
            object packet = Packet("projectile", id, owner, request);
            Put(packet, "fiveColor", true); Put(packet, "element", (int)LabOrbElement.None);
            Put(packet, "x", position.x); Put(packet, "y", position.y); Put(packet, "z", position.z);
            Put(packet, "wx", 1f); Put(packet, "sizeScale", 1f); Put(packet, "jigwiABRadiusScale", 1f);
            return packet;
        }

        private object ImpactPacket(string id, ulong owner, Vector3 point, Vector3 normal, float diameter)
        {
            object packet = Packet("targetImpact", id, owner, 0);
            Vector3 local = network.target.Hitbox.transform.InverseTransformPoint(point);
            Put(packet, "x", local.x); Put(packet, "y", local.y); Put(packet, "z", local.z);
            Put(packet, "wx", -normal.x * 10f); Put(packet, "wy", -normal.y * 10f); Put(packet, "wz", -normal.z * 10f);
            Put(packet, "fiveColor", true); Put(packet, "element", (int)LabOrbElement.None);
            Put(packet, "damage", 20); Put(packet, "impactValue", 20); Put(packet, "sizeScale", 1f);
            Type poseType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabElementContactPose", true);
            object pose = Activator.CreateInstance(poseType);
            poseType.GetField("localNormal").SetValue(pose, network.target.Hitbox.transform.InverseTransformDirection(normal));
            poseType.GetField("diameter").SetValue(pose, diameter);
            poseType.GetField("fiveColorAge").SetValue(pose, .63f);
            Put(packet, "payload", JsonUtility.ToJson(pose));
            return packet;
        }

        private static object Packet(string type, string id, ulong owner, long request)
        {
            Type packetType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
            object packet = Activator.CreateInstance(packetType);
            Put(packet, "type", type); Put(packet, "firstId", id); Put(packet, "owner", owner);
            Put(packet, "requestId", request); Put(packet, "sessionId", Session); Put(packet, "round", 1u);
            return packet;
        }

        private void Receive(object packet)
        {
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
            var wire = new byte[payload.Length + 8];
            Buffer.BlockCopy(BitConverter.GetBytes(3), 0, wire, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, wire, 4, 4);
            Buffer.BlockCopy(payload, 0, wire, 8, payload.Length);
            MethodInfo receive = typeof(LabNetwork).GetMethod("Receive", Private);
            Type readerType = receive.GetParameters()[1].ParameterType;
            ConstructorInfo constructor = readerType.GetConstructors().Single(candidate =>
                candidate.GetParameters().Length == 4 && candidate.GetParameters()[0].ParameterType == typeof(byte[]));
            object reader = constructor.Invoke(new[] { (object)wire,
                Enum.Parse(constructor.GetParameters()[1].ParameterType, "Temp"), -1, 0 });
            try { receive.Invoke(network, new[] { (object)0UL, reader }); }
            finally { ((IDisposable)reader).Dispose(); }
        }

        private static void Put(object packet, string field, object value) => packet.GetType().GetField(field).SetValue(packet, value);
        private static void AssertTint(MeshRenderer body, float phase)
        {
            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            Color actual = block.GetColor("_BaseColor"), expected = LabFiveColorSparkle.ColorAt(phase);
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.001f));
        }
        private void Set(string field, object value) => typeof(LabNetwork).GetField(field, Private).SetValue(network, value);
        private T Get<T>(string field) => (T)typeof(LabNetwork).GetField(field, Private).GetValue(network);
        private void Invoke(string method, params object[] args) => typeof(LabNetwork).GetMethod(method, Private).Invoke(network, args);
    }
}
