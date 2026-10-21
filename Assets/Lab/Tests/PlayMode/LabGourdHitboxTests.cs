using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>Saved gameplay gourd geometry, not a diagnostic primitive, owns projectile contact.</summary>
    public sealed class LabGourdHitboxTests
    {
        private readonly LabSavedSceneNetworkFixture savedScene = new LabSavedSceneNetworkFixture();
        private LabGourdPaint paint;
        private LabTarget target;
        private LabNetwork network;
        private LabConfig tuning;
        private GameObject shots;

        private sealed class Receipt
        {
            public int Count;
            public LabProjectileResult Result;
            public void Receive(LabProjectileResult result) { Count++; Result = result; }
        }

        [UnitySetUp]
        public IEnumerator LoadActualGourd()
        {
            yield return savedScene.Load();
            paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint, Is.Not.Null);
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            target = paint.target;
            network = paint.network;
            tuning = Object.Instantiate(network.config);
            tuning.name = "Gourd hitbox test tuning (runtime only)";
            SetTuning("Gourd Visual Size (x)", 1.4f);
            SetTuning("Gourd Rope Cycle Amplitude (world units)", 0f);
            SetTuning("Throw Maximum World Speed", 1000f);
            SetTuning("Throw Gravity", .001f);
            SetTuning("Throw Lifetime (seconds)", 1f);
            SetTuning("Throw Air Damping", 0f);
            network.config = tuning;
            LabHangingTarget hanging = target.GetComponent<LabHangingTarget>();
            hanging.ConfigureRopeMotion(0f, 3f, 0f, 0f, 0f, 0f);
            hanging.ResetSwing();
            hanging.enabled = false; // Freeze only this fixture's suspension, retaining its real hierarchy.
            shots = new GameObject("Saved gourd contact shots");
            yield return null; // The actual paint Update applies the DEV size to the visible shell and hitbox.
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (shots != null) Object.Destroy(shots);
            yield return savedScene.Cleanup();
            if (tuning != null) Object.Destroy(tuning);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedSceneHasNoCylinderAndUsesOnlyTheVisiblePaintShellForContact()
        {
            Assert.That(target.GetComponentsInChildren<Transform>(true)
                .Any(item => item.name == "Hitbox Cylinder"), Is.False,
                "The main gameplay Scene must not retain the obsolete hidden cylinder.");
            AssertPaintMeshAndWorldPose();
            Assert.That(target.GetComponentsInChildren<Collider>(true)
                .Where(item => item.enabled && !item.isTrigger).ToArray(), Is.EqualTo(new Collider[] { target.Hitbox }),
                "Rope, decorations and the visible prefab cannot silently contribute extra hitboxes.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeveloperSizeChangesKeepActualMeshBoundsAndSilhouetteTogether()
        {
            MeshCollider original = target.Hitbox;
            foreach (float size in new[] { .5f, 1.4f, 3f, .5f })
            {
                SetTuning("Gourd Visual Size (x)", size);
                yield return null;
                Physics.SyncTransforms();
                Assert.That(target.Hitbox, Is.SameAs(original), "DEV resizing must retain the registered target collider identity.");
                AssertPaintMeshAndWorldPose();
                Bounds visible = paint.Shell.GetComponent<Renderer>().bounds;
                Assert.That(Vector3.Distance(target.Hitbox.bounds.center, visible.center), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(target.Hitbox.bounds.size, visible.size), Is.LessThan(.001f));
                Vector3 front = FrontPoint(visible.center.x, visible.center.y);
                Assert.That(front.z, Is.GreaterThan(visible.center.z));
                Assert.That(target.Hitbox.Raycast(new Ray(new Vector3(visible.max.x + .05f,
                    visible.center.y, visible.max.z + 1f), Vector3.back), out _, visible.size.z + 2f), Is.False,
                    "The collider cannot remain larger than the displayed gourd after shrinking.");
            }
        }

        [UnityTest]
        public IEnumerator ShotsHitBothWideSidesAndTheLowerShellOutsideTheFormerCylinder()
        {
            AssertPaintMeshAndWorldPose();
            Bounds bounds = target.Hitbox.bounds;
            Vector3 origin = target.transform.position;
            Vector3[] contacts =
            {
                FrontPoint(origin.x - .9f, bounds.center.y),
                FrontPoint(origin.x + .9f, bounds.center.y),
                FrontPoint(origin.x, origin.y - 1.2f)
            };
            foreach (Vector3 contact in contacts)
            {
                Assert.That(Mathf.Abs(contact.x - origin.x) > .8f || contact.y < origin.y - .9f, Is.True,
                    "This case must exercise visible gourd surface outside the former cylinder.");
                var receipt = new Receipt();
                Launch(contact + Vector3.forward * 2f, Vector3.back * 20f, receipt);
                yield return Await(receipt);
                Assert.That(receipt.Result.Hit, Is.True,
                    "A shot through the visible side/lower shell must not pass through empty hidden-cylinder space.");
                Assert.That(Vector3.Distance(receipt.Result.HitPoint, contact), Is.LessThan(.3f));
                yield return AssertExactlyOnce(receipt);
            }
        }

        [UnityTest]
        public IEnumerator FastShrunkShotsHitTheRealShellAndMissOutsideItsActualRadius()
        {
            AssertPaintMeshAndWorldPose();
            Bounds bounds = target.Hitbox.bounds;
            Vector3 contact = FrontPoint(bounds.center.x, bounds.center.y);
            Collider backstop = Backstop(bounds);
            var hit = new Receipt();
            var miss = new Receipt();
            LabProjectile actual = Launch(contact + Vector3.forward * 3f, Vector3.back * 240f, hit, .5f, .3f, backstop);
            float radius = tuning.ThrowRadius * .5f * .3f;
            Assert.That(actual.GetComponent<SphereCollider>().radius, Is.EqualTo(radius).Within(.000001f));
            Launch(new Vector3(bounds.max.x + radius + .06f, bounds.center.y, bounds.max.z + 3f),
                Vector3.back * 240f, miss, .5f, .3f, backstop);
            yield return Await(hit, miss);
            Assert.That(hit.Result.Hit, Is.True, "A swept small sphere must hit this saved gourd even when it crosses the whole shell in one fixed step.");
            Assert.That(miss.Result.Hit, Is.False, "A speculative CCD contact cannot enlarge the actual gourd or shrunk sphere.");
            yield return AssertExactlyOnce(hit, miss);
        }

        [UnityTest]
        public IEnumerator SharedSwingAndYawMoveTheGourdAndColliderTogetherWithoutRecooking()
        {
            AssertPaintMeshAndWorldPose();
            LabHangingTarget hanging = target.GetComponent<LabHangingTarget>();
            Mesh originalMesh = target.Hitbox.sharedMesh;
            hanging.ConfigureRopeMotion(.25f, 2f, .2f, 45f, 3f, .5f);
            hanging.ConfigureSpin(80f, 1.2f, 720f);
            hanging.enabled = true;
            hanging.ApplyRemoteImpact(new Vector3(.25f, 0f, .1f), 60f);
            Vector3 before = target.Hitbox.bounds.center;
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(target.Hitbox.bounds.center, before), Is.GreaterThan(.0001f));
            AssertPaintMeshAndWorldPose();
            Assert.That(target.Hitbox.sharedMesh, Is.SameAs(originalMesh), "Common suspension motion must not replace or recook the gourd mesh.");
            Assert.That(target.Hitbox.attachedRigidbody.isKinematic, Is.True);
            hanging.enabled = false;
            Physics.SyncTransforms();
            Vector3 front = FrontPoint(target.Hitbox.bounds.center.x, target.Hitbox.bounds.center.y);
            var receipt = new Receipt();
            Launch(front + Vector3.forward * 2f, Vector3.back * 240f, receipt, .5f, .3f);
            yield return Await(receipt);
            Assert.That(receipt.Result.Hit, Is.True, "The rotated real hanging shell remains the projectile's registered target.");
        }

        [UnityTest]
        public IEnumerator PaintCrackAndPaintRemountsReplaceGeometryWithoutStaleCylinderOrCollider()
        {
            MeshCollider identity = target.Hitbox;
            Mesh paintMesh = target.Hitbox.sharedMesh;
            SetTuning("Gourd Mode (0 paint, 1 crack)", 1f);
            Assert.That(paint.ApplyMode(), Is.True, paint.InitializationError);
            yield return null;
            Physics.SyncTransforms();
            LabGourdCrack crack = target.GetComponent<LabGourdCrack>();
            Assert.That(crack.IsReady, Is.True, crack.InitializationError);
            Assert.That(target.Hitbox, Is.SameAs(identity));
            Assert.That(target.Hitbox.sharedMesh, Is.Not.SameAs(paintMesh));
            Assert.That(target.Hitbox.enabled, Is.True);
            Assert.That(target.Hitbox.bounds.size.sqrMagnitude, Is.GreaterThan(.1f));
            var receipt = new Receipt();
            Vector3 front = FrontPoint(target.Hitbox.bounds.center.x, target.Hitbox.bounds.center.y);
            Launch(front + Vector3.forward * 2f, Vector3.back * 30f, receipt);
            yield return Await(receipt);
            Assert.That(receipt.Result.Hit, Is.True, "Crack mode must bind the active shell pieces, not retain the departed paint body's geometry.");
            SetTuning("Gourd Mode (0 paint, 1 crack)", 0f);
            Assert.That(paint.ApplyMode(), Is.True, paint.InitializationError);
            yield return null;
            Physics.SyncTransforms();
            Assert.That(target.Hitbox, Is.SameAs(identity));
            AssertPaintMeshAndWorldPose();
            Assert.That(target.Hitbox.sharedMesh, Is.SameAs(paintMesh));
            Assert.That(target.GetComponentsInChildren<Collider>(true).Count(collider => collider.enabled), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ActualSoloHostThrowPaintsTheContactInTheVisibleShellsLocalCoordinates()
        {
            // Deterministic public generation/open/drag input; no model hit or paint injection.
            float[] generation = tuning.CaptureDeveloperValues();
            foreach (string kind in new[] { "Yin", "Yang", "Relay Orb", "Potion", "Bomb" })
                SetTuning(generation, kind + " Chance (%)", 0f);
            SetTuning(generation, "Secret Box Chance (%)", 100f);
            Assert.That(tuning.TryApplyDeveloperValues(generation, out string generationError), Is.True, generationError);
            SetTuning("Item Unlock Swipes Min", 0f);
            SetTuning("Item Unlock Swipes Max", 0f);
            SetTuning("Spawn Pop Duration (seconds)", 0f);
            SetTuning("Spawn Minimum Speed (board widths/s)", 0f);
            SetTuning("Spawn Maximum Speed (board widths/s)", 0f);
            SetTuning("Drum Drift Speed (board widths/s)", 0f);
            SetTuning("Gourd Bomb Interval Min (seconds, 0 = off)", 0f);
            SetTuning("Gourd Bomb Interval Max (seconds, 0 = off)", 0f);
            SetTuning("Gourd Color Combo (0 off, 1 on)", 0f);
            SetTuning("Paint Drying (0 off, 1 on)", 0f);
            SetTuning("Paint Initial Coverage (%)", .1f);
            SetTuning("Paint Pattern", 0f);
            SetTuning("Throw Origin Radius", 4f);
            SetTuning("Throw Origin Height Offset", -.5f);
            SetTuning("Throw Minimum Upward Speed", 2f);
            SetTuning("Throw Lateral Gain", 2.5f);
            SetTuning("Throw Upward Gain", 2.1f);
            SetTuning("Throw Forward Gain", 4.5f);
            SetTuning("Throw Maximum World Speed", 18f);
            SetTuning("Throw Gravity", 9.81f);
            SetTuning("Throw Lifetime (seconds)", 4f);
            SetTuning("Throw Power Exponent", 1f);
            SetTuning("Throw Loft Offset (degrees)", 0f);
            LabSceneController controller = Object.FindFirstObjectByType<LabSceneController>();
            controller.config = tuning;
            controller.ReconfigureBoard();
            savedScene.UseAvailablePort(network);
            Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
            yield return null;
            target.GetComponent<LabHangingTarget>().enabled = false;
            AssertPaintMeshAndWorldPose();
            network.Generate();
            LabOrbState box = network.Snapshot.orbs.Single();
            Assert.That(box.kind, Is.EqualTo(LabOrbKind.SecretBox));
            Tap(box.id, 10d);
            Tap(box.id, 10.2d);
            string id = network.Snapshot.orbs.First(orb => orb.kind == LabOrbKind.Combined).id;
            int approvals = 0;
            Vector3 localContact = default;
            System.Action<Vector3, LabOrbElement> record = (point, _) => { localContact = point; approvals++; };
            target.ApprovedHit += record;
            try
            {
                LabOrbView orb = View(id);
                Vector2 from = orb.Body.position;
                Vector2 aim = network.board.AreaShape.ToWorld(new Vector2(.5f, .25f), orb.HitSphere.radius);
                Assert.That(network.board.TryBeginDragAtWorld(from, 20d), Is.True);
                Assert.That(network.board.DragToWorld(aim, 21d), Is.True);
                Assert.That(network.board.EndDragAtWorld(aim + Vector2.up * (network.board.CenterBounds.width * .2f), 21.04d), Is.True);
                double deadline = Time.realtimeSinceStartupAsDouble + 4d;
                while (approvals == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(approvals, Is.EqualTo(1), "A real Solo Host collision must approve this input-launched projectile once.");
                Assert.That(network.LastShotReport, Does.StartWith("SHOT HIT"));
                Assert.That(network.Snapshot.orbs.Any(state => state.id == id), Is.False);
                Assert.That(network.PaintSurface.PaintCount, Is.EqualTo(1));
                Assert.That(network.PaintSurface.CoveredCount, Is.GreaterThan(0));
                Vector3 direction = localContact.normalized;
                int x = Mathf.FloorToInt((Mathf.Atan2(direction.z, direction.x) / (2f * Mathf.PI) + .5f)
                    * network.PaintSurface.Columns) % network.PaintSurface.Columns;
                int y = Mathf.Clamp(Mathf.FloorToInt((.5f - direction.y * .5f) * network.PaintSurface.Rows),
                    0, network.PaintSurface.Rows - 1);
                Color32[] pixels = new Color32[network.PaintSurface.CellCount];
                network.PaintSurface.CopyPixels(pixels);
                Assert.That(pixels[y * network.PaintSurface.Columns + x].a, Is.EqualTo(255),
                    "The actual contact cell must be painted in the visible mesh's shader coordinates, including its offset/scale.");
                yield return null;
                Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(pixels));
            }
            finally { target.ApprovedHit -= record; }
        }

        private void AssertPaintMeshAndWorldPose()
        {
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            MeshFilter shell = paint.Shell.GetComponent<MeshFilter>();
            Assert.That(target.Hitbox.sharedMesh, Is.SameAs(shell.sharedMesh),
                "The gameplay collider must use the actual visible PaintBody mesh.");
            Assert.That(target.Hitbox.convex, Is.False);
            Assert.That(target.Hitbox.isTrigger, Is.False);
            Assert.That(target.Hitbox.enabled, Is.True);
            Bounds mesh = shell.sharedMesh.bounds;
            foreach (Vector3 sample in new[] { mesh.center, mesh.min, mesh.max, mesh.center + Vector3.right * mesh.extents.x })
                Assert.That(Vector3.Distance(target.Hitbox.transform.TransformPoint(sample), shell.transform.TransformPoint(sample)),
                    Is.LessThan(.0001f), "Visible mesh and collision geometry must share the same world pose.");
        }

        private Vector3 FrontPoint(float x, float y)
        {
            Bounds bounds = target.Hitbox.bounds;
            Assert.That(target.Hitbox.Raycast(new Ray(new Vector3(x, y, bounds.max.z + 2f), Vector3.back),
                out RaycastHit hit, bounds.size.z + 4f), Is.True,
                "The fixture must aim at an actual triangle of the displayed shell: " + new Vector2(x, y));
            return hit.point;
        }

        private LabProjectile Launch(Vector3 position, Vector3 velocity, Receipt receipt,
            float blastScale = 1f, float comboScale = 1f, Collider backstop = null)
        {
            LabProjectile projectile = LabProjectile.Create(position, tuning, shots.transform, LabOrbElement.Fire, blastScale);
            projectile.SetRadiusScale(comboScale);
            projectile.SetMissSurface(backstop);
            Assert.That(projectile.Launch("gourd-contact-" + System.Guid.NewGuid().ToString("N"), 0,
                velocity, tuning, target, receipt.Receive), Is.True);
            return projectile;
        }

        private Collider Backstop(Bounds bounds)
        {
            GameObject backstop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backstop.transform.SetParent(shots.transform);
            backstop.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 2f);
            backstop.transform.localScale = new Vector3(bounds.size.x + 8f, bounds.size.y + 8f, .05f);
            return backstop.GetComponent<Collider>();
        }

        private void SetTuning(string label, float value)
        {
            float[] values = tuning.CaptureDeveloperValues();
            SetTuning(values, label, value);
            Assert.That(tuning.TryApplyDeveloperValues(values, out string error), Is.True, label + ": " + error);
        }

        private static void SetTuning(float[] values, string label, float value)
        {
            int index = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
                .Single(candidate => LabConfig.DeveloperFields[candidate].Label == label);
            values[index] = value;
        }

        private LabOrbView View(string id) => network.board.GetComponentsInChildren<LabOrbView>()
            .Single(view => view.OrbId == id);

        private void Tap(string id, double at)
        {
            Vector2 position = View(id).Body.position;
            Assert.That(network.board.TryBeginDragAtWorld(position, at), Is.True);
            Assert.That(network.board.EndDragAtWorld(position, at + .05d), Is.True);
        }

        private static IEnumerator Await(params Receipt[] receipts)
        {
            for (int step = 0; step < 100 && receipts.Any(receipt => receipt.Count == 0); step++)
                yield return new WaitForFixedUpdate();
            foreach (Receipt receipt in receipts) Assert.That(receipt.Count, Is.EqualTo(1));
        }

        private static IEnumerator AssertExactlyOnce(params Receipt[] receipts)
        {
            for (int step = 0; step < 3; step++) yield return new WaitForFixedUpdate();
            foreach (Receipt receipt in receipts) Assert.That(receipt.Count, Is.EqualTo(1));
        }
    }
}
