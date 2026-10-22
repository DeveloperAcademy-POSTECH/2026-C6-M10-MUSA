using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>Real board gestures and projectile queries; no Host approval is fabricated by these views.</summary>
    public sealed class LabJigwiPresentationTests
    {
        private GameObject root;
        private LabConfig config;
        private LabOrbBoard board;
        private static readonly Vector3 Arena = new Vector3(0f, 1201f, 0f);

        [SetUp]
        public void Prepare()
        {
            root = new GameObject("Jigwi presentation fixture");
            config = ScriptableObject.CreateInstance<LabConfig>();
            var cameraRoot = new GameObject("Input camera");
            cameraRoot.transform.SetParent(root.transform, false);
            cameraRoot.transform.position = Vector3.back * 10f;
            Camera camera = cameraRoot.AddComponent<Camera>(); camera.orthographic = true;
            var boardRoot = new GameObject("Board"); boardRoot.transform.SetParent(root.transform, false);
            board = boardRoot.AddComponent<LabOrbBoard>();
            board.Configure(config, camera, new Rect(-4f, -2f, 8f, 4f));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            yield return null;
        }

        [Test]
        public void OverheatKeepsAHeldOrbVisibleUntilReleaseAndRejectionRestoresIt()
        {
            LabOrbView orb = board.UpsertOrb("water", LabOrbKind.Combined, Vector2.one * .5f,
                element: LabOrbElement.Water);
            int burns = 0, throws = 0;
            board.JigwiBurnRequested += (first, second) => { burns++; Assert.That(first, Is.EqualTo("water")); Assert.That(second, Is.Null); };
            board.ThrowRequested += _ => throws++;
            board.SetJigwiOverheat(true);
            Assert.That(board.TryBeginDragAtWorld(Vector2.zero, 1d), Is.True);
            Assert.That(board.DragToWorld(new Vector2(0f, 1.2f), 1.1d), Is.True);
            Assert.That(orb.gameObject.activeSelf, Is.True, "Holding is permitted even while hot.");
            Assert.That(burns, Is.Zero);
            Assert.That(board.EndDragAtWorld(new Vector2(0f, 2f), 1.15d), Is.True);
            Assert.That(burns, Is.EqualTo(1)); Assert.That(throws, Is.Zero);
            Assert.That(board.IsSnapshotSuppressed("water"), Is.True);
            Assert.That(root.GetComponentsInChildren<LabJigwiBurnEffect>().Length, Is.EqualTo(1));
            Assert.That(board.PlayJigwiBurnPreview("water"), Is.False, "Receipt replay cannot add another effect.");
            Assert.That(board.RejectPendingLaunch("water"), Is.True);
            Assert.That(orb.gameObject.activeSelf && orb.Body.detectCollisions, Is.True);
        }

        [Test]
        public void OverheatManualCombinationBurnsBothButCancelAndDrumTapDoNot()
        {
            LabOrbView yin = board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.35f, .5f));
            LabOrbView yang = board.UpsertOrb("yang", LabOrbKind.Yang, new Vector2(.65f, .5f));
            LabOrbView drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.5f, .8f));
            int burns = 0, combinations = 0, taps = 0;
            board.JigwiBurnRequested += (first, second) => { burns++; Assert.That(first, Is.EqualTo("yin")); Assert.That(second, Is.EqualTo("yang")); };
            board.CombineRequested += (_, _, _, _) => combinations++;
            board.DrumTapped += _ => taps++;
            board.SetJigwiOverheat(true);
            Assert.That(board.TryBeginDragAtWorld(yin.Body.position, 1d), Is.True);
            board.CancelDrag();
            Assert.That(burns, Is.Zero); Assert.That(yin.gameObject.activeSelf, Is.True);
            Assert.That(board.TryBeginDragAtWorld(drum.Body.position, 2d), Is.True);
            Assert.That(board.EndDragAtWorld(drum.Body.position, 2.1d), Is.True);
            Assert.That(taps, Is.EqualTo(1)); Assert.That(drum.gameObject.activeSelf, Is.True);
            Assert.That(board.TryBeginDragAtWorld(yin.Body.position, 3d), Is.True);
            Assert.That(board.EndDragAtWorld(yang.Body.position, 3.5d), Is.True);
            Assert.That(burns, Is.EqualTo(1)); Assert.That(combinations, Is.Zero);
            Assert.That(board.IsSnapshotSuppressed("yin") && board.IsSnapshotSuppressed("yang"), Is.True);
            Assert.That(board.RejectPendingLaunch("yin") && board.RejectPendingLaunch("yang"), Is.True);
        }

        [UnityTest]
        public IEnumerator NaturalContactDuringOverheatKeepsTheExistingCombinationAndMarksOnlyItsCallback()
        {
            float[] values = config.CaptureDeveloperValues();
            values[49] = 0f; // Every genuine opposite-polarity contact may combine.
            values[51] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            int combines = 0, burns = 0;
            board.CombineRequested += (_, _, _, _) =>
            {
                combines++;
                Assert.That(board.IsContactCombineRequest, Is.True,
                    "The Host must distinguish passive physics from a manually released combination.");
            };
            board.JigwiBurnRequested += (_, _) => burns++;
            board.SetJigwiOverheat(true);
            board.UpsertOrb("natural-yin", LabOrbKind.Yin, new Vector2(.35f, .5f), new Vector2(.8f, 0f));
            board.UpsertOrb("natural-yang", LabOrbKind.Yang, new Vector2(.65f, .5f), new Vector2(-.8f, 0f));
            for (int i = 0; i < 50 && combines == 0; i++) yield return new WaitForFixedUpdate();
            Assert.That(combines, Is.EqualTo(1), "The established contact-combine setting still applies while hot.");
            Assert.That(burns, Is.Zero, "Passive contact is never a manual action penalty.");
            Assert.That(board.IsContactCombineRequest, Is.False, "Cause metadata cannot leak into a later gesture.");
            Assert.That(board.IsSnapshotSuppressed("natural-yin") || board.IsSnapshotSuppressed("natural-yang"), Is.False);
            Assert.That(root.GetComponentsInChildren<LabJigwiBurnEffect>().Length, Is.Zero);
        }

        [Test]
        public void ManualDropCombinationDoesNotInheritThePassiveContactMarker()
        {
            LabOrbView yin = board.UpsertOrb("manual-yin", LabOrbKind.Yin, new Vector2(.35f, .5f));
            LabOrbView yang = board.UpsertOrb("manual-yang", LabOrbKind.Yang, new Vector2(.65f, .5f));
            int combines = 0;
            board.CombineRequested += (_, _, _, _) =>
            {
                combines++;
                Assert.That(board.IsContactCombineRequest, Is.False);
            };
            Assert.That(board.TryBeginDragAtWorld(yin.Body.position, 1d), Is.True);
            Assert.That(board.EndDragAtWorld(yang.Body.position, 1.5d), Is.True);
            Assert.That(combines, Is.EqualTo(1));
            Assert.That(board.IsContactCombineRequest, Is.False);
        }

        [Test]
        public void JigwiRawScaleIsIndependentOfBlastSmallAndGenerationEffectNeverHidesDrum()
        {
            LabOrbView raw = board.UpsertOrb("curse", LabOrbKind.Yang, Vector2.one * .5f,
                jigwiScale: .65f, lifeSeconds: 5f, burnSeconds: 5f);
            Assert.That(raw.HitSphere.radius, Is.EqualTo(config.OrbRadius * .65f).Within(.00001f));
            board.UpsertOrb("curse", LabOrbKind.Yang, Vector2.one * .5f, small: true, jigwiScale: .65f);
            Assert.That(raw.HitSphere.radius, Is.EqualTo(config.OrbRadius * .65f * config.BlastShrinkScale).Within(.00001f));
            board.Configure(config, root.GetComponentInChildren<Camera>(), new Rect(-4f, -2f, 8f, 4f));
            Assert.That(raw.HitSphere.radius, Is.EqualTo(config.OrbRadius * .65f * config.BlastShrinkScale).Within(.00001f),
                "A layout/config rebind must retain the independent Jigwi scale.");
            LabOrbView drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.5f, .8f), jigwiScale: .65f);
            Assert.That(drum.HitSphere.radius, Is.EqualTo(config.DrumRadius));
            board.PlayJigwiGenerationBurn(new Vector2(.5f, .7f));
            Assert.That(drum.gameObject.activeSelf, Is.True);
            Assert.That(board.IsSnapshotSuppressed("drum"), Is.False);
            board.SetFlashGlare(1f);
            board.Clear(); Assert.That(board.JigwiOverheated, Is.False);
            Assert.That(board.FlashGlare, Is.Zero, "A cleared board cannot leak flash whitening into the next round.");
        }

        [UnityTest]
        public IEnumerator RegisteredFireIsTheFirstRealContactAndNeverReportsMonsterDamage()
        {
            LabTarget target = LabTarget.CreateCylinder(root.transform, Arena, .8f, 2f);
            LabJigwiFireSurface fire = LabJigwiFireSurface.Create(root.transform, 7, "challenge", "flame",
                Arena + Vector3.forward * 1.6f, .35f);
            yield return null; // Retire the target's temporary primitive presentation collider first.
            var shot = Shot(target, fire);
            bool received = false; LabProjectileResult result = default;
            Assert.That(shot.Launch("fire-hit", 7, Vector3.back * 80f, config, target,
                receipt => { received = true; result = receipt; }), Is.True);
            for (int i = 0; i < 100 && !received; i++) yield return new WaitForFixedUpdate();
            Assert.That(received, Is.True); Assert.That(result.Kind, Is.EqualTo(LabProjectileHitKind.Fire));
            Assert.That(result.Hit, Is.False); Assert.That(result.FireId, Is.EqualTo("challenge"));
            Assert.That(result.FireTargetId, Is.EqualTo("flame"));
            Assert.That(result.HitPoint.z, Is.GreaterThan(1.6f));
        }

        [UnityTest]
        public IEnumerator ForeignOrExtinguishedFireCannotStealAnActualMonsterContact()
        {
            foreach (bool foreign in new[] { true, false })
            {
                LabTarget target = LabTarget.CreateCylinder(root.transform, Arena, .8f, 2f);
                LabJigwiFireSurface fire = LabJigwiFireSurface.Create(root.transform, foreign ? 99ul : 7ul,
                    "old-challenge", "old-flame", Arena + Vector3.forward * 1.6f, .35f);
                if (!foreign) fire.Configure(fire.transform.position, .35f, false, false);
                yield return null;
                LabProjectile shot = Shot(target, fire);
                bool received = false; LabProjectileResult result = default;
                shot.Launch("normal-hit-" + foreign, 7, Vector3.back * 80f, config, target,
                    receipt => { received = true; result = receipt; });
                for (int i = 0; i < 100 && !received; i++) yield return new WaitForFixedUpdate();
                Assert.That(received, Is.True); Assert.That(result.Kind, Is.EqualTo(LabProjectileHitKind.Monster));
                Assert.That(result.Hit, Is.True); Assert.That(result.FireId, Is.Null);
                Object.Destroy(target.gameObject); Object.Destroy(fire.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator NearerMissSurfaceWinsOverFireAndBurnPresentationRetiresNaturally()
        {
            LabTarget target = LabTarget.CreateCylinder(root.transform, Arena, .8f, 2f);
            LabJigwiFireSurface fire = LabJigwiFireSurface.Create(root.transform, 7, "challenge", "flame",
                Arena + Vector3.forward * 1.6f, .35f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform, false);
            wall.transform.position = Arena + Vector3.forward * 2.4f;
            wall.transform.localScale = new Vector3(8f, 8f, .04f);
            yield return null;
            LabProjectile shot = Shot(target, fire);
            shot.SetMissSurface(wall.GetComponent<Collider>());
            bool received = false; LabProjectileResult result = default;
            shot.Launch("wall-first", 7, Vector3.back * 80f, config, target,
                receipt => { received = true; result = receipt; });
            LabJigwiBurnEffect effect = LabJigwiBurnEffect.Play(root.transform, Vector3.zero, .3f);
            for (int i = 0; i < 100 && !received; i++) yield return new WaitForFixedUpdate();
            Assert.That(received, Is.True); Assert.That(result.Kind, Is.EqualTo(LabProjectileHitKind.Miss));
            yield return new WaitForSeconds(LabJigwiBurnEffect.Duration + .1f);
            Assert.That(effect == null, Is.True);
        }

        private LabProjectile Shot(LabTarget target, LabJigwiFireSurface fire)
        {
            var shot = LabProjectile.Create(Arena + Vector3.forward * 3f, config, root.transform, LabOrbElement.Water);
            shot.SetFireSurfaces(new[] { fire });
            return shot;
        }
    }
}
