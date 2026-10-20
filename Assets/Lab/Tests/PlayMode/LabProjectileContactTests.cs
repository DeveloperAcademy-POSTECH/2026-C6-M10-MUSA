using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>Real PlayMode physics: a predictive CCD contact alone must not become a game hit.</summary>
    public sealed class LabProjectileContactTests
    {
        private static readonly Vector3 Center = new Vector3(0f, 1001f, 0f);
        private GameObject fixture;
        private LabConfig config;
        private LabTarget target;
        private Collider backstop;

        private sealed class Receipt
        {
            public int Count;
            public LabProjectileResult Result;
            public void Receive(LabProjectileResult result) { Count++; Result = result; }
        }

        [UnitySetUp]
        public IEnumerator Prepare()
        {
            fixture = new GameObject("Projectile geometric contact fixture");
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[32] = 1000f; // Permit a complete target crossing in one fixed step.
            values[33] = .001f; values[34] = 1f; values[38] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            target = LabTarget.CreateCylinder(fixture.transform, Center, .8f, 2f);
            backstop = Wall("Miss backstop", -3f, .1f);
            // Primitive presentation colliders finish their existing deferred removal first.
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (fixture != null) Object.Destroy(fixture);
            if (config != null) Object.Destroy(config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WholeGrazesAndHalfMissesRemainCorrectWhenTheirSidesAreSwapped()
        {
            foreach (float wholeSide in new[] { 1f, -1f })
            {
                var whole = new Receipt(); var small = new Receipt();
                Shot("whole-" + wholeSide, wholeSide * .92f, 3f, 1f, 1f, -12f, whole);
                Shot("half-" + wholeSide, -wholeSide * .92f, 3f, .5f, 1f, -12f, small);
                yield return Await(whole, small);
                Assert.That(whole.Result.Hit, Is.True, "The original whole-radius graze must still hit on either side.");
                Assert.That(small.Result.Hit, Is.False,
                    "At |x|=.92 the half radius leaves a real gap; a speculative contact is not a hit.");
                yield return AssertNoFurtherReceipts(whole, small);
            }
        }

        [UnityTest]
        public IEnumerator ComboAndBlastProductsUseTheirActualRadiusForBothGrazesAndMisses()
        {
            foreach (float comboScale in new[] { .3f, 1.5f })
            {
                float radius = config.ThrowRadius * .5f * comboScale;
                var grazing = new Receipt(); var outside = new Receipt();
                LabProjectile hit = Shot("combo-graze-" + comboScale, .8f + radius * .65f,
                    3f, .5f, comboScale, -12f, grazing);
                LabProjectile miss = Shot("combo-miss-" + comboScale, -(.8f + radius + .04f),
                    3f, .5f, comboScale, -12f, outside);
                Assert.That(hit.GetComponent<SphereCollider>().radius, Is.EqualTo(radius).Within(.00001f));
                Assert.That(miss.GetComponent<SphereCollider>().radius, Is.EqualTo(radius).Within(.00001f));
                yield return Await(grazing, outside);
                Assert.That(grazing.Result.Hit, Is.True, "A genuine overlap within the combined radius must hit.");
                Assert.That(outside.Result.Hit, Is.False,
                    "The unscaled throw radius must not expand a combo/blast-shrunk shot's reach.");
                yield return AssertNoFurtherReceipts(grazing, outside);
            }
        }

        [UnityTest]
        public IEnumerator AHighSpeedSmallShotHitsTheCenterButAnEarlierMissSurfaceWins()
        {
            // 240m/s travels 4.8m per usual fixed step: past both cylinder faces from z=3.
            var hit = new Receipt();
            Shot("fast-small-center", 0f, 3f, .5f, .3f, -240f, hit);
            yield return Await(hit);
            Assert.That(hit.Result.Hit, Is.True, "Sweeping the actual small sphere must retain fast real hits.");
            yield return AssertNoFurtherReceipts(hit);

            Collider obstruction = Wall("Earlier registered miss surface", 1.5f, .04f);
            var blocked = new Receipt();
            LabProjectile shot = Shot("fast-wall-first", 0f, 3f, .5f, .3f, -240f, blocked,
                obstruction);
            Assert.That(shot.GetComponent<Rigidbody>().collisionDetectionMode,
                Is.EqualTo(CollisionDetectionMode.ContinuousDynamic), "Only projectile CCD uses a real swept sphere; forces and settings stay unchanged.");
            yield return Await(blocked);
            Assert.That(blocked.Result.Hit, Is.False, "A later target cannot win over the first registered miss surface.");
            Assert.That(blocked.Result.HitPoint.z, Is.GreaterThan(1.4f),
                "The receipt must describe the front obstruction, not the cylinder behind it.");
            yield return AssertNoFurtherReceipts(blocked);
        }

        [UnityTest]
        public IEnumerator AStationarySmallSphereAlreadyOverlappingTheShellStillReportsOneHit()
        {
            float radius = config.ThrowRadius * .5f * .3f;
            var overlap = new Receipt();
            Shot("actual-initial-overlap", 0f, .8f + radius * .5f, .5f, .3f, 0f, overlap);
            yield return Await(overlap);
            Assert.That(overlap.Result.Hit, Is.True,
                "A zero-length sweep must not discard a sphere already overlapping the shell.");
            yield return AssertNoFurtherReceipts(overlap);
        }

        [UnityTest]
        public IEnumerator AMovingShellCanTouchAStationarySmallShotWithoutAProjectileSweep()
        {
            target.Configure(Center + Vector3.right * 4f, .8f, 2f);
            Physics.SyncTransforms();
            float radius = config.ThrowRadius * .5f * .3f;
            var contact = new Receipt();
            LabProjectile shot = Shot("shell-moves-into-stationary-shot", 0f,
                .8f + radius * .5f, .5f, .3f, 0f, contact);
            Vector3 stationary = shot.GetComponent<Rigidbody>().position;
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.That(contact.Count, Is.Zero, "The distant shell has not touched this shot yet.");
            Assert.That(Vector3.Distance(shot.GetComponent<Rigidbody>().position, stationary),
                Is.LessThan(.00001f), "This fixture must exercise a zero-displacement shot, not a forward sweep.");

            // Put the actual cylinder shell through the sphere while its own path stays still.
            // A query of projectile displacement alone cannot detect this genuine contact.
            target.Configure(Center, .8f, 2f);
            Physics.SyncTransforms();
            yield return Await(contact);
            Assert.That(contact.Result.Hit, Is.True,
                "Moving target geometry must still hit a stationary sphere at the current collider pose.");
            yield return AssertNoFurtherReceipts(contact);
        }

        [UnityTest]
        public IEnumerator TargetMovementIsRespectedAndVisualReplicasCannotReportAuthoritativeHits()
        {
            foreach (bool moveIntoPath in new[] { true, false })
            {
                target.Configure(Center + (moveIntoPath ? Vector3.right * 4f : Vector3.zero), .8f, 2f);
                Physics.SyncTransforms(); // One explicit fixture pose change, never a per-frame production workaround.
                var moving = new Receipt();
                Shot("moving-target-" + moveIntoPath, 0f, 3f, .5f, .3f, -12f, moving);
                for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
                Assert.That(moving.Count, Is.Zero, "The target is changed before the sphere can reach the shell.");
                target.Configure(Center + (moveIntoPath ? Vector3.zero : Vector3.right * 4f), .8f, 2f);
                Physics.SyncTransforms();
                yield return Await(moving);
                Assert.That(moving.Result.Hit, Is.EqualTo(moveIntoPath),
                    "Proof must use the target's current collider pose, not a cached cylinder position.");
                yield return AssertNoFurtherReceipts(moving);
            }

            target.Configure(Center, .8f, 2f);
            Physics.SyncTransforms();
            var replica = LabProjectile.Create(Center + Vector3.forward * 3f, config, fixture.transform,
                LabOrbElement.Fire, .5f);
            replica.SetRadiusScale(.3f);
            Assert.That(replica.LaunchVisual(Vector3.back * 12f, config, target), Is.True);
            Assert.That(replica.GetComponent<SphereCollider>().isTrigger, Is.True);
            var forbidden = new Receipt();
            Assert.That(replica.Launch("cannot-escalate-replica", 99, Vector3.back * 12f,
                config, target, forbidden.Receive), Is.False);
            for (int i = 0; i < 80 && replica != null; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(replica == null, Is.True, "A visual collision/lifetime still retires its local presentation.");
            Assert.That(forbidden.Count, Is.Zero, "Visual retirement cannot call the Host's hit callback.");
        }

        [UnityTest]
        public IEnumerator AHighSpeedSmallShotHitsTheRealMovingHangingHierarchy()
        {
            // Use the same component that reparents the saved target hitbox under a kinematic
            // suspension body. A static cylinder alone does not cover this live-game structure.
            LabHangingTarget hanging = target.gameObject.AddComponent<LabHangingTarget>();
            hanging.ConfigureRopeMotion(.2f, .5f, .18f, 45f, 3f, .5f);
            target.transform.rotation = Quaternion.Euler(8f, 37f, 12f);
            hanging.ApplyRemoteImpact(new Vector3(.3f, 0f, 0f), 40f);
            Physics.SyncTransforms();
            Vector3 beforeMotion = target.Hitbox.bounds.center;
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();

            Rigidbody suspension = target.Hitbox.attachedRigidbody;
            Assert.That(suspension, Is.Not.Null);
            Assert.That(suspension.isKinematic, Is.True);
            Assert.That(suspension.transform.IsChildOf(target.transform), Is.True);
            Assert.That(target.Hitbox.transform.IsChildOf(suspension.transform), Is.True);
            Assert.That(target.Hitbox.convex, Is.False);
            Assert.That(Vector3.Distance(target.Hitbox.bounds.center, beforeMotion), Is.GreaterThan(.0001f),
                "The real rope cycle must actually move this fixture's shell before the shot.");
            Assert.That(Quaternion.Angle(target.Hitbox.transform.rotation, Quaternion.identity), Is.GreaterThan(1f),
                "The nested hitbox is rotated, so axis-aligned-cylinder assumptions cannot satisfy this case.");

            var receipt = new Receipt();
            LabProjectile shot = LabProjectile.Create(target.Hitbox.bounds.center + Vector3.forward * 3f,
                config, fixture.transform, LabOrbElement.Fire, .5f);
            shot.SetRadiusScale(.3f);
            shot.SetMissSurface(backstop);
            Assert.That(shot.Launch("fast-real-hanging-shell", 10, Vector3.back * 240f,
                config, target, receipt.Receive), Is.True);
            Assert.That(shot.GetComponent<SphereCollider>().radius,
                Is.EqualTo(config.ThrowRadius * .5f * .3f).Within(.00001f));
            yield return Await(receipt);
            Assert.That(receipt.Result.Hit, Is.True,
                "A centered small fast shot must retain genuine hits on the moving kinematic-parent mesh.");
            yield return AssertNoFurtherReceipts(receipt);
        }

        private LabProjectile Shot(string id, float x, float z, float blastScale, float comboScale,
            float velocityZ, Receipt receipt, Collider missSurface = null)
        {
            var shot = LabProjectile.Create(Center + new Vector3(x, 0f, z), config,
                fixture.transform, LabOrbElement.Fire, blastScale);
            shot.SetRadiusScale(comboScale);
            shot.SetMissSurface(missSurface != null ? missSurface : backstop);
            Assert.That(shot.Launch(id, 10, Vector3.forward * velocityZ, config, target, receipt.Receive), Is.True);
            return shot;
        }

        private Collider Wall(string name, float z, float thickness)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(fixture.transform, false);
            wall.transform.position = Center + Vector3.forward * z;
            wall.transform.localScale = new Vector3(8f, 8f, thickness);
            return wall.GetComponent<Collider>();
        }

        private static IEnumerator Await(params Receipt[] receipts)
        {
            for (int step = 0; step < 100; step++)
            {
                bool complete = true;
                foreach (Receipt receipt in receipts) if (receipt.Count == 0) complete = false;
                if (complete) break;
                yield return new WaitForFixedUpdate();
            }
            foreach (Receipt receipt in receipts)
                Assert.That(receipt.Count, Is.EqualTo(1), "Each launched Host shot must resolve exactly once.");
        }

        private static IEnumerator AssertNoFurtherReceipts(params Receipt[] receipts)
        {
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            foreach (Receipt receipt in receipts)
                Assert.That(receipt.Count, Is.EqualTo(1), "Sweep, overlap and contact callbacks must not double-count.");
        }
    }
}
