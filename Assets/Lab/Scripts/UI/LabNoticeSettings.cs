using UnityEngine;

namespace C6Lab
{
    /// <summary>Device-local diagnostic visibility; never part of shared session configuration.</summary>
    public static class LabNoticeSettings
    {
        public const string PreferenceKey = "C6Lab.Developer.NoticeText";

        public static bool AvailableInThisBuild
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return PlayerPrefs.GetInt(PreferenceKey, 0) == 1;
#else
                // A preference saved by a Development build cannot reveal notices in Release.
                return false;
#endif
            }
        }

        public static void SetEnabled(bool enabled)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PlayerPrefs.SetInt(PreferenceKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
#endif
        }
    }
}
