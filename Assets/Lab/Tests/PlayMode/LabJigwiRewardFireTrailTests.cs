using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Real projectile presentation and scaled-time mesh histories, without sockets or device-play claims.</summary>
    public sealed class LabJigwiRewardFireTrailTests
    {
        private static readonly Vector3 Arena = new Vector3(0f, 90f, 0f);
        private readonly List<Object> owned = new List<Object>();
        private GameObject root;
        private LabConfig config;
        private LabTarget target;
        private LabJigwiRewardFireProfile profile;
        private float previousTimeScale;

        [SetUp]
        public void Prepare()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            root = Own(new GameObject("Dedicated Jigwi reward fire fixture"));
            config = Own(Object.Instantiate(Resources.Load<LabConfig>("LabConfig")));
            profile = Resources.Load<LabJigwiRewardFireProfile>(LabJigwiRewardFireProfile.ResourcePath);
            Assert.That(profile, Is.Not.Null, "The dedicated reward artwork/profile must be present in the build's Resources path.");
            target = LabTarget.CreateCylinder(root.transform, Arena + Vector3.forward * 50f, .8f, 2f);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = previousTimeScale;
            foreach (Object item in owned) if (item != null) Object.Destroy(item);
            owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiveElementsKeepTheirOwnInkAndPhysicalProfilesWhileOnlyRewardedShotsHaveTheDedicatedLayer()
        {
            Vector3 velocity = new Vector3(.6f, 3f, 8f);
            var ordinaryFire = Shot(LabOrbElement.Fire, -2f, false);
            Assert.That(ordinaryFire.LaunchVisual(velocity, config, target), Is.True);
            Assert.That(ordinaryFire.JigwiRewardFireTrail, Is.Null);
            var shots = new List<LabProjectile>();
            foreach (LabOrbElement element in new[] { LabOrbElement.Fire, LabOrbElement.Water,
                LabOrbElement.Wood, LabOrbElement.Metal, LabOrbElement.Earth })
            {
                var shot = Shot(element, (int)element * 2f, true);
                Assert.That(shot.Launch("reward-element-" + element, 42, velocity, config, target, _ => { }), Is.True);
                Assert.That(shot.JigwiABRewardActive, Is.True);
                Assert.That(shot.ElementFlightTrail.Element, Is.EqualTo(element), "The ordinary elemental ink keeps the actual pigment.");
                Assert.That(shot.JigwiRewardFireTrail, Is.Not.Null);
                Assert.That(shot.JigwiRewardFireTrail.Profile, Is.SameAs(profile));
                Assert.That(shot.JigwiRewardFireTrail.IsEmitting, Is.True);
                Assert.That(shot.InitialVelocity, Is.EqualTo(velocity));
                Assert.That(shot.GravityAcceleration, Is.EqualTo(config.EffectiveThrowGravity));
                Assert.That(shot.GetComponent<SphereCollider>().radius,
                    Is.EqualTo(config.ThrowRadius * 1.3f).Within(.00001f), "Reward art must not apply the approved radius multiplier twice.");
                Assert.That(shot.GetComponentsInChildren<Rigidbody>(true), Has.Length.EqualTo(1));
                Assert.That(shot.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1));
                shots.Add(shot);
            }
            Assert.That(shots[0].ElementFlightTrail.Profile, Is.SameAs(ordinaryFire.ElementFlightTrail.Profile),
                "Normal Fire stays unchanged; the additional reward layer is the distinguishing effect.");
            yield return new WaitForFixedUpdate(); yield return null;
            foreach (LabProjectile shot in shots)
            {
                Assert.That(shot.ElementFlightTrail.PathCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(shot.JigwiRewardFireTrail.PathCount, Is.GreaterThanOrEqualTo(2));
            }
        }

        [UnityTest]
        public IEnumerator ApprovalChangesOnlyTheDedicatedLayerAndRepeatedApprovalPreservesBothHistoriesAndFlight()
        {
            var shot = Shot(LabOrbElement.Metal, 0f, false);
            Assert.That(shot.LaunchVisual(new Vector3(.6f, 3f, 8f), config, target), Is.True);
            yield return new WaitForFixedUpdate(); yield return null;
            var ink = shot.ElementFlightTrail;
            int inkCount = ink.PathCount;
            Vector3 position = shot.transform.position, velocity = shot.GetComponent<Rigidbody>().linearVelocity;
            shot.SetJigwiABReward(true);
            var reward = shot.JigwiRewardFireTrail;
            Assert.That(reward, Is.Not.Null);
            Assert.That(ink.PathCount, Is.EqualTo(inkCount), "A late reward approval cannot restart the elemental history.");
            Assert.That(reward.GetPathPosition(0), Is.EqualTo(position));
            Assert.That(shot.transform.position, Is.EqualTo(position));
            Assert.That(shot.GetComponent<Rigidbody>().linearVelocity, Is.EqualTo(velocity));
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(reward.PathCount, Is.GreaterThanOrEqualTo(2));
            int rewardCount = reward.PathCount; inkCount = ink.PathCount;
            Vector3 first = reward.GetPathPosition(0); position = shot.transform.position;
            velocity = shot.GetComponent<Rigidbody>().linearVelocity;
            shot.ReconcileVisual(LabOrbElement.Metal, 1f, 1.3f);
            shot.SetJigwiABReward(true);
            Assert.That(shot.JigwiRewardFireTrail, Is.SameAs(reward));
            Assert.That(reward.PathCount, Is.EqualTo(rewardCount));
            Assert.That(reward.GetPathPosition(0), Is.EqualTo(first));
            Assert.That(ink.PathCount, Is.EqualTo(inkCount));
            Assert.That(ink.Element, Is.EqualTo(LabOrbElement.Metal));
            Assert.That(shot.transform.position, Is.EqualTo(position));
            Assert.That(shot.GetComponent<Rigidbody>().linearVelocity, Is.EqualTo(velocity));
            shot.SetJigwiABReward(false);
            Assert.That(shot.JigwiABRewardActive, Is.False);
            Assert.That(reward == null || !reward.IsEmitting && reward.PathCount == 0, Is.True);
            Assert.That(ink.PathCount, Is.EqualTo(inkCount));
            shot.SetJigwiABReward(true);
            Assert.That(shot.JigwiRewardFireTrail.IsEmitting, Is.True);
            Assert.That(shot.JigwiRewardFireTrail.PathCount, Is.EqualTo(1));
            Assert.That(shot.JigwiRewardFireTrail.GetPathPosition(0), Is.EqualTo(position), "A newly accepted layer cannot bridge to a discarded history.");
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(shot.JigwiRewardFireTrail.PathCount, Is.GreaterThanOrEqualTo(2));
            shot.gameObject.SetActive(false);
            Assert.That(shot.JigwiABRewardActive, Is.False);
            Assert.That(shot.JigwiRewardFireTrail.IsEmitting, Is.False);
            Assert.That(shot.JigwiRewardFireTrail.PathCount, Is.Zero);
            Vector3 reusedPosition = shot.transform.position + Vector3.right * 20f;
            shot.transform.position = reusedPosition;
            shot.gameObject.SetActive(true);
            shot.SetJigwiABReward(true);
            Assert.That(shot.JigwiRewardFireTrail.IsEmitting, Is.True,
                "A cleared existing child must begin emitting again when the projectile is re-enabled and rewarded.");
            Assert.That(shot.JigwiRewardFireTrail.PathCount, Is.EqualTo(1));
            Assert.That(shot.JigwiRewardFireTrail.GetPathPosition(0), Is.EqualTo(reusedPosition),
                "Reactivation starts at the current source, without connecting any pre-disable residue.");
        }

        [UnityTest]
        public IEnumerator ActualVisitedCornersPauseAndReuseKeepOneIndependentWorldHistory()
        {
            Transform source = Child("Reward source").transform;
            source.position = Arena;
            var trail = Child("Reward art").AddComponent<LabJigwiRewardFireTrail>();
            trail.Begin(source, config.ThrowRadius, profile);
            Vector3 corner = Arena + Vector3.right * .5f;
            source.position = corner;
            yield return Until(() => trail.PathCount >= 2, "The first real movement must be recorded.");
            Vector3 end = corner + Vector3.up * .5f;
            source.position = end;
            yield return Until(() => trail.PathCount >= 3, "The next actual corner must be retained.");
            int count = trail.PathCount;
            Assert.That(trail.GetPathPosition(1), Is.EqualTo(corner));
            Assert.That(trail.GetPathPosition(count - 1), Is.EqualTo(end));
            Vector3 first = trail.GetPathPosition(0);
            trail.Begin(source, config.ThrowRadius, profile);
            trail.Reconfigure(config.ThrowRadius * 1.3f);
            Assert.That(trail.PathCount, Is.EqualTo(count));
            Assert.That(trail.GetPathPosition(0), Is.EqualTo(first));
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(trail.PathCount, Is.EqualTo(count));
            Assert.That(trail.GetPathPosition(0), Is.EqualTo(first));
            Time.timeScale = 1f;
            trail.Clear();
            Assert.That(trail.PathCount, Is.Zero);
            Assert.That(trail.IsEmitting, Is.False);
            Assert.That(Array.Exists(trail.GetComponentsInChildren<MeshFilter>(true),
                filter => filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0), Is.False);
            Transform next = Child("Reused far-away source").transform;
            next.position = Arena + Vector3.right * 20f;
            trail.Begin(next, config.ThrowRadius, profile);
            Assert.That(trail.PathCount, Is.EqualTo(1));
            Assert.That(trail.GetPathPosition(0), Is.EqualTo(next.position));
            Assert.That(trail.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(trail.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [UnityTest]
        public IEnumerator DetachedHistoryStopsSamplingAndFadesOnlyWithTheScaledClock()
        {
            Transform source = Child("Detaching source").transform;
            source.position = Arena;
            var trail = Child("Detaching reward art").AddComponent<LabJigwiRewardFireTrail>();
            trail.Begin(source, config.ThrowRadius, profile);
            source.position += Vector3.right;
            yield return Until(() => trail.PathCount >= 2, "Record a real segment before detaching.");
            trail.StopAndFade(root.transform);
            Assert.That(trail.IsEmitting, Is.False);
            Assert.That(trail.transform.parent, Is.EqualTo(root.transform));
            int count = trail.PathCount;
            Vector3 end = trail.GetPathPosition(count - 1);
            source.position += Vector3.right * 20f;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(Mathf.Max(profile.Lifetime, profile.EmberLifetime) + .08f);
            Assert.That(trail, Is.Not.Null, "Pause cannot consume the detached residue's scaled lifetime.");
            Assert.That(trail.PathCount, Is.EqualTo(count));
            Assert.That(trail.GetPathPosition(count - 1), Is.EqualTo(end));
            Time.timeScale = 1f;
            yield return Until(() => trail == null, "The detached layer must expire naturally without a Scene change.",
                Mathf.Max(profile.Lifetime, profile.EmberLifetime) + .5f);
        }

        private LabProjectile Shot(LabOrbElement element, float x, bool reward)
        {
            var shot = LabProjectile.Create(Arena + Vector3.right * x, config, root.transform, element);
            shot.SetRadiusScale(1.3f); // Same captured physical size in both ordinary/reward comparison fixtures.
            shot.SetJigwiABReward(reward);
            return shot;
        }
        private GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child;
        }
        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static IEnumerator Until(Func<bool> condition, string message, float seconds = 1f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
