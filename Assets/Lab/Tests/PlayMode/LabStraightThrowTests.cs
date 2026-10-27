using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>Saved tuning, real board input and physical straight-flight regression coverage.</summary>
    public sealed class LabStraightThrowTests
    {
        private LabNetwork startedNetwork;
        [UnityTearDown]
        public IEnumerator UnloadSavedScene()
        {
            // NGO can move its GameObject into DontDestroyOnLoad after starting Solo.
            // Dispose only the instance owned by this test, even if Lab has already unloaded.
            if (startedNetwork != null)
            {
                if (startedNetwork.IsConnected) startedNetwork.Stop();
                Object.Destroy(startedNetwork.gameObject);
                startedNetwork = null;
                yield return null;
            }
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            Scene cleanup = SceneManager.CreateScene("Straight throw test cleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator SavedSpeedStraightBoardSwipeApprovesOneRealCollisionAndOnePaintHit()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            Scene loadedScene = SceneManager.GetActiveScene();
            LabGourdPaint paint = Resources.FindObjectsOfTypeAll<LabGourdPaint>()
                .Single(candidate => candidate.gameObject.scene == loadedScene);
            LabNetwork network = paint.network;
            startedNetwork = network;
            LabSceneController controller = Resources.FindObjectsOfTypeAll<LabSceneController>()
                .Single(candidate => candidate.gameObject.scene == loadedScene);
            LabSeatCamera seatCamera = Resources.FindObjectsOfTypeAll<LabSeatCamera>()
                .Single(candidate => candidate.gameObject.scene == loadedScene && candidate.network == network);
            LabConfig source = network.config;
            LabConfig savedBoard = controller.config;
            LabConfig savedCamera = seatCamera.config;
            float[] sourceValues = source.CaptureDeveloperValues();
            LabConfig fixture = CollisionFixture(source);
            network.config = fixture;
            controller.config = fixture;
            seatCamera.config = fixture;
            controller.ReconfigureBoard();
            seatCamera.RefreshPose();
            network.port = 17890;
            int approvedEvents = 0;
            Vector3 approvedPoint = Vector3.zero;
            System.Action<Vector3, LabOrbElement> onApproved = (point, element) =>
            {
                approvedEvents++;
                approvedPoint = point;
                Assert.That(element, Is.Not.EqualTo(LabOrbElement.None));
            };
            network.target.ApprovedHit += onApproved;
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                yield return null;
                Assert.That(fixture.ThrowMaxWorldSpeed, Is.EqualTo(12.5f),
                    "This fixture must exercise the saved speed cap, rather than increasing shot speed to obtain a hit.");
                Assert.That(fixture.ThrowMaxWorldSpeed, Is.EqualTo(source.ThrowMaxWorldSpeed));
                Assert.That(fixture.ThrowUpGain, Is.EqualTo(source.ThrowUpGain));
                Assert.That(fixture.ThrowForwardGain, Is.EqualTo(source.ThrowForwardGain));
                Assert.That(fixture.ThrowPowerExponent, Is.EqualTo(source.ThrowPowerExponent));
                Vector3 cameraPosition = seatCamera.battleCamera.transform.position;
                Quaternion cameraRotation = seatCamera.battleCamera.transform.rotation;
                Rect cameraRect = seatCamera.battleCamera.rect;
                float cameraFov = seatCamera.battleCamera.fieldOfView;
                network.Generate();
                LabOrbState box = network.Snapshot.orbs.Single();
                Assert.That(box.kind, Is.EqualTo(LabOrbKind.SecretBox));
                TapOrb(network.board, box.id, 10d);
                TapOrb(network.board, box.id, 10.2d);
                LabOrbState shot = network.Snapshot.orbs.First(orb => orb.kind == LabOrbKind.Combined);
                network.target.GetComponent<LabHangingTarget>().ResetSwing();
                LaunchThreeBoardWidthsPerSecond(network.board, shot.id, 20d);
                Assert.That(network.Snapshot.orbs.Single(orb => orb.id == shot.id).inFlight, Is.True);
                LabProjectile projectile = network.projectileRoot.GetComponentsInChildren<LabProjectile>()
                    .Single(candidate => candidate.ProjectileId == shot.id);
                SphereCollider sphere = projectile.GetComponent<SphereCollider>();
                Rigidbody body = projectile.GetComponent<Rigidbody>();
                Assert.That(sphere, Is.Not.Null);
                Assert.That(sphere.isTrigger, Is.False, "The Host owns the physical projectile, not a visual-only replica.");
                Assert.That(sphere.center, Is.EqualTo(Vector3.zero));
                Assert.That(projectile.VisualMount.position, Is.EqualTo(body.position));
                Assert.That(projectile.VisualMount.GetComponentsInChildren<Collider>(true), Is.Empty,
                    "The visible sphere follows the root collider; no second collider is introduced by this form.");
                Renderer visible = projectile.VisualMount.GetComponentsInChildren<Renderer>()
                    .First(renderer => renderer.enabled && !(renderer is TrailRenderer));
                Assert.That(Vector3.Distance(visible.bounds.center, sphere.bounds.center), Is.LessThan(.001f));
                Assert.That(body.linearVelocity.magnitude, Is.LessThanOrEqualTo(source.ThrowMaxWorldSpeed + .001f));
                Assert.That(projectile.GravityAcceleration, Is.EqualTo(fixture.EffectiveThrowGravity));
                Vector3 startViewport = seatCamera.battleCamera.WorldToViewportPoint(body.position);
                Assert.That(startViewport.z, Is.GreaterThan(0f));
                Assert.That(startViewport.x, Is.InRange(0f, 1f));
                Assert.That(startViewport.y, Is.InRange(0f, .5f), "The actual sphere starts inside the lower part of the unchanged battle camera.");
                foreach (Vector3 edge in new[] { seatCamera.battleCamera.transform.up,
                    -seatCamera.battleCamera.transform.up, seatCamera.battleCamera.transform.right,
                    -seatCamera.battleCamera.transform.right })
                {
                    Vector3 projected = seatCamera.battleCamera.WorldToViewportPoint(body.position + edge * sphere.radius);
                    Assert.That(projected.x, Is.InRange(0f, 1f), "Default Straight spawn must display the sphere edges.");
                    Assert.That(projected.y, Is.InRange(0f, 1f), "Default Straight spawn must display the sphere edges.");
                }
                int previousHp = network.Snapshot.hp;
                for (int step = 0; step < 150 && approvedEvents == 0; step++)
                    yield return new WaitForFixedUpdate();
                Assert.That(approvedEvents, Is.EqualTo(1), network.LastShotReport);
                Assert.That(network.LastShotReport, Does.StartWith("SHOT HIT"));
                Assert.That(network.Snapshot.hp, Is.EqualTo(previousHp - fixture.Damage));
                Assert.That(network.Snapshot.orbs.Any(orb => orb.id == shot.id), Is.False);
                Assert.That(network.PaintSurface.PaintCount, Is.EqualTo(1));
                Assert.That(network.PaintSurface.CoveredCount, Is.GreaterThan(0));
                Assert.That(float.IsNaN(approvedPoint.x) || float.IsNaN(approvedPoint.y) || float.IsNaN(approvedPoint.z), Is.False);
                var pixels = new Color32[network.PaintSurface.CellCount];
                network.PaintSurface.CopyPixels(pixels);
                Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(pixels));
                for (int step = 0; step < 5; step++) yield return new WaitForFixedUpdate();
                Assert.That(approvedEvents, Is.EqualTo(1), "One physical shot cannot award a second hit or paint mark.");
                Assert.That(network.PaintSurface.PaintCount, Is.EqualTo(1));
                Assert.That(seatCamera.battleCamera.transform.position, Is.EqualTo(cameraPosition));
                Assert.That(Quaternion.Angle(seatCamera.battleCamera.transform.rotation, cameraRotation), Is.LessThan(.001f));
                Assert.That(seatCamera.battleCamera.rect, Is.EqualTo(cameraRect));
                Assert.That(seatCamera.battleCamera.fieldOfView, Is.EqualTo(cameraFov));
                Assert.That(source.CaptureDeveloperValues(), Is.EqualTo(sourceValues), "The source asset's tuning is never changed by this runtime fixture.");
            }
            finally
            {
                network.target.ApprovedHit -= onApproved;
                if (network.IsConnected) network.Stop();
                network.config = source;
                controller.config = savedBoard;
                seatCamera.config = savedCamera;
                controller.ReconfigureBoard();
                seatCamera.RefreshPose();
                Object.Destroy(fixture);
            }
        }

        [UnityTest]
        public IEnumerator SameSavedPowerMakesTheStraightProjectileRiseLessThanTheArc()
        {
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            Assert.That(source, Is.Not.Null);
            LabConfig arc = Object.Instantiate(source);
            LabConfig straight = Object.Instantiate(source);
            SetForm(arc, 0f);
            SetForm(straight, 1f);
            Vector3 center = Vector3.zero;
            Vector3 anchor = LabThrowMath.SeatPosition(0, 1, center,
                arc.ThrowOriginRadius, arc.ThrowOriginHeightOffset);
            Vector2 gesture = new Vector2(0f, 3f);
            Vector3 arcVelocity = LabThrowMath.LaunchVelocity(gesture, anchor, center, arc);
            Vector3 straightVelocity = LabThrowMath.LaunchVelocity(gesture, anchor, center, straight);
            Vector3 origin = new Vector3(1000f, 1000f, 1000f);
            LabProjectile arcProjectile = LabProjectile.Create(origin, arc);
            LabProjectile straightProjectile = LabProjectile.Create(origin + Vector3.right * 2f, straight);
            try
            {
                Assert.That(arcVelocity.magnitude, Is.EqualTo(straightVelocity.magnitude).Within(.0001f),
                    "Straight changes direction and gravity; the existing swipe-to-speed calculation remains shared.");
                Assert.That(straightVelocity.y, Is.GreaterThan(0f));
                Assert.That(straightVelocity.y, Is.LessThan(arcVelocity.y));
                Assert.That(arcProjectile.LaunchVisual(arcVelocity, arc), Is.True);
                Assert.That(straightProjectile.LaunchVisual(straightVelocity, straight), Is.True);
                float arcPeak = origin.y;
                float straightPeak = origin.y;
                for (int step = 0; step < 40; step++)
                {
                    yield return new WaitForFixedUpdate();
                    arcPeak = Mathf.Max(arcPeak, arcProjectile.transform.position.y);
                    straightPeak = Mathf.Max(straightPeak, straightProjectile.transform.position.y);
                }
                Assert.That(arcPeak - origin.y, Is.GreaterThan(.2f));
                Assert.That(straightPeak - origin.y, Is.GreaterThan(0f));
                Assert.That(straightPeak - origin.y, Is.LessThan(arcPeak - origin.y),
                    "Actual Rigidbody samples must show the lower flight, not only a different calculated initial vector.");
            }
            finally
            {
                if (arcProjectile != null) Object.Destroy(arcProjectile.gameObject);
                if (straightProjectile != null) Object.Destroy(straightProjectile.gameObject);
                Object.Destroy(arc);
                Object.Destroy(straight);
            }
        }

        [UnityTest]
        public IEnumerator ALaunchedStraightProjectileKeepsItsGravityAndLifetimeWhenTheConfigChanges()
        {
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            LabConfig changed = Object.Instantiate(source);
            LabConfig unchanged = Object.Instantiate(source);
            SetForm(changed, 1f);
            SetForm(unchanged, 1f);
            Vector3 start = new Vector3(1000f, 1000f, 1000f);
            LabProjectile active = LabProjectile.Create(start, changed);
            LabProjectile reference = LabProjectile.Create(start + Vector3.right * 2f, unchanged);
            try
            {
                Vector3 initialVelocity = new Vector3(0f, 2f, 6f);
                Assert.That(active.LaunchVisual(initialVelocity, changed), Is.True);
                Assert.That(reference.LaunchVisual(initialVelocity, unchanged), Is.True);
                float capturedGravity = active.GravityAcceleration;
                float capturedLifetime = active.LifetimeSeconds;
                for (int step = 0; step < 8; step++) yield return new WaitForFixedUpdate();
                float[] replacement = changed.CaptureDeveloperValues();
                replacement[111] = 0f;
                replacement[117] = 1f;
                replacement[33] = changed.ThrowGravity * 2f;
                replacement[34] = .02f;
                Assert.That(changed.TryApplyDeveloperValues(replacement, out string error), Is.True, error);
                Assert.That(changed.EffectiveThrowGravity, Is.Not.EqualTo(capturedGravity));
                for (int step = 0; step < 12; step++) yield return new WaitForFixedUpdate();
                yield return null;
                Assert.That(active != null, Is.True,
                    "An in-flight projectile keeps the captured lifetime, even when the config now has an expired shorter lifetime.");
                Assert.That(active.IsResolved, Is.False);
                Assert.That(active.GravityAcceleration, Is.EqualTo(capturedGravity));
                Assert.That(active.LifetimeSeconds, Is.EqualTo(capturedLifetime));
                Assert.That(active.GetComponent<Rigidbody>().linearVelocity,
                    Is.EqualTo(reference.GetComponent<Rigidbody>().linearVelocity));
                Assert.That(active.transform.position.y, Is.EqualTo(reference.transform.position.y).Within(.001f));
                Assert.That(active.transform.position.z, Is.EqualTo(reference.transform.position.z).Within(.001f));
            }
            finally
            {
                if (active != null) Object.Destroy(active.gameObject);
                if (reference != null) Object.Destroy(reference.gameObject);
                Object.Destroy(changed);
                Object.Destroy(unchanged);
            }
        }

        private static void SetForm(LabConfig config, float value)
        {
            float[] values = config.CaptureDeveloperValues();
            values[111] = value;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
        }

        private static LabConfig CollisionFixture(LabConfig source)
        {
            LabConfig fixture = Object.Instantiate(source);
            float[] tuning = fixture.CaptureDeveloperValues();
            tuning[111] = 1f;
            SetTuning(tuning, "Battle Duration (seconds)", 30f);
            SetTuning(tuning, "Orb Generation Cost", 1f);
            SetTuning(tuning, "Item Lifetime (seconds)", 30f);
            SetTuning(tuning, "Item Unlock Swipes Min", 0f);
            SetTuning(tuning, "Item Unlock Swipes Max", 0f);
            SetTuning(tuning, "Yin Chance (%)", 0f);
            SetTuning(tuning, "Yang Chance (%)", 0f);
            SetTuning(tuning, "Relay Orb Chance (%)", 0f);
            SetTuning(tuning, "Secret Box Chance (%)", 100f);
            SetTuning(tuning, "Potion Chance (%)", 0f);
            SetTuning(tuning, "Bomb Chance (%)", 0f);
            SetTuning(tuning, "Spawn Pop Duration (seconds)", 0f);
            SetTuning(tuning, "Spawn Minimum Speed (board widths/s)", 0f);
            SetTuning(tuning, "Spawn Maximum Speed (board widths/s)", 0f);
            SetTuning(tuning, "Drum Drift Speed (board widths/s)", 0f);
            SetTuning(tuning, "Common Gimmick Interval (seconds, 0 = none)", 0f);
            tuning[104] = 0f;
            Assert.That(fixture.TryApplyDeveloperValues(tuning, out string error), Is.True, error);
            return fixture;
        }

        private static void TapOrb(LabOrbBoard board, string id, double now)
        {
            Vector3 position = board.GetComponentsInChildren<LabOrbView>().Single(view => view.OrbId == id).Body.position;
            Vector2 point = new Vector2(position.x, position.y);
            Assert.That(board.TryBeginDragAtWorld(point, now), Is.True);
            Assert.That(board.EndDragAtWorld(point, now + .05d), Is.True);
        }

        private static void LaunchThreeBoardWidthsPerSecond(LabOrbBoard board, string id, double now)
        {
            LabOrbView view = board.GetComponentsInChildren<LabOrbView>().Single(candidate => candidate.OrbId == id);
            Vector3 position = view.Body.position;
            Vector2 from = new Vector2(position.x, position.y);
            Vector2 aim = board.AreaShape.ToWorld(new Vector2(.5f, .25f), view.HitSphere.radius);
            Assert.That(board.TryBeginDragAtWorld(from, now), Is.True);
            Assert.That(board.DragToWorld(aim, now + 1d), Is.True);
            Assert.That(board.DragToWorld(aim, now + 1.12d), Is.True);
            Assert.That(board.EndDragAtWorld(aim + Vector2.up * (board.CenterBounds.width * .36f), now + 1.24d), Is.True);
        }

        private static void SetTuning(float[] values, string label, float value)
        {
            int index = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
                .Single(candidate => LabConfig.DeveloperFields[candidate].Label == label);
            values[index] = value;
        }
    }
}
