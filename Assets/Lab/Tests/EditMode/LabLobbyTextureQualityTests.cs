using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabLobbyTextureQualityTests
    {
        [Test]
        public void ImportedArtworkRetainsEverySourcePixelOnEditorAndIPhone()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Lab/Art/Lobby" });
            Assert.That(guids.Length, Is.GreaterThan(10));
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture.width, Is.EqualTo(width), path + " was resized horizontally.");
                Assert.That(texture.height, Is.EqualTo(height), path + " was resized vertically.");
                Assert.That(texture.format, Is.EqualTo(TextureFormat.RGBA32), path + " has lossy GPU compression.");
                Assert.That(texture.mipmapCount, Is.EqualTo(1), path + " must not select a smaller mip.");
                foreach (string platform in new[] { "Standalone", "iPhone" })
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    Assert.That(settings.overridden, Is.True, path + " / " + platform);
                    Assert.That(settings.format, Is.EqualTo(TextureImporterFormat.RGBA32), path + " / " + platform);
                    Assert.That(settings.maxTextureSize, Is.GreaterThanOrEqualTo(Mathf.Max(width, height)), path);
                }
            }
        }

        [TestCase("Assets/Lab/Art/Fonts/ShillaCultureBold.otf")]
        [TestCase("Assets/Lab/Art/Fonts/Shilla_Culture(M).otf")]
        public void FontsRemainDynamicForNativeResolutionGlyphRendering(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            Assert.That(font, Is.Not.Null);
            Assert.That(font.dynamic, Is.True, "A fixed-size font atlas would blur on a 3x display.");
        }
    }
}
