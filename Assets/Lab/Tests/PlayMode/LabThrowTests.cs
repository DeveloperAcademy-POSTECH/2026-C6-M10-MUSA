using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabThrowTests
    {
        [Test]
        public void ThreeSeatsAreEquidistantAndThrowsFaceTheTarget()
        {
            LabConfig config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Vector3 center = new Vector3(2f, 0f, -1f);
                for (int seat = 0; seat < 3; seat++)
                {
                    Vector3 origin = LabThrowMath.SeatPosition(seat, 3, center, 4f, 1.1f);
                    Vector3 horizontal = Vector3.ProjectOnPlane(origin - center, Vector3.up);
                    Assert.That(horizontal.magnitude, Is.EqualTo(4f).Within(0.0001f));
                    Vector3 velocity = LabThrowMath.LaunchVelocity(
                        new Vector2(0f, 1.5f), origin, center + Vector3.up, config);
                    Assert.Greater(Vector3.Dot(velocity, -horizontal.normalized), 0f);
                    Assert.GreaterOrEqual(velocity.y, config.ThrowMinUpSpeed);
                }
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void CameraAndThrowUseTheSameThreeSeatDirections()
        {
            LabConfig config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Vector3 center = new Vector3(0f, 1.35f, 10f);
                for (int seat = 0; seat < 3; seat++)
                {
                    Vector3 cameraPosition = LabThrowMath.SeatPosition(
                        seat, 3, center, config.CameraRadius, config.CameraHeightOffset);
                    Vector3 anchor = LabThrowMath.SeatPosition(
                        seat, 3, center, config.ThrowOriginRadius, config.ThrowOriginHeightOffset);
                    Vector3 cameraRight = Quaternion.LookRotation(center - cameraPosition) * Vector3.right;
                    Vector3 central = LabThrowMath.LaunchPosition(seat, 3, center, .5f, config);
                    Vector3 side = LabThrowMath.LaunchPosition(seat, 3, center, 1f, config);
                    Vector3 straight = LabThrowMath.LaunchVelocity(new Vector2(0f, 1.5f), anchor, center, config);
                    Vector3 rightFlick = LabThrowMath.LaunchVelocity(new Vector2(1f, 1.5f), anchor, center, config);
                    Assert.That(Vector3.Dot(side - central, cameraRight), Is.GreaterThan(0f));
                    Assert.That(Vector3.Dot(rightFlick - straight, cameraRight), Is.GreaterThan(0f));
                    Assert.That(Vector3.Distance(cameraPosition, center), Is.GreaterThan(
                        Vector3.Distance(anchor, center)));
                }
                Vector3 p0 = LabThrowMath.SeatPosition(0, 3, center, config.CameraRadius, 0f);
                Vector3 p1 = LabThrowMath.SeatPosition(1, 3, center, config.CameraRadius, 0f);
                Vector3 p2 = LabThrowMath.SeatPosition(2, 3, center, config.CameraRadius, 0f);
                Assert.That(Vector3.Angle(p0 - center, p1 - center), Is.EqualTo(120f).Within(.01f));
                Assert.That(Vector3.Angle(p1 - center, p2 - center), Is.EqualTo(120f).Within(.01f));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void PowerAndLoftControlsChangeTheStraightLaunchIndependently()
        {
            LabConfig config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Vector3 center = new Vector3(0f, 1.35f, 0f);
                Vector3 anchor = LabThrowMath.SeatPosition(0, 3, center, 4f, -.5f);
                Vector3 baseline = LabThrowMath.LaunchVelocity(new Vector2(0f, 2.5f), anchor, center, config);
                float[] values = config.CaptureDeveloperValues();
                values[36] = 1.7f;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                Vector3 stronger = LabThrowMath.LaunchVelocity(new Vector2(0f, 2.5f), anchor, center, config);
                Assert.That(stronger.magnitude, Is.GreaterThan(baseline.magnitude));
                values[36] = 1f;
                values[37] = 15f;
                Assert.That(config.TryApplyDeveloperValues(values, out error), Is.True, error);
                Vector3 higher = LabThrowMath.LaunchVelocity(new Vector2(0f, 2.5f), anchor, center, config);
                Assert.That(higher.y, Is.GreaterThan(baseline.y));
                Assert.That(Vector3.Dot(higher, Vector3.back), Is.LessThan(Vector3.Dot(baseline, Vector3.back)));
                Assert.That(higher.magnitude, Is.EqualTo(baseline.magnitude).Within(.001f));
                Assert.That(higher.x, Is.EqualTo(0f).Within(.001f));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void ThrowGestureNeedsAnIntentionalUpwardFlick()
        {
            LabConfig config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(LabThrowMath.IsThrowGesture(new Vector2(0f, 1.5f), .2f, config), Is.True);
                Assert.That(LabThrowMath.IsThrowGesture(new Vector2(0f, .3f), .2f, config), Is.False);
                Assert.That(LabThrowMath.IsThrowGesture(new Vector2(2f, .9f), .2f, config), Is.False);
                Assert.That(LabThrowMath.IsThrowGesture(new Vector2(0f, 1.5f), .01f, config), Is.False);
                Assert.That(LabThrowMath.IsThrowGesture(new Vector2(float.NaN, 1f), .2f, config), Is.False);
            }
            finally { Object.DestroyImmediate(config); }
        }

        [UnityTest]
        public IEnumerator ARealFlickCanHitFromEachSeatAndAimedSidewaysCanMiss()
        {
            LabConfig config = ScriptableObject.CreateInstance<LabConfig>();
            Vector3 center = new Vector3(0f, 1.35f, 0f);
            LabTarget target = LabTarget.CreateCylinder(null, center, .6f, 2.7f);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.position = new Vector3(0f, -.1f, 0f);
            floor.transform.localScale = Vector3.one * 2f;
            Collider floorCollider = floor.GetComponent<Collider>();
            try
            {
                for (int seat = 0; seat < 3; seat++)
                {
                    Vector3 anchor = LabThrowMath.SeatPosition(seat, 3, center,
                        config.ThrowOriginRadius, config.ThrowOriginHeightOffset);
                    Vector3 position = LabThrowMath.LaunchPosition(seat, 3, center, .5f, config);
                    Vector3 velocity = LabThrowMath.LaunchVelocity(new Vector2(0f, 1.5f),
                        anchor, center, config);
                    LabProjectile projectile = LabProjectile.Create(position, config);
                    projectile.SetMissSurface(floorCollider);
                    int reports = 0;
                    bool hit = false;
                    Assert.That(projectile.Launch("seat-" + seat, (ulong)seat, velocity,
                        config, target, result => { reports++; hit = result.Hit; }), Is.True);
                    for (int step = 0; step < 120 && reports == 0; step++)
                        yield return new WaitForFixedUpdate();
                    Assert.That(reports, Is.EqualTo(1), "Each seat needs a reachable real target hit.");
                    Assert.That(hit, Is.True, "A centred upward flick should hit the actual cylinder.");
                    yield return null;
                }

                Vector3 sideAnchor = LabThrowMath.SeatPosition(0, 3, center,
                    config.ThrowOriginRadius, config.ThrowOriginHeightOffset);
                LabProjectile miss = LabProjectile.Create(
                    LabThrowMath.LaunchPosition(0, 3, center, 1f, config), config);
                miss.SetMissSurface(floorCollider);
                int missReports = 0;
                bool unexpectedHit = true;
                Assert.That(miss.Launch("side-miss", 0UL,
                    LabThrowMath.LaunchVelocity(new Vector2(1f, 1.5f), sideAnchor, center, config),
                    config, target, result => { missReports++; unexpectedHit = result.Hit; }), Is.True);
                for (int step = 0; step < 120 && missReports == 0; step++)
                    yield return new WaitForFixedUpdate();
                Assert.That(missReports, Is.EqualTo(1));
                Assert.That(unexpectedHit, Is.False, "A sideways shot must not receive target damage.");
            }
            finally
            {
                Object.Destroy(target.gameObject);
                Object.Destroy(floor);
                Object.Destroy(config);
            }
        }

        [UnityTest]
        public IEnumerator AirDampingReducesTravelForTheSameStraightThrow()
        {
            LabConfig noDrag = ScriptableObject.CreateInstance<LabConfig>();
            LabConfig withDrag = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = withDrag.CaptureDeveloperValues();
            values[38] = 1.5f;
            Assert.That(withDrag.TryApplyDeveloperValues(values, out string error), Is.True, error);
            LabProjectile free = LabProjectile.Create(new Vector3(-2f, 1f, 3f), noDrag);
            LabProjectile resisted = LabProjectile.Create(new Vector3(2f, 1f, 3f), withDrag);
            try
            {
                Assert.That(free.LaunchVisual(new Vector3(0f, 5f, -9f), noDrag), Is.True);
                Assert.That(resisted.LaunchVisual(new Vector3(0f, 5f, -9f), withDrag), Is.True);
                for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
                Assert.That(resisted.transform.position.z, Is.GreaterThan(free.transform.position.z + .2f));
            }
            finally
            {
                Object.Destroy(free.gameObject);
                Object.Destroy(resisted.gameObject);
                Object.Destroy(noDrag);
                Object.Destroy(withDrag);
            }
        }

        [Test]
        public void TargetVisibleCylinderMeshIsItsPhysicalHitbox()
        {
            LabTarget target = LabTarget.CreateCylinder(null, new Vector3(0f, 1f, 0f), .7f, 2f);
            try
            {
                MeshFilter filter = target.Hitbox.GetComponent<MeshFilter>();
                MeshRenderer renderer = target.Hitbox.GetComponent<MeshRenderer>();
                CapsuleCollider extra = target.Hitbox.GetComponent<CapsuleCollider>();
                Assert.IsNotNull(filter);
                Assert.IsNotNull(renderer);
                Assert.IsTrue(renderer.enabled);
                Assert.AreSame(filter.sharedMesh, target.Hitbox.sharedMesh);
                Assert.IsFalse(target.Hitbox.convex);
                Assert.IsTrue(target.Hitbox.enabled);
                Assert.IsTrue(extra == null || !extra.enabled);
            }
            finally { Object.DestroyImmediate(target.gameObject); }
        }

        [UnityTest]
        public IEnumerator HostProjectileReportsOnePhysicalHit()
        {
            LabConfig config = ScriptableObject.CreateInstance<LabConfig>();
            LabTarget target = LabTarget.CreateCylinder(null, new Vector3(0f, 1f, 0f), .8f, 2f);
            LabProjectile projectile = LabProjectile.Create(new Vector3(0f, 1f, 3f), config);
            int reports = 0;
            bool hit = false;
            Assert.IsTrue(projectile.Launch("shot-1", 42UL, new Vector3(0f, 0f, -9f),
                config, target, result =>
                {
                    reports++;
                    hit = result.Hit;
                    Assert.AreEqual("shot-1", result.ProjectileId);
                    Assert.AreEqual(42UL, result.OwnerPlayerId);
                }));
            Assert.IsFalse(projectile.Launch("shot-1", 42UL, Vector3.forward,
                config, target, _ => reports++));

            yield return new WaitForSeconds(1f);
            Assert.AreEqual(1, reports);
            Assert.IsTrue(hit);
            Object.Destroy(target.gameObject);
            Object.Destroy(config);
        }

        [UnityTest]
        public IEnumerator ClientVisualCannotReportAuthoritativeDamage()
        {
            LabConfig config = ScriptableObject.CreateInstance<LabConfig>();
            LabTarget target = LabTarget.CreateCylinder(null, new Vector3(0f, 1f, 0f), .8f, 2f);
            LabProjectile projectile = LabProjectile.Create(new Vector3(0f, 1f, 3f), config);
            Assert.IsTrue(projectile.LaunchVisual(new Vector3(0f, 0f, -9f), config, target));
            yield return new WaitForSeconds(.5f);
            Assert.IsTrue(projectile == null || !projectile.IsResolved);
            Object.Destroy(target.gameObject);
            Object.Destroy(config);
            if (projectile != null) Object.Destroy(projectile.gameObject);
        }
    }
}
