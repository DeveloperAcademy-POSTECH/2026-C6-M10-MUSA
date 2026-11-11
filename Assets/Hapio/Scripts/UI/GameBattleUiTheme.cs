using C6Lab;
using UnityEngine;

namespace Hapio
{
    [CreateAssetMenu(menuName = "Hapio/Battle UI Theme")]
    public sealed class GameBattleUiTheme : ScriptableObject
    {
        public const string ResourcePath = "Hapio/UI/BattleUiTheme";
        public LabOrbAppearance orbAppearance;
        public Texture2D[] boardTextures;
        public Sprite paper, underlineDark, underlineLight, pauseStroke, staminaCells;
        public Sprite[] playerBadges, minimapBadges;
        public Sprite[] territoryBlueBadges, territoryRedBadges;
        public Sprite[] teamPositionArtwork; // Figma 2:2 red/blue, 3:3 red/blue component assets.
        public Sprite teamBlueMarker, teamRedMarker, drumIcon;
        public Sprite teamPairConnector, teamTrioConnector;
        public Sprite blueFlagDark, redFlagDark, blueFlagLight, redFlagLight, tickDark, tickLight, tickCenter;
        public Font medium, bold;
        public Color[] staminaBorders;
        public static bool LightInkForStage(int stage) => stage == 2 || stage == 3;
        public Texture2D BoardFor(int stage) => boardTextures[Mathf.Clamp(stage - 1, 0, boardTextures.Length - 1)];
    }
}
