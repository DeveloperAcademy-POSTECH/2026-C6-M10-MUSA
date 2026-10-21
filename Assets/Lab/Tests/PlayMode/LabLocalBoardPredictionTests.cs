using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>Real board bodies with the approval intentionally withheld; no network timing assumptions.</summary>
    public sealed class LabLocalBoardPredictionTests
    {
        private GameObject root;
        private LabConfig config;
        private LabOrbBoard board;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Local board prediction tests");
            config = ScriptableObject.CreateInstance<LabConfig>();
            var cameraObject = new GameObject("Input camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(root.transform, false);
            board = boardObject.AddComponent<LabOrbBoard>();
            board.Configure(config, camera, new Rect(-4f, -2f, 8f, 4f));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnapprovedCombinationMovesLocallyAndOldSnapshotsCannotResurrectItsInputs()
        {
            AddPair();
            Assert.That(board.TryCreateCombinePreview("preview", "yin", "yang", new Vector2(.5f, .5f),
                new Vector2(.3f, 0f)), Is.True);
            LabOrbView preview = View("preview");
            Assert.That(preview.Element, Is.EqualTo(LabOrbElement.None), "The Host has not drawn an element yet.");
            Assert.That(preview.Body.isKinematic, Is.False);
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(board.IsProvisional("preview"), Is.True);
            Assert.That(board.ShouldPreserveFromSnapshot("preview"), Is.True);
            foreach (string id in new[] { "yin", "yang" })
            {
                Assert.That(board.IsSnapshotSuppressed(id), Is.True);
                LabOrbView material = View(id);
                board.UpsertOrb(id, material.Kind, Vector2.one * .5f, Vector2.one);
                Assert.That(material.gameObject.activeSelf, Is.False);
                Assert.That(material.Body.detectCollisions, Is.False);
            }
            float before = preview.Body.position.x;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(preview.Body.position.x, Is.GreaterThan(before), "No approval is supplied in this test.");
        }

        [Test]
        public void RejectedCombinationRestoresItsOriginalInputsAndSavedMotion()
        {
            LabOrbView yin = board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.4f, .4f), new Vector2(.2f, .1f));
            LabOrbView yang = board.UpsertOrb("yang", LabOrbKind.Yang, new Vector2(.6f, .6f), new Vector2(-.1f, .3f));
            Vector3 yinPosition = yin.Body.position, yangPosition = yang.Body.position;
            Vector3 yinVelocity = yin.Body.linearVelocity, yangVelocity = yang.Body.linearVelocity;
            Assert.That(board.TryCreateCombinePreview("preview", "yin", "yang", Vector2.one * .5f, Vector2.zero), Is.True);
            Assert.That(board.TryBeginDragAtWorld(Vector2.zero, 1d), Is.True);
            Assert.That(board.DragToWorld(new Vector2(1f, .5f), 1.1d), Is.True);

            Assert.That(board.RejectCombinePreview("preview"), Is.True);
            Assert.That(board.RejectCombinePreview("preview"), Is.False);
            Assert.That(board.Count, Is.EqualTo(2));
            Assert.That(board.IsProvisional("preview"), Is.False);
            Assert.That(yin.gameObject.activeSelf && yang.gameObject.activeSelf, Is.True);
            Assert.That(Vector3.Distance(yin.Body.position, yinPosition), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(yang.Body.position, yangPosition), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(yin.Body.linearVelocity, yinVelocity), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(yang.Body.linearVelocity, yangVelocity), Is.LessThan(.00001f));
            Assert.That(yin.Body.detectCollisions && yang.Body.detectCollisions, Is.True);
        }

        [Test]
        public void ApprovalPromotesTheSameMovingBodyWithoutApplyingStaleHostMotion()
        {
            AddPair();
            Assert.That(board.TryCreateCombinePreview("preview", "yin", "yang", Vector2.one * .5f,
                new Vector2(.25f, .1f)), Is.True);
            LabOrbView preview = View("preview");
            preview.Body.position = new Vector3(1f, .4f, 0f);
            Vector3 velocity = preview.Body.linearVelocity;
            Assert.That(board.CommitCombinePreview("preview", Approved()), Is.True);

            Assert.That(View("committed"), Is.SameAs(preview));
            Assert.That(preview.Body.position, Is.EqualTo(new Vector3(1f, .4f, 0f)));
            Assert.That(preview.Body.linearVelocity, Is.EqualTo(velocity));
            Assert.That(preview.Element, Is.EqualTo(LabOrbElement.Fire));
            Assert.That(board.IsProvisional("committed"), Is.False);
            Assert.That(board.TryGetMotion("yin", out _, out _), Is.False);
            Assert.That(board.TryGetMotion("yang", out _, out _), Is.False);
            Assert.That(board.Count, Is.EqualTo(1));
        }

        [Test]
        public void ApprovalDuringAHeldGestureKeepsTheFingerAttachedToTheRenamedBody()
        {
            AddPair();
            Assert.That(board.TryCreateCombinePreview("preview", "yin", "yang", Vector2.one * .5f, Vector2.zero), Is.True);
            LabOrbView preview = View("preview");
            Assert.That(board.TryBeginDragAtWorld(Vector2.zero, 1d), Is.True);
            Assert.That(board.DragToWorld(new Vector2(.8f, .3f), 1.1d), Is.True);
            Assert.That(board.CommitCombinePreview("preview", Approved()), Is.True);
            Assert.That(preview.Body.isKinematic, Is.True);
            Assert.That(board.DragToWorld(new Vector2(1.4f, .6f), 1.2d), Is.True);
            Assert.That(preview.Body.position, Is.EqualTo(new Vector3(1.4f, .6f, 0f)));
            board.CancelDrag();
            Assert.That(preview.Body.isKinematic, Is.False);
        }

        [Test]
        public void ApprovalAfterARecoverySnapshotKeepsOnlyThePredictedVisibleBody()
        {
            AddPair();
            Assert.That(board.TryCreateCombinePreview("preview", "yin", "yang", Vector2.one * .5f, Vector2.zero), Is.True);
            LabOrbView predicted = View("preview");
            LabOrbView fromSnapshot = board.UpsertOrb("committed", LabOrbKind.Combined, Vector2.one * .2f,
                element: LabOrbElement.Fire);
            Assert.That(board.CommitCombinePreview("preview", Approved()), Is.True);
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(predicted.OrbId, Is.EqualTo("committed"));
            Assert.That(predicted.gameObject.activeSelf, Is.True);
            Assert.That(fromSnapshot.gameObject.activeSelf, Is.False);
            Assert.That(board.GetComponentsInChildren<LabOrbView>(true)
                .Count(view => view.OrbId == "committed" && view.gameObject.activeSelf), Is.EqualTo(1));
        }

        [Test]
        public void ARejectedPreviewNeverRecreatesAnInputAlreadyRemovedByAuthority()
        {
            AddPair();
            Assert.That(board.TryCreateCombinePreview("preview", "yin", "yang", Vector2.one * .5f, Vector2.zero), Is.True);
            Assert.That(board.RemoveOrb("yin"), Is.True);
            Assert.That(board.RejectCombinePreview("preview"), Is.True);
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(board.TryGetMotion("yin", out _, out _), Is.False);
            Assert.That(View("yang").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void DefinitivePreviewRemovalAlsoDiscardsItsHiddenRetainedInputs()
        {
            AddPair();
            Assert.That(board.TryCreateCombinePreview("preview", "yin", "yang", Vector2.one * .5f, Vector2.zero), Is.True);
            Assert.That(board.BeginPendingLaunch("preview"), Is.True);
            Assert.That(board.RemoveOrb("preview"), Is.True);
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.TryGetMotion("yin", out _, out _), Is.False);
            Assert.That(board.TryGetMotion("yang", out _, out _), Is.False);
            Assert.That(board.RejectCombinePreview("preview"), Is.False);
            Assert.That(board.RejectPendingLaunch("preview"), Is.False);
        }

        [Test]
        public void AProvisionalCombinationRetainsKnownSmallSizeWithoutInventingAnElement()
        {
            board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.4f, .5f), small: true);
            board.UpsertOrb("yang", LabOrbKind.Yang, new Vector2(.6f, .5f));
            Assert.That(board.TryCreateCombinePreview("preview", "yin", "yang", Vector2.one * .5f, Vector2.zero), Is.True);
            Assert.That(board.TryGetLocalAppearance("preview", out LabOrbElement element, out bool small), Is.True);
            Assert.That(small, Is.True);
            Assert.That(element, Is.EqualTo(LabOrbElement.None));
            string[] beforeRemoval = board.OrbIds;
            Assert.That(beforeRemoval, Is.EquivalentTo(new[] { "yin", "yang", "preview" }));
            foreach (string id in beforeRemoval) board.RemoveOrb(id);
            Assert.That(board.OrbIds, Is.Empty, "Reconciliation enumerates a copy, so removal is safe.");
        }

        [Test]
        public void LegacyRejectionByInputIdRestoresBothCombinationMaterials()
        {
            AddPair();
            Assert.That(board.TryCreateCombinePreview("preview", "yin", "yang", Vector2.one * .5f, Vector2.zero), Is.True);
            Assert.That(board.RejectEdge("yin"), Is.True);
            Assert.That(board.Count, Is.EqualTo(2));
            Assert.That(board.IsSnapshotSuppressed("yin") || board.IsSnapshotSuppressed("yang"), Is.False);
        }

        [Test]
        public void AProvisionalCombinationCanRequestItsThrowBeforeTheCombinationReply()
        {
            AddPair();
            Assert.That(board.TryCreateCombinePreview("preview", "yin", "yang", new Vector2(.5f, .3f), Vector2.zero), Is.True);
            string thrownId = null;
            board.ThrowRequested += gesture =>
            {
                thrownId = gesture.OrbId;
                Assert.That(board.BeginPendingLaunch(gesture.OrbId), Is.True);
            };
            Assert.That(board.TryBeginDragAtWorld(new Vector2(0f, -.8f), 1d), Is.True);
            Assert.That(board.EndDragAtWorld(new Vector2(0f, .9f), 1.1d), Is.True);
            Assert.That(thrownId, Is.EqualTo("preview"));
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.IsSnapshotSuppressed("preview"), Is.True);

            Assert.That(board.CommitCombinePreview("preview", Approved()), Is.True);
            Assert.That(board.IsSnapshotSuppressed("committed"), Is.True);
            Assert.That(board.RejectPendingLaunch("committed"), Is.True);
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(View("committed").Element, Is.EqualTo(LabOrbElement.Fire));
            Assert.That(View("committed").Body.isKinematic, Is.False);
        }

        [Test]
        public void ARejectedLaunchRestoresTheBodyButACommittedLaunchRemovesItExactlyOnce()
        {
            LabOrbView orb = board.UpsertOrb("orb", LabOrbKind.Combined, new Vector2(.5f, .4f),
                new Vector2(.2f, .1f), element: LabOrbElement.Water);
            Vector3 position = orb.Body.position, velocity = orb.Body.linearVelocity;
            Assert.That(board.BeginPendingLaunch("orb"), Is.True);
            Assert.That(board.BeginPendingLaunch("orb"), Is.False);
            Assert.That(board.Count, Is.Zero);
            board.UpsertOrb("orb", LabOrbKind.Combined, Vector2.zero, element: LabOrbElement.Water);
            Assert.That(orb.gameObject.activeSelf, Is.False);
            Assert.That(board.RejectPendingLaunch("orb"), Is.True);
            Assert.That(orb.gameObject.activeSelf, Is.True);
            Assert.That(Vector3.Distance(orb.Body.position, position), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(orb.Body.linearVelocity, velocity), Is.LessThan(.00001f));
            Assert.That(board.BeginPendingLaunch("orb"), Is.True);
            Assert.That(board.CommitPendingLaunch("orb"), Is.True);
            Assert.That(board.CommitPendingLaunch("orb"), Is.False);
            Assert.That(board.TryGetMotion("orb", out _, out _), Is.False);
            Assert.That(board.RejectPendingLaunch("orb"), Is.False);
        }

        [Test]
        public void PendingGenerationIsOnlyANeutralMarkerAndClearDropsEveryProvisionalState()
        {
            Assert.That(board.TryCreateGenerationPreview("draw", Vector2.one * .5f), Is.True);
            Assert.That(board.TryCreateGenerationPreview("draw", Vector2.one * .5f), Is.False);
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.GetComponentsInChildren<LabOrbView>(true), Is.Empty);
            LineRenderer ring = board.GetComponentInChildren<LineRenderer>();
            Assert.That(ring, Is.Not.Null);
            Assert.That(ring.GetComponent<Collider>(), Is.Null);
            Assert.That(board.IsProvisional("draw"), Is.True);
            AddPair();
            Assert.That(board.TryCreateCombinePreview("preview", "yin", "yang", Vector2.one * .5f, Vector2.zero), Is.True);
            Assert.That(board.BeginPendingLaunch("preview"), Is.True);
            board.Clear();
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.IsProvisional("preview"), Is.False);
            Assert.That(board.IsProvisional("draw"), Is.False);
            Assert.That(board.RejectPendingLaunch("preview"), Is.False);
            Assert.That(board.RejectCombinePreview("preview"), Is.False);
        }

        private void AddPair()
        {
            board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.4f, .5f));
            board.UpsertOrb("yang", LabOrbKind.Yang, new Vector2(.6f, .5f));
        }

        private LabOrbView View(string id) => board.GetComponentsInChildren<LabOrbView>(true)
            .Single(view => view.OrbId == id);

        private static LabOrbState Approved() => new LabOrbState
        {
            id = "committed", owner = 1ul, kind = LabOrbKind.Combined, element = LabOrbElement.Fire,
            x = .1f, y = .1f, vx = -.1f, vy = -.1f
        };
    }
}
