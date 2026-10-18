using System;
using System.Collections.Generic;
using System.Linq;
using C6Lab.Experiments;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace C6Lab.Tests
{
    // This is an isolated visual study. These fixtures do not create a battle,
    // authorize a hit, or claim that the production Host paint map was changed.
    public sealed class LabPaintStyleStudyTests
    {
        private const string AssetRoot = "Assets/Lab/Art/GourdPaint/Experiments/StyleComparison/";

        [Test]
        public void ComparisonUsesTheFiveSrgbColorsSampledFromTheUserPaletteInStampOrder()
        {
            // Orange, yellow, blue, green and brown: these are authoring sRGB values,
            // before conversion into the comparison's linear-data paint texture.
            var expected = new[]
            {
                new Color32(216, 87, 54, 255), new Color32(248, 183, 42, 255),
                new Color32(20, 113, 167, 255), new Color32(61, 130, 88, 255),
                new Color32(111, 74, 50, 255)
            };
            Assert.That(Enumerable.Range(0, expected.Length).Select(LabPaintStyleStudy.PaletteColor).ToArray(),
                Is.EqualTo(expected));
        }

        [TestCase(-1)]
        [TestCase(5)]
        public void PaletteIndexOutsideTheFiveAuthoredColorsFailsExplicitly(int index)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LabPaintStyleStudy.PaletteColor(index));
        }

        [Test]
        public void MaskCopiesInputAndBilinearlySamplesTheFootprint()
        {
            var pixels = new[]
            {
                new Color32(0, 0, 0, 255), new Color32(255, 255, 255, 255),
                new Color32(0, 0, 0, 255), new Color32(255, 255, 255, 255)
            };
            var mask = new LabPaintStudyMask(2, 2, pixels);
            float before = mask.Sample(.5f, .5f);
            Assert.That(before, Is.EqualTo(.5f).Within(.01f), "A midpoint must blend rather than select one texel.");
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
            Assert.That(mask.Sample(.5f, .5f), Is.EqualTo(before).Within(.000001f),
                "A caller editing its pixel array must not mutate the stored footprint.");
        }

        [Test]
        public void TransparentMaskTexelsCannotContributePaint()
        {
            var invisibleWhite = new Color32(255, 255, 255, 0);
            Assert.Throws<ArgumentException>(() => new LabPaintStudyMask(2, 2,
                Enumerable.Repeat(invisibleWhite, 4).ToArray()));
        }

        [TestCase(-.01f, .5f)]
        [TestCase(1.01f, .5f)]
        [TestCase(.5f, -.01f)]
        [TestCase(.5f, 1.01f)]
        public void MaskOutsideItsRectangleDoesNotRepeatOrClampEdgePaint(float u, float v)
        {
            var mask = new LabPaintStudyMask(2, 2,
                Enumerable.Repeat(new Color32(255, 255, 255, 255), 4).ToArray());
            Assert.That(mask.Sample(u, v), Is.Zero);
        }

        [TestCase(0, 8)]
        [TestCase(-1, 8)]
        [TestCase(8, 0)]
        [TestCase(8, -1)]
        public void InvalidMaskDimensionsFailExplicitly(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LabPaintStudyMask(width, height, new Color32[64]));
        }

        [Test]
        public void MissingMismatchedAndEmptyMaskDataCannotProduceASilentComparison()
        {
            Assert.Throws<ArgumentNullException>(() => new LabPaintStudyMask(8, 8, null));
            Assert.Throws<ArgumentException>(() => new LabPaintStudyMask(8, 8, new Color32[63]));
            Assert.Throws<ArgumentException>(() => new LabPaintStudyMask(8, 8, new Color32[64]));
        }

        [TestCase(LabPaintStudyStyle.Broad)]
        [TestCase(LabPaintStudyStyle.Splatter)]
        [TestCase(LabPaintStudyStyle.Flow)]
        public void ComparisonStagesSpendTheSameExactAreaAndCompletionLeavesNoTransparentCells(LabPaintStudyStyle style)
        {
            const int columns = 64, rows = 32;
            LabPaintStudyMask mask = AsymmetricMask();
            LabPaintStudyMap single = LabPaintStyleStudy.Build(mask, style, LabPaintStudyStage.Single, columns, rows, 200u);
            LabPaintStudyMap multicolor = LabPaintStyleStudy.Build(mask, style, LabPaintStudyStage.Multicolor, columns, rows, 200u);
            LabPaintStudyMap complete = LabPaintStyleStudy.Build(mask, style, LabPaintStudyStage.Complete, columns, rows, 200u);
            AssertMap(single, columns, rows, (int)Math.Floor(columns * rows * .06d));
            AssertMap(multicolor, columns, rows, (int)Math.Floor(columns * rows * .18d));
            AssertMap(complete, columns, rows, columns * rows);
            Assert.That(complete.Coverage, Is.EqualTo(1f).Within(.000001f));
            Assert.That(complete.Pixels.All(pixel => pixel.a == 255), Is.True,
                "A decorative texture must not make a completed surface look unpainted.");
        }

        [TestCase(LabPaintStudyStyle.Broad)]
        [TestCase(LabPaintStudyStyle.Splatter)]
        [TestCase(LabPaintStudyStyle.Flow)]
        public void IdenticalInputsAreDeterministicAndBuildingAnotherStyleCannotMutateAnEarlierMap(LabPaintStudyStyle style)
        {
            LabPaintStudyMask mask = AsymmetricMask();
            LabPaintStudyMap first = LabPaintStyleStudy.Build(mask, style, LabPaintStudyStage.Multicolor, 64, 32, 207u);
            Color32[] original = first.Pixels.ToArray();
            LabPaintStyleStudy.Build(mask, (LabPaintStudyStyle)(((int)style + 1) % 3), LabPaintStudyStage.Single, 32, 16, 901u);
            LabPaintStudyMap repeated = LabPaintStyleStudy.Build(mask, style, LabPaintStudyStage.Multicolor, 64, 32, 207u);
            Assert.That(repeated.Pixels, Is.EqualTo(original));
            Assert.That(first.Pixels, Is.EqualTo(original));
            Assert.That(repeated.CoveredCount, Is.EqualTo(first.CoveredCount));
        }

        [Test]
        public void EachStyleChangesGeometryRatherThanOnlyRelabelingTheSameImage()
        {
            LabPaintStudyMask mask = AsymmetricMask();
            var hashes = new HashSet<string>();
            foreach (LabPaintStudyStyle style in Enum.GetValues(typeof(LabPaintStudyStyle)))
            {
                LabPaintStudyMap map = LabPaintStyleStudy.Build(mask, style, LabPaintStudyStage.Multicolor, 128, 64, 200u);
                AssertMap(map, 128, 64, (int)Math.Floor(128 * 64 * .18d));
                hashes.Add(LabPaintPublisher.Hash(map.Pixels));
            }
            Assert.That(hashes.Count, Is.EqualTo(3), "All three panels need a different actual paint map.");
        }

        [TestCase(0, 32)]
        [TestCase(64, 0)]
        [TestCase(-1, 32)]
        [TestCase(64, -1)]
        public void InvalidOutputDimensionsCannotReturnAPartialMap(int columns, int rows)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LabPaintStyleStudy.Build(
                AsymmetricMask(), LabPaintStudyStyle.Broad, LabPaintStudyStage.Single, columns, rows, 200u));
        }

        [TestCase("Broad.png")]
        [TestCase("Splatter.png")]
        [TestCase("Flow.png")]
        public void AuthoredMasksHaveReadableLinearUncompressedDataAndBlackMargins(string filename)
        {
            string path = AssetRoot + filename;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.isReadable, Is.True, "The standalone comparison samples these mask pixels on the CPU.");
            Assert.That(importer.sRGBTexture, Is.False, "A footprint is linear scalar data, not a color photograph.");
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.mipmapEnabled, Is.False);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.GreaterThanOrEqualTo(512));
            Assert.That(texture.height, Is.GreaterThanOrEqualTo(512));
            Color32[] pixels = texture.GetPixels32();
            Assert.That(pixels.All(pixel => Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)) - Math.Min(pixel.r, Math.Min(pixel.g, pixel.b)) <= 16), Is.True,
                path + " must be near-monochrome luminance data rather than baked element colors. AI source edges may differ by up to 16/255 between channels; the builder explicitly samples luminance.");
            int visible = pixels.Count(pixel => pixel.r > 0 && pixel.a > 0);
            Assert.That(visible, Is.GreaterThan(0), "The file must contain an actual footprint.");
            Assert.That(visible, Is.LessThan(pixels.Length), "The file must leave a surrounding black area.");
            for (int x = 0; x < texture.width; x++)
            {
                AssertBlack(pixels[x], path);
                AssertBlack(pixels[(texture.height - 1) * texture.width + x], path);
            }
            for (int y = 0; y < texture.height; y++)
            {
                AssertBlack(pixels[y * texture.width], path);
                AssertBlack(pixels[y * texture.width + texture.width - 1], path);
            }
        }

        [Test]
        public void ActualThreeMasksProduceDistinctMapsAtTheSameAreaAndCompleteTheirEntireSurface()
        {
            var hashes = new HashSet<string>();
            foreach (LabPaintStudyStyle style in Enum.GetValues(typeof(LabPaintStudyStyle)))
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot + style + ".png");
                Assert.That(texture, Is.Not.Null, style.ToString());
                var mask = new LabPaintStudyMask(texture.width, texture.height, texture.GetPixels32());
                LabPaintStudyMap map = LabPaintStyleStudy.Build(mask, style, LabPaintStudyStage.Multicolor, 256, 128, 200u);
                AssertMap(map, 256, 128, (int)Math.Floor(256 * 128 * .18d));
                hashes.Add(LabPaintPublisher.Hash(map.Pixels));
                LabPaintStudyMap complete = LabPaintStyleStudy.Build(mask, style, LabPaintStudyStage.Complete, 256, 128, 200u);
                AssertMap(complete, 256, 128, 256 * 128);
            }
            Assert.That(hashes.Count, Is.EqualTo(3));
        }

        private static LabPaintStudyMask AsymmetricMask()
        {
            const int width = 32, height = 32;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = (x + .5f) / width, v = (y + .5f) / height;
                bool main = Mathf.Pow((u - .43f) / .25f, 2) + Mathf.Pow((v - .58f) / .24f, 2) < 1f;
                bool lobe = Mathf.Pow((u - .66f) / .17f, 2) + Mathf.Pow((v - .51f) / .15f, 2) < 1f;
                bool tail = u > .4f && u < .49f && v > .12f && v < .51f;
                byte value = main || lobe || tail ? (byte)255 : (byte)0;
                pixels[y * width + x] = new Color32(value, value, value, 255);
            }
            return new LabPaintStudyMask(width, height, pixels);
        }

        private static void AssertMap(LabPaintStudyMap map, int columns, int rows, int expectedCovered)
        {
            Assert.That(map, Is.Not.Null);
            Assert.That(map.Columns, Is.EqualTo(columns));
            Assert.That(map.Rows, Is.EqualTo(rows));
            Assert.That(map.Pixels, Has.Length.EqualTo(columns * rows));
            Assert.That(map.CoveredCount, Is.EqualTo(expectedCovered));
            Assert.That(map.Pixels.Count(pixel => pixel.a == 255), Is.EqualTo(expectedCovered));
            Assert.That(map.Coverage, Is.EqualTo((float)expectedCovered / (columns * rows)).Within(.000001f));
            Assert.That(double.IsNaN(map.Coverage) || double.IsInfinity(map.Coverage), Is.False);
            Assert.That(map.Pixels.All(pixel => pixel.a == 0 || pixel.a == 255), Is.True,
                "Logical coverage must be binary.");
            Assert.That(map.Pixels.Where(pixel => pixel.a == 0).All(pixel => pixel.r == 0 && pixel.g == 0 && pixel.b == 0),
                Is.True, "Uncovered texels must not leak leftover paint color.");
        }

        private static void AssertBlack(Color32 pixel, string path)
        {
            float luma = (.2126f * pixel.r + .7152f * pixel.g + .0722f * pixel.b) * pixel.a / 65025f;
            Assert.That(luma, Is.LessThan(LabPaintStudyMask.FootprintThreshold),
                path + " must not clip its visible footprint at the edge. Sub-threshold source background noise is not ink.");
        }
    }
}
