using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hapio.Tests
{
    public sealed class GameBattleUiAssetTests
    {
        [Test]
        public void ThemeContainsAllStageAndTeamVariantsAndDoesNotReplaceTheLabAppearance()
        {
            var theme=Resources.Load<GameBattleUiTheme>(GameBattleUiTheme.ResourcePath);
            Assert.That(theme,Is.Not.Null);
            Assert.That(theme.boardTextures,Has.Length.EqualTo(3));
            foreach(var texture in theme.boardTextures) Assert.That(texture,Is.Not.Null);
            foreach(var art in theme.teamPositionArtwork) Assert.That(art,Is.Not.Null);
            Assert.That(theme.teamPositionArtwork,Has.Length.EqualTo(4));
            Assert.That(theme.underlineDark,Is.Not.Null); Assert.That(theme.underlineLight,Is.Not.Null);
            Assert.That(theme.tickDark,Is.Not.Null); Assert.That(theme.tickLight,Is.Not.Null);
            Assert.That(theme.playerBadges,Has.Length.EqualTo(6)); Assert.That(theme.minimapBadges,Has.Length.EqualTo(6));
            var source=AssetDatabase.LoadAssetAtPath<LabConfig>("Assets/Lab/Resources/LabConfig.asset");
            var baseline=source.OrbAppearance;
            var copy=Object.Instantiate(source);
            try
            {
                float[] before=copy.CaptureDeveloperValues();
                copy.SetOrbPresentationOverride(theme.orbAppearance);
                Assert.That(copy.OrbAppearance,Is.SameAs(theme.orbAppearance));
                Assert.That(copy.CaptureDeveloperValues(),Is.EqualTo(before));
                Assert.That(source.OrbAppearance,Is.SameAs(baseline));
                Assert.That(baseline,Is.Not.SameAs(theme.orbAppearance));
            }
            finally { Object.DestroyImmediate(copy); }
        }
    }
}
