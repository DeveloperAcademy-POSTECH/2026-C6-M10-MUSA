#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hapio.Experiments.Tests
{
    public sealed class ClayPreviewShapeMaskTests
    {
        [Test]
        public void ImportedAlphaKeepsAspectHoleDropletAndDoesNotUseRgbAsCoverage()
        {
            byte[] red = FixturePng(Color.red), blue = FixturePng(Color.blue);
            using (var a = ClayPreviewShapeMask.LoadBytes(red, "red.png"))
            using (var b = ClayPreviewShapeMask.LoadBytes(blue, "blue.png"))
            {
                Assert.That(a.Succeeded, Is.True, a.Error);
                Assert.That(b.Succeeded, Is.True, b.Error);
                Assert.That(a.Brush.width, Is.EqualTo(512)); Assert.That(a.Brush.height, Is.EqualTo(512));
                Assert.That(a.SourceWidth, Is.EqualTo(64)); Assert.That(a.SourceHeight, Is.EqualTo(32));
                CollectionAssert.AreEqual(a.Brush.GetPixels32(), b.Brush.GetPixels32(), "Changing RGB cannot change the silhouette or bevel.");
                var pixels = a.Brush.GetPixels32();
                Assert.That(pixels.Where((_, i) => i % 512 < 32 || i % 512 >= 480 || i / 512 < 32 || i / 512 >= 480).All(c => c.a == 0), Is.True,
                    "The documented input canvas retains at least 32 transparent pixels around the source canvas.");
                Assert.That(At(a.Brush, 14, 15).r, Is.GreaterThan(240), "The opaque body remains.");
                Assert.That(At(a.Brush, 31, 15).a, Is.Zero, "Do not fill the authored inner hole.");
                Assert.That(At(a.Brush, 60, 26).a, Is.GreaterThan(240), "A detached droplet is retained.");
                Assert.That(At(a.Brush, 14, 15).b, Is.GreaterThan(0), "The body has rounded relief data.");
                Assert.That(pixels.All(c => c.r == c.a), Is.True, "Coverage channels remain compatible with the actual compositor.");
                CollectionAssert.AreEqual(red, a.SourceBytes);
                Assert.That(a.SourceSha256, Is.Not.EqualTo(b.SourceSha256), "Profile identity includes the real source bytes.");
            }
        }

        [Test]
        public void MissingAlphaEmptySilhouetteAndBrokenDataAreRejectedWithoutReleasingAValidMask()
        {
            using (var valid = ClayPreviewShapeMask.LoadBytes(FixturePng(Color.red), "valid.png"))
            {
                Assert.That(valid.Succeeded, Is.True, valid.Error); var held = valid.Brush;
                foreach (byte[] data in new[] { FilledPng(Color.white), FilledPng(Color.clear), new byte[] { 1, 2, 3 }, Array.Empty<byte>() })
                using (var invalid = ClayPreviewShapeMask.LoadBytes(data, "invalid.png"))
                {
                    Assert.That(invalid.Succeeded, Is.False); Assert.That(invalid.Error, Is.Not.Null.And.Not.Empty);
                    Assert.That(invalid.Brush, Is.Null); Assert.That(valid.Brush, Is.SameAs(held));
                }
                using (var invalid = ClayPreviewShapeMask.LoadBytes(new byte[16 * 1024 * 1024 + 1], "oversized.png"))
                    Assert.That(invalid.Succeeded, Is.False);
            }
        }

        [Test]
        public void TinyCornerFeatureLostDuring512PixelFitIsRejectedInsteadOfPublishingAnEmptyMask()
        {
            var texture = new Texture2D(2048, 2048, TextureFormat.RGBA32, false);
            byte[] png;
            try
            {
                var pixels = new Color32[2048 * 2048];
                pixels[0] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels); texture.Apply(); png = texture.EncodeToPNG();
            }
            finally { Object.DestroyImmediate(texture); }
            using (var mask = ClayPreviewShapeMask.LoadBytes(png, "single-corner-pixel.png"))
            {
                Assert.That(mask.Succeeded, Is.False,
                    "A valid source PNG must not succeed when its only feature vanishes from the 512-pixel fitted mask.");
                Assert.That(mask.Error, Does.Contain("512").And.Contain("threshold"));
                Assert.That(mask.Brush, Is.Null); Assert.That(mask.Preview, Is.Null);
            }
        }

        [Test]
        public void SavedProfileEmbedsItsSourceAndAllShapeSettingsAndRestoresWithoutTheOriginalFile()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "clay-shape-profile-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "profile.json"), source = Path.Combine(directory, "source.png");
            var before = JsonUtility.ToJson(UnityEngine.Random.state);
            try
            {
                File.WriteAllBytes(source, FixturePng(Color.red));
                var profile = new ClayPreviewShapeProfile();
                using (var mask = ClayPreviewShapeMask.LoadFile(source))
                {
                    Assert.That(mask.Succeeded, Is.True, mask.Error); profile.SetImage(mask);
                }
                profile.source = ClayPreviewShapeSource.Image; profile.seed = 572; profile.core = .1625f;
                profile.spikes = 9; profile.length = .21f; profile.width = .048f; profile.roundness = .85f;
                profile.irregularity = 1; profile.asymmetry = 1; profile.droplets = 4;
                profile.dropletSize = .04f; profile.dropletDistance = .1438f;
                Assert.That(ClayPreviewShapeProfile.TrySave(path, profile, out string error), Is.True, error);
                File.Delete(source);
                Assert.That(ClayPreviewShapeProfile.TryLoad(path, out var loaded, out error), Is.True, error);
                Assert.That(JsonUtility.ToJson(loaded), Is.EqualTo(JsonUtility.ToJson(profile)));
                Assert.That(loaded.TryGetImageBytes(out byte[] restored, out error), Is.True, error);
                CollectionAssert.AreEqual(FixturePng(Color.red), restored);
                using (var mask = ClayPreviewShapeMask.LoadBytes(restored, loaded.imageFileName))
                    Assert.That(mask.Succeeded, Is.True, mask.Error);
                Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(before), "Preview/profile operations do not use the game RNG.");
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void TamperedProfileAndInvalidValuesCannotOverwriteTheLastValidProfile()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "clay-shape-profile-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, "profile.json");
            try
            {
                var profile = new ClayPreviewShapeProfile();
                using (var image = ClayPreviewShapeMask.LoadBytes(FixturePng(Color.red), "source.png")) profile.SetImage(image);
                profile.source = ClayPreviewShapeSource.Image;
                Assert.That(ClayPreviewShapeProfile.TrySave(path, profile, out string error), Is.True, error);
                byte[] lastGood = File.ReadAllBytes(path); profile.imageSha256 = new string('0', 64);
                Assert.That(profile.Validate(out error), Is.False);
                Assert.That(ClayPreviewShapeProfile.TrySave(path, profile, out error), Is.False);
                CollectionAssert.AreEqual(lastGood, File.ReadAllBytes(path));
                Assert.That(ClayPreviewShapeProfile.TryLoad(path, out profile, out error), Is.True, error);
                profile.core = float.NaN;
                Assert.That(ClayPreviewShapeProfile.TrySave(path, profile, out error), Is.False);
                CollectionAssert.AreEqual(lastGood, File.ReadAllBytes(path));
            }
            finally { Directory.Delete(directory, true); }
        }

        internal static byte[] FixturePng(Color color)
        {
            var texture = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[64 * 32];
                for (int y = 4; y <= 23; y++) for (int x = 8; x <= 49; x++)
                    if (!(x >= 26 && x <= 37 && y >= 12 && y <= 19)) pixels[y * 64 + x] = color;
                for (int y = 25; y <= 27; y++) for (int x = 59; x <= 61; x++) pixels[y * 64 + x] = color;
                texture.SetPixels32(pixels); texture.Apply(); return texture.EncodeToPNG();
            }
            finally { Object.DestroyImmediate(texture); }
        }
        static byte[] FilledPng(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            try { texture.SetPixels(Enumerable.Repeat(color, 64).ToArray()); texture.Apply(); return texture.EncodeToPNG(); }
            finally { Object.DestroyImmediate(texture); }
        }
        static Color32 At(Texture2D texture, int x, int y) => texture.GetPixel(Mathf.FloorToInt(32 + (x + .5f) * 7), Mathf.FloorToInt(144 + (y + .5f) * 7));
    }
}
#endif
