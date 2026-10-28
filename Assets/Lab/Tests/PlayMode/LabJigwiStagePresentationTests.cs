using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabJigwiStagePresentationTests
    {
        private GameObject root;
        private LabTarget target;
        private LabJigwiStagePresentation view;
        private LabSnapshot latest;
        private LabJigwiStageSnapshot stage;

        [SetUp]
        public void Prepare()
        {
            root = new GameObject("Jigwi stage surface presentation fixture");
            target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 1801f, 0f), 1.1f, 2.2f);
            var visual = new GameObject("Passive surface marks"); visual.transform.SetParent(root.transform, false);
            view = visual.AddComponent<LabJigwiStagePresentation>();
            stage = new LabJigwiStageSnapshot { enabled = true, startedAt = 0d, cutoffAt = 100d,
                groups = new[] { Group("curtain", 41, 0, LabJigwiStagePattern.Curtain) } };
            latest = new LabSnapshot { round = 1, phase = LabPhase.Playing, serverTime = 11.1d,
                jigwiStage = stage,
                players = new[] { new LabPlayerState { id = 41, seat = 0 },
                    new LabPlayerState { id = 0, seat = 1 }, new LabPlayerState { id = 77, seat = 2 } } };
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [Test]
        public void TheActualPaintFrameIsUsedEvenWithRotatedNonUniformColliderScale()
        {
            Transform frame = target.Hitbox.transform;
            frame.localScale = new Vector3(1.4f, .7f, 2.1f);
            frame.localRotation = Quaternion.Euler(13f, 37f, 0f);
            Physics.SyncTransforms();
            Vector3 direction = new Vector3(.23f, .12f, 1f).normalized;
            Assert.That(LabJigwiStagePresentation.TrySurfacePoint(target, direction, out Vector3 point, out Vector3 normal), Is.True);
            Vector3 actual = frame.InverseTransformPoint(point).normalized;
            Assert.That(Vector3.Angle(actual, direction), Is.LessThan(.05f),
                "The mark must match the same mesh-local direction used by authoritative paint/hit queries.");
            Assert.That(normal.sqrMagnitude, Is.GreaterThan(.9f));
        }

        [Test]
        public void HelperSeesOnlyTheirRootAndRecipientSeesOnlyTheirOwnFiniteFirePatches()
        {
            int collidersBefore = root.GetComponentsInChildren<Collider>(true).Length;
            view.Apply(stage, target, latest, 0, 11.1d, true);
            Assert.That(view.VisibleRoots, Is.EqualTo(1));
            Assert.That(view.VisibleFirePatches, Is.Zero);
            view.Apply(stage, target, latest, 41, 11.1d, true);
            Assert.That(view.VisibleRoots, Is.Zero, "The victim cannot find a self-service source in a curtain.");
            Assert.That(view.VisibleFirePatches, Is.EqualTo(1));
            view.Apply(stage, target, latest, 41, 13.1d, true);
            Assert.That(view.VisibleFirePatches, Is.EqualTo(3));
            view.Apply(stage, target, latest, 77, 13.1d, true);
            Assert.That(view.VisibleRoots, Is.Zero); Assert.That(view.VisibleFirePatches, Is.Zero);
            Assert.That(root.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(collidersBefore),
                "The surface overlays cannot intercept, deflect or judge a projectile.");
        }

        [Test]
        public void MutualCurtainsResolveIndependentlyAndCutoffLeavesNoSourceOrFire()
        {
            LabJigwiStageGroup one = stage.groups[0];
            LabJigwiStageGroup two = Group("reverse", 0, 41, LabJigwiStagePattern.Curtain);
            stage.groups = new[] { one, two };
            view.Apply(stage, target, latest, 0, 11.1d, true);
            Assert.That(view.VisibleRoots, Is.EqualTo(1));
            Assert.That(view.VisibleFirePatches, Is.EqualTo(1));
            one.resolution = LabJigwiStageResolution.Extinguished; one.resolvedAt = 11.2d;
            view.Apply(stage, target, latest, 0, 11.25d, true);
            Assert.That(view.VisibleRoots, Is.Zero);
            Assert.That(view.VisibleFirePatches, Is.EqualTo(1), "The helper's own curtain remains until the other player succeeds.");
            two.resolution = LabJigwiStageResolution.Cancelled; two.resolvedAt = 100d; stage.fever = true;
            view.Apply(stage, target, latest, 0, 100d, true);
            Assert.That(view.VisibleRoots, Is.Zero); Assert.That(view.VisibleFirePatches, Is.Zero);
            Assert.That(view.VisibleGroups, Is.Zero);
        }

        [Test]
        public void TheHudUsesPSeatsRatherThanConnectionIdsAndShowsBothIndependentHelpDirections()
        {
            string helper = LabJigwiHud.DescribeStage(stage, latest, 0, 11.1d);
            string victim = LabJigwiHud.DescribeStage(stage, latest, 41, 11.1d);
            Assert.That(helper, Does.Contain("P1의 불장막"));
            Assert.That(victim, Does.Contain("P2가 도와줄 수 있어요"));
            Assert.That(helper, Does.Not.Contain("P41"));
            stage.groups = new[] { stage.groups[0], Group("reverse", 0, 41, LabJigwiStagePattern.Curtain) };
            string mutual = LabJigwiHud.DescribeStage(stage, latest, 0, 11.1d);
            Assert.That(mutual, Does.Contain("P1가 내 불을 끌 수 있어요"));
            Assert.That(mutual, Does.Contain("P1를 도와주세요"));
            string warning = LabJigwiHud.DescribeStage(stage, latest, 0, 10.5d);
            Assert.That(warning, Does.Contain("선덕여왕 사랑해요!!!"));
        }

        [Test]
        public void CutoffAndResultGuidanceNeverPretendThatNaturalExpiryWasTeammateSuccess()
        {
            var group = stage.groups[0];
            group.resolution = LabJigwiStageResolution.Expired; group.resolvedAt = 18d;
            Assert.That(LabJigwiHud.DescribeStage(stage, latest, 41, 18.1d), Is.Empty);
            group.resolution = LabJigwiStageResolution.Extinguished; group.resolvedAt = 17d;
            Assert.That(LabJigwiHud.DescribeStage(stage, latest, 41, 17.1d), Does.Contain("P2가 불을 꺼줬어요"));
            stage.fever = true;
            Assert.That(LabJigwiHud.DescribeStage(stage, latest, 41, 100d), Does.Contain("자동 회복 2배"));
            latest.phase = LabPhase.Victory;
            Assert.That(LabJigwiHud.DescribeStage(stage, latest, 41, 100d), Is.Empty);
        }

        [Test]
        public void RuntimeGuidanceUsesTheExistingCanvasAndDoesNotBlockAnyTouch()
        {
            var go = new GameObject("Existing battle canvas", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(root.transform, false);
            var canvas = go.GetComponent<Canvas>();
            var hud = go.AddComponent<LabJigwiHud>(); hud.canvas = canvas;
            hud.ApplyStageSnapshot(latest, 0, 11.1d);
            Assert.That(hud.stageNotice.transform.parent, Is.EqualTo(canvas.transform));
            Assert.That(hud.stageNotice.raycastTarget, Is.False);
            Assert.That(hud.stageNotice.rectTransform.anchorMin.y, Is.GreaterThan(.5f), "Keep guidance outside the lower orb board.");
            Assert.That(hud.stageNotice.rectTransform.anchorMax.y, Is.LessThan(.9f), "Leave the upper timer/pause band clear.");
            Assert.That(hud.GetComponentsInChildren<UnityEngine.UI.Button>(true), Is.Empty,
                "Stage guidance adds no new button or modal interaction.");
            latest.phase = LabPhase.Idle;
            hud.ApplyStageSnapshot(latest, 0, 11.1d);
            Assert.That(hud.stageNotice.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void SurfaceFlamesUseTheDedicatedInkMaterialAndItsRealTransparentTexture()
        {
            Material source = Resources.Load<Material>("Jigwi/SurfaceFlame");
            Texture2D wash = Resources.Load<Texture2D>("Jigwi/SurfaceFlameWash");
            Assert.That(source, Is.Not.Null, "The flame material must be a Player-loadable Resource.");
            Assert.That(wash, Is.Not.Null);
            Assert.That(source.shader.name, Is.EqualTo("C6Lab/Jigwi Ink Flame"));
            Assert.That(source.shader.isSupported, Is.True, "The actual render pipeline must support the new flame shader.");
            Assert.That(source.GetTexture("_BaseMap"), Is.SameAs(wash));
            Assert.That(source.renderQueue, Is.GreaterThanOrEqualTo(3000));

            int colliders = root.GetComponentsInChildren<Collider>(true).Length;
            view.Apply(stage, target, latest, 41, 11.1d, true);
            MeshRenderer[] flames = FlameRenderers();
            Assert.That(flames.Length, Is.GreaterThan(0), "The affected player's live surface must use that material, not an unused asset.");
            foreach (MeshRenderer renderer in flames)
            {
                Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"), Is.SameAs(wash));
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv.Any(uv => uv.x < .1f), Is.True);
                Assert.That(mesh.uv.Any(uv => uv.x > .9f), Is.True);
            }
            Assert.That(root.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliders));
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("GPU unavailable: the actual imported texture alpha was not read back.");
            Color32[] pixels = ReadTexture(wash);
            Assert.That(pixels.Count(pixel => pixel.a < 10), Is.GreaterThan(4), "The exported art must have real transparent space, without a rectangular background.");
            Assert.That(pixels.Count(pixel => pixel.a > 180), Is.GreaterThan(4), "The ink/flame strokes must remain visible through their alpha channel.");
        }

        [UnityTest]
        public IEnumerator FlameShaderUsesTheHeldBattleClockAndAdvancesOnlyAfterResume()
        {
            view.Apply(stage, target, latest, 0, 11.1d);
            MeshRenderer renderer = FlameRenderers().Single();
            float started = FlameTime(renderer);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(FlameTime(renderer), Is.GreaterThan(started + .02f));

            latest.paused = true;
            view.Apply(stage, target, latest, 0, 11.4d, true);
            float held = FlameTime(renderer);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(FlameTime(renderer), Is.EqualTo(held).Within(.0001f),
                "Pausing must freeze the shader's supplied game clock, not continue through Unity's global time.");

            latest.paused = false;
            view.Apply(stage, target, latest, 0, 11.4d);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(FlameTime(renderer), Is.GreaterThan(held + .02f));
            stage.fever = true;
            stage.groups[0].resolution = LabJigwiStageResolution.Cancelled;
            stage.groups[0].resolvedAt = 100d;
            view.Apply(stage, target, latest, 0, 100d, true);
            Assert.That(FlameRenderers(), Is.Empty, "The new animated art must not survive the authoritative cutoff.");
        }

        private MeshRenderer[] FlameRenderers() => root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy
                && renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name == "C6Lab/Jigwi Ink Flame")
            .ToArray();

        private static float FlameTime(Renderer renderer)
        {
            var values = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(values);
            return values.GetFloat("_FlameTime");
        }

        private static Color32[] ReadTexture(Texture source)
        {
            var capture = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, capture);
                RenderTexture.active = capture;
                readback.ReadPixels(new Rect(0, 0, capture.width, capture.height), 0, 0);
                readback.Apply(false, false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                capture.Release();
                Object.DestroyImmediate(capture);
                Object.DestroyImmediate(readback);
            }
        }

        private static LabJigwiStageGroup Group(string id, ulong affected, ulong helper, LabJigwiStagePattern pattern)
            => new LabJigwiStageGroup { id = id, slot = 1, pattern = pattern,
                affectedPlayer = affected, helperPlayer = helper, warningAt = 10d, activeAt = 11d,
                expiresAt = 18d, rootDirection = Vector3.forward,
                patchDirections = new[] { Vector3.forward, new Vector3(.22f, .03f, 1f).normalized,
                    new Vector3(-.2f, .12f, 1f).normalized },
                rootAngleDegrees = 18f, fireAngleDegrees = 30f, spreadSeconds = 1f, geometryConfigured = true };
    }
}
