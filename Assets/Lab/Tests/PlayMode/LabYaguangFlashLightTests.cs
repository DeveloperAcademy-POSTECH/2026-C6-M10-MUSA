using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    [Category("GPU")]
    public sealed class LabYaguangFlashLightTests
    {
        [UnityTest]
        public IEnumerator UsesNativeHdrBloomAndReleasesItsPrivateCamera()
        {
            var canvas = new GameObject("HDR flash test", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var root = new GameObject("Light", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(349f, 765f);
            GameObject rig = null;
            try
            {
                Canvas.ForceUpdateCanvases();
                var light = root.AddComponent<LabYaguangFlashLight>();
                light.Present(1f, -1f, 0f);
                Assert.That(light.Ready, Is.True);
                rig = light.PrivateRig;
                var camera = rig.GetComponentInChildren<Camera>();
                Assert.That(camera.allowHDR, Is.True);
                Assert.That(camera.targetTexture.format, Is.EqualTo(RenderTextureFormat.ARGBHalf));
                Assert.That(camera.cullingMask, Is.EqualTo(1 << 31));
                Assert.That(root.GetComponent<RawImage>().raycastTarget, Is.False);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var bloom = typeof(LabYaguangFlashLight).GetField("bloom", flags).GetValue(light);
                Assert.That(bloom.GetType().FullName, Is.EqualTo("UnityEngine.Rendering.Universal.Bloom"));
                // URP's existing LDR grading caps its final output; verify HDR before grading,
                // then verify real Bloom adds light outside the emitter after post-processing.
                MonoBehaviour cameraData = null;
                foreach (var component in camera.GetComponents<MonoBehaviour>())
                    if (component.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData") cameraData = component;
                Assert.That(cameraData, Is.Not.Null);
                var post = cameraData.GetType().GetProperty("renderPostProcessing");
                post.SetValue(cameraData, false);
                yield return null;
                yield return null;
                var source = Read(light.LightTexture);
                Assert.That(source[0].maxColorComponent, Is.GreaterThan(1f), "The emitter produces actual HDR radiance before grading.");
                post.SetValue(cameraData, true);
                yield return null;
                yield return null;
                var bloomed = Read(light.LightTexture);
                Assert.That(bloomed[0].r, Is.GreaterThan(.9f), "The emitter reaches white.");
                Assert.That(bloomed[1].r, Is.GreaterThan(source[1].r + .01f), "Native Bloom spreads light beyond the emitter.");
                Assert.That(bloomed[0].g, Is.GreaterThan(bloomed[0].r * .85f), "White light, not a yellow sheet.");
                root.SetActive(false);
                Assert.That(camera.enabled, Is.False);
                Assert.That(light.FlashAlpha, Is.Zero);
            }
            finally
            {
                Object.Destroy(root); Object.Destroy(canvas);
            }
            yield return null;
            yield return null;
            Assert.That(rig == null, Is.True, "No off-screen camera or volume survives the HUD.");
        }
        [UnityTest]
        public IEnumerator TuningChangesHdrColorIntensityAndAreaWithoutMutatingCaller()
        {
            var root = new GameObject("Tuned light", typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 766);
            try
            {
                var light = root.AddComponent<LabYaguangFlashLight>();
                var tuning = new LabYaguangFlashTuning { sourceArea = .15f, flashRadiance = 4f,
                    coreColor = Color.red, glowColor = Color.black, flashBloom = 0f };
                light.Configure(tuning);
                tuning.coreColor = Color.blue;
                light.Present(0f, 0f, 0f);
                var camera = light.PrivateRig.GetComponentInChildren<Camera>();
                foreach (var component in camera.GetComponents<MonoBehaviour>())
                    if (component.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData")
                        component.GetType().GetProperty("renderPostProcessing").SetValue(component, false);
                yield return null; yield return null;
                Color first = Pixel(light.LightTexture, .5f, .5f);
                Assert.That(first.r, Is.GreaterThan(3f));
                Assert.That(first.b, Is.LessThan(.01f), "Configure owns a copy of the caller's settings.");
                Assert.That(Pixel(light.LightTexture, .5f, .8f).maxColorComponent, Is.LessThan(.01f));
                tuning.flashRadiance = 12f; tuning.sourceArea = 1f;
                light.Configure(tuning); light.Present(0f, 0f, 0f);
                yield return null; yield return null;
                Color second = Pixel(light.LightTexture, .5f, .8f);
                Assert.That(second.b, Is.GreaterThan(10f), "New color, radiance and area reach the native HDR renderer.");
                Assert.That(second.r, Is.LessThan(.01f));
                light.Present(0f, 0f, 0f, 0, true);
                yield return null; yield return null;
                Assert.That(Pixel(light.LightTexture, .03f, .03f).b, Is.GreaterThan(10f),
                    "The whole screen, including corners, flashes on its first frame.");
                light.Present(0f, .5f, 0f, 0, true);
                yield return null; yield return null;
                float decayed = Pixel(light.LightTexture, .03f, .03f).b;
                Assert.That(decayed, Is.InRange(2.5f, 3.5f), "The whole-screen flash fades with a visible tail.");

            }
            finally { Object.Destroy(root); }
            yield return null;
        }
        [TestCase(0)]
        [TestCase(5)]
        [TestCase(9)]
        [TestCase(10)]
        public void ScreenCompositeFadesLinearlyWithRecognizedShakes(int shakes)
        {
            var shader = Resources.Load<Shader>("Yaguang/Light/Composite");
            var material = new Material(shader);
            var target = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBFloat);
            var previous = RenderTexture.active;
            try
            {
                target.Create(); RenderTexture.active = target;
                GL.Clear(false, true, new Color(.4f, .4f, .4f));
                material.SetFloat("_Aspect", 1f); material.SetFloat("_LightLimit", .22f);
                material.SetFloat("_Visibility", 1f - shakes / 10f);
                Graphics.Blit(Texture2D.whiteTexture, target, material);
                var pixel = Pixel(target, .5f, .5f);
                Assert.That(pixel.r, Is.EqualTo(.4f + .132f * (1f - shakes / 10f)).Within(.005f), "Each shake removes its share of the final light, including at HDR saturation.");
                Assert.That(pixel.r, Is.LessThan(.6f));
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release(); Object.Destroy(target); Object.Destroy(material);
            }
        }
        private static Color Pixel(RenderTexture target, float x, float y)
        {
            var previous = RenderTexture.active;
            var sample = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            try
            {
                RenderTexture.active = target;
                sample.ReadPixels(new Rect((int)(target.width * x), (int)(target.height * y), 1, 1), 0, 0);
                sample.Apply(); return sample.GetPixel(0, 0);
            }
            finally { RenderTexture.active = previous; Object.Destroy(sample); }
        }
        private static Color[] Read(RenderTexture target)
        {
            var previous = RenderTexture.active;
            Texture2D sample = null;
            try
            {
                RenderTexture.active = target;
                sample = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
                sample.ReadPixels(new Rect(0, 0, sample.width, sample.height), 0, 0); sample.Apply();
                return new[] { sample.GetPixel(sample.width / 2, (int)(sample.height * .97f)), sample.GetPixel(sample.width / 2, (int)(sample.height * .77f)) };
            }
            finally { RenderTexture.active = previous; if (sample != null) Object.Destroy(sample); }
        }
    }
}
