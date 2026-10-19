using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    public sealed class LabElementTrailRenderTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Scene lab = SceneManager.GetSceneByName("Lab");
            if (!lab.IsValid() || !lab.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Element trail render cleanup"));
            yield return SceneManager.UnloadSceneAsync(lab);
        }

        [UnityTest]
        public IEnumerator ActualShortFlightsRenderInkForAllFiveElementsAndReleaseComparisonResources()
        {
            if (!Application.isEditor || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Editor GPU required: no actual trail pixels were checked.");
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a =>
                a.GetType("C6Lab.Editor.LabElementTrailComparisonSession")).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "The opt-in production comparison tool must be available.");
            MethodInfo start = type.GetMethod("StartVolley"), render = type.GetMethod("RenderFrames");
            MethodInfo read = type.GetMethod("ReadContactSheet");
            string evidence = Path.Combine(Application.dataPath, "..", "Builds", "Validation", "ElementTrails");
            Directory.CreateDirectory(evidence);
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabSeatCamera seat = Object.FindFirstObjectByType<LabSeatCamera>();
            Assert.That(seat != null && seat.battleCamera != null, Is.True);
            foreach (int view in new[] { 0, 1, 2, 3 })
            {
                bool side = view == 1;
                bool shortFlight = view != 3;
                bool closeUp = view >= 2;
                IDisposable session = null;
                Texture2D sheet = null, ink = null;
                try
                {
                    session = (IDisposable)Activator.CreateInstance(type,
                        new object[] { seat.config, seat.battleCamera, side, shortFlight, closeUp });
                    start.Invoke(session, null);
                    float began = Time.time;
                    while (Time.time - began < (shortFlight ? .14f : .34f)) yield return null;
                    // Let the production LateUpdate record the displayed, interpolated position.
                    yield return null;
                    var shots = Object.FindObjectsByType<LabProjectile>(FindObjectsSortMode.None)
                        .Where(p => p.gameObject.scene.name.StartsWith("Element flight comparison ", StringComparison.Ordinal)).ToArray();
                    Assert.That(shots.Length, Is.EqualTo(5), "The short real flights must still be alive at capture.");
                    foreach (LabProjectile shot in shots)
                    {
                        Assert.That(shot.ElementFlightTrail.PathCount, Is.GreaterThanOrEqualTo(2));
                        Assert.That(shot.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude, Is.GreaterThan(0f));
                    }
                    render.Invoke(session, null);
                    sheet = (Texture2D)read.Invoke(session, null);
                    string file = side ? "five-elements-side-short.png"
                        : closeUp ? "five-elements-front-closeup-" + (shortFlight ? "short" : "long") + ".png"
                        : "five-elements-front-short.png";
                    File.WriteAllBytes(Path.Combine(evidence, file),
                        sheet.EncodeToPNG());
                    // Check actual trail pixels separately from the coloured orb body.
                    foreach (LabProjectile shot in shots) shot.VisualMount.gameObject.SetActive(false);
                    render.Invoke(session, null);
                    ink = (Texture2D)read.Invoke(session, null);
                    Color32[] pixels = ink.GetPixels32();
                    int width = ink.width / 5;
                    for (int element = 0; element < 5; element++)
                    {
                        int colored = 0, magenta = 0;
                        for (int y = 0; y < ink.height; y++)
                            for (int x = element * width; x < (element + 1) * width; x++)
                            {
                                Color32 c = pixels[y * ink.width + x];
                                if (Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b)) > 28)
                                    colored++;
                                if (c.r > 220 && c.b > 180 && c.g < 60) magenta++;
                            }
                        Assert.That(colored, Is.GreaterThan(8), $"Element {element + 1}, view={view}: no readable coloured ink trail.");
                        Assert.That(magenta, Is.LessThan(8), "A missing URP shader must not pass as an effect.");
                        TestContext.WriteLine($"ELEMENT_TRAIL_GPU view={view} element={element + 1} inkPixels={colored}");
                    }
                }
                finally
                {
                    if (sheet != null) Object.Destroy(sheet);
                    if (ink != null) Object.Destroy(ink);
                    session?.Dispose();
                }
                yield return null;
                Assert.That(Object.FindObjectsByType<LabProjectile>(FindObjectsSortMode.None)
                    .Any(p => p.gameObject.scene.name.StartsWith("Element flight comparison ", StringComparison.Ordinal)), Is.False);
            }
        }
    }
}
