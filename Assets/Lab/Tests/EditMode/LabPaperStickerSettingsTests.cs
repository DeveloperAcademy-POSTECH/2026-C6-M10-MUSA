using System;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabPaperStickerSettingsTests
    {
        [Test]
        public void LegacyConstructionAndApprovedSavedDefaultRemainDistinct()
        {
            var legacy = new LabWatercolorStampSettings();
            Assert.That(legacy.mode, Is.EqualTo(LabPaintStampMode.Watercolor));
            Assert.That(legacy.UsesPaperStickers, Is.False);
            Assert.That(legacy.Version, Does.StartWith(LabWatercolorStampSettings.StyleVersion + "/"));
            var paper = new LabWatercolorStampSettings { mode = LabPaintStampMode.PaperSticker };
            Assert.That(paper.UsesPaperStickers, Is.True);
            Assert.That(paper.UsesClayStickers, Is.False);
            Assert.That(paper.Version, Does.StartWith(LabWatercolorStampSettings.PaperStickerStyleVersion + "/"));
            var current = LabWatercolorStampSettings.CaptureDefault();
            Assert.That(current.mode, Is.EqualTo(LabPaintStampMode.ClaySticker));
            Assert.That(current.UsesPaperStickers, Is.False);
            Assert.That(current.UsesClayStickers, Is.True);
            Assert.That(current.Version, Does.StartWith(LabClayStickerSettings.StyleVersion + "/"));
            Assert.That(current.Version, Is.Not.EqualTo(paper.Version));
            Assert.That(current.revealSeconds, Is.Zero, "The opaque sticker default must not cross-fade lower paint through a new sticker.");
            Assert.That(current.Version, Is.Not.EqualTo(legacy.Version));
            Assert.DoesNotThrow(current.Validate);
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(265), "The two clay controls append after the existing 257 slots; retained paper parameters stay in their profile.");
        }

        [Test]
        public void CapturedPaperSettingsOwnTheirNestedCopyAndFingerprint()
        {
            var source = new LabWatercolorStampSettings { mode = LabPaintStampMode.PaperSticker };
            var captured = source.Clone();
            string version = captured.Version;
            int fingerprint = captured.Fingerprint;
            source.paperSticker.grainStrength = .3f;
            source.paperSticker.edgeStrength = .3f;
            source.paperSticker.grainScale = 8f;
            Assert.That(source.Fingerprint, Is.Not.EqualTo(fingerprint));
            Assert.That(captured.Version, Is.EqualTo(version));
            Assert.That(captured.Clone().Version, Is.EqualTo(version));
            Assert.That(captured.SanitizedCopy().Version, Is.EqualTo(version));
            foreach (string field in new[] { "grainStrength", "edgeStrength", "grainScale" }) {
                var changed = captured.Clone();
                if (field == "grainStrength") changed.paperSticker.grainStrength = .3f;
                else if (field == "edgeStrength") changed.paperSticker.edgeStrength = .3f;
                else changed.paperSticker.grainScale = 8f;
                Assert.That(changed.Version, Is.Not.EqualTo(version), field);
                var serialized = JsonUtility.FromJson<LabWatercolorStampSettings>(JsonUtility.ToJson(changed));
                Assert.That(serialized.Fingerprint, Is.EqualTo(changed.Fingerprint));
                Assert.That(serialized.mode, Is.EqualTo(LabPaintStampMode.PaperSticker));
            }
            Assert.That(captured.Fingerprint, Is.EqualTo(fingerprint));
        }
    }
}
