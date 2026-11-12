using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>
    /// #507: each stage's saved battle gourd is what baking its art prefab produces now (re-bake in memory and compare),
    /// and the bake meets its geometric checks. Re-run `Hapio > Gourds > Bake All Stage Gourds` when this fails after an
    /// art update.
    /// </summary>
    [TestFixture(1)]
    [TestFixture(2)]
    [TestFixture(3)]
    public sealed class GameGourdRigBakeTests
    {
        private readonly GameGourdRigBake.Spec spec;
        private string Prefab => spec.PrefabPath;
        /// <summary>Triangles in the battle gourd. Stage 2 bakes the 100K re-export (#526, about 71k).</summary>
        private const int TriangleBudget = 90000;

        private GameGourdRigBake.Result result;

        public GameGourdRigBakeTests(int stage) { spec = GameGourdRigBake.For(stage); }

        [OneTimeSetUp]
        public void Bake() => result = GameGourdRigBake.Compute(spec);

        [OneTimeTearDown]
        public void Release() { result?.Destroy(); result = null; }

        [Test]
        public void TheSavedMeshesAreTheCurrentBake()
        {
            AssertSame(result.PaintShell, spec.Generated("PaintShell.asset"));
            AssertSame(result.LidPaint, spec.Generated("LidPaint.asset"));
            AssertSame(result.HitShell, spec.Generated("HitShell.asset"));
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(spec.Generated("HitShell.asset")).isReadable, Is.True,
                "The runtime MeshCollider needs a readable hit shell.");
        }

        [Test]
        public void TheComputedAndSavedLidNormalsAreFiniteUnitAndIdentical()
        {
            string path = spec.Generated("LidPaint.asset");
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Assert.That(saved, Is.Not.Null, path);
            Vector3[] bakedNormals = result.LidPaint.normals, savedNormals = saved.normals;
            Assert.That(bakedNormals.Length, Is.EqualTo(result.LidPaint.vertexCount), "Every computed lid vertex needs a normal.");
            Assert.That(savedNormals.Length, Is.EqualTo(saved.vertexCount), "Every saved lid vertex needs a normal.");
            Assert.That(savedNormals.Length, Is.EqualTo(bakedNormals.Length), path + " normal count");
            int invalidBaked = InvalidNormalCount(bakedNormals), invalidSaved = InvalidNormalCount(savedNormals);
            Assert.That(invalidBaked + invalidSaved, Is.Zero,
                path + " requires finite, nonzero unit normals: computed invalid=" + invalidBaked + ", saved invalid=" + invalidSaved);
            float worst = 0f;
            for (int i = 0; i < bakedNormals.Length; i++)
                worst = Mathf.Max(worst, Vector3.Distance(bakedNormals[i], savedNormals[i]));
            Assert.That(worst, Is.LessThan(1e-5f), path + " normals must match the current bake.");
        }

        private static int InvalidNormalCount(Vector3[] normals)
        {
            int invalid = 0;
            foreach (Vector3 normal in normals)
                if (float.IsNaN(normal.x) || float.IsInfinity(normal.x)
                    || float.IsNaN(normal.y) || float.IsInfinity(normal.y)
                    || float.IsNaN(normal.z) || float.IsInfinity(normal.z)
                    || Mathf.Abs(normal.sqrMagnitude - 1f) > 1e-4f) invalid++;
            return invalid;
        }

        [Test]
        public void TheSavedTimelineIsTheCurrentBakeInOrderWithExistingPaths()
        {
            var profile = AssetDatabase.LoadAssetAtPath<LabGourdYokaiProfile>(spec.Generated("YokaiProfile.asset"));
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.IsValid(out string reason), Is.True, reason);
            Assert.That(profile.lidOpensAt, Is.EqualTo(result.LidOpensAt).Within(1e-5f));
            Assert.That(profile.risenAt, Is.EqualTo(result.RisenAt).Within(1e-5f));
            Assert.That(profile.lookFrom, Is.EqualTo(result.LookFrom).Within(1e-5f));
            Assert.That(profile.lookTo, Is.EqualTo(result.LookTo).Within(1e-5f));
            Assert.That(profile.eyesBackAt, Is.EqualTo(result.EyesBackAt).Within(1e-5f));
            Assert.That(profile.sinkFrom, Is.EqualTo(result.SinkFrom).Within(1e-5f));
            Assert.That(profile.sunkAt, Is.EqualTo(result.SunkAt).Within(1e-5f));
            Assert.That(profile.lidClosedAt, Is.EqualTo(result.LidClosedAt).Within(1e-5f));
            Assert.That(profile.clipLength, Is.EqualTo(result.ClipLength).Within(1e-5f));
            Assert.That(profile.idleLoop, Is.False);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(spec.Generated("YokaiPeek.anim"));
            Assert.That(clip.legacy, Is.True);
            Assert.That(clip.length, Is.EqualTo(profile.clipLength).Within(1e-4f));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var rig = prefab.GetComponent<LabGourdVisualRig>();
            Assert.That(rig.yokaiAnimation.clip, Is.SameAs(clip));
            Assert.That(rig.yokaiProfile, Is.SameAs(profile));
            Transform model = rig.yokaiAnimation.transform;
            Assert.That(model.Find(profile.bodyPath), Is.Not.Null, "The flinch target exists.");
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                Assert.That(model.Find(binding.path), Is.Not.Null, "Every curve path exists: " + binding.path);
            // The clip's own lid matches the source performance at each extracted time.
            AnimationCurve saved = GameGourdRigBake.Curve(clip, result.LidHingePath, "m_LocalRotation.x");
            Assert.That(saved.Evaluate(profile.lidOpensAt), Is.EqualTo(saved.Evaluate(0f)).Within(1e-3f), "Closed until it opens.");
            Assert.That(saved.Evaluate(profile.lidClosedAt), Is.EqualTo(saved.Evaluate(0f)).Within(1e-3f), "Closed again.");
            Assert.That(Mathf.Abs(saved.Evaluate(profile.risenAt) - saved.Evaluate(0f)), Is.GreaterThan(.2f), "Open when risen.");
        }

        [Test]
        public void TheBakePassesItsGeometryChecks()
        {
            TestContext.WriteLine(result.Log.ToString());
            Assert.That(result.StarShapedRatio, Is.GreaterThanOrEqualTo(.99f), "Paint directions map one-to-one onto the shell.");
            Assert.That(result.OuterTriangles, Is.LessThanOrEqualTo(result.BodyTriangles), "Only the outer wall of the body.");
            Assert.That(result.OuterTriangles, Is.GreaterThan(result.BodyTriangles / 3));
            Assert.That(result.HitMeanDeviation, Is.LessThan(.03f), "The hit shell hugs the painted surface.");
            Assert.That(result.HitMaxDeviation, Is.LessThan(.08f));
            Assert.That(result.HitShell.triangles.Length / 3, Is.LessThanOrEqualTo(2400));
            Assert.That(result.Scale, Is.InRange(.5f, 2f), "Scaled to the white gourd's in-battle height.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var rig = prefab.GetComponent<LabGourdVisualRig>();
            Assert.That(rig.hitShell.transform.parent, Is.SameAs(rig.paintShell.transform));
            for (Transform t = rig.hitShell.transform; t != null; t = t.parent)
                Assert.That(t.GetComponent<Animation>() == null && t.GetComponent<Animator>() == null, Is.True,
                    "No animation above the hit shell: " + t.name);
            Matrix4x4 shell = rig.paintShell.transform.localToWorldMatrix, lid = rig.paintExtras[0].transform.localToWorldMatrix;
            for (int i = 0; i < 16; i++)
                Assert.That(lid[i], Is.EqualTo(shell[i]).Within(1e-4f), "The closed lid paints in the body's paint space.");
            Assert.That(rig.paintShell.sharedMaterial.IsKeywordEnabled("_UNDERPAINT_MAP"), Is.True);
            Assert.That(rig.paintExtras[0].sharedMaterial.IsKeywordEnabled("_UNDERPAINT_MAP"), Is.True);
            Assert.That(Resources.Load<Material>("GourdPaint/WhiteGourd").IsKeywordEnabled("_UNDERPAINT_MAP"), Is.False,
                "The white gourd keeps the original shader variant.");

            int triangles = 0, renderers = 0;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                renderers++;
                for (int s = 0; s < mesh.subMeshCount; s++) triangles += (int)mesh.GetIndexCount(s) / 3;
            }
            TestContext.WriteLine("renderers=" + renderers + " triangles=" + triangles);
            Assert.That(triangles, Is.LessThanOrEqualTo(TriangleBudget));
        }

        private static void AssertSame(Mesh baked, string path)
        {
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Assert.That(saved, Is.Not.Null, path);
            Vector3[] a = baked.vertices, b = saved.vertices;
            Assert.That(b.Length, Is.EqualTo(a.Length), path + " vertex count");
            float worst = 0f; int at = -1;
            for (int i = 0; i < a.Length; i++)
            {
                float distance = Vector3.Distance(a[i], b[i]);
                if (distance > worst) { worst = distance; at = i; }
            }
            Assert.That(worst, Is.LessThan(1e-5f), path + " vertex " + at);
            Assert.That(saved.triangles, Is.EqualTo(baked.triangles), path + " triangles");
        }
    }
}
