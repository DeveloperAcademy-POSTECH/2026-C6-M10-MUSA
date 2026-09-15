using System;
using System.Collections;
using System.Collections.Generic;
using C6.Prototype.Orbs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.Attack.Tests
{
    /// <summary>Actual contacts in an isolated 3D physics scene; no synthetic hit callbacks.</summary>
    public sealed class BallisticProjectilePhysicsTests
    {
        private Scene scene;
        private PhysicsScene physics;
        private GameObject root;
        private readonly List<HostProjectile3D> projectiles = new List<HostProjectile3D>();
        private readonly List<ProjectileOutcome> outcomes = new List<ProjectileOutcome>();
        private Vector3 originalGravity;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalGravity = Physics.gravity;
            projectiles.Clear(); outcomes.Clear();
            scene = SceneManager.CreateScene("C6 P2 ballistic " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics = scene.GetPhysicsScene();
            Assert.That(physics.IsValid(), Is.True);
            root = new GameObject("Owned ballistic test scene");
            SceneManager.MoveGameObjectToScene(root, scene);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Assert.That(Physics.gravity, Is.EqualTo(originalGravity), "P2 must not mutate the global gravity setting.");
            if (root != null) Object.Destroy(root);
            yield return null;
            if (scene.IsValid() && scene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) while (!unload.isDone) yield return null;
            }
        }

        private static BallisticLaunch Launch(Vector2 delta, float lifetime = 4f)
        {
            var basis = new ProjectileLaunchBasis(new Vector3(0, 1.08f, -4.5f), Vector3.right, 3f, new Vector3(0, 1.4f, 0));
            var tuning = new ThrowTuning(.35f, 8f, 8f, 2.8f, 6f, 30f, 9.81f, .45f, lifetime, .165f, .12f, .02f);
            Assert.That(ThrowMapping.TryCalculate(new Vector2(.5f, 1), new OrbThrowInput(delta, .1f),
                basis, tuning, out var launch, out var error), Is.True, error);
            return launch;
        }

        private HostProjectile3D Spawn(BallisticLaunch launch, string id = "ballistic", Action<ProjectileOutcome> callback = null)
        {
            var projectile = HostProjectile3D.SpawnBallistic(true, id, 17, launch, callback ?? outcomes.Add, root.transform);
            projectiles.Add(projectile);
            Assert.That(projectile.gameObject.scene, Is.EqualTo(scene));
            return projectile;
        }

        private GameObject Box(string name, Vector3 position, Vector3 size, string targetId = null)
        {
            var game = new GameObject(name);
            SceneManager.MoveGameObjectToScene(game, scene);
            game.transform.SetParent(root.transform, false);
            game.transform.position = position;
            game.AddComponent<BoxCollider>().size = size;
            if (targetId != null) game.AddComponent<MonsterHitTarget>().Configure(targetId);
            return game;
        }
        private void Target() => Box("P0 physical target", new Vector3(0, 1.4f, 0), new Vector3(1.2f, 2.6f, .65f), "dev-training-dummy");
        private void Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                foreach (var projectile in projectiles)
                    if (projectile != null && projectile.isActiveAndEnabled)
                        projectile.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
                physics.Simulate(Time.fixedDeltaTime);
            }
        }

        [Test]
        public void NormalSwipeActuallyHitsTheFixedColliderAndCommitsOnlyOnce()
        {
            Target();
            HostProjectile3D projectile = null;
            projectile = Spawn(Launch(new Vector2(0, .125f)), callback: outcome =>
            {
                Assert.That(projectile.HasCompleted, Is.True, "Commit before callbacks, including reentrant cleanup.");
                outcomes.Add(outcome);
                projectile.CancelAndDestroy();
            });
            Assert.That(projectile.BallisticActive, Is.True);
            Assert.That(projectile.Body.useGravity, Is.False);
            Assert.That(projectile.Body.isKinematic, Is.False);
            Assert.That(projectile.Body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            Step(40);
            Assert.That(projectile.CollisionCallbackCount, Is.GreaterThan(0));
            Assert.That(outcomes, Has.Count.EqualTo(1));
            Assert.That(outcomes[0].Kind, Is.EqualTo(ProjectileOutcomeKind.Hit));
            Assert.That(outcomes[0].TargetId, Is.EqualTo("dev-training-dummy"));
            Assert.That(outcomes[0].OrbId, Is.EqualTo("ballistic"));
            Assert.That(outcomes[0].AttackerPlayerId, Is.EqualTo(17ul));
            Assert.That(outcomes[0].ElapsedPhysicsTime, Is.GreaterThan(.2f));
        }

        [TestCase(-.1f)]
        [TestCase(.1f)]
        public void LeftAndRightThrowsActuallyMissAndExpireInsteadOfHoming(float deltaX)
        {
            Target();
            var projectile = Spawn(Launch(new Vector2(deltaX, .125f), 1.2f));
            Step(80);
            Assert.That(outcomes, Has.Count.EqualTo(1));
            Assert.That(outcomes[0].Kind, Is.EqualTo(ProjectileOutcomeKind.Expired));
            Assert.That(outcomes[0].TargetId, Is.Null);
            Assert.That(Mathf.Sign(outcomes[0].Position.x), Is.EqualTo(Mathf.Sign(deltaX)));
            Assert.That(Mathf.Abs(outcomes[0].Position.x), Is.GreaterThan(2f));
            Assert.That(projectile.CollisionCallbackCount, Is.Zero);
        }

        [Test]
        public void SlowUpwardSwipeIsValidButFallsBelowTheTargetAndExpires()
        {
            Target();
            var projectile = Spawn(Launch(new Vector2(0, .04f), 2f));
            Step(110);
            Assert.That(outcomes, Has.Count.EqualTo(1));
            Assert.That(outcomes[0].Kind, Is.EqualTo(ProjectileOutcomeKind.Expired));
            Assert.That(outcomes[0].Position.y, Is.LessThan(-1f));
            Assert.That(projectile.CollisionCallbackCount, Is.Zero);
        }

        [Test]
        public void GroundCollisionBouncesWithoutClaimingMonsterHitThenExpiresOnce()
        {
            Box("Unmarked physical floor", new Vector3(0, -.1f, 0), new Vector3(30, .2f, 30));
            var projectile = Spawn(Launch(new Vector2(0, .05f), 1.6f));
            bool falling = false, bounced = false;
            for (int i = 0; i < 60; i++)
            {
                Step(1);
                if (projectile.Body.linearVelocity.y < -1f) falling = true;
                if (falling && projectile.CollisionCallbackCount > 0 && projectile.Body.linearVelocity.y > .5f) bounced = true;
            }
            Assert.That(projectile.CollisionCallbackCount, Is.GreaterThan(0));
            Assert.That(bounced, Is.True, "A real unmarked Collider must reverse falling velocity.");
            Assert.That(outcomes, Is.Empty);
            Step(40);
            Assert.That(outcomes, Has.Count.EqualTo(1));
            Assert.That(outcomes[0].Kind, Is.EqualTo(ProjectileOutcomeKind.Expired));
            Assert.That(outcomes[0].TargetId, Is.Null);
        }

        [Test]
        public void CustomGravityChangesVerticalVelocityButKeepsLegacyFlightStraight()
        {
            var ballistic = Spawn(Launch(new Vector2(0, .125f)));
            var oldPose = new ProjectileLaunchPose(new Vector3(5, 1.08f, -4.5f), Vector3.forward);
            var legacy = HostProjectile3D.Spawn(true, "legacy", 17, oldPose,
                new ProjectilePhysicsTuning(10, 4, .165f), outcomes.Add, root.transform);
            projectiles.Add(legacy);
            Step(10);
            Assert.That(ballistic.Body.linearVelocity.y,
                Is.EqualTo(3.5f - 9.81f * 10 * Time.fixedDeltaTime).Within(.0002f));
            Assert.That(legacy.BallisticActive, Is.False);
            Assert.That(legacy.Body.linearVelocity, Is.EqualTo(Vector3.forward * 10));
            Assert.That(legacy.Body.position.y, Is.EqualTo(1.08f).Within(.00001f));
            Assert.That(Physics.gravity, Is.EqualTo(originalGravity));
        }

        [Test]
        public void BallisticLifetimeAndCancelKeepExactlyOneOrNoOutcome()
        {
            var expires = Spawn(Launch(new Vector2(0, .125f), .08f), "expires");
            var cancelled = Spawn(Launch(new Vector2(0, .125f), .08f), "cancelled");
            cancelled.CancelAndDestroy();
            Step(20);
            Assert.That(expires.HasCompleted, Is.True);
            Assert.That(outcomes, Has.Count.EqualTo(1));
            Assert.That(outcomes[0].OrbId, Is.EqualTo("expires"));
            Assert.That(outcomes[0].Kind, Is.EqualTo(ProjectileOutcomeKind.Expired));
            Assert.That(outcomes[0].ElapsedPhysicsTime, Is.InRange(.079f, .101f));
            Assert.That(cancelled.Body.detectCollisions, Is.False);
        }

        [Test]
        public void NonHostAndInvalidLaunchRejectBeforeCreatingAnyObject()
        {
            int before = root.transform.childCount;
            Assert.Throws<InvalidOperationException>(() => HostProjectile3D.SpawnBallistic(false, "denied", 17,
                Launch(new Vector2(0, .125f)), outcomes.Add, root.transform));
            Assert.Throws<ArgumentException>(() => HostProjectile3D.SpawnBallistic(true, "invalid", 17,
                default, outcomes.Add, root.transform));
            Assert.That(root.transform.childCount, Is.EqualTo(before));
            Assert.That(outcomes, Is.Empty);
        }
    }
}
