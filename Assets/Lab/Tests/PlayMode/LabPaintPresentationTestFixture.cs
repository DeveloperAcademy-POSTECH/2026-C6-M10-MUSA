using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Explicit presentation choices for synthetic-map tests, separate from real round recording.</summary>
    internal static class LabPaintPresentationTestFixture
    {
        public static void SelectRetainedStyleForSyntheticMap(LabNetwork network)
        {
            // These fixtures deliberately manufacture RGBA/P maps to isolate material and
            // texture ownership. They have no historical strokes to invent. Keep testing
            // the retained comparison path; real recorded default-viewer flow is covered
            // by LabPaintDryingPlayModeTests and exact stamp pixels by the runtime suite.
            LabWatercolorStampSettings retained = LabWatercolorStampSettings.CaptureDefault();
            retained.enabled = false;
            typeof(LabNetwork).GetField("pinnedWatercolorSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(network, retained);
            Assert.That(network.RequiresPaintArt, Is.False);
        }

        public static IEnumerator SettleRecordedArtwork(LabNetwork network, LabGourdPaint paint)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5d;
            while (!paint.CompletionVisualReady && Time.realtimeSinceStartupAsDouble < deadline)
            {
                paint.RefreshCommittedMap();
                yield return null;
            }
            Assert.That(network.PaintArtJournal, Is.Not.Null);
            Assert.That(network.PaintArtJournal.IsSealed, Is.True);
            Assert.That(network.PaintArtError, Is.Null);
            Assert.That(network.PaintArtCompletionReady, Is.True, "Recorded operations and the final map must match the completed GPU image.");
            Assert.That(network.PinnedWatercolorSettings.UsesClayStickers ? paint.ClayStickersActive : paint.WatercolorStampsActive, Is.True, "This fixture must exercise the actual new default, not a fallback.");
            Assert.That(paint.CompletionVisualReady, Is.True, paint.WatercolorStampError);
        }
    }
}
