using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    public sealed class LabGourdPaintTests
    {
        [UnityTearDown]
        public IEnumerator UnloadLabScene()
        {
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            Scene cleanup = SceneManager.CreateScene("Gourd paint test cleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator MainSceneUsesWhitePaintGourdAndApprovedHitsPaintTheSurface()
        {
            Assert.That(LabGourdPaint.EnabledInMainScene, Is.True);
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint, Is.Not.Null, "The ordinary build Scene must mount the paint gourd without a separate Scene.");
            Assert.That(paint.Shell, Is.Not.Null);
            Assert.That(paint.target.MountedVisual, Is.Not.Null);
            Assert.That(paint.Shell.IsChildOf(paint.target.MountedVisual.transform), Is.True,
                "The crack gourd must not replace the white paint gourd.");
            foreach (var crack in Object.FindObjectsByType<LabGourdCrack>(FindObjectsSortMode.None))
                if (crack.target == paint.target) Assert.That(crack.enabled, Is.False);
            Assert.That(paint.PaintCount, Is.Zero);

            var check = new MaterialPropertyBlock();
            var renderer = paint.Shell.GetComponent<Renderer>();
            paint.target.NotifyApprovedHit(new Vector3(0f, 0f, .5f), LabOrbElement.Fire);
            Assert.That(paint.PaintCount, Is.EqualTo(1));
            paint.target.NotifyApprovedHit(new Vector3(0f, 0f, .5f), LabOrbElement.None);
            Assert.That(paint.PaintCount, Is.EqualTo(1), "A hit without an element does not paint.");

            // 회전·흔들림에도 자국은 박 표면 기준 좌표로 남는다.
            renderer.GetPropertyBlock(check);
            Vector4 first = check.GetVectorArray("_PaintPositions")[0];
            paint.target.VisualMount.Rotate(0f, 25f, 0f);
            renderer.GetPropertyBlock(check);
            Assert.That(check.GetVectorArray("_PaintPositions")[0], Is.EqualTo(first));

            Assert.That(paint.Paint(new Vector3(float.NaN, 0f, 0f), Color.red), Is.False);
            paint.ClearPaint();
            Assert.That(paint.PaintCount, Is.Zero);
            for (int i = 0; i < LabGourdPaint.Capacity; i++) Assert.That(paint.Paint(Vector3.forward, Color.red), Is.True);
            Assert.That(paint.Paint(Vector3.forward, Color.blue), Is.False, "The shader keeps at most 128 marks.");
            paint.ClearPaint();
        }
    }
}
