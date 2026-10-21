using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// 조합판 결합 구슬의 얕은 오행 꼬리 (#254): 던진 구슬과 같은 오행 꼬리를 판 크기에 맞춰 작게 쓰고,
    /// 움직일 때만, 빠를수록 진하게 보이며, 음양 구슬이나 DEV OFF에서는 없다.
    /// </summary>
    public sealed class LabOrbBoardTrailTests
    {
        private const float BoardRadius = .32f, ThrowRadius = .165f;
        private GameObject orb;
        private LabOrbView view;
        private LabOrbBoardTrail trail;
        private LabOrbAppearance look;
        private GameObject boardRoot;
        private LabConfig boardConfig;

        [SetUp]
        public void MakeOrb()
        {
            var config = Resources.Load<LabConfig>("LabConfig");
            Assert.That(config, Is.Not.Null);
            look = config.OrbAppearance != null ? config.OrbAppearance : LabOrbAppearance.Defaults;
            orb = new GameObject("Board trail orb");
            view = orb.AddComponent<LabOrbView>();
            view.Configure("trail-orb", LabOrbKind.Combined, BoardRadius, LabOrbElement.Fire);
            view.ConfigureAppearance(look);
            trail = orb.AddComponent<LabOrbBoardTrail>();
            trail.enabled = false; // the test steps time itself
        }

        [TearDown]
        public void DestroyOrb()
        {
            if (orb != null) Object.DestroyImmediate(orb);
            if (boardRoot != null) Object.DestroyImmediate(boardRoot);
            if (boardConfig != null) Object.DestroyImmediate(boardConfig);
        }

        [Test]
        public void ACombinedOrbGetsItsElementsTrailScaledToTheBoard()
        {
            LabElementTrailProfile thrown = look.ElementTrailFor(LabOrbElement.Fire);
            Assume.That(thrown, Is.Not.Null, "The saved appearance has a Fire flight trail.");
            trail.Sync(view, look, true, BoardRadius / ThrowRadius);
            Assert.That(trail.On, Is.True);
            Assert.That(trail.Trail.Element, Is.EqualTo(LabOrbElement.Fire), "The same ink as a thrown Fire orb.");
            float scale = BoardRadius / ThrowRadius * trail.Size;
            Assert.That(trail.Trail.Profile.Width, Is.EqualTo(thrown.Width * scale).Within(1e-4f));
            Assert.That(trail.Trail.Profile.MaxLength, Is.EqualTo(thrown.MaxLength * scale).Within(1e-4f));
            Assert.That(trail.Trail.Profile, Is.Not.SameAs(thrown), "The shared thrown-orb profile is never edited.");
            Assert.That(thrown.Width, Is.EqualTo(look.ElementTrailFor(LabOrbElement.Fire).Width));
        }

        [Test]
        public void ItShowsOnlyWhileMovingAndDeepensWithSpeed()
        {
            trail.Sync(view, look, true, BoardRadius / ThrowRadius);
            trail.Step(.02f); // first frame only remembers where the orb is
            Assert.That(trail.Intensity, Is.Zero);

            trail.Step(.02f);
            Assert.That(trail.Intensity, Is.Zero, "Still: no trail.");

            orb.transform.position += Vector3.right * BoardRadius * .02f; // 1 radius per second
            trail.Step(.02f);
            Assert.That(trail.Intensity, Is.Zero, "Barely moving: no trail.");

            orb.transform.position += Vector3.right * BoardRadius * 5f * .02f;
            trail.Step(.02f);
            float medium = trail.Intensity;
            Assert.That(medium, Is.GreaterThan(0f).And.LessThan(trail.Strength));

            orb.transform.position += Vector3.right * BoardRadius * 20f * .02f;
            trail.Step(.02f);
            Assert.That(trail.Intensity, Is.EqualTo(trail.Strength).Within(1e-5f), "Fast: as deep as it gets, still shallow.");
            Assert.That(trail.Strength, Is.LessThan(1f), "Shallower than a thrown orb's trail.");

            Assert.That(trail.IntensityFor(5f), Is.EqualTo(medium).Within(1e-4f));
            Assert.That(trail.IntensityFor(3f), Is.LessThan(trail.IntensityFor(6f)));
        }

        [Test]
        public void YinYangOrbsAndADevOffHaveNoTrail()
        {
            trail.Sync(view, look, false, BoardRadius / ThrowRadius);
            Assert.That(trail.On, Is.False, "DEV Board Orb Trail OFF.");

            trail.Sync(view, look, true, BoardRadius / ThrowRadius);
            Assert.That(trail.On, Is.True);
            view.Configure("trail-orb", LabOrbKind.Yin, BoardRadius);
            trail.Sync(view, look, true, BoardRadius / ThrowRadius);
            Assert.That(trail.On, Is.False, "Raw Yin/Yang orbs are not thrown, so no element trail.");
            Assert.That(trail.Trail.PathCount, Is.Zero, "Turning off clears what was drawn.");

            view.Configure("trail-orb", LabOrbKind.Combined, BoardRadius, LabOrbElement.Water);
            trail.Sync(view, look, true, BoardRadius / ThrowRadius);
            Assume.That(look.ElementTrailFor(LabOrbElement.Water), Is.Not.Null);
            Assert.That(trail.Trail.Element, Is.EqualTo(LabOrbElement.Water), "A new element brings its own ink.");
        }

        [UnityTest]
        public IEnumerator PendingLaunchStaysClearUntilRestoredAtItsApprovedBoardPosition()
        {
            LabOrbBoard board = CreateBoard();
            view = board.UpsertOrb("pending-trail", LabOrbKind.Combined, new Vector2(.3f, .5f),
                element: LabOrbElement.Fire);
            orb = view.gameObject;
            Rigidbody originalBody = view.Body;
            Vector3 approvedPosition = view.Body.position;
            yield return null;
            trail = view.GetComponent<LabOrbBoardTrail>();
            Assert.That(trail, Is.Not.Null, "The real board attaches the effect in LateUpdate.");
            Assert.That(trail.On, Is.True);

            Assert.That(board.BeginPendingLaunch(view.OrbId), Is.True);
            Assert.That(view.gameObject.activeInHierarchy, Is.False);
            // An outstanding request may later be restored to its saved position. It is not a flight.
            view.Body.position = approvedPosition + Vector3.right;
            yield return null;
            yield return null;
            Assert.That(trail.On, Is.False, "Board LateUpdate must not restart a hidden pending body's trail.");
            Assert.That(trail.Trail.PathCount, Is.Zero);

            Assert.That(board.RejectPendingLaunch(view.OrbId), Is.True);
            yield return null;
            Assert.That(view.Body, Is.SameAs(originalBody), "Presentation reset preserves the existing body and identity.");
            Assert.That(view.OrbId, Is.EqualTo("pending-trail"));
            Assert.That(view.Body.position, Is.EqualTo(approvedPosition));
            Assert.That(trail.On, Is.True);
            Assert.That(trail.Trail.PathCount, Is.EqualTo(1), "Restore starts a new path without a bridge from the hidden body.");
            Assert.That(trail.Trail.GetPathPosition(0), Is.EqualTo(approvedPosition));
            Assert.That(trail.Intensity, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ABlastShrunkCombinedOrbRescalesItsTrailFromTheActualColliderRadius()
        {
            LabOrbBoard board = CreateBoard();
            LabElementTrailProfile authored = look.ElementTrailFor(LabOrbElement.Fire);
            Assert.That(authored, Is.Not.Null);
            float authoredWidth = authored.Width;
            view = board.UpsertOrb("shrinking-trail", LabOrbKind.Combined, Vector2.one * .5f,
                element: LabOrbElement.Fire);
            orb = view.gameObject;
            Rigidbody originalBody = view.Body;
            yield return null;
            trail = view.GetComponent<LabOrbBoardTrail>();
            Assert.That(trail, Is.Not.Null);
            float originalWidth = trail.Trail.Profile.Width;

            board.UpsertOrb(view.OrbId, LabOrbKind.Combined, Vector2.one * .5f,
                element: LabOrbElement.Fire, small: true);
            yield return null;
            Assert.That(view.HitSphere.radius, Is.LessThan(boardConfig.OrbRadius));
            float actualScale = view.HitSphere.radius / boardConfig.ThrowRadius * trail.Size;
            Assert.That(trail.Trail.Profile.Width, Is.EqualTo(authored.Width * actualScale).Within(1e-4f));
            Assert.That(trail.Trail.Profile.MaxLength, Is.EqualTo(authored.MaxLength * actualScale).Within(1e-4f));
            Assert.That(trail.Trail.Profile.Width, Is.LessThan(originalWidth));
            Assert.That(view.Body, Is.SameAs(originalBody));
            Assert.That(authored.Width, Is.EqualTo(authoredWidth), "A board's radius must never edit the shared flight profile.");

            board.UpsertOrb(view.OrbId, LabOrbKind.Combined, Vector2.one * .5f,
                element: LabOrbElement.Fire, small: false);
            yield return null;
            Assert.That(trail.Trail.Profile.Width, Is.EqualTo(originalWidth).Within(1e-4f));
        }

        private LabOrbBoard CreateBoard()
        {
            Object.DestroyImmediate(orb);
            orb = null;
            boardConfig = Object.Instantiate(Resources.Load<LabConfig>("LabConfig"));
            boardRoot = new GameObject("Board trail integration");
            var cameraObject = new GameObject("Board trail camera");
            cameraObject.transform.SetParent(boardRoot.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.enabled = false;
            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(boardRoot.transform, false);
            var board = boardObject.AddComponent<LabOrbBoard>();
            board.Configure(boardConfig, camera, new Rect(-4f, -2f, 8f, 4f));
            board.SetInteractionEnabled(false);
            return board;
        }
    }
}
