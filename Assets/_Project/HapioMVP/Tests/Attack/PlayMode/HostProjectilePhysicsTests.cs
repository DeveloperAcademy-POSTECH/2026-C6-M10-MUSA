using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Attack.Tests
{
    /// <summary>Real PhysX simulation during the player loop. No injected Collision or timer-based hit.</summary>
    public sealed class HostProjectilePhysicsTests
    {
        private static readonly Vector3 Origin = new Vector3(1000f, 1000f, 1000f);
        private readonly List<GameObject> owned = new List<GameObject>();
        private readonly List<ProjectileOutcome> outcomes = new List<ProjectileOutcome>();

        [UnityTearDown]
        public IEnumerator CleanupOnlyOwnedPhysicsObjects()
        {
            foreach (var item in owned)
            {
                if (item == null) continue;
                var projectile = item.GetComponent<HostProjectile3D>();
                if (projectile != null) projectile.CancelAndDestroy();
                else Object.Destroy(item);
            }
            owned.Clear();
            outcomes.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DefaultSpeedTravelsBeforeARealTargetCollision()
        {
            Target(Origin + Vector3.forward * 3f, new Vector3(1f, 1f, .1f));
            var projectile = Spawn("default-speed", Origin, Vector3.forward, 12f, 3f);
            Assert.That(projectile.Body.isKinematic, Is.False);
            Assert.That(projectile.Body.useGravity, Is.False);
            Assert.That(projectile.Body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            Assert.That(projectile.HitCollider.isTrigger, Is.False);
            Assert.That(outcomes, Is.Empty, "Spawn must not apply an immediate fake hit.");
            yield return new WaitForFixedUpdate();
            Assert.That(projectile.Body.position.z, Is.GreaterThan(Origin.z));
            Assert.That(outcomes, Is.Empty, "The target is beyond the first 12 units/sec physics step.");
            yield return WaitForOutcomes(1);
            Assert.That(outcomes[0].Kind, Is.EqualTo(ProjectileOutcomeKind.Hit));
            Assert.That(outcomes[0].OrbId, Is.EqualTo("default-speed"));
            Assert.That(outcomes[0].AttackerPlayerId, Is.EqualTo(17ul));
            Assert.That(outcomes[0].TargetId, Is.EqualTo("actual-physics-target"));
            Assert.That(outcomes[0].ElapsedPhysicsTime, Is.GreaterThan(0f));
            Debug.Log("C6_T06_PHYSICS speed=12 outcome=Hit elapsed=" + outcomes[0].ElapsedPhysicsTime);
            yield return null;
            Assert.That(projectile == null, Is.True, "A hit projectile must be removed after its result.");
        }

        [UnityTest]
        public IEnumerator ExplicitHighSpeedStressDoesNotTunnelThroughThinStaticCollider()
        {
            Target(Origin + Vector3.forward * 3f, new Vector3(1f, 1f, .02f));
            var projectile = Spawn("stress-120", Origin, Vector3.forward, 120f, 3f);
            Assert.That(projectile.Body.linearVelocity.magnitude, Is.EqualTo(120f).Within(.001f));
            yield return WaitForOutcomes(1);
            Assert.That(outcomes[0].Kind, Is.EqualTo(ProjectileOutcomeKind.Hit));
            Assert.That(outcomes[0].Position.z, Is.LessThan(Origin.z + 3.2f));
            Debug.Log("C6_T06_PHYSICS speed=120 STRESS_ONLY targetThickness=0.02 outcome=Hit");
        }

        [UnityTest]
        public IEnumerator TwoActualColliderPairsCompleteOnlyOnceAndCommitBeforeCallback()
        {
            var target = new GameObject("T06 compound target");
            owned.Add(target);
            target.transform.position = Origin + Vector3.forward * 2f;
            target.AddComponent<MonsterHitTarget>().Configure("compound-target");
            AddChildBox(target.transform, new Vector3(-.2f, 0f, 0f), new Vector3(.6f, 1f, .1f));
            AddChildBox(target.transform, new Vector3(.2f, 0f, 0f), new Vector3(.6f, 1f, .1f));
            HostProjectile3D projectile = null;
            bool completedBeforeCallback = false;
            int callbacksObserved = 0;
            // This collision-multiplicity fixture starts in simultaneous contact with both
            // boxes and uses Discrete only on its owned test body. A CCD sweep legitimately
            // stops at the first time-of-impact pair, so two coplanar targets alone do not
            // guarantee two callbacks. Production/default-12/stress-120 CCD remain unchanged.
            projectile = Spawn("compound-hit", Origin + Vector3.forward * 1.96f, Vector3.forward, .5f, 3f, outcome =>
            {
                completedBeforeCallback = projectile.HasCompleted;
                outcomes.Add(outcome);
            });
            projectile.Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Physics.SyncTransforms();
            yield return WaitForOutcomes(1);
            // The managed counter survives deferred Unity destruction and records the actual
            // callback count, including subsequent pairs from the first contact step.
            callbacksObserved = projectile.CollisionCallbackCount;
            Assert.That(completedBeforeCallback, Is.True);
            Assert.That(callbacksObserved, Is.GreaterThanOrEqualTo(2), "Both real collider pairs must exercise the duplicate guard.");
            yield return new WaitForFixedUpdate();
            Assert.That(outcomes.Count, Is.EqualTo(1));
            Debug.Log("C6_T06_PHYSICS SIMULTANEOUS_CONTACT_STRESS mode=Discrete speed=0.5 compoundCollisionCallbacks="
                + callbacksObserved + " resultCount=1 committedBeforeCallback=true");
        }

        [UnityTest]
        public IEnumerator ARealMissExpiresWithoutAHitOrDamageCallback()
        {
            Target(Origin + new Vector3(4f, 0f, 2f), Vector3.one);
            var projectile = Spawn("short-lifetime-miss", Origin, Vector3.forward, 12f, .08f);
            yield return WaitForOutcomes(1);
            Assert.That(outcomes[0].Kind, Is.EqualTo(ProjectileOutcomeKind.Expired));
            Assert.That(outcomes[0].TargetId, Is.Null);
            Assert.That(outcomes[0].ElapsedPhysicsTime, Is.GreaterThanOrEqualTo(.08f));
            Assert.That(outcomes[0].Position.z, Is.GreaterThan(Origin.z));
            yield return null;
            Assert.That(projectile == null, Is.True);
            Assert.That(outcomes.Count, Is.EqualTo(1));
            Debug.Log("C6_T06_PHYSICS SHORT_LIFETIME=0.08 miss=Expired hitCount=0");
        }

        [UnityTest]
        public IEnumerator DefaultThreeSecondLifetimeRunsInFullAndExpiresOnce()
        {
            var projectile = Spawn("default-lifetime", Origin, Vector3.forward, 12f, 3f);
            float deadline = Time.realtimeSinceStartup + 8f;
            while (projectile != null && projectile.ElapsedPhysicsTime < 2.8f && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(outcomes, Is.Empty);
                yield return new WaitForFixedUpdate();
            }
            yield return WaitForOutcomes(1);
            Assert.That(outcomes[0].Kind, Is.EqualTo(ProjectileOutcomeKind.Expired));
            Assert.That(outcomes[0].ElapsedPhysicsTime, Is.GreaterThanOrEqualTo(3f));
            Assert.That(outcomes[0].ElapsedPhysicsTime, Is.LessThan(3f + Time.fixedDeltaTime * 1.1f));
            Debug.Log("C6_T06_PHYSICS FULL_LIFETIME=3 outcome=Expired elapsed=" + outcomes[0].ElapsedPhysicsTime);
        }

        [UnityTest]
        public IEnumerator ProjectilePairsPassThroughEachOtherWithoutGlobalCollisionMatrixChanges()
        {
            bool originalLayerPolicy = Physics.GetIgnoreLayerCollision(0, 0);
            var left = Spawn("pair-left", Origin - Vector3.right, Vector3.right, 12f, .4f);
            var right = Spawn("pair-right", Origin + Vector3.right, Vector3.left, 12f, .4f);
            Assert.That(Physics.GetIgnoreCollision(left.HitCollider, right.HitCollider), Is.True);
            for (int i = 0; i < 7; i++) yield return new WaitForFixedUpdate();
            Assert.That(left.Body.position.x, Is.GreaterThan(right.Body.position.x));
            Assert.That(left.CollisionCallbackCount, Is.Zero);
            Assert.That(right.CollisionCallbackCount, Is.Zero);
            Assert.That(Physics.GetIgnoreLayerCollision(0, 0), Is.EqualTo(originalLayerPolicy));
            Assert.That(outcomes, Is.Empty);
        }

        [UnityTest]
        public IEnumerator UnmarkedObstacleCannotProduceMonsterHit()
        {
            var obstacle = new GameObject("T06 non-target obstacle");
            owned.Add(obstacle);
            obstacle.transform.position = Origin + Vector3.forward;
            obstacle.AddComponent<BoxCollider>().size = Vector3.one;
            var projectile = Spawn("unmarked-obstacle", Origin, Vector3.forward, 12f, .3f);
            int collisionCallbacks = 0;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (outcomes.Count == 0 && Time.realtimeSinceStartup < deadline)
            {
                if (projectile != null) collisionCallbacks = Math.Max(collisionCallbacks, projectile.CollisionCallbackCount);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(outcomes.Count, Is.EqualTo(1), "Non-target collision must terminate at its configured lifetime.");
            Assert.That(collisionCallbacks, Is.GreaterThan(0), "An actual non-monster collision must occur.");
            Assert.That(outcomes[0].Kind, Is.EqualTo(ProjectileOutcomeKind.Expired));
            Assert.That(outcomes[0].TargetId, Is.Null);
        }

        [UnityTest]
        public IEnumerator ConfirmedCancellationRemovesPhysicsWithoutInventingOutcome()
        {
            Target(Origin + Vector3.forward, Vector3.one * .5f);
            var projectile = Spawn("cancelled", Origin, Vector3.forward, 12f, 3f);
            projectile.CancelAndDestroy();
            Assert.That(projectile.HasCompleted, Is.True);
            Assert.That(projectile.HitCollider.enabled, Is.False);
            Assert.That(projectile.Body.isKinematic, Is.True);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(outcomes, Is.Empty);
            Assert.That(projectile == null, Is.True);
        }

        [Test]
        public void NonHostCannotCreateAViewOrPhysicsBody()
        {
            int before = HostProjectile3D.LiveCount;
            Assert.Throws<InvalidOperationException>(() => HostProjectile3D.Spawn(false, "client-denied", 17ul,
                new ProjectileLaunchPose(Origin, Vector3.forward), new ProjectilePhysicsTuning(12f, 3f, .1f), outcomes.Add));
            Assert.That(HostProjectile3D.LiveCount, Is.EqualTo(before));
            Assert.That(outcomes, Is.Empty);
        }

        private HostProjectile3D Spawn(string id, Vector3 position, Vector3 direction, float speed, float lifetime,
            Action<ProjectileOutcome> callback = null)
        {
            var projectile = HostProjectile3D.Spawn(true, id, 17ul, new ProjectileLaunchPose(position, direction),
                new ProjectilePhysicsTuning(speed, lifetime, .1f), callback ?? outcomes.Add);
            owned.Add(projectile.gameObject);
            return projectile;
        }

        private GameObject Target(Vector3 position, Vector3 size)
        {
            var target = new GameObject("T06 real fixed target");
            owned.Add(target);
            target.transform.position = position;
            target.AddComponent<MonsterHitTarget>().Configure("actual-physics-target");
            target.AddComponent<BoxCollider>().size = size;
            Assert.That(target.GetComponent<Rigidbody>(), Is.Null);
            return target;
        }

        private static void AddChildBox(Transform parent, Vector3 localPosition, Vector3 size)
        {
            var child = new GameObject("Target collision pair");
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.AddComponent<BoxCollider>().size = size;
        }

        private IEnumerator WaitForOutcomes(int expected)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (outcomes.Count < expected && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();
            Assert.That(outcomes.Count, Is.EqualTo(expected), "Real physics outcome timeout or duplicate result.");
        }
    }
}
