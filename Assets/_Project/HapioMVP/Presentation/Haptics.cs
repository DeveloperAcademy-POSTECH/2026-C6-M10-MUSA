using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace C6.Prototype.Presentation
{
    public enum HapticImpact { Light = 0, Medium = 1, Heavy = 2 }

    /// <summary>
    /// Short iOS Taptic Engine feedback (Plugins/iOS/C6Haptics.mm). The Editor and desktop builds have no Taptic
    /// Engine, so they only log C6_HAPTIC lines for verification. It never affects gameplay.
    /// </summary>
    public static class Haptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void C6Haptics_Impact(int style);
#endif

        public static void Impact(HapticImpact strength)
        {
#if UNITY_IOS && !UNITY_EDITOR
            C6Haptics_Impact((int)strength);
#else
            Debug.Log("C6_HAPTIC impact=" + strength);
#endif
        }
    }
}
