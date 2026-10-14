using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 이 기기에서만 쓰는 소리 크기 (0~1). 방장 설정과 상관없이 기기마다 따로 저장되고,
    /// 앱을 다시 켜도 유지된다. 각 소리의 기본 크기에 곱해진다 (1 = 기본 크기, 0 = 끔).
    /// </summary>
    public static class LabAudioSettings
    {
        private const string BgmKey = "C6Lab.Audio.BgmVolume";
        private const string SfxKey = "C6Lab.Audio.SfxVolume";

        private static bool loaded;
        private static float bgmVolume = 1f;
        private static float sfxVolume = 1f;

        /// <summary>값이 바뀔 때마다 알린다.</summary>
        public static event Action Changed;

        public static float BgmVolume
        {
            get { Load(); return bgmVolume; }
            set { Load(); Save(ref bgmVolume, value, BgmKey); }
        }

        public static float SfxVolume
        {
            get { Load(); return sfxVolume; }
            set { Load(); Save(ref sfxVolume, value, SfxKey); }
        }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmKey, 1f));
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f));
        }

        private static void Save(ref float field, float value, string key)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(field, value)) return;
            field = value;
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
