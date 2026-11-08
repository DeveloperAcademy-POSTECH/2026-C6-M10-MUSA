using UnityEngine;

namespace Hapio
{
    // Exact sRGB components from Art/UI/Source/Hapio.tokens.json. Presentation only.
    public static class GameBattleUiColors
    {
        // TextColor/PrimaryBrown/700
        public static readonly Color Ink = new Color(0.1905882359f, 0.1670588255f, 0.1388235241f, 1f);
        // TextColor/PrimaryBrown/500***
        public static readonly Color Brown = new Color(0.3176470697f, 0.2784313858f, 0.2313725501f, 1f);
        // TextColor/SecondaryLightIvory/200
        public static readonly Color Light = new Color(0.9639215469f, 0.9294117689f, 0.890196085f, 1f);
        // TextColor/DarkRed/500***
        public static readonly Color Red = new Color(0.6881760955f, 0.2785799503f, 0.2187512815f, 1f);
        // 5OrbsColor/WaterBlue/500***
        public static readonly Color Blue = new Color(0.05490196124f, 0.380392164f, 0.5725490451f, 1f);
        // 5OrbsColor/WaterBlue/100
        public static readonly Color BlueEmpty = new Color(0.8109803796f, 0.8760784268f, 0.9145098329f, 1f);
        // 5OrbsColor/Firered/500***
        public static readonly Color Fire = new Color(0.8274509907f, 0.180392161f, 0.08235294372f, 1f);
        // TextColor/Stemina/Stage1
        public static readonly Color Stamina1 = new Color(0.9370492697f, 0.8026745319f, 0.6499758959f, 1f);
        // TextColor/Stemina/Stage2
        public static readonly Color Stamina2 = new Color(0.8323129416f, 0.7421581149f, 0.6441640854f, 1f);
        // TextColor/Stemina/Stage3
        public static readonly Color Stamina3 = new Color(0.8155235648f, 0.6451166272f, 0.4514724314f, 1f);
    }
}
