using System.Runtime.InteropServices;
using UnityEngine;

namespace C6Lab
{
    /// <summary>iOS 햅틱 종류. Light → Heavy 로 갈수록 묵직하다. Soft·Rigid 는 iOS 13 이상.</summary>
    public enum LabHapticStyle { Light = 0, Medium = 1, Heavy = 2, Soft = 3, Rigid = 4 }

    /// <summary>어떤 상황의 진동이었는지 (테스트·로그용).</summary>
    public enum LabHapticKind { None, Generate, Hit, ItemHit, Bomb, Combine }

    /// <summary>
    /// 아이폰 진동. 설정(종류·세기·켜기)은 모두 LabConfig 에서 읽는다 (Inspector · DEV 패널).
    /// - 내 구슬이 생성되어 떠오를 때: 생성 진동
    /// - 내가 던진 구슬이 요괴에 맞았을 때: 기본 피해 이하는 타격 진동, 그보다 큰 피해(아이템)는 아이템 타격 진동
    /// - 내 판에서 잠긴 폭탄이 터졌을 때: 폭발 진동
    /// - 내가 구슬을 결합했을 때(Host 승인): 결합 진동 (약하게)
    /// 모두 해당 플레이어 기기에서만 난다. 아이폰 빌드에서만 실제로 진동하고, 에디터·Mac에서는 아무 일도 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabHaptics : MonoBehaviour
    {
        private LabNetwork network;
        private LabOrbBoard board;

        public LabHapticKind LastKind { get; private set; }
        public int PlayCount { get; private set; }

        public void Bind(LabNetwork targetNetwork, LabOrbBoard targetBoard)
        {
            Unbind();
            network = targetNetwork;
            board = targetBoard;
            if (network != null) network.LocalThrowHit += OnLocalThrowHit;
            if (network != null) network.BombBlasted += OnBombBlasted;
            if (network != null) network.LocalCombined += OnLocalCombined;
            if (board != null) board.SpawnStarted += OnSpawnStarted;
        }

        private LabConfig Config => network != null ? network.config : null;

        private void OnLocalThrowHit(int damage) => PlayForHit(damage, Config);
        private void OnSpawnStarted(string orbId) => PlayForGenerate(Config);
        private void OnBombBlasted(string bombId, Vector2 position) => PlayForBomb(Config);
        private void OnLocalCombined() => PlayForCombine(Config);

        /// <summary>기본 피해(Damage per Hit)보다 크면 아이템 타격 진동, 아니면 타격 진동.</summary>
        public bool PlayForHit(int damage, LabConfig config)
        {
            if (config == null || !config.HapticsEnabled) return false;
            bool item = damage > config.Damage;
            return Play(item ? LabHapticKind.ItemHit : LabHapticKind.Hit,
                item ? config.ItemHitHapticStyle : config.HitHapticStyle,
                item ? config.ItemHitHapticIntensity : config.HitHapticIntensity);
        }

        public bool PlayForGenerate(LabConfig config)
        {
            if (config == null || !config.HapticsEnabled) return false;
            return Play(LabHapticKind.Generate, config.GenerateHapticStyle, config.GenerateHapticIntensity);
        }

        public bool PlayForBomb(LabConfig config)
        {
            if (config == null || !config.HapticsEnabled) return false;
            return Play(LabHapticKind.Bomb, config.BombHapticStyle, config.BombHapticIntensity);
        }

        public bool PlayForCombine(LabConfig config)
        {
            if (config == null || !config.HapticsEnabled) return false;
            return Play(LabHapticKind.Combine, config.CombineHapticStyle, config.CombineHapticIntensity);
        }

        private bool Play(LabHapticKind kind, LabHapticStyle style, float intensity)
        {
            if (intensity <= 0f) return false;
            LastKind = kind;
            PlayCount++;
#if UNITY_IOS && !UNITY_EDITOR
            C6Lab_PlayImpactHaptic((int)style, Mathf.Clamp01(intensity));
#endif
            return true;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void C6Lab_PlayImpactHaptic(int style, float intensity);
#endif

        private void Unbind()
        {
            if (network != null) network.LocalThrowHit -= OnLocalThrowHit;
            if (network != null) network.BombBlasted -= OnBombBlasted;
            if (network != null) network.LocalCombined -= OnLocalCombined;
            if (board != null) board.SpawnStarted -= OnSpawnStarted;
        }

        private void OnDestroy() => Unbind();
    }
}
