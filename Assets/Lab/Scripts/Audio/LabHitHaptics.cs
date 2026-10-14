using System.Runtime.InteropServices;
using UnityEngine;

namespace C6Lab
{
    /// <summary>iOS 햅틱 종류. Light → Heavy 로 갈수록 묵직하다. Soft·Rigid 는 iOS 13 이상.</summary>
    public enum LabHapticStyle { Light = 0, Medium = 1, Heavy = 2, Soft = 3, Rigid = 4 }

    /// <summary>
    /// 내가 던진 구슬이 요괴에 맞으면 던진 사람 아이폰에서만 진동한다.
    /// - 기본 피해(설정의 Damage per Hit) 이하: 약한 진동
    /// - 그보다 큰 피해(릴레이·폭탄 등 아이템): 강한 진동
    /// 아이폰 빌드에서만 실제로 진동하고, 에디터·Mac에서는 아무 일도 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabHitHaptics : MonoBehaviour
    {
        public bool hapticsEnabled = true;
        public LabHapticStyle normalStyle = LabHapticStyle.Light;
        [Range(0f, 1f)] public float normalIntensity = .6f;
        public LabHapticStyle strongStyle = LabHapticStyle.Heavy;
        [Range(0f, 1f)] public float strongIntensity = 1f;

        private LabNetwork network;

        /// <summary>마지막으로 낸 진동이 강한 쪽이었는지 (테스트·로그용).</summary>
        public bool LastWasStrong { get; private set; }
        public int PlayCount { get; private set; }

        public void Bind(LabNetwork target)
        {
            if (network != null) network.LocalThrowHit -= OnLocalThrowHit;
            network = target;
            if (network != null) network.LocalThrowHit += OnLocalThrowHit;
        }

        private void OnLocalThrowHit(int damage)
        {
            int baseDamage = network != null && network.config != null ? network.config.Damage : 0;
            PlayForDamage(damage, baseDamage);
        }

        /// <summary>기본 피해보다 크면 강하게, 아니면 약하게 진동한다.</summary>
        public void PlayForDamage(int damage, int baseDamage)
        {
            if (!hapticsEnabled) return;
            bool strong = baseDamage > 0 && damage > baseDamage;
            LastWasStrong = strong;
            PlayCount++;
            Play(strong ? strongStyle : normalStyle, strong ? strongIntensity : normalIntensity);
        }

        public static void Play(LabHapticStyle style, float intensity)
        {
            if (intensity <= 0f) return;
#if UNITY_IOS && !UNITY_EDITOR
            C6Lab_PlayImpactHaptic((int)style, Mathf.Clamp01(intensity));
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void C6Lab_PlayImpactHaptic(int style, float intensity);
#endif

        private void OnDestroy()
        {
            if (network != null) network.LocalThrowHit -= OnLocalThrowHit;
        }
    }
}
