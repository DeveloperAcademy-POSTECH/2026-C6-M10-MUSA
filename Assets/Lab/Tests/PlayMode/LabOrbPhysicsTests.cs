using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>Real 3D Rigidbody/SphereCollider integration, not a mocked movement calculator.</summary>
    public sealed class LabOrbPhysicsTests
    {
        private const float Width = 8f;
        private static readonly Rect Bounds = new Rect(-4f, -2f, Width, 4f);
        private GameObject root;
        private LabConfig config;
        private LabOrbBoard board;
        private Camera inputCamera;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Lab physics test root");
            config = ScriptableObject.CreateInstance<LabConfig>();
            var cameraObject = new GameObject("Board camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            inputCamera = cameraObject.AddComponent<Camera>();
            inputCamera.orthographic = true;
            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(root.transform, false);
            board = boardObject.AddComponent<LabOrbBoard>();
            board.Configure(config, inputCamera, Bounds);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            yield return null;
        }

        [Test]
        public void BoardOrbUsesA3DSphereAndAnIndependentReplaceableMeshVisual()
        {
            var orb = board.UpsertOrb("sphere", LabOrbKind.Yin, new Vector2(.5f, .5f));

            Assert.That(orb.Body, Is.SameAs(orb.GetComponent<Rigidbody>()));
            Assert.That(orb.HitSphere, Is.SameAs(orb.GetComponent<SphereCollider>()));
            Assert.That(orb.GetComponent<Rigidbody2D>(), Is.Null);
            Assert.That(orb.GetComponent<CircleCollider2D>(), Is.Null);
            Assert.That(orb.HitSphere.radius, Is.EqualTo(config.OrbRadius).Within(.001f));
            Assert.That(orb.HitSphere.isTrigger, Is.False);
            Assert.That(orb.Body.useGravity, Is.False);
            Assert.That(orb.Body.constraints & RigidbodyConstraints.FreezePositionZ,
                Is.EqualTo(RigidbodyConstraints.FreezePositionZ),
                "3D physics must stay on the existing XY interaction plane.");
            Assert.That(orb.Visual, Is.Not.Null);
            Assert.That(orb.Visual, Is.Not.SameAs(orb.transform),
                "The visual must be replaceable without changing the physics root.");
            var mesh = orb.Visual.GetComponentInChildren<MeshFilter>(true);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.sharedMesh, Is.Not.Null);
            Assert.That(orb.Visual.GetComponentInChildren<MeshRenderer>(true), Is.Not.Null);
            Assert.That(orb.Visual.GetComponentInChildren<SpriteRenderer>(true), Is.Null);
            Assert.That(orb.Visual.GetComponentInChildren<Collider>(true), Is.Null,
                "A replacement visual must not add a second gameplay hitbox.");
        }

        [UnityTest]
        public IEnumerator SharedAppearanceKeepsBoardAndProjectileArtSeparateFromPhysics()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            var yinMaterial = new Material(shader);
            var yangMaterial = new Material(shader);
            var combinedMaterial = new Material(shader);
            var shadowMaterial = new Material(shader);
            string colorProperty = yinMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            yinMaterial.SetColor(colorProperty, Color.blue);
            yangMaterial.SetColor(colorProperty, Color.red);
            combinedMaterial.SetColor(colorProperty, Color.green);
            var art = new GameObject("Shared orb art");
            var artSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            artSphere.name = "Sphere";
            artSphere.transform.SetParent(art.transform, false);
            var appearance = ScriptableObject.CreateInstance<LabOrbAppearance>();
            SetSerializedField(appearance, "visualPrefab", art);
            SetSerializedField(appearance, "yinMaterial", yinMaterial);
            SetSerializedField(appearance, "yangMaterial", yangMaterial);
            SetSerializedField(appearance, "combinedMaterial", combinedMaterial);
            SetSerializedField(appearance, "shadowMaterial", shadowMaterial);
            // Element materials win over temporary colors. Pure colors stay identical through
            // linear/gamma conversion, like the materials above.
            var waterMaterial = new Material(shader);
            var fireMaterial = new Material(shader);
            waterMaterial.SetColor(colorProperty, Color.cyan);
            fireMaterial.SetColor(colorProperty, new Color(1f, 1f, 0f));
            SetSerializedField(appearance, "waterMaterial", waterMaterial);
            SetSerializedField(appearance, "fireMaterial", fireMaterial);
            var bombIcon = new Texture2D(8, 8);
            SetSerializedField(appearance, "bombIcon", bombIcon);
            var lockIcon = new Texture2D(8, 8);
            SetSerializedField(appearance, "lockIcon", lockIcon);
            var drumTexture = new Texture2D(8, 8);
            var drumSprite = Sprite.Create(drumTexture, new Rect(0f, 0f, 8f, 8f), new Vector2(.5f, .5f), 8f);
            SetSerializedField(appearance, "drumSprite", drumSprite);
            SetSerializedField(config, "orbAppearance", appearance);

            LabProjectile projectile = null;
            LabProjectile fireShot = null;
            try
            {
                board.Configure(config, inputCamera, Bounds);
                LabOrbView yin = board.UpsertOrb("styled-yin", LabOrbKind.Yin, new Vector2(.3f, .5f));
                LabOrbView yang = board.UpsertOrb("styled-yang", LabOrbKind.Yang, new Vector2(.7f, .5f));
                projectile = LabProjectile.Create(new Vector3(0f, 1f, 3f), config);
                LabOrbView water = board.UpsertOrb("styled-water", LabOrbKind.Combined, new Vector2(.5f, .5f),
                    element: LabOrbElement.Water);
                fireShot = LabProjectile.Create(new Vector3(1f, 1f, 3f), config, null, LabOrbElement.Fire);
                LabOrbView bomb = board.UpsertOrb("styled-bomb", LabOrbKind.Bomb, new Vector2(.5f, .2f), lockSwipes: 2);
                yield return null; // Visual-only collider removal completes at frame end.

                Assert.That(ArtSurface(yin.Visual).sharedMaterial, Is.SameAs(yinMaterial));
                Assert.That(ArtSurface(yang.Visual).sharedMaterial, Is.SameAs(yangMaterial));
                Assert.That(ArtSurface(projectile.VisualMount).sharedMaterial, Is.SameAs(combinedMaterial));
                Assert.That(ArtColor(yin.Visual, colorProperty), Is.EqualTo(Color.blue));
                Assert.That(ArtColor(yang.Visual, colorProperty), Is.EqualTo(Color.red));
                Assert.That(ArtColor(projectile.VisualMount, colorProperty), Is.EqualTo(Color.green));
                Assert.That(ArtSurface(water.Visual).sharedMaterial, Is.SameAs(waterMaterial));
                Assert.That(ArtColor(water.Visual, colorProperty), Is.EqualTo(Color.cyan),
                    "A Combined orb uses its element material.");
                Assert.That(ArtSurface(fireShot.VisualMount).sharedMaterial, Is.SameAs(fireMaterial));
                Assert.That(ArtColor(fireShot.VisualMount, colorProperty), Is.EqualTo(new Color(1f, 1f, 0f)),
                    "A thrown orb keeps its element color in flight.");
                Assert.That(bomb.ItemMarker, Is.Not.Null);
                Assert.That(bomb.ItemMarker.IconVisible, Is.True, "An assigned item icon is drawn on the orb.");
                Assert.That(bomb.ItemMarker.transform.parent, Is.SameAs(bomb.transform),
                    "The icon sits on the orb root so it does not roll with the art.");
                Assert.That(bomb.ItemMarker.LockVisible, Is.True, "A locked item shows a padlock over its icon.");
                Assert.That(bomb.ItemMarker.IconAlpha, Is.EqualTo(appearance.LockedIconAlpha).Within(.001f),
                    "The icon under the padlock is dimmed.");
                board.UpsertOrb("styled-bomb", LabOrbKind.Bomb, new Vector2(.5f, .2f), lockSwipes: 0);
                Assert.That(bomb.ItemMarker.LockVisible, Is.False);
                Assert.That(bomb.ItemMarker.IconAlpha, Is.EqualTo(1f), "An unlocked item's icon is sharp again.");
                Assert.That(bomb.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
                Assert.That(yin.ItemMarker, Is.Null, "Only items carry an icon marker.");
                LabOrbView drum = board.UpsertOrb("styled-drum", LabOrbKind.Drum, new Vector2(.8f, .8f));
                Assert.That(drum.DrumArtVisible, Is.True, "The drum shows its flat art from the appearance.");
                Assert.That(drum.Visual.GetComponentInChildren<MeshRenderer>(), Is.Null, "The drum art replaces the ball.");
                Assert.That(drum.Visual.GetComponentInChildren<SpriteRenderer>().bounds.size.x,
                    Is.EqualTo(2f * config.DrumRadius).Within(.01f), "The art matches the drum's collision size.");
                Assert.That(yin.GetComponentsInChildren<Rigidbody>().Length, Is.EqualTo(1));
                Assert.That(yin.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
                Assert.That(projectile.GetComponentsInChildren<Rigidbody>().Length, Is.EqualTo(1));
                Assert.That(projectile.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
                Assert.That(yin.HitSphere, Is.SameAs(yin.GetComponent<SphereCollider>()));
                Assert.That(projectile.GetComponent<SphereCollider>(), Is.Not.Null);

                Transform contactShadow = yin.transform.Find("Contact Shadow");
                Assert.That(contactShadow, Is.Not.Null);
                Assert.That(contactShadow.parent, Is.SameAs(yin.transform),
                    "The contact shadow must remain still while the visual mount rolls.");
                Assert.That(contactShadow.GetComponent<MeshRenderer>().sharedMaterial,
                    Is.SameAs(shadowMaterial));
                Assert.That(contactShadow.GetComponent<Collider>(), Is.Null);
            }
            finally
            {
                if (projectile != null) Object.Destroy(projectile.gameObject);
                if (fireShot != null) Object.Destroy(fireShot.gameObject);
                Object.Destroy(bombIcon);
                Object.Destroy(lockIcon);
                Object.Destroy(drumSprite);
                Object.Destroy(drumTexture);
                Object.Destroy(waterMaterial);
                Object.Destroy(fireMaterial);
                Object.Destroy(appearance);
                Object.Destroy(art);
                Object.Destroy(yinMaterial);
                Object.Destroy(yangMaterial);
                Object.Destroy(combinedMaterial);
                Object.Destroy(shadowMaterial);
            }
        }

        [UnityTest]
        public IEnumerator ReplacingOrbArtCannotIntroduce2DPhysics()
        {
            var orb = board.UpsertOrb("replaceable", LabOrbKind.Yang, new Vector2(.5f, .5f));
            var artPrefab = new GameObject("Art with legacy 2D physics");
            var artChild = new GameObject("Physics child");
            artChild.transform.SetParent(artPrefab.transform, false);
            artChild.AddComponent<CircleCollider2D>();
            artChild.AddComponent<Rigidbody2D>();

            orb.SetVisualPrefab(artPrefab);
            Object.Destroy(artPrefab);
            Collider2D artCollider = orb.Visual.GetComponentInChildren<Collider2D>(true);
            Rigidbody2D artBody = orb.Visual.GetComponentInChildren<Rigidbody2D>(true);
            if (artCollider != null) Assert.That(artCollider.enabled, Is.False);
            if (artBody != null) Assert.That(artBody.simulated, Is.False);

            yield return null; // Destroy(Component) finishes at the end of the frame.
            Assert.That(orb.Visual.GetComponentInChildren<Collider2D>(true), Is.Null);
            Assert.That(orb.Visual.GetComponentInChildren<Rigidbody2D>(true), Is.Null);
            Assert.That(orb.HitSphere.enabled, Is.True, "Replacing art must leave gameplay collision intact.");
            orb.SetVisualPrefab(null);
            Assert.That(orb.Visual.Find("Default Sphere").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator FreeRollingDeceleratesToAStopWithoutInput()
        {
            board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.5f, .5f), new Vector2(.125f, 0f));
            for (int i = 0; i < 55; i++) yield return new WaitForFixedUpdate();
            Assert.That(board.TryGetMotion("yin", out _, out var velocity), Is.True);
            Assert.That(velocity.magnitude, Is.LessThanOrEqualTo(config.OrbStopSpeed + .001f));
        }

        [UnityTest]
        public IEnumerator RepeatedSnapshotsDoNotResetTheOwnersVelocity()
        {
            var orb = board.UpsertOrb("steady", LabOrbKind.Yin, new Vector2(.2f, .5f), new Vector2(1f, 0f));
            float before = orb.Body.linearVelocity.x;
            for (int i = 0; i < 8; i++)
            {
                board.UpsertOrb("steady", LabOrbKind.Yin, new Vector2(.2f, .5f), Vector2.zero);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(board.TryGetMotion("steady", out var position, out var velocity), Is.True);
            Assert.That(position.x, Is.GreaterThan(.2f));
            Assert.That(velocity.x, Is.GreaterThan(0f));
            Assert.That(orb.Body.linearVelocity.x, Is.LessThan(before),
                "Friction should reduce momentum, while repeated snapshots must not reset it.");
        }

        [UnityTest]
        public IEnumerator TopBoundaryReflectsInsteadOfTransferring()
        {
            board.UpsertOrb("up", LabOrbKind.Yin, new Vector2(.5f, .97f),
                new Vector2(0f, .9f));
            int crossings = 0;
            board.EdgeCrossed += _ => crossings++;
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            Assert.That(board.TryGetMotion("up", out _, out var velocity), Is.True);
            Assert.That(velocity.y, Is.LessThan(0f));
            Assert.That(crossings, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OutwardEdgeEmitsExactlyOneTransferWithMotionAndCanRecoverAfterRejection()
        {
            var orb = board.UpsertOrb("out", LabOrbKind.Yang, new Vector2(.995f, .65f),
                new Vector2(.35f, .025f));
            int crossings = 0;
            LabOrbEdgeCrossing saved = default;
            board.EdgeCrossed += crossing => { crossings++; saved = crossing; };
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            Assert.That(crossings, Is.EqualTo(1));
            Assert.That(saved.OrbId, Is.EqualTo("out"));
            Assert.That(saved.ToRight, Is.True);
            Assert.That(saved.Height01, Is.GreaterThan(.5f));
            Assert.That(saved.VelocityInBoardWidthsPerSecond.x, Is.GreaterThan(0f));
            Assert.That(orb.Body.isKinematic, Is.True);
            Assert.That(board.RejectEdge("out"), Is.True);
            Assert.That(orb.Body.isKinematic, Is.False);
            Assert.That(board.TryGetMotion("out", out _, out var speed), Is.True);
            Assert.That(speed, Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator RepeatedScreenCrossingsPreserveSpeedThenFrictionStopsTheOrb()
        {
            var secondObject = new GameObject("Second board");
            secondObject.transform.SetParent(root.transform, false);
            var second = secondObject.AddComponent<LabOrbBoard>();
            second.Configure(config, inputCamera, new Rect(12f, -2f, Width, 4f));
            LabOrbBoard active = board;
            int crossings = 0;
            bool removedEveryTime = true;
            float velocityDifference = 0f;

            void Relay(LabOrbBoard source, LabOrbBoard receiver, LabOrbEdgeCrossing crossing)
            {
                crossings++;
                removedEveryTime &= source.RemoveOrb(crossing.OrbId);
                receiver.UpsertOrb(crossing.OrbId, LabOrbKind.Yang,
                    new Vector2(crossing.ToRight ? 0f : 1f, crossing.Height01),
                    crossing.VelocityInBoardWidthsPerSecond);
                receiver.TryGetMotion(crossing.OrbId, out _, out var receivedVelocity);
                velocityDifference = Mathf.Max(velocityDifference,
                    (receivedVelocity - crossing.VelocityInBoardWidthsPerSecond).magnitude);
                active = receiver;
            }

            board.EdgeCrossed += crossing => Relay(board, second, crossing);
            second.EdgeCrossed += crossing => Relay(second, board, crossing);
            board.UpsertOrb("rolling", LabOrbKind.Yang, new Vector2(.95f, .5f), new Vector2(2.8f, 0f));
            Assert.That(board.TryGetMotion("rolling", out _, out var initial), Is.True);
            Assert.That(initial.x, Is.EqualTo(2.8f).Within(.001f),
                "OrbMaxReleaseSpeed is in board widths/second, not world units/second.");

            for (int i = 0; i < 150; i++) yield return new WaitForFixedUpdate();
            Assert.That(crossings, Is.GreaterThanOrEqualTo(2),
                "A strong roll should cross more than one screen before friction stops it.");
            Assert.That(removedEveryTime, Is.True);
            Assert.That(velocityDifference, Is.LessThan(.001f));
            Assert.That(active.TryGetMotion("rolling", out _, out var resting), Is.True);
            Assert.That(resting.magnitude, Is.LessThanOrEqualTo(config.OrbStopSpeed + .001f));
        }

        [UnityTest]
        public IEnumerator DisabledContactCombineLeavesDirectYinYangDropAvailable()
        {
            SetSerializedField(config, "contactCombineEnabled", false);
            Assert.That(config.ContactCombineEnabled, Is.False);
            var yin = board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.35f, .5f),
                new Vector2(.9f, 0f));
            var yang = board.UpsertOrb("yang", LabOrbKind.Yang, new Vector2(.65f, .5f),
                new Vector2(-.9f, 0f));
            int combines = 0;
            board.CombineRequested += (first, second, center, velocity) => combines++;
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            Assert.That(combines, Is.Zero, "Contact must not request a combination when disabled.");

            Vector2 from = BoardPoint(yin);
            Vector2 to = BoardPoint(yang);
            Assert.That(board.TryBeginDragAtWorld(from, 1d), Is.True);
            Assert.That(board.DragToWorld(to, 1.1d), Is.True);
            Assert.That(board.EndDragAtWorld(to, 1.12d), Is.True);
            Assert.That(combines, Is.EqualTo(1));
            Assert.That(yin.Body.isKinematic, Is.True);
            Assert.That(yang.Body.isKinematic, Is.True);
            Assert.That(board.RejectEdge("yin"), Is.True);
            Assert.That(yin.Body.isKinematic, Is.False);
            Assert.That(yang.Body.isKinematic, Is.False);
        }

        [UnityTest]
        public IEnumerator EnabledContactCombineRequestsOneYinYangCombination()
        {
            SetSerializedField(config, "contactCombineEnabled", true);
            SetSerializedField(config, "dropCombineEnabled", false);
            Assert.That(config.ContactCombineEnabled, Is.True);
            Assert.That(config.DropCombineEnabled, Is.False);
            var yin = board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.35f, .5f),
                new Vector2(.9f, 0f));
            var yang = board.UpsertOrb("yang", LabOrbKind.Yang, new Vector2(.65f, .5f),
                new Vector2(-.9f, 0f));
            int combines = 0;
            board.CombineRequested += (first, second, center, velocity) => combines++;

            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            Assert.That(combines, Is.EqualTo(1), "One physical contact should request one combination.");
            Assert.That(yin.Body.isKinematic, Is.True);
            Assert.That(yang.Body.isKinematic, Is.True);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(combines, Is.EqualTo(1), "Pending partners must not request a duplicate.");
        }

        [UnityTest]
        public IEnumerator ContactCombineCarriesTheAverageVelocityOfBothOrbs()
        {
            SetSerializedField(config, "contactCombineEnabled", true);
            SetSerializedField(config, "dropCombineEnabled", false);
            // A moving Yin hits a resting Yang. Equal masses keep the average velocity.
            board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.3f, .5f), new Vector2(1.2f, 0f));
            board.UpsertOrb("yang", LabOrbKind.Yang, new Vector2(.6f, .5f));
            int combines = 0;
            Vector2 carried = Vector2.zero;
            board.CombineRequested += (first, second, center, velocity) =>
            {
                combines++;
                carried = velocity;
            };

            for (int i = 0; i < 40 && combines == 0; i++) yield return new WaitForFixedUpdate();
            Assert.That(combines, Is.EqualTo(1));
            Assert.That(carried.x, Is.GreaterThan(.1f), "The combined orb should keep moving the way Yin was rolling.");
            Assert.That(carried.x, Is.LessThan(1.2f), "Two orbs share one momentum, so it cannot be faster than Yin.");
            Assert.That(Mathf.Abs(carried.y), Is.LessThan(.2f));
        }

        [Test]
        public void DroppingRawOnAnItemOrAnItemOnRawNeverRequestsCombine()
        {
            var yin = board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.3f, .3f));
            var bomb = board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.7f, .3f));
            var relay = board.UpsertOrb("relay", LabOrbKind.Relay, new Vector2(.3f, .7f));
            var yang = board.UpsertOrb("yang", LabOrbKind.Yang, new Vector2(.7f, .7f));
            int combines = 0;
            board.CombineRequested += (first, second, center, velocity) => combines++;

            Assert.That(board.TryBeginDragAtWorld(BoardPoint(yin), 1d), Is.True);
            Assert.That(board.DragToWorld(BoardPoint(bomb), 1.1d), Is.True);
            Assert.That(board.EndDragAtWorld(BoardPoint(bomb), 1.12d), Is.True);
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(relay), 2d), Is.True);
            Assert.That(board.DragToWorld(BoardPoint(yang), 2.1d), Is.True);
            Assert.That(board.EndDragAtWorld(BoardPoint(yang), 2.12d), Is.True);
            Assert.That(combines, Is.Zero, "Only a Yin/Yang pair may request a combination.");
        }

        [Test]
        public void LockedItemMovesLikeAnyOrbAndItsBadgeFollowsTheHostCount()
        {
            var potion = board.UpsertOrb("potion", LabOrbKind.Potion, new Vector2(.5f, .5f), lockSwipes: 3);
            Assert.That(potion.LockSwipes, Is.EqualTo(3));
            Assert.That(potion.ItemMarker.BadgeVisible, Is.True);
            Assert.That(potion.ItemMarker.BadgeText, Is.EqualTo("3"));
            Assert.That(potion.Body.isKinematic, Is.False, "Locked items must still roll to a neighbor's screen.");

            Vector2 start = BoardPoint(potion);
            Vector2 moved = start + new Vector2(1.5f, 0f);
            Assert.That(board.TryBeginDragAtWorld(start, 1d), Is.True);
            Assert.That(board.DragToWorld(moved, 1.5d), Is.True);
            Assert.That(board.EndDragAtWorld(moved, 2d), Is.True);
            Assert.That(BoardPoint(potion), Is.EqualTo(moved));

            board.UpsertOrb("potion", LabOrbKind.Potion, new Vector2(.5f, .5f), lockSwipes: 2);
            Assert.That(potion.ItemMarker.BadgeText, Is.EqualTo("2"), "The badge follows the Host count after a handoff.");
            board.UpsertOrb("potion", LabOrbKind.Potion, new Vector2(.5f, .5f), lockSwipes: 0);
            Assert.That(potion.ItemMarker.BadgeVisible, Is.False);
        }

        [Test]
        public void JustUnlockedItemPlaysTheUnlockEffectOnceAndAnOpenItemDoesNot()
        {
            var relay = board.UpsertOrb("relay", LabOrbKind.Relay, new Vector2(.4f, .3f), lockSwipes: 0, relayStage: 1,
                justUnlocked: true);
            Assert.That(relay.UnlockEffectPlays, Is.EqualTo(1), "An item arriving just unlocked plays the effect.");
            board.UpsertOrb("relay", LabOrbKind.Relay, new Vector2(.4f, .3f), lockSwipes: 0, relayStage: 1,
                justUnlocked: true);
            Assert.That(relay.UnlockEffectPlays, Is.EqualTo(1), "Repeated snapshots of the same unlock do not replay it.");

            var open = board.UpsertOrb("open", LabOrbKind.Potion, new Vector2(.6f, .3f), lockSwipes: 0);
            Assert.That(open.UnlockEffectPlays, Is.Zero, "An item created unlocked, or seen late, shows already open.");
        }

        [UnityTest]
        public IEnumerator UnlockLiftsThePadlockThenLandsAShrinkingRingWithOneGlow()
        {
            var appearance = ScriptableObject.CreateInstance<LabOrbAppearance>();
            var relayIcon = new Texture2D(8, 8);
            var lockIcon = new Texture2D(8, 8);
            SetSerializedField(appearance, "relayIcon", relayIcon);
            SetSerializedField(appearance, "lockIcon", lockIcon);
            SetSerializedField(config, "orbAppearance", appearance);
            try
            {
                board.Configure(config, inputCamera, Bounds);
                var relay = board.UpsertOrb("relay", LabOrbKind.Relay, new Vector2(.5f, .5f), lockSwipes: 0,
                    relayStage: 1, justUnlocked: true);
                LabOrbItemMarker marker = relay.ItemMarker;
                Assert.That(marker.UnlockPlaying, Is.True);
                Assert.That(marker.LockVisible, Is.True, "A just-unlocked item starts with the padlock on its icon.");
                Assert.That(marker.IconAlpha, Is.EqualTo(appearance.LockedIconAlpha).Within(.001f));
                Assert.That(marker.RingVisible, Is.False, "The ring follows a moment after the padlock starts.");

                relay.SetItemState(0, 2);
                Assert.That(marker.LockVisible, Is.True, "A snapshot update mid-effect does not cut it short.");

                yield return new WaitForSeconds(appearance.UnlockRingDelay + appearance.UnlockRingSeconds * .5f);
                float orbDiameter = 2f * config.OrbRadius;
                Assert.That(marker.RingVisible, Is.True, "A ring shrinks onto the orb.");
                Assert.That(marker.RingDiameter, Is.InRange(orbDiameter, orbDiameter * appearance.UnlockRingStartScale));

                bool glowed = false;
                float giveUp = Time.time + 2f;
                while (marker.UnlockPlaying && Time.time < giveUp)
                {
                    glowed |= marker.GlowVisible;
                    yield return null;
                }
                Assert.That(glowed, Is.True, "The orb glows once as the ring lands.");
                Assert.That(marker.UnlockPlaying, Is.False);
                Assert.That(marker.LockVisible, Is.False, "The padlock is gone once it has risen and faded.");
                Assert.That(marker.RingVisible, Is.False);
                Assert.That(marker.GlowVisible, Is.False);
                Assert.That(marker.IconAlpha, Is.EqualTo(1f), "The icon is sharp once the effect ends.");
            }
            finally
            {
                Object.Destroy(relayIcon);
                Object.Destroy(lockIcon);
                Object.Destroy(appearance);
            }
        }

        [Test]
        public void TappingADrumGeneratesWithoutStoppingItAndAFlickMovesItButNeverThrows()
        {
            var drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.5f, .5f));
            Assert.That(drum.HitSphere.radius, Is.EqualTo(config.DrumRadius).Within(.0001f), "A drum uses its own, larger size.");
            var taps = new System.Collections.Generic.List<string>();
            int throws = 0;
            board.DrumTapped += id => taps.Add(id);
            board.ThrowRequested += gesture => throws++;

            drum.Body.linearVelocity = new Vector3(.4f, 0f, 0f);
            Vector2 center = BoardPoint(drum);
            // A touch on the drum's body away from its centre still grabs it.
            Vector2 rim = center + new Vector2(config.DrumRadius * .9f, 0f);
            Tap(rim, 1d);
            Tap(rim, 1.1d);
            Tap(rim, 1.2d);
            Assert.That(taps, Is.EqualTo(new[] { "drum", "drum", "drum" }), "Every tap generates, so rapid taps burst.");
            Assert.That(drum.Body.linearVelocity.x, Is.EqualTo(.4f).Within(.0001f), "A tap does not stop the drum.");
            Assert.That(Vector2.Distance(BoardPoint(drum), center), Is.LessThan(.0001f),
                "Tapping the rim does not jump the drum's centre under the finger.");

            Assert.That(board.TryBeginDragAtWorld(center, 2d), Is.True);
            Assert.That(board.EndDragAtWorld(center + new Vector2(0f, 1.5f), 2.1d), Is.True);
            Assert.That(throws, Is.Zero, "A drum is never thrown.");
            Assert.That(taps.Count, Is.EqualTo(3), "A flick is not a tap.");
            Assert.That(drum.Body.linearVelocity.y, Is.GreaterThan(0f), "The flick sends the drum up the board.");
        }

        [Test]
        public void DoubleTapOnAnUnlockedPotionOrBoxRequestsUseButLockedOrSlowTapsDoNot()
        {
            var potion = board.UpsertOrb("potion", LabOrbKind.Potion, new Vector2(.3f, .5f), lockSwipes: 0);
            var locked = board.UpsertOrb("locked", LabOrbKind.Potion, new Vector2(.7f, .5f), lockSwipes: 2);
            var slow = board.UpsertOrb("slow", LabOrbKind.Potion, new Vector2(.5f, .85f), lockSwipes: 0);
            var box = board.UpsertOrb("box", LabOrbKind.SecretBox, new Vector2(.5f, .2f), lockSwipes: 0);
            var uses = new System.Collections.Generic.List<string>();
            board.UseRequested += id => uses.Add(id);

            Vector2 lockedPoint = BoardPoint(locked);
            Tap(lockedPoint, 1d);
            Tap(lockedPoint, 1.2d);
            Assert.That(uses, Is.Empty, "A locked item cannot be used.");

            Vector2 slowPoint = BoardPoint(slow);
            Tap(slowPoint, 2d);
            Tap(slowPoint, 3d);
            Assert.That(uses, Is.Empty, "Taps further apart than the double-tap window do not use an item.");

            Vector2 potionPoint = BoardPoint(potion);
            Tap(potionPoint, 4d);
            Assert.That(uses, Is.Empty);
            Tap(potionPoint, 4.2d);
            Assert.That(uses, Is.EqualTo(new[] { "potion" }));
            Assert.That(BoardPoint(potion), Is.EqualTo(potionPoint), "A double tap uses the item in place.");
            Assert.That(board.TryBeginDragAtWorld(potionPoint, 5d), Is.False,
                "The item waits for the Host result instead of being dragged again.");

            Vector2 boxPoint = BoardPoint(box);
            Tap(boxPoint, 6d);
            Tap(boxPoint, 6.2d);
            Assert.That(uses, Is.EqualTo(new[] { "potion", "box" }));
        }

        [Test]
        public void UnlockedRelayShowsItsStageAndCanBeThrownButALockedRelayCannot()
        {
            var lockedRelay = board.UpsertOrb("locked-relay", LabOrbKind.Relay, new Vector2(.4f, .3f), lockSwipes: 2);
            var relay = board.UpsertOrb("relay", LabOrbKind.Relay, new Vector2(.6f, .3f), lockSwipes: 0, relayStage: 1);
            Assert.That(lockedRelay.ItemMarker.BadgeText, Is.EqualTo("2"));
            Assert.That(relay.ItemMarker.BadgeText, Is.EqualTo("×1"), "The badge turns into the relay stage on unlock.");
            board.UpsertOrb("relay", LabOrbKind.Relay, new Vector2(.6f, .3f), lockSwipes: 0, relayStage: 2);
            Assert.That(relay.ItemMarker.BadgeText, Is.EqualTo("×2"));
            board.UpsertOrb("relay", LabOrbKind.Relay, new Vector2(.6f, .3f), lockSwipes: 0, relayStage: 3);
            Assert.That(relay.ItemMarker.BadgeText, Is.EqualTo("MAX"));
            Assert.That(relay.ItemMarker.BadgeVisible, Is.True);

            int throws = 0;
            string thrown = null;
            board.ThrowRequested += gesture => { throws++; thrown = gesture.OrbId; };
            Assert.That(board.TryBeginDragAtWorld(new Vector2(-.8f, -.8f), 1d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(-.8f, .9f), 1.1d), Is.True);
            Assert.That(throws, Is.Zero, "A locked relay stays on the board.");

            Assert.That(board.TryBeginDragAtWorld(new Vector2(.8f, -.8f), 2d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(.8f, .9f), 2.1d), Is.True);
            Assert.That(throws, Is.EqualTo(1));
            Assert.That(thrown, Is.EqualTo("relay"));
        }

        [Test]
        public void ALockedBombCannotBeThrownButAnUnlockedOneCanWithAnUpwardFlick()
        {
            var bomb = board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.6f, .3f), lockSwipes: 2, fuseSeconds: 11.2f);
            int throws = 0;
            string thrown = null;
            board.ThrowRequested += gesture => { throws++; thrown = gesture.OrbId; };
            Vector2 from = BoardPoint(bomb);
            Assert.That(board.TryBeginDragAtWorld(from, 1d), Is.True);
            Assert.That(board.EndDragAtWorld(from + new Vector2(0f, 1f), 1.1d), Is.True);
            Assert.That(throws, Is.Zero, "A locked bomb can only be passed on.");

            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.6f, .3f), lockSwipes: 0, fuseSeconds: -1f);
            from = BoardPoint(bomb);
            Assert.That(board.TryBeginDragAtWorld(from, 2d), Is.True);
            Assert.That(board.EndDragAtWorld(from + new Vector2(0f, 1f), 2.1d), Is.True);
            Assert.That(throws, Is.EqualTo(1), "Once handoffs open it, it is thrown like an attack orb (#212).");
            Assert.That(thrown, Is.EqualTo("bomb"));
        }

        [UnityTest]
        public IEnumerator ALoneDrumDriftsGentlyOnItsOwnWithinTheBoardAndTurns()
        {
            SetDrumDrift(.4f, 1f); // a quicker drift so a short test sees it wander
            var drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.5f, .5f));
            int crossings = 0;
            board.EdgeCrossed += crossing => crossings++;
            Vector2 last = BoardPoint(drum);
            float path = 0f, topSpeed = 0f;
            // A bounce off a side edge may leave the centre a physics step past the limit.
            var board01 = new Rect(Bounds.xMin - .2f, Bounds.yMin - .2f, Bounds.width + .4f, Bounds.height + .4f);
            for (int step = 0; step < 125; step++) // about 2.5 seconds of physics
            {
                yield return new WaitForFixedUpdate();
                Vector2 now = BoardPoint(drum);
                path += Vector2.Distance(now, last);
                last = now;
                topSpeed = Mathf.Max(topSpeed, drum.Body.linearVelocity.magnitude);
                Assert.That(board01.Contains(now), Is.True, "It stays on its own board.");
            }
            Assert.That(path, Is.GreaterThan(.5f), "Left alone, the drum moves on its own.");
            Assert.That(topSpeed, Is.LessThanOrEqualTo(config.DrumDriftSpeed * Width * 1.06f), "It drifts gently.");
            Assert.That(crossings, Is.Zero, "It never wanders onto a neighbour's screen by itself.");
            Assert.That(Quaternion.Angle(drum.Visual.localRotation, Quaternion.identity), Is.GreaterThan(1f),
                "Its art turns as the drift path curves.");
            Assert.That(Vector3.Angle(drum.Visual.forward, Vector3.forward), Is.LessThan(.01f),
                "It turns about the view axis and never rolls over.");
        }

        [UnityTest]
        public IEnumerator ADriftingDrumTurnsBackAtASideEdgeButASwipePassesIt()
        {
            var drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(1f, .5f));
            int crossings = 0;
            board.EdgeCrossed += crossing => crossings++;
            drum.Body.linearVelocity = new Vector3(config.DrumDriftSpeed * Width, 0f, 0f); // drifting outward
            for (int step = 0; step < 10; step++) yield return new WaitForFixedUpdate();
            Assert.That(crossings, Is.Zero, "A gentle drift never carries the drum onto a neighbour's screen.");
            Assert.That(drum.Body.linearVelocity.x, Is.LessThan(0f), "It turns back from the edge.");

            drum.Body.linearVelocity = new Vector3(3f * Width, 0f, 0f); // a swipe to the right
            for (int step = 0; step < 3; step++) yield return new WaitForFixedUpdate();
            Assert.That(crossings, Is.EqualTo(1), "A swipe passes it to the neighbour.");
        }

        [UnityTest]
        public IEnumerator AGlancingHitSpinsTheDrumTheWayTheContactSlides()
        {
            SetDrumDrift(0f, config.DrumDriftTurning); // no drift spin, so only the hit can turn it
            var drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.5f, .5f));
            // An orb skims leftward over the drum's upper rim (board centre is world 0,0).
            float above = (config.DrumRadius + config.OrbRadius) * .8f;
            board.UpsertOrb("skim", LabOrbKind.Yin, new Vector2(.75f, .5f + above / Bounds.height), new Vector2(-2f, 0f));
            for (int step = 0; step < 15; step++) yield return new WaitForFixedUpdate();
            Assert.That(drum.DrumSpin, Is.GreaterThan(30f),
                "Dragged leftward across its top, the drum turns anticlockwise like a real one.");
        }

        [Test]
        public void ABlastFlingsNearbyOrbsDrumsAndTheHeldOrbAwayWithoutRemovingAny()
        {
            // Board centre is world (0,0); positions below are given in 0..1 board units.
            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2);
            var near = board.UpsertOrb("near", LabOrbKind.Yin, new Vector2(.625f, .5f));   // world (1, 0)
            var drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.3125f, .5f)); // world (-1.5, 0)
            var held = board.UpsertOrb("held", LabOrbKind.Yang, new Vector2(.5f, .8f));    // world (0, 1.2)
            var far = board.UpsertOrb("far", LabOrbKind.Yin, new Vector2(.95f, .95f));     // beyond the reach
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(held), 1d), Is.True);

            var other = board.UpsertOrb("other-bomb", LabOrbKind.Bomb, new Vector2(.5f, .25f), lockSwipes: 2); // world (0, -1)
            var flung = new System.Collections.Generic.List<string>();
            int pushed = board.Blast("bomb", new Vector2(.5f, .5f), flung);
            float reach = config.BombBlastRadius * Width;
            float strength = config.BombBlastStrength * Width;
            Assert.That(pushed, Is.EqualTo(4));
            Assert.That(board.TryGetMotion("bomb", out _, out _), Is.False, "The bomb itself is gone.");
            Assert.That(board.Count, Is.EqualTo(5), "The blast removes no other orb.");
            Assert.That(flung, Is.EquivalentTo(new[] { "near", "held" }),
                "It names the orbs it flung for the Host to set alight; not the drum, the other bomb or the far orb (#213).");
            Assert.That(other.Body.linearVelocity.y, Is.LessThan(0f), "Another bomb is flung, but does not burn.");
            Assert.That(near.Body.linearVelocity.x, Is.EqualTo(strength * (1f - 1f / reach)).Within(.01f),
                "An orb is flung straight away from the bomb, harder the closer it was.");
            Assert.That(near.Body.linearVelocity.y, Is.EqualTo(0f).Within(.001f));
            Assert.That(drum.Body.linearVelocity.x, Is.LessThan(0f), "The drum is flung too.");
            Assert.That(held.Body.isKinematic, Is.False, "A held orb is knocked out of the hand.");
            Assert.That(held.Body.linearVelocity.y, Is.GreaterThan(0f));
            Assert.That(far.Body.linearVelocity.magnitude, Is.EqualTo(0f), "Beyond the blast radius nothing moves.");
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(far), 2d), Is.True, "No gesture is left stuck after the blast.");
        }

        [Test]
        public void ABlastShrunkOrbIsSmallerToSeeGrabAndBumpAndStaysSmall()
        {
            Assert.That(config.BlastShrinkScale, Is.EqualTo(.5f));
            float radius = config.OrbRadius;
            var whole = board.UpsertOrb("whole", LabOrbKind.Yin, new Vector2(.3f, .5f));
            var small = board.UpsertOrb("small", LabOrbKind.Yin, new Vector2(.7f, .5f), small: true);
            Assert.That(whole.HitSphere.radius, Is.EqualTo(radius).Within(1e-5f));
            Assert.That(small.HitSphere.radius, Is.EqualTo(.5f * radius).Within(1e-5f),
                "A blast-shrunk orb bumps into others at half the radius (#213).");
            Assert.That(small.Visual.Find("Default Sphere").localScale.x, Is.EqualTo(radius).Within(1e-4f),
                "It looks half as big.");
            Assert.That(whole.Visual.Find("Default Sphere").localScale.x, Is.EqualTo(2f * radius).Within(1e-4f));

            // A touch just past a small orb's reach misses it, where the same touch grabs a whole one.
            Vector2 offset = new Vector2(0f, .9f * radius);
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(small) + offset, 1d), Is.False, "It is harder to grab.");
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(whole) + offset, 2d), Is.True);
            board.CancelDrag();
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(small), 3d), Is.True, "Right on it, it is still grabbed.");
            board.CancelDrag();

            board.UpsertOrb("small", LabOrbKind.Yin, new Vector2(.7f, .5f), small: true);
            Assert.That(small.HitSphere.radius, Is.EqualTo(.5f * radius).Within(1e-5f), "Repeated snapshots keep it small.");
            board.UpsertOrb("whole", LabOrbKind.Yin, new Vector2(.3f, .5f), small: true);
            Assert.That(whole.HitSphere.radius, Is.EqualTo(.5f * radius).Within(1e-5f),
                "An orb the Host has just marked shrinks where it is.");
            Assert.That(whole.Visual.Find("Default Sphere").localScale.x, Is.EqualTo(radius).Within(1e-4f));

            var drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.5f, .2f), small: true);
            Assert.That(drum.HitSphere.radius, Is.EqualTo(config.DrumRadius).Within(1e-5f), "A drum never shrinks.");

            // The DEV form sets how small.
            float[] values = config.CaptureDeveloperValues();
            values[146] = .25f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            var smaller = board.UpsertOrb("smaller", LabOrbKind.Yang, new Vector2(.5f, .8f), small: true);
            Assert.That(smaller.HitSphere.radius, Is.EqualTo(.25f * radius).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator ABlastFlungOrbBurnsForTheHostsSecondsThenGoesOut()
        {
            var cold = board.UpsertOrb("cold", LabOrbKind.Yang, new Vector2(.2f, .5f));
            Assert.That(cold.Burning, Is.False);
            Assert.That(cold.Burn, Is.Null, "An orb no blast touched carries no fire at all.");

            var hot = board.UpsertOrb("hot", LabOrbKind.Yin, new Vector2(.5f, .5f), small: true, burnSeconds: .8f);
            LabOrbBurn fire = hot.Burn;
            Assert.That(hot.Burning, Is.True, "An orb a blast flung burns (#213).");
            Assert.That(fire.SecondsLeft, Is.EqualTo(.8f).Within(1e-4f), "For the seconds the Host gives.");
            Assert.That(fire.Life, Is.LessThan(.1f), "It catches from nothing...");
            Assert.That(fire.PartsShown, Is.GreaterThan(10), "Tongues of flame, a halo and embers.");
            Assert.That(fire.FullHeight, Is.GreaterThan(2f * hot.HitSphere.radius), "Its flames rise well above the ball.");
            Assert.That(fire.BackDepth, Is.EqualTo(0f),
                "The big flames stand in the ball's middle, so the ball hides their roots and keeps its colour,");
            Assert.That(fire.FrontDepth, Is.LessThan(-hot.HitSphere.radius), "and only short licks come over its front.");

            var whole = board.UpsertOrb("whole", LabOrbKind.Yin, new Vector2(.8f, .5f), burnSeconds: .8f);
            Assert.That(whole.Burn.FullHeight, Is.EqualTo(2f * fire.FullHeight).Within(1e-4f),
                "The fire is sized to its ball: a shrunk orb's is half as tall.");
            board.UpsertOrb("whole", LabOrbKind.Yin, new Vector2(.8f, .5f), burnSeconds: 3f);
            Assert.That(whole.Burn.SecondsLeft, Is.EqualTo(3f).Within(1e-4f), "A Host clock well apart puts it right.");

            float until = Time.time + .6f;
            while (Time.time < until && fire.Life < .9f) yield return null;
            Assert.That(fire.Life, Is.GreaterThan(.9f), "...and is soon at full size.");

            float left = fire.SecondsLeft;
            board.UpsertOrb("hot", LabOrbKind.Yin, new Vector2(.5f, .5f), small: true, burnSeconds: left + .1f);
            Assert.That(fire.SecondsLeft, Is.EqualTo(left).Within(1e-4f),
                "A later snapshot a moment apart does not restart or nudge it.");

            until = Time.time + 1.5f;
            while (Time.time < until && hot.Burning) yield return null;
            Assert.That(hot.Burning, Is.False, "It goes out when its seconds run down.");
            Assert.That(fire.PartsShown, Is.Zero);
            Assert.That(hot.HitSphere.radius, Is.EqualTo(.5f * config.OrbRadius).Within(1e-5f), "The orb stays small.");
            board.UpsertOrb("hot", LabOrbKind.Yin, new Vector2(.5f, .5f), small: true, burnSeconds: 0f);
            Assert.That(hot.Burning, Is.False, "A snapshot after the fire does not relight it.");
        }

        [UnityTest]
        public IEnumerator AnOrbABlastJustShrankSqueezesDownToItsNewSize()
        {
            var orb = board.UpsertOrb("orb", LabOrbKind.Yin, new Vector2(.5f, .5f));
            Assert.That(orb.Visual.localScale.x, Is.EqualTo(1f).Within(1e-4f));
            board.UpsertOrb("orb", LabOrbKind.Yin, new Vector2(.5f, .5f), small: true, burnSeconds: 4f);
            Assert.That(orb.HitSphere.radius, Is.EqualTo(.5f * config.OrbRadius).Within(1e-5f), "Its body is small at once (#213).");
            Assert.That(orb.IsShrinking, Is.True);
            Assert.That(orb.Visual.localScale.x, Is.EqualTo(2f).Within(1e-4f), "Its art starts at its old size...");

            float until = Time.time + 1f;
            while (Time.time < until && orb.IsShrinking) yield return null;
            Assert.That(orb.IsShrinking, Is.False);
            Assert.That(orb.Visual.localScale.x, Is.EqualTo(1f).Within(1e-4f), "...and squeezes down to the new one.");

            var arrived = board.UpsertOrb("arrived", LabOrbKind.Yang, new Vector2(.2f, .5f), small: true, burnSeconds: 2f);
            Assert.That(arrived.IsShrinking, Is.False, "An orb that comes in already small just is small.");
            Assert.That(arrived.Visual.localScale.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(arrived.Burning, Is.True, "And it still burns for what is left of its fire.");
        }

        [UnityTest]
        public IEnumerator AnOrbABlastFlungIsOrdinaryAgainOnceItStopsOrIsGrabbed()
        {
            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2);
            var rests = board.UpsertOrb("rests", LabOrbKind.Yin, new Vector2(.35f, .5f));
            var held = board.UpsertOrb("held", LabOrbKind.Yang, new Vector2(.65f, .5f));
            var kinds = new System.Collections.Generic.Dictionary<string, LabEdgeCrossingKind>();
            board.EdgeCrossed += crossing => kinds[crossing.OrbId] = crossing.Kind;
            float[] values = config.CaptureDeveloperValues();
            values[81] = .5f; // A weak blast comes to rest well before either edge.
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Vector2 before = BoardPoint(rests);
            Assert.That(board.Blast("bomb", new Vector2(.5f, .5f)), Is.EqualTo(2));
            Assert.That(rests.Body.linearVelocity.x, Is.LessThan(0f));
            Assert.That(held.Body.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(held), 1d), Is.True, "Caught in the hand.");
            board.CancelDrag();
            for (int step = 0; step < 150 && rests.Body.linearVelocity.sqrMagnitude > 0f; step++) yield return new WaitForFixedUpdate();
            Assert.That(rests.Body.linearVelocity.sqrMagnitude, Is.Zero, "The other comes to rest.");
            Assert.That(BoardPoint(rests).x, Is.LessThan(before.x), "It actually moved from the blast before stopping.");
            Assert.That(kinds, Is.Empty, "The weak blast and the caught orb never crossed an edge.");

            // Check the stop path before any grab could clear it. The other orb exercises the grab path.
            var entries = (System.Collections.IDictionary)typeof(LabOrbBoard)
                .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(board);
            foreach (string id in new[] { "rests", "held" })
            {
                object entry = entries[id];
                bool blasted = (bool)entry.GetType().GetField("Blasted").GetValue(entry);
                Assert.That(blasted, Is.False, id + " has lost the blast exemption before being relaunched.");
            }

            // A real impulse wakes a body that has naturally gone to sleep; the board must still report
            // both actual edge crossings as ordinary passes after the two independent reset paths.
            rests.Body.AddForce(new Vector3(-3f * Width, 0f, 0f), ForceMode.VelocityChange);
            held.Body.AddForce(new Vector3(3f * Width, 0f, 0f), ForceMode.VelocityChange);
            for (int step = 0; step < 50 && (!kinds.ContainsKey("rests") || !kinds.ContainsKey("held")); step++)
                yield return new WaitForFixedUpdate();
            Assert.That(kinds.ContainsKey("rests"), Is.True, "Rested orb must cross: position=" + rests.Body.position
                + ", velocity=" + rests.Body.linearVelocity + ", kinematic=" + rests.Body.isKinematic);
            Assert.That(kinds.ContainsKey("held"), Is.True, "Caught orb must cross: position=" + held.Body.position
                + ", velocity=" + held.Body.linearVelocity + ", kinematic=" + held.Body.isKinematic);
            Assert.That(kinds["rests"], Is.EqualTo(LabEdgeCrossingKind.Swipe));
            Assert.That(kinds["held"], Is.EqualTo(LabEdgeCrossingKind.Swipe));
        }

        [Test]
        public void VanishingAnOrbTakesItOffTheBoardAndSaysSoUnlikeRemovingIt()
        {
            var vanished = new System.Collections.Generic.List<string>();
            board.OrbVanished += id => vanished.Add(id);
            var old = board.UpsertOrb("old", LabOrbKind.Yin, new Vector2(.5f, .5f));
            var passed = board.UpsertOrb("passed", LabOrbKind.Yang, new Vector2(.2f, .5f));
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(old), 1d), Is.True, "Held as its time runs out.");

            Assert.That(board.Vanish("old"), Is.True);
            Assert.That(board.TryGetMotion("old", out _, out _), Is.False, "Its lifetime ran out, so it is gone (#235).");
            Assert.That(vanished, Is.EqualTo(new[] { "old" }), "The board says it expired.");
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(passed), 2d), Is.True, "No gesture is left stuck on it.");
            board.CancelDrag();

            Assert.That(board.RemoveOrb("passed"), Is.True);
            Assert.That(vanished, Is.EqualTo(new[] { "old" }), "An orb handed on or combined just leaves; it did not expire.");
            Assert.That(board.Vanish("never"), Is.False);
            Assert.That(board.Vanish(null), Is.False);
        }

        [UnityTest]
        public IEnumerator AnOrbFlashesWhiteInItsLastSecondsThenGoesUpInSmoke()
        {
            var fresh = board.UpsertOrb("fresh", LabOrbKind.Yin, new Vector2(.5f, .5f), lifeSeconds: 6f);
            Assert.That(fresh.LifeSecondsLeft, Is.EqualTo(6f).Within(1e-4f));
            Assert.That(fresh.ExpiryWarning, Is.False, "Not yet in its last three seconds.");
            Assert.That(fresh.ExpiryFlash, Is.Zero);
            board.UpsertOrb("fresh", LabOrbKind.Yin, new Vector2(.5f, .5f), lifeSeconds: 5.9f);
            Assert.That(fresh.LifeSecondsLeft, Is.EqualTo(6f).Within(1e-4f),
                "A later snapshot a moment apart does not nudge the countdown.");

            var dying = board.UpsertOrb("dying", LabOrbKind.Yang, new Vector2(.3f, .5f), lifeSeconds: 2.5f);
            Assert.That(dying.ExpiryWarning, Is.True, "In its last three seconds it warns (#235)...");
            Assert.That(dying.ExpiryFlash, Is.GreaterThan(.8f), "...jumping to white at once...");
            Assert.That(dying.ExpiryFlashRadius, Is.EqualTo(dying.HitSphere.radius).Within(1e-4f), "...over the whole ball.");

            int jumps = 0;
            float last = dying.ExpiryFlash;
            float until = Time.time + 1.5f;
            while (Time.time < until)
            {
                yield return null;
                float now = dying.ExpiryFlash;
                if (now > last + .2f) jumps++;
                last = now;
            }
            Assert.That(jumps, Is.GreaterThanOrEqualTo(2), "It flashes again and again.");
            Assert.That(dying.LifeSecondsLeft, Is.LessThan(1.1f), "And counts its own seconds down.");

            Vector3 at = dying.transform.position;
            Assert.That(board.Vanish("dying"), Is.True);
            var smoke = Object.FindFirstObjectByType<LabOrbVanishSmoke>();
            Assert.That(smoke, Is.Not.Null, "When it goes, it goes up in smoke...");
            Assert.That(smoke.transform.position.x, Is.EqualTo(at.x).Within(1e-4f), "...where it was...");
            Assert.That(smoke.transform.position.z, Is.LessThan(at.z - dying.HitSphere.radius), "...in front of the orbs.");
            until = Time.time + 2f;
            while (Time.time < until && smoke != null) yield return null;
            Assert.That(smoke == null, Is.True, "The smoke clears by itself.");

            board.UpsertOrb("fresh", LabOrbKind.Yin, new Vector2(.5f, .5f), lifeSeconds: -1f);
            Assert.That(fresh.LifeSecondsLeft, Is.LessThan(0f), "No lifetime given: nothing to warn about.");
            Assert.That(fresh.ExpiryWarning, Is.False);
            var drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.7f, .2f), lifeSeconds: 1f);
            Assert.That(drum.ExpiryWarning, Is.False, "A drum never runs out.");
            Assert.That(drum.ExpiryFlash, Is.Zero);
        }

        [Test]
        public void TheExpiryWarningSecondsComeFromTheAppearanceInspector()
        {
            var appearance = ScriptableObject.CreateInstance<LabOrbAppearance>();
            SetSerializedField(appearance, "expiryWarningSeconds", 1f);
            SetSerializedField(config, "orbAppearance", appearance);
            try
            {
                board.Configure(config, inputCamera, Bounds);
                Assert.That(appearance.ExpiryWarningSeconds, Is.EqualTo(1f));
                var orb = board.UpsertOrb("orb", LabOrbKind.Combined, new Vector2(.5f, .5f), element: LabOrbElement.Fire,
                    lifeSeconds: 1.5f);
                Assert.That(orb.ExpiryWarning, Is.False, "With one second set, 1.5 seconds left is not yet the warning.");
                board.UpsertOrb("orb", LabOrbKind.Combined, new Vector2(.5f, .5f), element: LabOrbElement.Fire,
                    lifeSeconds: .9f);
                Assert.That(orb.ExpiryWarning, Is.True);
                Assert.That(orb.ExpiryFlash, Is.GreaterThan(.8f));

                SetSerializedField(appearance, "expiryWarningSeconds", 0f);
                board.UpsertOrb("orb", LabOrbKind.Combined, new Vector2(.5f, .5f), element: LabOrbElement.Fire,
                    lifeSeconds: .5f);
                Assert.That(orb.ExpiryWarning, Is.False, "Zero turns the flash off.");
                Assert.That(orb.ExpiryFlash, Is.Zero);
            }
            finally
            {
                SetSerializedField(config, "orbAppearance", null);
                Object.Destroy(appearance);
            }
        }

        [UnityTest]
        public IEnumerator AnOrbAndADrumFlungPastASideEdgeGoToTheNeighbour()
        {
            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.75f, .5f), lockSwipes: 2); // world (2, 0)
            board.UpsertOrb("orb", LabOrbKind.Yin, new Vector2(.875f, .5f));                // world (3, 0)
            board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.875f, .75f));             // world (3, 1)
            var crossed = new System.Collections.Generic.List<string>();
            var kinds = new System.Collections.Generic.Dictionary<string, LabEdgeCrossingKind>();
            board.EdgeCrossed += crossing =>
            {
                if (!crossing.ToRight) return;
                crossed.Add(crossing.OrbId);
                kinds[crossing.OrbId] = crossing.Kind;
            };

            Assert.That(board.Blast("bomb", new Vector2(.75f, .5f)), Is.EqualTo(2));
            for (int step = 0; step < 15 && crossed.Count < 2; step++) yield return new WaitForFixedUpdate();
            Assert.That(crossed, Is.EquivalentTo(new[] { "orb", "drum" }),
                "Flung past the right edge, both go to the neighbour through the usual handoff.");
            Assert.That(kinds["drum"], Is.EqualTo(LabEdgeCrossingKind.Pushed),
                "The drum tells the Host a blast flung it, so it stops inside the next board.");
            Assert.That(kinds["orb"], Is.EqualTo(LabEdgeCrossingKind.Pushed),
                "A flung orb says it was pushed too, so the gourd's swipe block never stops it (#215).");
        }

        [UnityTest]
        public IEnumerator ABlastShowsACartoonBurstWithBombThatClearsItself()
        {
            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2);
            board.Blast("bomb", new Vector2(.5f, .5f));
            var effect = board.GetComponentInChildren<LabBombBlastEffect>();
            Assert.That(effect, Is.Not.Null, "A blast draws a burst where the bomb was.");
            Assert.That(effect.Label, Is.EqualTo("BOMB!"));
            Assert.That(effect.gameObject.layer, Is.EqualTo(board.gameObject.layer), "It shows on the board camera only.");
            Assert.That(effect.GetComponentsInChildren<Collider>(), Is.Empty, "The burst never touches physics.");
            Vector3 at = effect.transform.position;
            Assert.That(new Vector2(at.x, at.y), Is.EqualTo(Vector2.zero), "It bursts at the bomb (board centre).");

            yield return new WaitForSeconds(LabBombBlastEffect.Seconds + .2f);
            Assert.That(board.GetComponentInChildren<LabBombBlastEffect>() == null, Is.True, "The burst clears itself.");
        }

        [UnityTest]
        public IEnumerator ABombFlashesRedInItsLastSecondsFasterAsItRunsOut()
        {
            var bomb = board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: 5f);
            var relay = board.UpsertOrb("relay", LabOrbKind.Relay, new Vector2(.2f, .5f), lockSwipes: 0, relayStage: 1);
            yield return null;
            Assert.That(bomb.ItemMarker.WarningHz, Is.Zero, "With more than three seconds left it does not warn yet.");

            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: 2.8f);
            float early = bomb.ItemMarker.WarningHz;
            Assert.That(early, Is.GreaterThan(0f), "In its last three seconds the bomb starts flashing.");
            // Watch long enough for several flashes and stop once one is seen, so a slow frame rate
            // (few samples per second) cannot miss the bright part of every flash.
            float brightest = 0f;
            float until = Time.time + 2f;
            while (Time.time < until && brightest <= .5f)
            {
                yield return null;
                brightest = Mathf.Max(brightest, bomb.ItemMarker.WarningLevel);
            }
            Assert.That(brightest, Is.GreaterThan(.5f), "It flashes red.");

            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: .3f);
            Assert.That(bomb.ItemMarker.WarningHz, Is.GreaterThan(early), "It flashes faster as the fuse runs out.");
            Assert.That(relay.ItemMarker.WarningHz, Is.Zero, "Only a bomb's fuse warns.");

            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 0, fuseSeconds: -1f);
            Assert.That(bomb.ItemMarker.WarningHz, Is.Zero);
            Assert.That(bomb.ItemMarker.WarningLevel, Is.Zero, "Without a fuse it stops warning.");
        }

        [UnityTest]
        public IEnumerator ABombWithArtDrawsTheCartoonBombWithItsRedUnlockCountAndABurningFuse()
        {
            var appearance = ScriptableObject.CreateInstance<LabOrbAppearance>();
            var bombArt = new Texture2D(8, 8);
            var bombFuse = new Texture2D(8, 8);
            var bombIcon = new Texture2D(8, 8);
            var lockIcon = new Texture2D(8, 8);
            SetSerializedField(appearance, "bombArt", bombArt);
            SetSerializedField(appearance, "bombFuseArt", bombFuse);
            SetSerializedField(appearance, "bombIcon", bombIcon);
            SetSerializedField(appearance, "lockIcon", lockIcon);
            SetSerializedField(config, "orbAppearance", appearance);
            try
            {
                board.Configure(config, inputCamera, Bounds);
                var bomb = board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 3, fuseSeconds: 9.2f);
                LabOrbItemMarker marker = bomb.ItemMarker;
                Assert.That(bomb.BombArtVisible, Is.True, "An assigned bomb image is drawn on the orb.");
                Assert.That(bomb.Visual.GetComponentInChildren<MeshRenderer>(), Is.Null, "The bomb art replaces the ball.");
                Assert.That(marker.IconVisible, Is.False, "No icon disc under the art.");
                Assert.That(marker.LockVisible, Is.False, "No padlock on the bomb art.");
                Assert.That(marker.BadgeVisible, Is.False, "No badge square on the bomb art.");
                Assert.That(marker.BombCountText, Is.EqualTo("3"), "The unlock handoffs left are written on it (#212).");
                // A TextMesh keeps its colour in 8-bit channels, so compare at that precision.
                Assert.That((Color32)marker.BombCountColor, Is.EqualTo((Color32)appearance.BombCountColor), "The count is red.");
                Assert.That(marker.SparkVisible, Is.True, "The bomb's fuse sparks while it burns.");
                Assert.That(marker.RainbowVisible, Is.False, "A locked bomb has no rainbow.");
                SpriteRenderer art = marker.GetComponentsInChildren<SpriteRenderer>()
                    .Single(renderer => renderer.name == "Bomb Art");
                Assert.That(art.bounds.size.x, Is.EqualTo(appearance.BombBodySize * 2f * config.OrbRadius).Within(.01f),
                    "The ball image width is set in orb diameters.");
                Assert.That(marker.FuseArtVisible, Is.True, "The cap and fuse are drawn on the ball.");
                Assert.That(marker.FuseSortingOrder, Is.GreaterThan(art.sortingOrder), "The cap is drawn over the ball.");
                Assert.That(bomb.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1), "The art adds no collider.");
                Assert.That(bomb.HitSphere.radius, Is.EqualTo(config.OrbRadius).Within(.001f), "Its body stays orb sized.");

                board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: 2.5f);
                Assert.That(marker.BombCountText, Is.EqualTo("2"), "The count follows the Host's handoffs.");
                Assert.That(marker.RopeShare, Is.Zero, "In its last three seconds the rope has burnt down to the cap.");
                // Several flashes' worth, stopping at the first red one (see the fallback flashing test).
                float reddest = 1f;
                float until = Time.time + 2f;
                while (Time.time < until && reddest >= .6f)
                {
                    yield return null;
                    reddest = Mathf.Min(reddest, marker.BombArtColor.g);
                }
                Assert.That(reddest, Is.LessThan(.6f), "In its last seconds the bomb art itself flashes red.");

                // The last handoff opens it: the Host cuts its fuse.
                board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 0, fuseSeconds: -1f);
                Assert.That(bomb.BombArtVisible, Is.True);
                Assert.That(marker.BombCountVisible, Is.False, "With no handoffs left there is no count.");
                Assert.That(marker.SparkVisible, Is.False, "The fuse goes out...");
                Assert.That(marker.SmokeVisible, Is.True, "...in a puff of smoke.");
                Assert.That(marker.RainbowVisible, Is.True, "A rainbow ring says it can now be thrown.");
                Assert.That(marker.BombArtColor, Is.EqualTo(Color.white), "And it stops flashing.");
                until = Time.time + 2f;
                while (Time.time < until && marker.SmokeVisible) yield return null;
                Assert.That(marker.SmokeVisible, Is.False, "The smoke clears.");
                Assert.That(marker.RainbowVisible, Is.True, "The rainbow stays while it can be thrown.");

                var relay = board.UpsertOrb("relay", LabOrbKind.Relay, new Vector2(.2f, .5f), lockSwipes: 1);
                Assert.That(relay.BombArtVisible, Is.False, "Only bombs use the bomb art.");
                Assert.That(relay.ItemMarker.BadgeText, Is.EqualTo("1"));
            }
            finally
            {
                SetSerializedField(config, "orbAppearance", null);
                Object.Destroy(appearance);
                Object.Destroy(bombArt);
                Object.Destroy(bombFuse);
                Object.Destroy(bombIcon);
                Object.Destroy(lockIcon);
            }
        }

        [UnityTest]
        public IEnumerator ABombReceivedOnItsUnlockingHandoffSmokesOnceWithoutAPriorLocalFuse()
        {
            var appearance = ScriptableObject.CreateInstance<LabOrbAppearance>();
            var bombArt = new Texture2D(8, 8);
            var bombFuse = new Texture2D(8, 8);
            SetSerializedField(appearance, "bombArt", bombArt);
            SetSerializedField(appearance, "bombFuseArt", bombFuse);
            SetSerializedField(config, "orbAppearance", appearance);
            try
            {
                board.Configure(config, inputCamera, Bounds);
                // SyncBoard first sees the receiving player's orb after the Host's last handoff
                // has already removed its lock and fuse. There is no burning local view to update.
                var bomb = board.UpsertOrb("received", LabOrbKind.Bomb, new Vector2(.5f, .5f),
                    lockSwipes: 0, fuseSeconds: -1f, justUnlocked: true);
                LabOrbItemMarker marker = bomb.ItemMarker;
                Assert.That(bomb.UnlockEffectPlays, Is.EqualTo(1));
                Assert.That(marker.SmokeVisible, Is.True,
                    "The actual receiving-board path must show the fuse going out, even with no previous local fuse.");
                Assert.That(marker.SparkVisible, Is.False);
                Assert.That(marker.RainbowVisible, Is.True);
                Assert.That(marker.BombCountVisible, Is.False);

                var repeated = board.UpsertOrb("received", LabOrbKind.Bomb, new Vector2(.5f, .5f),
                    lockSwipes: 0, fuseSeconds: -1f, justUnlocked: true);
                Assert.That(repeated, Is.SameAs(bomb));
                Assert.That(bomb.UnlockEffectPlays, Is.EqualTo(1), "A repeated unlock snapshot is not another handoff.");

                float until = Time.time + 2f;
                while (Time.time < until && marker.SmokeVisible) yield return null;
                Assert.That(marker.SmokeVisible, Is.False, "The receiving-board puff fades out.");
                board.UpsertOrb("received", LabOrbKind.Bomb, new Vector2(.5f, .5f),
                    lockSwipes: 0, fuseSeconds: -1f, justUnlocked: true);
                Assert.That(marker.SmokeVisible, Is.False, "Repeated snapshots cannot restart a completed puff.");
                Assert.That(bomb.UnlockEffectPlays, Is.EqualTo(1));
                Assert.That(marker.RainbowVisible, Is.True, "The throwable state remains after the smoke ends.");

                var alreadyOpen = board.UpsertOrb("already-open", LabOrbKind.Bomb, new Vector2(.2f, .5f),
                    lockSwipes: 0, fuseSeconds: -1f);
                Assert.That(alreadyOpen.UnlockEffectPlays, Is.Zero);
                Assert.That(alreadyOpen.ItemMarker.SmokeVisible, Is.False,
                    "An orb created unlocked, or received after the unlock event, must not invent an unlock puff.");
            }
            finally
            {
                SetSerializedField(config, "orbAppearance", null);
                Object.Destroy(appearance);
                Object.Destroy(bombArt);
                Object.Destroy(bombFuse);
            }
        }

        [Test]
        public void TheFuseRopeBurnsDownTowardTheCapOverTheFuseLessItsWarningSeconds()
        {
            var appearance = ScriptableObject.CreateInstance<LabOrbAppearance>();
            var bombArt = new Texture2D(8, 8);
            var bombFuse = new Texture2D(8, 8);
            SetSerializedField(appearance, "bombArt", bombArt);
            SetSerializedField(appearance, "bombFuseArt", bombFuse);
            SetSerializedField(config, "orbAppearance", appearance);
            try
            {
                board.Configure(config, inputCamera, Bounds);
                Assert.That(config.ItemLifetimeSeconds, Is.EqualTo(15f));
                Assert.That(appearance.BombWarningSeconds, Is.EqualTo(3f));
                var bomb = board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: 15f);
                LabOrbItemMarker marker = bomb.ItemMarker;
                Assert.That(marker.RopeShare, Is.EqualTo(1f).Within(1e-4f), "A new bomb's rope is whole.");
                int wholeDots = marker.RopeDotsShown;
                float wholeReach = marker.FuseTipPosition.magnitude;

                board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: 9f);
                Assert.That(marker.RopeShare, Is.EqualTo(.5f).Within(1e-4f), "It burns over the first 12 of 15 seconds.");
                Assert.That(marker.RopeDotsShown, Is.InRange(wholeDots / 2 - 1, wholeDots / 2 + 1));
                float halfReach = marker.FuseTipPosition.magnitude;
                Assert.That(halfReach, Is.LessThan(wholeReach), "The spark moves in toward the bomb.");
                Assert.That(marker.WarningHz, Is.Zero, "No red glow while the rope still burns.");

                board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: 3f);
                Assert.That(marker.RopeShare, Is.Zero, "At three seconds left the spark reaches the cap...");
                Assert.That(marker.FuseTipPosition.magnitude, Is.LessThan(halfReach));
                board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: 2.9f);
                Assert.That(marker.WarningHz, Is.GreaterThan(0f), "...and the bomb starts to glow red.");

                // A longer item lifetime from the DEV form burns the rope more slowly to the same end.
                float[] values = config.CaptureDeveloperValues();
                values[45] = 27f; // item lifetime (seconds)
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var slow = board.UpsertOrb("slow", LabOrbKind.Bomb, new Vector2(.3f, .3f), lockSwipes: 2, fuseSeconds: 15f);
                Assert.That(slow.ItemMarker.RopeShare, Is.EqualTo(.5f).Within(1e-4f), "12 of its 24 burning seconds left.");

                // The handoff that unlocks it also plays the unlock effect, which must not hide the rainbow.
                board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 0, fuseSeconds: -1f, justUnlocked: true);
                Assert.That(marker.UnlockPlaying, Is.True);
                Assert.That(marker.RainbowVisible, Is.True, "The rainbow shows from the moment it unlocks.");

                var open = board.UpsertOrb("open", LabOrbKind.Bomb, new Vector2(.7f, .7f), lockSwipes: 0, fuseSeconds: -1f);
                Assert.That(open.ItemMarker.RopeShare, Is.EqualTo(1f), "A bomb made open never lit its rope.");
                Assert.That(open.ItemMarker.SparkVisible, Is.False);
                Assert.That(open.ItemMarker.SmokeVisible, Is.False, "Nothing went out, so no smoke.");
                Assert.That(open.ItemMarker.RainbowVisible, Is.True);
            }
            finally
            {
                SetSerializedField(config, "orbAppearance", null);
                Object.Destroy(appearance);
                Object.Destroy(bombArt);
                Object.Destroy(bombFuse);
            }
        }

        [Test]
        public void AHeavyBombTrailsTheFingerAndOutweighsAnOrbWhileAnOrbSticksToTheFinger()
        {
            Assert.That(config.BombDragLag, Is.EqualTo(.12f));
            var orb = board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.3f, .3f));
            var bomb = board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.3f, .7f), lockSwipes: 2, fuseSeconds: 10f);
            Assert.That(orb.Body.mass, Is.EqualTo(1f));
            Assert.That(bomb.Body.mass, Is.EqualTo(4f), "A bomb is heavy, so orbs bump off it (#212).");

            Vector2 move = new Vector2(1.5f, 0f);
            Vector2 start = BoardPoint(orb);
            Assert.That(board.TryBeginDragAtWorld(start, 1d), Is.True);
            Assert.That(board.DragToWorld(start + move, 1.05d), Is.True);
            Assert.That(Vector2.Distance(BoardPoint(orb), start + move), Is.LessThan(1e-4f), "An orb is right under the finger.");
            board.CancelDrag();

            start = BoardPoint(bomb);
            Assert.That(board.TryBeginDragAtWorld(start, 2d), Is.True);
            Assert.That(board.DragToWorld(start + move, 2.05d), Is.True);
            float share = Vector2.Distance(BoardPoint(bomb), start) / move.magnitude;
            Assert.That(share, Is.EqualTo(1f - Mathf.Exp(-.05f / .12f)).Within(.01f),
                "50 ms after the finger moves, the bomb has closed about a third of the gap.");
            for (int i = 1; i <= 20; i++) board.DragToWorld(start + move, 2.05d + i * .05d);
            Assert.That(Vector2.Distance(BoardPoint(bomb), start + move), Is.LessThan(.01f),
                "Held still, it catches up with the finger.");
            board.CancelDrag();

            // From the DEV form: no lag sticks the bomb to the finger like an orb; a new mass applies to new bombs.
            float[] values = config.CaptureDeveloperValues();
            values[141] = 0f;
            values[144] = 10f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            start = BoardPoint(bomb);
            Assert.That(board.TryBeginDragAtWorld(start, 4d), Is.True);
            Assert.That(board.DragToWorld(start - move, 4.05d), Is.True);
            Assert.That(Vector2.Distance(BoardPoint(bomb), start - move), Is.LessThan(1e-4f));
            board.CancelDrag();
            Assert.That(board.UpsertOrb("heavier", LabOrbKind.Bomb, new Vector2(.7f, .5f), lockSwipes: 2, fuseSeconds: 10f).Body.mass,
                Is.EqualTo(10f));
        }

        [UnityTest]
        public IEnumerator ABombThePlayerSendsOnPassesAtTheEdgeEvenSlowlyButADriftingOneTurnsBack()
        {
            // No drag lag here, so the dragged bomb sits exactly on the edge when it is let go.
            float[] values = config.CaptureDeveloperValues();
            values[141] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            var crossed = new System.Collections.Generic.Dictionary<string, LabOrbEdgeCrossing>();
            board.EdgeCrossed += crossing => crossed[crossing.OrbId] = crossing;
            float edge = board.CenterBounds.xMax;
            float driftPass = 1.5f * config.DrumDriftSpeed * Width; // slower than this, a drifting bomb turns back

            var sent = board.UpsertOrb("sent", LabOrbKind.Bomb, new Vector2(.9f, .5f), lockSwipes: 2, fuseSeconds: 10f);
            Vector2 start = BoardPoint(sent);
            Assert.That(board.TryBeginDragAtWorld(start, 1d), Is.True);
            // The finger drags it to the edge and past it, then lets go with a gentle push outward.
            Assert.That(board.DragToWorld(new Vector2(edge + .3f, start.y), 1.05d), Is.True);
            Assert.That(board.DragToWorld(new Vector2(edge + .3f, start.y), 1.5d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(edge + .45f, start.y), 1.6d), Is.True);
            Assert.That(sent.Body.linearVelocity.x, Is.InRange(.01f, driftPass), "A gentle push, slower than a drift could ever pass.");
            for (int step = 0; step < 20 && !crossed.ContainsKey("sent"); step++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.ContainsKey("sent"), Is.True, "Sent on by the player, it passes at the edge however slowly (#237).");
            Assert.That(crossed["sent"].ToRight, Is.True);
            Assert.That(crossed["sent"].Kind, Is.EqualTo(LabEdgeCrossingKind.Swipe), "It is the player's own pass.");

            // The gourd's swipe block still holds that edge against it.
            board.SetSwipeBlock(1, 5f);
            var blocked = board.UpsertOrb("blocked", LabOrbKind.Bomb, new Vector2(.9f, .2f), lockSwipes: 2, fuseSeconds: 10f);
            start = BoardPoint(blocked);
            Assert.That(board.TryBeginDragAtWorld(start, 2d), Is.True);
            Assert.That(board.DragToWorld(new Vector2(edge + .3f, start.y), 2.05d), Is.True);
            Assert.That(board.DragToWorld(new Vector2(edge + .3f, start.y), 2.5d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(edge + .45f, start.y), 2.6d), Is.True);
            for (int step = 0; step < 20; step++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.ContainsKey("blocked"), Is.False, "A blocked edge still bounces the player's pass (#215).");
            Assert.That(board.BlockBounces, Is.GreaterThanOrEqualTo(1));
            board.SetSwipeBlock(0, 0f);

            // A bomb nobody sent drifts and turns back at the edge, as before.
            var drifting = board.UpsertOrb("drifting", LabOrbKind.Bomb, new Vector2(.98f, .8f), lockSwipes: 2, fuseSeconds: 10f);
            drifting.Body.linearVelocity = new Vector3(.5f * driftPass, 0f, 0f);
            for (int step = 0; step < 40; step++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.ContainsKey("drifting"), Is.False, "A bomb nobody sent never wanders across.");
        }

        [UnityTest]
        public IEnumerator AHeavyBombLeavesTheFingerSlowerAndSlidesToAStopSoonerThanAnOrb()
        {
            var orb = board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.2f, .3f));
            var bomb = board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.2f, .7f), lockSwipes: 2, fuseSeconds: 10f);

            // The same sideways flick on each: the release is worked out from the finger, not the trailing bomb.
            Vector2 Flick(LabOrbView view, double at)
            {
                Vector2 from = BoardPoint(view);
                Assert.That(board.TryBeginDragAtWorld(from, at), Is.True);
                Assert.That(board.DragToWorld(from + new Vector2(.4f, 0f), at + .05d), Is.True);
                Assert.That(board.EndDragAtWorld(from + new Vector2(.8f, 0f), at + .1d), Is.True);
                return new Vector2(view.Body.linearVelocity.x, view.Body.linearVelocity.y);
            }
            Vector2 orbRelease = Flick(orb, 1d);
            Vector2 bombRelease = Flick(bomb, 2d);
            Assert.That(orbRelease.x, Is.GreaterThan(.2f * Width), "The flick sends the orb on.");
            Assert.That(config.BombReleaseSpeed, Is.EqualTo(.75f), "Since #237, three quarters of an orb's release.");
            Assert.That(bombRelease.x, Is.EqualTo(config.BombReleaseSpeed * orbRelease.x).Within(.001f * Width),
                "A bomb leaves the finger at three quarters of an orb's speed; it stays heavier than an orb (#237).");
            Assert.That(Mathf.Abs(bombRelease.y), Is.LessThan(.001f));

            // Then each slides on the floor: the bomb loses speed twice as fast.
            const float Launch = .6f * Width;
            orb.Body.linearVelocity = new Vector3(Launch, 0f, 0f);
            bomb.Body.linearVelocity = new Vector3(Launch, 0f, 0f);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            float orbLoss = Launch - orb.Body.linearVelocity.x;
            float bombLoss = Launch - bomb.Body.linearVelocity.x;
            Assert.That(orbLoss, Is.GreaterThan(0f));
            Assert.That(bomb.Body.linearVelocity.x, Is.GreaterThan(config.DrumDriftSpeed * Width * 1.05f),
                "Still sliding, not yet drifting.");
            Assert.That(bombLoss / orbLoss, Is.EqualTo(config.BombSlideDeceleration).Within(.15f));
        }

        [UnityTest]
        public IEnumerator AMovingBombRollsItsCapAndFuseAroundTheBallIn3D()
        {
            var appearance = ScriptableObject.CreateInstance<LabOrbAppearance>();
            var bombArt = new Texture2D(8, 8);
            var bombFuse = new Texture2D(8, 8);
            SetSerializedField(appearance, "bombArt", bombArt);
            SetSerializedField(appearance, "bombFuseArt", bombFuse);
            SetSerializedField(config, "orbAppearance", appearance);
            SetDrumDrift(0f, config.DrumDriftTurning); // hold the bomb still so only the test moves it
            try
            {
                board.Configure(config, inputCamera, Bounds);
                var bomb = board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: 10f);
                LabOrbItemMarker marker = bomb.ItemMarker;
                yield return null;
                Assert.That(Quaternion.Angle(marker.BombTurn, Quaternion.identity), Is.LessThan(.01f), "A still bomb does not roll.");
                Vector2 restingTip = marker.FuseTipPosition;

                // Roll it a quarter turn to the left: distance = quarter of the ball's circumference.
                float ballRadius = appearance.BombBodySize * config.OrbRadius;
                float step = ballRadius * Mathf.PI * .5f / 10f;
                bomb.Body.interpolation = RigidbodyInterpolation.None; // so the moved transform is what renders
                for (int frame = 0; frame < 10; frame++)
                {
                    bomb.transform.localPosition += new Vector3(-step, 0f, 0f);
                    yield return null;
                }
                Assert.That(Quaternion.Angle(marker.BombTurn, Quaternion.identity), Is.EqualTo(90f).Within(2f),
                    "It turns by the distance it travelled over its radius.");
                Assert.That(Vector2.Distance(marker.FuseTipPosition, restingTip), Is.GreaterThan(.1f),
                    "The fuse moves around the ball as it rolls.");

                // Rolling on to the right carries the cap (on the right outline at rest) back toward the
                // far side; it slides along the outline instead and never vanishes behind the ball.
                for (int frame = 0; frame < 60; frame++)
                {
                    bomb.transform.localPosition += new Vector3(step * .5f, frame % 20 < 10 ? step : -step, 0f);
                    yield return null;
                    Assert.That(marker.CapDirection.z, Is.LessThanOrEqualTo(1e-4f), "Frame " + frame + ": the cap stays in view.");
                    Assert.That(marker.FuseArtVisible, Is.True);
                }

                // Even a pose that would point it straight away from the camera keeps it on the outline.
                float angle = appearance.BombCapAngle * Mathf.Deg2Rad;
                var cap = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                marker.SetBombTurn(Quaternion.FromToRotation(cap, Vector3.forward));
                Assert.That(marker.CapDirection.z, Is.LessThanOrEqualTo(1e-4f));
                Assert.That(new Vector2(marker.CapDirection.x, marker.CapDirection.y).magnitude, Is.EqualTo(1f).Within(.01f));
                marker.SetBombTurn(Quaternion.FromToRotation(cap, (cap + Vector3.back).normalized));
                Assert.That(marker.CapDirection.z, Is.LessThan(0f), "Rolled toward the camera, it stands out over the ball.");
                Assert.That(marker.BombCountText, Is.EqualTo("2"), "The red count stays upright on the ball.");
            }
            finally
            {
                SetSerializedField(config, "orbAppearance", null);
                Object.Destroy(appearance);
                Object.Destroy(bombArt);
                Object.Destroy(bombFuse);
            }
        }

        [UnityTest]
        public IEnumerator AnExpectedGourdBombFallsInAtOnceWithNoLandingWarningAndBouncesBeforeItCanBeTouched()
        {
            LabOrbAppearance look = LabOrbAppearance.Defaults;
            int landed = 0;
            board.DropLanded += id => { if (id == "gourd") landed++; };
            int childrenBefore = board.transform.childCount;
            board.ExpectDrop("gourd");
            var bomb = board.UpsertOrb("gourd", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 3, fuseSeconds: 14f);
            float radius = bomb.HitSphere.radius;
            Assert.That(board.IsDropping("gourd"), Is.True);
            Assert.That(bomb.IsDropping, Is.True);
            Assert.That(board.transform.childCount, Is.EqualTo(childrenBefore + 1),
                "Only the bomb itself appears: no landing warning on the board (#212).");
            Assert.That(bomb.Visual.gameObject.activeSelf, Is.True, "It is in sight from the start.");
            Assert.That(bomb.Visual.localPosition.y, Is.GreaterThan(radius), "It starts falling at once, from above its spot.");
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(bomb), 1d), Is.False, "It cannot be grabbed in the air.");
            Assert.That(bomb.Body.isKinematic, Is.True, "It waits on its spot without drifting.");
            Assert.That(bomb.Body.detectCollisions, Is.False, "Nothing bumps into it in the air.");
            Assert.That(board.ShortestFuse(), Is.EqualTo(14f), "Its fuse is already burning.");

            bool touchedDown = false;
            float highestHop = 0f;
            float until = Time.time + look.DropSeconds + 1f;
            while (landed == 0 && Time.time < until)
            {
                yield return null;
                float y = bomb.Visual.localPosition.y;
                if (y < .05f * radius) touchedDown = true;
                else if (touchedDown) highestHop = Mathf.Max(highestHop, y);
            }
            Assert.That(landed, Is.EqualTo(1), "It lands once.");
            Assert.That(highestHop, Is.GreaterThan(.2f * radius), "It bounces after touching down.");
            Assert.That(bomb.Visual.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(board.IsDropping("gourd"), Is.False);
            Assert.That(bomb.Body.isKinematic, Is.False);
            Assert.That(bomb.Body.detectCollisions, Is.True);
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(bomb), 5d), Is.True, "Once landed it is an ordinary bomb.");
            board.CancelDrag();

            var plain = board.UpsertOrb("plain", LabOrbKind.Bomb, new Vector2(.2f, .3f), lockSwipes: 2, fuseSeconds: 14f);
            Assert.That(plain.IsDropping, Is.False, "Only a bomb the gourd announced drops in.");
        }

        [UnityTest]
        public IEnumerator AHandedOnDrumComesToRestOnTheHostsSpotWhateverSpeedItCameInWith()
        {
            SetDrumDrift(0f, config.DrumDriftTurning); // no drift, so the resting place is the arrival's alone
            int crossings = 0;
            board.EdgeCrossed += crossing => crossings++;
            // A hard swipe in from the left aiming 30% in, and a slow one in from the right aiming 70% in.
            var fast = board.UpsertOrb("fast", LabOrbKind.Drum, new Vector2(0f, .25f), new Vector2(5f, 0f),
                arrival: LabDrumArrival.Stop, stopAt: .3f);
            var slow = board.UpsertOrb("slow", LabOrbKind.Drum, new Vector2(1f, .75f), new Vector2(-.05f, 0f),
                arrival: LabDrumArrival.Stop, stopAt: .7f);
            Assert.That(board.ArrivalOf("fast"), Is.EqualTo(LabDrumArrival.Stop));
            Assert.That(board.ArrivalOf("slow"), Is.EqualTo(LabDrumArrival.Stop));
            // A later snapshot of the same drum does not restart or change its arrival.
            board.UpsertOrb("fast", LabOrbKind.Drum, new Vector2(0f, .25f), new Vector2(5f, 0f),
                arrival: LabDrumArrival.Pass, stopAt: -1f);
            Assert.That(board.ArrivalOf("fast"), Is.EqualTo(LabDrumArrival.Stop));

            Rect bounds = board.CenterBounds;
            float slowestTravel = 0f;
            for (int step = 0; step < 100 && (board.ArrivalOf("fast") != LabDrumArrival.None
                || board.ArrivalOf("slow") != LabDrumArrival.None); step++) // up to 2 seconds
            {
                yield return new WaitForFixedUpdate();
                slowestTravel = Mathf.Max(slowestTravel, Mathf.Abs(slow.Body.linearVelocity.x));
            }
            Assert.That(board.ArrivalOf("fast"), Is.EqualTo(LabDrumArrival.None), "The hard swipe has come to rest.");
            Assert.That(board.ArrivalOf("slow"), Is.EqualTo(LabDrumArrival.None), "The slow one got there in time too.");
            Assert.That((BoardPoint(fast).x - bounds.xMin) / bounds.width, Is.EqualTo(.3f).Within(.02f),
                "However hard it came in, it rests 30% in from the side it entered by.");
            Assert.That((bounds.xMax - BoardPoint(slow).x) / bounds.width, Is.EqualTo(.7f).Within(.02f),
                "Counted from the right when it came in from the right.");
            Assert.That(slowestTravel, Is.GreaterThan(.05f * Width), "A slow drum is sped up to arrive within a second or so.");
            Assert.That(crossings, Is.Zero, "Neither went on to another player.");
            for (int step = 0; step < 10; step++) yield return new WaitForFixedUpdate();
            Assert.That(fast.Body.linearVelocity.magnitude, Is.LessThan(.01f), "It stays at rest (no drift in this test).");
        }

        [UnityTest]
        public IEnumerator ABlockedEdgeBouncesTheBoardsOwnPassesButLetsAPushOrAPassingDrumThrough()
        {
            var crossed = new System.Collections.Generic.Dictionary<string, LabOrbEdgeCrossing>();
            board.EdgeCrossed += crossing => crossed[crossing.OrbId] = crossing;
            board.SetSwipeBlock(1, 5f);
            Assert.That(board.BlockedSide, Is.EqualTo(1), "The gourd holds this board's right edge shut (#215).");

            var own = board.UpsertOrb("own", LabOrbKind.Yin, new Vector2(.9f, .3f));
            var back = board.UpsertOrb("back", LabOrbKind.Yang, new Vector2(.1f, .3f));
            own.Body.linearVelocity = new Vector3(3f * Width, 0f, 0f);   // swiped right, into the block
            back.Body.linearVelocity = new Vector3(-3f * Width, 0f, 0f); // swiped left
            for (int step = 0; step < 20 && !crossed.ContainsKey("back"); step++) yield return new WaitForFixedUpdate();
            for (int step = 0; step < 5; step++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.ContainsKey("own"), Is.False, "The player's own pass that way does not cross...");
            Assert.That(own.Body.linearVelocity.x, Is.LessThan(0f), "...it bounces off the edge like a wall.");
            Assert.That(BoardPoint(own).x, Is.LessThanOrEqualTo(board.CenterBounds.xMax + 1e-4f));
            Assert.That(board.BlockBounces, Is.GreaterThanOrEqualTo(1));
            Assert.That(crossed["back"].ToRight, Is.False, "The other way stays open.");
            Assert.That(crossed["back"].Kind, Is.EqualTo(LabEdgeCrossingKind.Swipe));
            board.RemoveOrb("own");

            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.8f, .7f), lockSwipes: 2);
            board.UpsertOrb("flung", LabOrbKind.Yin, new Vector2(.9f, .7f));
            Assert.That(board.Blast("bomb", new Vector2(.8f, .7f)), Is.EqualTo(1));
            for (int step = 0; step < 20 && !crossed.ContainsKey("flung"); step++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.ContainsKey("flung"), Is.True, "A blast's push still goes through.");
            Assert.That(crossed["flung"].ToRight, Is.True);
            Assert.That(crossed["flung"].Kind, Is.EqualTo(LabEdgeCrossingKind.Pushed));

            // Clear the bodies waiting at the edges, so nothing stands in the drum's way.
            board.RemoveOrb("back");
            board.RemoveOrb("flung");
            board.UpsertOrb("passing", LabOrbKind.Drum, new Vector2(0f, .25f), new Vector2(.5f, 0f), arrival: LabDrumArrival.Pass);
            for (int step = 0; step < 150 && !crossed.ContainsKey("passing"); step++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.ContainsKey("passing"), Is.True, "And so does a drum running on through.");
            Assert.That(crossed["passing"].Kind, Is.EqualTo(LabEdgeCrossingKind.Passing));

            board.SetSwipeBlock(0, 0f);
            Assert.That(board.BlockedSide, Is.Zero);
            var after = board.UpsertOrb("after", LabOrbKind.Yang, new Vector2(.9f, .5f));
            after.Body.linearVelocity = new Vector3(3f * Width, 0f, 0f);
            for (int step = 0; step < 20 && !crossed.ContainsKey("after"); step++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.ContainsKey("after"), Is.True, "Once lifted, the edge passes again.");
            Assert.That(crossed["after"].Kind, Is.EqualTo(LabEdgeCrossingKind.Swipe));
        }

        [UnityTest]
        public IEnumerator ClearingTheBoardLiftsAnActiveSwipeBlockBeforeTheNextRound()
        {
            board.UpsertOrb("old-round", LabOrbKind.Yin, new Vector2(.5f, .5f));
            board.SetSwipeBlock(1, 10f);
            LabOrbEdgeBlock edge = board.EdgeBlock;
            Assert.That(edge.Visible, Is.True);

            board.Clear(); // Stop and return-to-lobby use this without another battle snapshot.
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.BlockedSide, Is.Zero);
            Assert.That(board.BlockSecondsLeft, Is.Zero);
            Assert.That(edge.Visible, Is.False, "The previous round's glow and seconds disappear immediately.");

            LabOrbEdgeCrossing? crossed = null;
            board.EdgeCrossed += crossing => crossed = crossing;
            var next = board.UpsertOrb("new-round", LabOrbKind.Yang, new Vector2(.9f, .5f));
            next.Body.linearVelocity = new Vector3(3f * Width, 0f, 0f);
            for (int step = 0; step < 20 && !crossed.HasValue; step++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.HasValue, Is.True, "The old block cannot bounce an orb in the next round.");
            Assert.That(crossed.Value.ToRight, Is.True);
            Assert.That(crossed.Value.Kind, Is.EqualTo(LabEdgeCrossingKind.Swipe));
            Assert.That(edge.Visible, Is.False);
        }

        [UnityTest]
        public IEnumerator ABlockedEdgeGlowsRedAndCountsItsSecondsDownInItsMiddle()
        {
            Rect bounds = board.CenterBounds;
            board.SetSwipeBlock(-1, 3.2f);
            LabOrbEdgeBlock edge = board.EdgeBlock;
            Assert.That(edge, Is.Not.Null);
            Assert.That(edge.Visible, Is.True, "The blocked edge glows (#215).");
            Assert.That(edge.Right, Is.False);
            Assert.That(edge.SecondsText, Is.EqualTo("4"), "The whole seconds left, rounded up.");
            Assert.That(edge.GlowPosition.x, Is.EqualTo(bounds.xMin).Within(1e-4f), "The glow lies on the left edge...");
            Assert.That(edge.SecondsPosition.y, Is.EqualTo(bounds.center.y).Within(1e-3f), "...with the seconds at its middle...");
            Assert.That(edge.SecondsPosition.x, Is.InRange(bounds.xMin, bounds.xMin + .15f * bounds.width), "...just inside it.");
            Assert.That(edge.SecondsPosition.z, Is.LessThan(-config.OrbRadius), "The seconds stay in front of the orbs.");
            Assert.That(edge.Opening, Is.True, "As the block begins, the edge flashes...");
            Assert.That(edge.Brightness, Is.GreaterThan(.9f), "...bright red...");
            Assert.That(edge.GlowReach, Is.GreaterThan(.15f * bounds.width), "...reaching further in than its glow.");

            board.SetSwipeBlock(-1, 3.1f);
            Assert.That(board.BlockSecondsLeft, Is.EqualTo(3.2f).Within(1e-4f),
                "A Host snapshot a moment apart does not nudge the countdown.");
            float until = Time.time + 1.5f;
            while (Time.time < until && edge.SecondsText != "3") yield return null;
            until = Time.time + LabOrbEdgeBlock.OpeningSeconds + .5f;
            Assert.That(edge.SecondsText, Is.EqualTo("3"), "It counts down.");
            while (Time.time < until && edge.Opening) yield return null;
            Assert.That(edge.Opening, Is.False, "After three flashes...");
            Assert.That(edge.Brightness, Is.InRange(.2f, .6f), "...it settles into a light red that stays...");
            Assert.That(edge.GlowReach, Is.EqualTo(.1f * bounds.width).Within(1e-3f), "...at its usual width.");
            Assert.That(edge.Visible, Is.True, "...for as long as the block lasts.");

            board.SetSwipeBlock(1, .6f);
            Assert.That(edge.Right, Is.True, "A new block moves it to the other edge.");
            Assert.That(edge.GlowPosition.x, Is.EqualTo(bounds.xMax).Within(1e-4f));
            Assert.That(edge.SecondsText, Is.EqualTo("1"));
            until = Time.time + 2f;
            while (Time.time < until && edge.Visible) yield return null;
            Assert.That(edge.Visible, Is.False, "It disappears when its seconds run out.");
            Assert.That(board.BlockedSide, Is.Zero, "And the edge is open again.");
        }

        [UnityTest]
        public IEnumerator APassingDrumRunsOnToTheNextPlayerAndSaysItIsPassing()
        {
            var crossed = new System.Collections.Generic.List<LabOrbEdgeCrossing>();
            board.EdgeCrossed += crossing => crossed.Add(crossing);
            // Even a slow pass keeps going across the whole board.
            board.UpsertOrb("passing", LabOrbKind.Drum, new Vector2(0f, .25f), new Vector2(.1f, 0f),
                arrival: LabDrumArrival.Pass);
            var swiped = board.UpsertOrb("swiped", LabOrbKind.Drum, new Vector2(.6f, .75f));
            swiped.Body.linearVelocity = new Vector3(3f * Width, 0f, 0f); // an ordinary swipe to the right
            for (int step = 0; step < 150 && crossed.Count < 2; step++) yield return new WaitForFixedUpdate();
            Assert.That(crossed.Count, Is.EqualTo(2));
            LabOrbEdgeCrossing passing = crossed.Single(crossing => crossing.OrbId == "passing");
            Assert.That(passing.ToRight, Is.True, "It leaves by the far side.");
            Assert.That(passing.Kind, Is.EqualTo(LabEdgeCrossingKind.Passing),
                "It tells the Host it is passing on, so it stops on the near side of the next board.");
            Assert.That(crossed.Single(crossing => crossing.OrbId == "swiped").Kind, Is.EqualTo(LabEdgeCrossingKind.Swipe));
        }

        [UnityTest]
        public IEnumerator ADrumABlastFlungInSlidesOnItsOwnSpeedButStopsInsideTheBoard()
        {
            SetDrumDrift(0f, config.DrumDriftTurning); // no drift, so where it rests is the slide's alone
            int crossings = 0;
            board.EdgeCrossed += crossing => crossings++;
            // Flung in hard from the left (it would slide over two board widths) and gently from the right.
            var strong = board.UpsertOrb("strong", LabOrbKind.Drum, new Vector2(0f, .25f), new Vector2(3f, 0f),
                arrival: LabDrumArrival.Coast, stopAt: .8f);
            var gentle = board.UpsertOrb("gentle", LabOrbKind.Drum, new Vector2(1f, .75f), new Vector2(-.6f, 0f),
                arrival: LabDrumArrival.Coast, stopAt: .8f);
            Assert.That(strong.Body.linearVelocity.x, Is.EqualTo(3f * Width).Within(.01f), "It keeps the blast's speed.");
            for (int step = 0; step < 150 && (board.ArrivalOf("strong") != LabDrumArrival.None
                || board.ArrivalOf("gentle") != LabDrumArrival.None); step++) yield return new WaitForFixedUpdate();
            for (int step = 0; step < 25; step++) yield return new WaitForFixedUpdate();

            Rect bounds = board.CenterBounds;
            Assert.That(crossings, Is.Zero, "Neither goes on to another player.");
            Assert.That((BoardPoint(strong).x - bounds.xMin) / bounds.width, Is.EqualTo(.8f).Within(.02f),
                "A hard fling stops at the far zone's end instead of sliding through.");
            float gentleShare = (bounds.xMax - BoardPoint(gentle).x) / bounds.width;
            Assert.That(gentleShare, Is.InRange(.05f, .3f), "A gentle fling slides only as far as its own speed takes it.");
            Assert.That(strong.Body.linearVelocity.magnitude, Is.LessThan(.01f));
            Assert.That(gentle.Body.linearVelocity.magnitude, Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator GrabbingABlastFlungDrumMakesItsNextCrossingAnOrdinarySwipe()
        {
            var kinds = new System.Collections.Generic.List<LabEdgeCrossingKind>();
            board.EdgeCrossed += crossing => kinds.Add(crossing.Kind);
            var drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.4f, .5f));
            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.35f, .5f), fuseSeconds: 1f);
            board.Blast("bomb", new Vector2(.35f, .5f));
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(drum), 1d), Is.True, "Caught before it reaches the edge.");
            board.CancelDrag();
            drum.Body.linearVelocity = new Vector3(3f * Width, 0f, 0f); // the player swipes it on
            for (int step = 0; step < 30 && kinds.Count == 0; step++) yield return new WaitForFixedUpdate();
            Assert.That(kinds, Is.EqualTo(new[] { LabEdgeCrossingKind.Swipe }), "Once caught, the blast no longer counts.");
        }

        [Test]
        public void GrabbingOrBlastingAnArrivingDrumHandsItBackToPhysics()
        {
            var held = board.UpsertOrb("held", LabOrbKind.Drum, new Vector2(0f, .25f), new Vector2(2f, 0f),
                arrival: LabDrumArrival.Stop, stopAt: .5f);
            Assert.That(board.TryBeginDragAtWorld(BoardPoint(held), 1d), Is.True);
            Assert.That(board.ArrivalOf("held"), Is.EqualTo(LabDrumArrival.None), "The player takes it over.");
            board.CancelDrag();

            board.UpsertOrb("flung", LabOrbKind.Drum, new Vector2(1f, .75f), new Vector2(-2f, 0f),
                arrival: LabDrumArrival.Pass);
            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.95f, .75f), fuseSeconds: 1f);
            board.Blast("bomb", new Vector2(.95f, .75f));
            Assert.That(board.ArrivalOf("flung"), Is.EqualTo(LabDrumArrival.None), "A blast's push takes over from the arrival.");

            var orb = board.UpsertOrb("orb", LabOrbKind.Yin, new Vector2(0f, .5f), new Vector2(1f, 0f),
                arrival: LabDrumArrival.Stop, stopAt: .5f);
            Assert.That(board.ArrivalOf("orb"), Is.EqualTo(LabDrumArrival.None), "Only drums arrive this way.");
        }

        [UnityTest]
        public IEnumerator ABombDriftsOnItsOwnButOnlyAPushOrSwipePassesItToANeighbour()
        {
            SetDrumDrift(.4f, 1f); // a quicker drift so a short test sees it wander
            var bomb = board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: 10f);
            int crossings = 0;
            board.EdgeCrossed += crossing => crossings++;
            Vector2 last = BoardPoint(bomb);
            float path = 0f;
            for (int step = 0; step < 100; step++) // about 2 seconds of physics
            {
                yield return new WaitForFixedUpdate();
                Vector2 now = BoardPoint(bomb);
                path += Vector2.Distance(now, last);
                last = now;
            }
            Assert.That(path, Is.GreaterThan(.4f), "Left alone, the bomb wanders like the drum.");
            Assert.That(crossings, Is.Zero, "It never wanders onto a neighbour's screen by itself.");

            board.UpsertOrb("edge", LabOrbKind.Bomb, new Vector2(1f, .3f), lockSwipes: 2, fuseSeconds: 10f);
            var edge = board.GetComponentsInChildren<LabOrbView>().Single(view => view.OrbId == "edge");
            edge.Body.linearVelocity = new Vector3(config.DrumDriftSpeed * Width, 0f, 0f); // drifting outward
            for (int step = 0; step < 10; step++) yield return new WaitForFixedUpdate();
            Assert.That(crossings, Is.Zero, "A gentle drift turns back at the side edge.");

            edge.Body.linearVelocity = new Vector3(3f * Width, 0f, 0f); // a swipe to the right
            for (int step = 0; step < 3 && crossings == 0; step++) yield return new WaitForFixedUpdate();
            Assert.That(crossings, Is.EqualTo(1), "A swipe still passes it to the neighbour.");
        }

        [UnityTest]
        public IEnumerator TheFuseSizzlesWhileABombBurnsAndStopsWithoutAFuse()
        {
            var sound = root.AddComponent<LabFuseSound>();
            sound.Bind(board, null);
            yield return null;
            Assert.That(sound.Burning, Is.False, "No bomb, no sizzle.");
            Assert.That(sound.Clip, Is.Not.Null, "Without a file in Resources/Audio/Fuse a generated sizzle is used.");

            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: 10f);
            yield return null;
            Assert.That(sound.Burning, Is.True, "A locked bomb's fuse sizzles.");
            Assert.That(sound.Clip.length, Is.GreaterThan(0f));
            float calmPitch = sound.CurrentPitch;
            float calmVolume = sound.CurrentVolume;

            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2, fuseSeconds: .5f);
            yield return null;
            Assert.That(sound.CurrentPitch, Is.GreaterThan(calmPitch), "It hurries in the last seconds.");
            // The device SFX slider (saved per machine) may be 0; louder only shows when it is not.
            if (LabAudioSettings.SfxVolume > 0f)
                Assert.That(sound.CurrentVolume, Is.GreaterThan(calmVolume), "And gets louder.");

            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 0, fuseSeconds: -1f);
            yield return null;
            Assert.That(sound.Burning, Is.False, "Without a fuse it no longer burns.");
        }

        [Test]
        public void TheGeneratedSizzleIsAOneSecondAudibleLoopWithinRange()
        {
            AudioClip clip = LabFuseSound.GeneratedSizzle();
            Assert.That(clip, Is.SameAs(LabFuseSound.GeneratedSizzle()), "It is made once and reused.");
            Assert.That(clip.length, Is.EqualTo(1f).Within(.01f));
            var samples = new float[clip.samples];
            Assert.That(clip.GetData(samples, 0), Is.True);
            Assert.That(samples.Max(Mathf.Abs), Is.InRange(.2f, 1f), "It is audible and never clips past full scale.");
        }

        private void SetDrumDrift(float speed, float turning)
        {
            float[] values = config.CaptureDeveloperValues();
            values[76] = speed;
            values[77] = turning;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
        }

        private void Tap(Vector2 point, double at)
        {
            Assert.That(board.TryBeginDragAtWorld(point, at), Is.True);
            Assert.That(board.EndDragAtWorld(point, at + .05d), Is.True);
        }

        [Test]
        public void OnlyCombinedFlickReleasedInsideTheBoardRequestsThrow()
        {
            board.UpsertOrb("raw", LabOrbKind.Yin, new Vector2(.4f, .3f));
            board.UpsertOrb("combined", LabOrbKind.Combined, new Vector2(.6f, .3f));
            int throws = 0;
            LabOrbThrowGesture gesture = default;
            board.ThrowRequested += value => { throws++; gesture = value; };

            Assert.That(board.TryBeginDragAtWorld(new Vector2(-.8f, -.8f), 1d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(-.8f, .9f), 1.1d), Is.True);
            Assert.That(throws, Is.Zero, "A Raw orb must remain non-attackable.");

            Assert.That(board.TryBeginDragAtWorld(new Vector2(.8f, -.8f), 2d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(.8f, .9f), 2.1d), Is.True);
            Assert.That(throws, Is.EqualTo(1));
            Assert.That(gesture.OrbId, Is.EqualTo("combined"));
            Assert.That(gesture.Velocity.y, Is.GreaterThan(config.ThrowSwipeMinSpeed));
            Assert.That(gesture.ReleaseX01, Is.EqualTo(.6f).Within(.001f));
            Assert.That(gesture.DistanceInBoardWidths, Is.GreaterThan(config.ThrowSwipeMinDistance));
        }

        [Test]
        public void SlowOrSidewaysCombinedMovementDoesNotThrow()
        {
            board.UpsertOrb("slow", LabOrbKind.Combined, new Vector2(.5f, .35f));
            board.UpsertOrb("side", LabOrbKind.Combined, new Vector2(.5f, .65f));
            int throws = 0;
            board.ThrowRequested += _ => throws++;

            Assert.That(board.TryBeginDragAtWorld(new Vector2(0f, -.6f), 1d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(0f, .2f), 1.5d), Is.True);
            Assert.That(board.TryBeginDragAtWorld(new Vector2(0f, .6f), 2d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(1.5f, .7f), 2.1d), Is.True);
            Assert.That(throws, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OutwardCombinedFlickTransfersBeforeThrowing()
        {
            board.UpsertOrb("edge-combined", LabOrbKind.Combined, new Vector2(.9f, .3f));
            int throws = 0;
            int crossings = 0;
            board.ThrowRequested += _ => throws++;
            board.EdgeCrossed += crossing =>
            {
                Assert.That(crossing.OrbId, Is.EqualTo("edge-combined"));
                Assert.That(crossing.ToRight, Is.True);
                crossings++;
            };

            Assert.That(board.TryBeginDragAtWorld(new Vector2(3.2f, -.8f), 1d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(4.5f, 1.1f), 1.04d), Is.True);
            for (int i = 0; i < 8 && crossings == 0; i++) yield return new WaitForFixedUpdate();

            Assert.That(throws, Is.Zero, "An outward side exit takes precedence over a Combined throw.");
            Assert.That(crossings, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NewGeneratedOrbPopsOutOfItsDrumOnceAndFliesAtOnce()
        {
            var drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.5f, .5f));
            Vector2 drumCenter = BoardPoint(drum);
            Vector2 launch = new Vector2(-.3f, .9f); // board widths per second, up and to the left
            // The Host starts a generated orb at its drum's centre.
            LabOrbView orb = board.UpsertOrb("generated", LabOrbKind.Yin, new Vector2(.5f, .5f), launch, playSpawn: true);
            Vector2 start = BoardPoint(orb);
            Assert.That(Vector2.Distance(start, drumCenter), Is.EqualTo(config.DrumRadius + config.OrbRadius).Within(.001f),
                "The orb's body starts just outside the drum.");
            Assert.That(Vector2.Dot((start - drumCenter).normalized, launch.normalized), Is.GreaterThan(.999f),
                "It starts on the side it flies toward.");
            Assert.That(orb.IsSpawning, Is.True);
            Assert.That(orb.Body.isKinematic, Is.False, "The orb flies at once instead of waiting.");
            Assert.That(orb.HitSphere.enabled, Is.True, "Walls still stop it while it pops out.");
            Assert.That(Physics.GetIgnoreCollision(orb.HitSphere, drum.HitSphere), Is.True,
                "It passes through the drum and other orbs while it pops out.");
            Vector3 art = orb.Visual.position;
            Assert.That(Vector2.Distance(new Vector2(art.x, art.y), drumCenter), Is.LessThan(.001f),
                "Its art starts inside the drum.");
            Assert.That(orb.Visual.localScale.x, Is.EqualTo(config.SpawnPopStartScale).Within(.001f));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Vector2 moved = BoardPoint(orb) - start;
            Assert.That(moved.x, Is.LessThan(0f), "The launch applies from the first physics step.");
            Assert.That(moved.y, Is.GreaterThan(0f));

            yield return new WaitForSeconds(config.SpawnPopDuration + .08f);
            Assert.That(orb.IsSpawning, Is.False);
            Assert.That(orb.Visual.localPosition.magnitude, Is.LessThan(.001f));
            Assert.That(orb.Visual.localScale.x, Is.EqualTo(1f).Within(.001f));
            Assert.That(Physics.GetIgnoreCollision(orb.HitSphere, drum.HitSphere), Is.False,
                "It collides normally once out.");

            board.UpsertOrb("generated", LabOrbKind.Yin, new Vector2(.5f, .5f), playSpawn: true);
            Assert.That(orb.IsSpawning, Is.False, "Repeated snapshots must not replay the pop.");
            Assert.That(board.RemoveOrb("generated"), Is.True);
            LabOrbView returned = board.UpsertOrb("generated", LabOrbKind.Yin,
                new Vector2(.5f, .5f), playSpawn: true);
            Assert.That(returned.IsSpawning, Is.False, "Returning with the same ID must not replay the pop.");
        }

        [UnityTest]
        public IEnumerator ABurstOfOrbsFromOneDrumFliesApartWithoutJammingOrShovingIt()
        {
            SetDrumDrift(0f, config.DrumDriftTurning); // hold the drum still so only a shove could move it
            var drum = board.UpsertOrb("drum", LabOrbKind.Drum, new Vector2(.5f, .5f));
            Vector2 center = BoardPoint(drum);
            var orbs = new System.Collections.Generic.List<LabOrbView>();
            for (int i = 0; i < 6; i++) // six rapid taps in one frame, each in its own direction
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector2 launch = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * .8f;
                orbs.Add(board.UpsertOrb("burst-" + i, LabOrbKind.Yin, new Vector2(.5f, .5f), launch, playSpawn: true));
            }

            yield return new WaitForSeconds(config.SpawnPopDuration + .1f);
            foreach (var orb in orbs)
                Assert.That(Vector2.Distance(BoardPoint(orb), center),
                    Is.GreaterThan(config.DrumRadius + config.OrbRadius + .05f), orb.OrbId + " keeps flying out.");
            Assert.That(Vector2.Distance(BoardPoint(drum), center), Is.LessThan(.05f), "Popping orbs do not shove the drum.");
        }

        private static Vector2 BoardPoint(LabOrbView orb)
        {
            Vector3 position = orb.Body.position;
            return new Vector2(position.x, position.y);
        }

        private static MeshRenderer ArtSurface(Transform mount)
        {
            Transform sphere = mount.Find("Replacement Visual/Sphere");
            Assert.That(sphere, Is.Not.Null, "The configured visual prefab must be active.");
            return sphere.GetComponent<MeshRenderer>();
        }

        private static Color ArtColor(Transform mount, string property)
        {
            var block = new MaterialPropertyBlock();
            ArtSurface(mount).GetPropertyBlock(block);
            Assert.That(block.isEmpty, Is.False, "Per-orb color must be sent to the renderer.");
            return block.GetColor(property);
        }

        private static void SetSerializedField(Object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name + " should be Inspector-editable.");
            field.SetValue(target, value);
        }
    }
}
