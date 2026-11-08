using UnityEngine;

namespace Hapio
{
    // sRGB components from Art/UI/Source/Hapio.tokens.json; UI presentation only.
    public static class GameLobbySeatColors
    {
        // TextColor/PrimaryBrown/700
        public static readonly Color Ink = new Color(0.190588236f, 0.167058825f, 0.138823524f, 1f);
        // TextColor/PrimaryBrown/800
        public static readonly Color SeatInk = new Color(0.127058834f, 0.111372553f, 0.0925490186f, 1f);
        // TextColor/PrimaryBrown/500***
        public static readonly Color Brown = new Color(0.31764707f, 0.278431386f, 0.23137255f, 1f);
        // TextColor/GreenGray/500***
        public static readonly Color Muted = new Color(0.431372553f, 0.435294122f, 0.380392164f, 1f);
        // TextColor/PrimaryBrown/400
        public static readonly Color Host = new Color(0.454117656f, 0.422745109f, 0.38509804f, 1f);
        // TextColor/SecondaryLightIvory/100
        public static readonly Color LightText = new Color(0.981960773f, 0.964705884f, 0.945098042f, 1f);
        // TextColor/DarkRed/500***
        public static readonly Color Ready = new Color(0.688176095f, 0.27857995f, 0.218751281f, 1f);
        // TextColor/PrimaryBrown/300
        public static readonly Color Disabled = new Color(0.590588212f, 0.567058802f, 0.538823545f, 1f);
        // 5OrbsColor/WaterBlue/500***
        public static readonly Color Blue = new Color(0.0549019612f, 0.380392164f, 0.572549045f, 1f);
        // 5OrbsColor/Firered/500***
        public static readonly Color Red = new Color(0.827450991f, 0.180392161f, 0.0823529437f, 1f);
        // TextColor/DarkRed/600
        public static readonly Color Versus = new Color(0.550540864f, 0.222863957f, 0.175001025f, 1f);
    }
}
