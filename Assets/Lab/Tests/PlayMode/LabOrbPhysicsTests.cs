using System.Collections;
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
            var bomb = board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 3);
            Assert.That(bomb.LockSwipes, Is.EqualTo(3));
            Assert.That(bomb.ItemMarker.BadgeVisible, Is.True);
            Assert.That(bomb.ItemMarker.BadgeText, Is.EqualTo("3"));
            Assert.That(bomb.Body.isKinematic, Is.False, "Locked items must still roll to a neighbor's screen.");

            Vector2 start = BoardPoint(bomb);
            Vector2 moved = start + new Vector2(1.5f, 0f);
            Assert.That(board.TryBeginDragAtWorld(start, 1d), Is.True);
            Assert.That(board.DragToWorld(moved, 1.5d), Is.True);
            Assert.That(board.EndDragAtWorld(moved, 2d), Is.True);
            Assert.That(BoardPoint(bomb), Is.EqualTo(moved));

            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 2);
            Assert.That(bomb.ItemMarker.BadgeText, Is.EqualTo("2"), "The badge follows the Host count after a handoff.");
            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.5f, .5f), lockSwipes: 0);
            Assert.That(bomb.ItemMarker.BadgeVisible, Is.False);
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
        public void BombShowsItsFuseAboveTheOrbAndCanBeThrownOnlyOnceUnlocked()
        {
            var bomb = board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.6f, .3f), lockSwipes: 1, fuseSeconds: 11.2f);
            Assert.That(bomb.ItemMarker.FuseVisible, Is.True);
            Assert.That(bomb.ItemMarker.FuseText, Is.EqualTo("12"), "Remaining seconds round up.");
            Assert.That(bomb.ItemMarker.BadgeText, Is.EqualTo("1"), "The lock badge stays beside the fuse.");
            var relay = board.UpsertOrb("relay", LabOrbKind.Relay, new Vector2(.3f, .7f), lockSwipes: 0, relayStage: 1);
            Assert.That(relay.ItemMarker.FuseVisible, Is.False, "Only bombs burn a fuse.");

            int throws = 0;
            board.ThrowRequested += gesture => throws++;
            Assert.That(board.TryBeginDragAtWorld(new Vector2(.8f, -.8f), 1d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(.8f, .9f), 1.1d), Is.True);
            Assert.That(throws, Is.Zero, "A locked bomb cannot be thrown.");

            board.UpsertOrb("bomb", LabOrbKind.Bomb, new Vector2(.6f, .3f), lockSwipes: 0, fuseSeconds: 3.4f);
            Assert.That(bomb.ItemMarker.FuseText, Is.EqualTo("4"));
            Assert.That(bomb.ItemMarker.BadgeVisible, Is.False);
            Vector2 from = BoardPoint(bomb);
            Assert.That(board.TryBeginDragAtWorld(from, 2d), Is.True);
            Assert.That(board.EndDragAtWorld(from + new Vector2(0f, 1f), 2.1d), Is.True);
            Assert.That(throws, Is.EqualTo(1), "An unlocked bomb is thrown with an upward flick.");
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
