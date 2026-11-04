using NUnit.Framework;

namespace C6Lab.Tests
{
    /// <summary>The 지귀 overlay is built ahead only where it can play (#493).</summary>
    public sealed class LabJigwiOverlayStagePolicyTests
    {
        [Test]
        public void OnlyTheJigwiStageOfTheGameAndEveryLabRoundBuildTheOverlayAhead()
        {
            Assert.That(LabJigwiHud.WantsScreenPresentation(true, LabStageYokai.Jigwi), Is.True);
            Assert.That(LabJigwiHud.WantsScreenPresentation(true, LabStageYokai.Gumiho), Is.False);
            Assert.That(LabJigwiHud.WantsScreenPresentation(true, LabStageYokai.Yaguang), Is.False);
            Assert.That(LabJigwiHud.WantsScreenPresentation(true, LabStageYokai.None), Is.False);
            foreach (LabStageYokai yokai in new[] { LabStageYokai.None, LabStageYokai.Gumiho, LabStageYokai.Yaguang, LabStageYokai.Jigwi })
                Assert.That(LabJigwiHud.WantsScreenPresentation(false, yokai), Is.True, "Lab keeps its manual 지귀 experiments ready.");
        }
    }
}
