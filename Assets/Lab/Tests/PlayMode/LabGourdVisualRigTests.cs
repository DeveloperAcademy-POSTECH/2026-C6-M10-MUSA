using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// #507 PR-1: the gourd model contract on a synthetic rig mounted on the saved Lab target. A hit on the hit shell
    /// paints the same direction as the paint shell, extras share the paint binding, the size anchor stays put, the
    /// yokai uses the rig's own animation and timeline, and an invalid rig always falls back to the white gourd.
    /// </summary>
    public sealed class LabGourdVisualRigTests
    {
        private readonly LabSavedSceneNetworkFixture fixture = new LabSavedSceneNetworkFixture();
        private GameObject template;
        private Material material;
        private Mesh bodyMesh, hitMesh, lidMesh;
        private LabGourdYokaiProfile profile;
        private AnimationClip clip;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (Object item in new Object[] { template, material, bodyMesh, hitMesh, lidMesh, profile, clip })
                if (item != null) Object.Destroy(item);
            yield return fixture.Cleanup();
        }

        [UnityTest]
        public IEnumerator AValidRigPaintsItsShellAndExtrasAndCollidesWithItsHitShell()
        {
            LabGourdPaint paint = null;
            yield return MountRig(true, value => paint = value);
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Assert.That(paint.RigFallbackReason, Is.Null);
            LabGourdVisualRig rig = paint.Rig;
            Assert.That(rig, Is.Not.Null);
            Assert.That(paint.VisualInstance.GetComponent<LabGourdVisualRig>(), Is.SameAs(rig));
            Assert.That(paint.Shell, Is.SameAs(rig.paintShell.transform));
            Assert.That(paint.ActivePaintMaterial, Is.SameAs(material));
            Assert.That(rig.paintShell.sharedMaterial, Is.SameAs(material));
            Assert.That(paint.PaintExtras.Count, Is.EqualTo(1));
            Assert.That(paint.PaintExtras[0].sharedMaterial, Is.SameAs(material));

            LabTarget target = paint.target;
            Assert.That(target.Hitbox.sharedMesh, Is.SameAs(rig.hitShell.sharedMesh), "Collides with the hit shell, not the art.");
            Matrix4x4 hitbox = target.Hitbox.transform.localToWorldMatrix, hitShell = rig.hitShell.transform.localToWorldMatrix;
            for (int i = 0; i < 16; i++) Assert.That(hitbox[i], Is.EqualTo(hitShell[i]).Within(1e-4f), "Hitbox pose = hit shell pose.");

            // A hit at a hitbox-local point paints the same direction in the paint shell's space.
            Vector3 local = new Vector3(.3f, .25f, -.2f);
            Vector3 inShell = paint.Shell.InverseTransformPoint(target.Hitbox.transform.TransformPoint(local));
            Assert.That(Vector3.Distance(inShell, local), Is.LessThan(1e-4f));

            paint.RefreshCommittedMap();
            var shellBlock = new MaterialPropertyBlock(); rig.paintShell.GetPropertyBlock(shellBlock);
            var extraBlock = new MaterialPropertyBlock(); paint.PaintExtras[0].GetPropertyBlock(extraBlock);
            Assert.That(shellBlock.GetFloat("_PaintMapReady"), Is.EqualTo(1f));
            Assert.That(extraBlock.GetFloat("_PaintMapReady"), Is.EqualTo(1f), "Extras get the same paint binding.");
            Assert.That(extraBlock.GetTexture("_PaintMap"), Is.SameAs(paint.VisualPaintTexture));
            Assert.That(extraBlock.GetTexture("_PainterMap"), Is.SameAs(paint.VisualPainterTexture));

            var hanging = paint.target.GetComponent<LabHangingTarget>();
            Assert.That(hanging, Is.Not.Null, "The saved Lab target hangs.");
            Assert.That(rig.ropeSocket, Is.Not.Null);
            Assert.That(hanging.RopeSocket, Is.SameAs(rig.ropeSocket), "The rig hands its rope socket to the hanging target.");
            paint.enabled = false;
            Assert.That(hanging.RopeSocket, Is.Null, "Releasing the rig clears it.");
        }

        [UnityTest]
        public IEnumerator TheRigSizeKeepsItsAnchorInPlace()
        {
            LabGourdPaint paint = null;
            yield return MountRig(true, value => paint = value);
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            LabTarget target = paint.target;
            Vector3 anchor = paint.Rig.sizeAnchor.localPosition;
            FieldInfo size = typeof(LabConfig).GetField("gourdVisualSize", BindingFlags.Instance | BindingFlags.NonPublic);
            float original = (float)size.GetValue(paint.network.config);
            try
            {
                foreach (float value in new[] { 1.2f, 2.4f })
                {
                    size.SetValue(paint.network.config, value);
                    paint.RefreshCommittedMap();
                    float ratio = value / paint.Rig.referenceVisualSize;
                    Assert.That(paint.VisualInstance.transform.localScale.x, Is.EqualTo(ratio).Within(1e-5f));
                    Vector3 drawn = target.VisualMount.InverseTransformPoint(paint.Rig.sizeAnchor.position);
                    Assert.That(Vector3.Distance(drawn, anchor), Is.LessThan(1e-4f), "The anchor stays put at size " + value);
                    Assert.That(target.Hitbox.sharedMesh, Is.SameAs(paint.Rig.hitShell.sharedMesh));
                    Assert.That(target.Hitbox.transform.lossyScale.x, Is.EqualTo(paint.Rig.hitShell.transform.lossyScale.x).Within(1e-4f),
                        "The collider follows the resized hit shell.");
                }
            }
            finally { size.SetValue(paint.network.config, original); }
        }

        [UnityTest]
        public IEnumerator AnInvalidRigAlwaysFallsBackToTheWhiteGourd()
        {
            LabGourdPaint paint = null;
            yield return MountRig(false, value => paint = value);
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Assert.That(paint.Rig, Is.Null);
            Assert.That(paint.RigFallbackReason, Does.Contain("identity"));
            Assert.That(paint.Shell.name, Is.EqualTo("White Gourd"));
            Assert.That(paint.ActivePaintMaterial, Is.SameAs(paint.paintMaterial));
            Assert.That(paint.PaintExtras, Is.Empty);
            Assert.That(paint.target.Hitbox.sharedMesh, Is.SameAs(paint.Shell.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(paint.VisualInstance.GetComponent<LabGourdYokai>().Profile, Is.SameAs(LabGourdYokaiProfile.Legacy));
        }

        [UnityTest]
        public IEnumerator TheRigYokaiUsesItsOwnAnimationAndRestsClosedWithoutAnIdleLoop()
        {
            LabGourdPaint paint = null;
            yield return MountRig(true, value => paint = value);
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            var yokai = paint.VisualInstance.GetComponent<LabGourdYokai>();
            Assert.That(yokai, Is.Not.Null);
            Assert.That(yokai.Ready, Is.True);
            Assert.That(yokai.Profile, Is.SameAs(profile));
            yokai.enabled = false;
            Animation player = paint.Rig.yokaiAnimation;
            AnimationState state = player[player.clip.name];
            Assert.That(state.time, Is.EqualTo(profile.lidClosedAt), "Without an idle loop it rests closed.");
            Assert.That(state.speed, Is.Zero);

            var warning = new LabSnapshot { sessionId = "rig", round = 1, phase = LabPhase.Playing, serverTime = 17.1d,
                comboWarningAt = 17d, comboNextAt = 20d };
            yokai.Advance(warning, 17.1d, 0f);
            Assert.That(yokai.Holding, Is.True, "The same triggers as the white gourd.");
            for (int i = 0; i < 40; i++) yokai.Advance(warning, 17.1d + i * .05d, .05f);
            Transform spirit = player.transform.Find(profile.bodyPath);
            Assert.That(spirit.localPosition.y, Is.EqualTo(1f).Within(.01f), "Risen along the rig's own clip.");

            yokai.Advance(new LabSnapshot { sessionId = "rig", round = 1, phase = LabPhase.Playing, serverTime = 30d }, 30d, 0f);
            Assert.That(yokai.Holding, Is.False);
            state.time = profile.lidClosedAt + .2f; // the clip ran past closing between frames
            yokai.Advance(null, 0d, .05f);
            Assert.That(state.time, Is.EqualTo(profile.lidClosedAt));
            Assert.That(state.speed, Is.Zero, "It stops closed instead of looping.");
        }

        private IEnumerator MountRig(bool valid, System.Action<LabGourdPaint> found)
        {
            yield return fixture.Load();
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint, Is.Not.Null);
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            template = BuildRig(valid);
            Assert.That(LabGourdPaint.PrepareVisual(paint.target, template), Is.SameAs(paint));
            // Remount: the saved Scene already attached the white gourd.
            paint.enabled = false;
            paint.enabled = true;
            yield return null;
            found(paint);
        }

        private GameObject BuildRig(bool valid)
        {
            material = new Material(Resources.Load<Material>("GourdPaint/WhiteGourd")) { name = "Synthetic rig paint" };
            bodyMesh = Readable(PrimitiveType.Sphere, "Synthetic body");
            hitMesh = Readable(PrimitiveType.Sphere, "Synthetic hit shell");
            lidMesh = Readable(PrimitiveType.Cube, "Synthetic lid");

            var root = new GameObject("Synthetic Gourd Rig");
            root.transform.position = new Vector3(0f, -1000f, 0f);
            var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, .2f, 0f);
            body.transform.localScale = Vector3.one * 1.6f;
            body.GetComponent<MeshFilter>().sharedMesh = bodyMesh;
            var hit = new GameObject("Hit Shell", typeof(MeshFilter));
            hit.transform.SetParent(body.transform, false);
            if (!valid) hit.transform.localScale = Vector3.one * 1.1f;
            hit.GetComponent<MeshFilter>().sharedMesh = hitMesh;
            var lid = new GameObject("Lid", typeof(MeshFilter), typeof(MeshRenderer));
            lid.transform.SetParent(body.transform, false);
            lid.transform.localPosition = new Vector3(0f, .5f, 0f);
            lid.transform.localScale = Vector3.one * .2f;
            lid.GetComponent<MeshFilter>().sharedMesh = lidMesh;
            var socket = new GameObject("Rope Socket").transform;
            socket.SetParent(root.transform, false);
            socket.localPosition = new Vector3(0f, 1.3f, 0f);
            var anchor = new GameObject("Size Anchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(0f, 1.1f, 0f);

            var yokaiRoot = new GameObject("Yokai");
            yokaiRoot.transform.SetParent(root.transform, false);
            var spirit = new GameObject("Spirit").transform;
            spirit.SetParent(yokaiRoot.transform, false);
            clip = new AnimationClip { legacy = true, name = "Synthetic peek", wrapMode = WrapMode.Loop };
            clip.SetCurve("Spirit", typeof(Transform), "localPosition.y",
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(.2f, 0f), new Keyframe(.6f, 1f),
                    new Keyframe(1.4f, 1f), new Keyframe(1.8f, 0f), new Keyframe(2f, 0f)));
            var animation = yokaiRoot.AddComponent<Animation>();
            animation.AddClip(clip, clip.name);
            animation.clip = clip;
            animation.playAutomatically = false;

            profile = ScriptableObject.CreateInstance<LabGourdYokaiProfile>();
            profile.lidOpensAt = .1f; profile.risenAt = .6f; profile.lookFrom = .7f; profile.lookTo = 1.2f;
            profile.eyesBackAt = 1.3f; profile.sinkFrom = 1.4f; profile.sunkAt = 1.8f; profile.lidClosedAt = 1.9f;
            profile.clipLength = 2f; profile.bodyPath = "Spirit"; profile.hopAxis = Vector3.up; profile.hop = .05f;
            profile.idleLoop = false;

            var rig = root.AddComponent<LabGourdVisualRig>();
            rig.paintShell = body.GetComponent<MeshRenderer>();
            rig.paintExtras = new Renderer[] { lid.GetComponent<MeshRenderer>() };
            rig.hitShell = hit.GetComponent<MeshFilter>();
            rig.paintMaterial = material;
            rig.referenceVisualSize = 1.8f;
            rig.sizeAnchor = anchor;
            rig.ropeSocket = socket;
            rig.yokaiAnimation = animation;
            rig.yokaiProfile = profile;
            Assert.That(rig.Validate(out string reason), Is.EqualTo(valid), reason);
            return root;
        }

        private static Mesh Readable(PrimitiveType shape, string name)
        {
            GameObject primitive = GameObject.CreatePrimitive(shape);
            Mesh copy = Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
            copy.name = name;
            Object.DestroyImmediate(primitive);
            Assert.That(copy.isReadable, Is.True);
            return copy;
        }
    }
}
