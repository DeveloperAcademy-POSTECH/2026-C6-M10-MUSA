using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>
    /// #507 PR-2: the saved Stage 2 battle gourd is what baking the art prefab produces now (re-bake in memory and compare),
    /// and the bake meets its geometric checks. Re-run `Hapio > Gourds > Bake Stage 2 Yagwanggwi Gourd` when this fails
    /// after an art update.
    /// </summary>
    public sealed class GameGourdRigBakeTests
    {
        private const string Generated = "Assets/Hapio/Art/Gourds/Stage02_Yagwanggwi/Generated/";
        private const string Prefab = "Assets/Hapio/Resources/Hapio/Gourds/Stage02_Yagwanggwi_Battle.prefab";
        /// <summary>Before the lighter art re-export. Lower to 60k when it arrives.</summary>
        private const int TriangleBudget = 320000;

        private static GameGourdRigBake.Result result;

        [OneTimeSetUp]
        public void Bake() => result = GameGourdRigBake.Compute();

        [OneTimeTearDown]
        public void Release() { result?.Destroy(); result = null; }

        [Test]
        public void TheSavedMeshesAreTheCurrentBake()
        {
            AssertSame(result.PaintShell, Generated + "Stage02_PaintShell.asset");
            AssertSame(result.LidPaint, Generated + "Stage02_LidPaint.asset");
            AssertSame(result.HitShell, Generated + "Stage02_HitShell.asset");
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(Generated + "Stage02_HitShell.asset").isReadable, Is.True,
                "The runtime MeshCollider needs a readable hit shell.");
        }

        [Test]
        public void TheSavedTimelineIsTheCurrentBakeInOrderWithExistingPaths()
        {
            var profile = AssetDatabase.LoadAssetAtPath<LabGourdYokaiProfile>(Generated + "Stage02_YokaiProfile.asset");
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

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Generated + "Stage02_YokaiPeek.anim");
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
            AnimationCurve saved = GameGourdRigBake.Curve(clip, GameGourdRigBake.LidHingePath, "m_LocalRotation.x");
            Assert.That(saved.Evaluate(profile.lidOpensAt), Is.EqualTo(saved.Evaluate(0f)).Within(1e-3f), "Closed until it opens.");
            Assert.That(saved.Evaluate(profile.lidClosedAt), Is.EqualTo(saved.Evaluate(0f)).Within(1e-3f), "Closed again.");
            Assert.That(Mathf.Abs(saved.Evaluate(profile.risenAt) - saved.Evaluate(0f)), Is.GreaterThan(.2f), "Open when risen.");
        }

        [Test]
        public void TheBakePassesItsGeometryChecks()
        {
            TestContext.WriteLine(result.Log.ToString());
            Assert.That(result.StarShapedRatio, Is.GreaterThanOrEqualTo(.99f), "Paint directions map one-to-one onto the shell.");
            Assert.That(result.OuterTriangles, Is.LessThan(result.BodyTriangles), "Only the outer wall of the double-walled body.");
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
            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
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
