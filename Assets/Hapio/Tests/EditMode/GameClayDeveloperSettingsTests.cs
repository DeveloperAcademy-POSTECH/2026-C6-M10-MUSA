using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    public sealed class GameClayDeveloperSettingsTests
    {
        [Test]
        public void ClayLivesInCommonAndSurvivesFreshStageProfilesWithoutChangingOtherValues()
        {
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            Assert.That(source, Is.Not.Null);
            float[] saved = source.CaptureDeveloperValues();
            using (var settings = new GameStageSettings(source, GameStageCatalog.RequireDefault()))
            {
                float[][] before = { settings.Capture(0), settings.Capture(1), settings.Capture(2), settings.Capture(3) };
                float[] edited = settings.Capture(0);
                edited[LabConfig.ClayThicknessField] = .019f; edited[LabConfig.ClayGlossField] = .9f;
                Assert.That(settings.TryApply(0, edited, out string error), Is.True, error);
                for (int scope = 0; scope <= 3; scope++)
                {
                    float[] current = settings.Capture(scope);
                    Assert.That(current[LabConfig.ClayThicknessField], Is.EqualTo(.019f));
                    Assert.That(current[LabConfig.ClayGlossField], Is.EqualTo(.9f));
                    Assert.That(current.Take(257), Is.EqualTo(before[scope].Take(257)));
                }
                Assert.That(GameStageSettings.IsFieldVisible(0, 257), Is.True);
                Assert.That(GameStageSettings.IsFieldVisible(0, 258), Is.True);
                Assert.That(GameStageSettings.IsFieldVisible(1, 257), Is.False);
                Assert.That(GameStageSettings.IsFieldVisible(GameStageSettings.TerritoryScope, 257), Is.False);
            }
            Assert.That(source.CaptureDeveloperValues(), Is.EqualTo(saved));
        }
    }
}
