using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace C6Lab
{
    /// <summary>iOS 햅틱 종류. Light → Heavy 로 갈수록 묵직하다. Soft·Rigid 는 iOS 13 이상.</summary>
    public enum LabHapticStyle { Light = 0, Medium = 1, Heavy = 2, Soft = 3, Rigid = 4 }

    /// <summary>어떤 상황의 진동이었는지 (테스트·로그용).</summary>
    public enum LabHapticKind { None, Generate, Hit, ItemHit, Bomb, Combine, ComboClear, ComboFail, YaguangFlash }

    /// <summary>
    /// 아이폰 진동. 설정(종류·세기·켜기)은 모두 LabConfig 에서 읽는다 (Inspector · DEV 패널).
    /// - 내 구슬이 생성되어 떠오를 때: 생성 진동
    /// - 내가 던진 구슬이 요괴에 맞았을 때: 기본 피해 이하는 타격 진동, 그보다 큰 피해(아이템)는 아이템 타격 진동
    /// - 내 판에서 잠긴 폭탄이 터졌을 때: 폭발 진동
    /// - 내가 구슬을 결합했을 때(Host 승인): 결합 진동 (약하게)
    /// - 박 색상 콤보(#222) 성공: 센 진동 3번 (모든 기기)
    /// - 박 색상 콤보 실패(틀린 색 / 시간 초과): 약 2초 동안 크게 이어지는 진동 (모든 기기)
    /// - 야광귀 섬광(#268)을 맞았을 때: 센(Heavy) 진동 한 번 (맞은 사람 기기)
    /// 콤보 진동 외에는 해당 플레이어 기기에서만 난다. 아이폰 빌드에서만 실제로 진동하고, 에디터·Mac에서는 아무 일도 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabHaptics : MonoBehaviour
    {
        [Header("박 색상 콤보 (#222)")]
        [Tooltip("콤보 성공 때 울리는 횟수")]
        [SerializeField, Range(1, 8)] private int comboClearPulses = 5;
        [Tooltip("콤보 성공 진동 사이 간격 (초)")]
        [SerializeField, Range(.05f, .5f)] private float comboClearGapSeconds = .1f;
        [Tooltip("콤보 실패 때 이어지는 진동 길이 (초)")]
        [SerializeField, Range(.2f, 5f)] private float comboFailSeconds = 2.5f;

        private LabNetwork network;
        private LabOrbBoard board;
        private string comboSession;
        private uint comboRound;
        private uint comboResultCount;

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

        /// <summary>야광귀 섬광 (#268): 번쩍이는 순간 센(Heavy) 진동 한 번. 섬광 화면이 부른다.</summary>
        public bool PlayForFlash(LabConfig config)
        {
            if (config == null || !config.HapticsEnabled) return false;
            return Play(LabHapticKind.YaguangFlash, LabHapticStyle.Heavy, 1f);
        }

        public float ComboFailSeconds => comboFailSeconds;
        public int ComboClearPulses => comboClearPulses;
        public float ComboClearGapSeconds => comboClearGapSeconds;

        /// <summary>콤보 성공: 센(Heavy) 진동을 짧은 간격으로 5번 (#301에서 3 → 5번, 간격 0.14 → 0.1초로 더 세게).</summary>
        public bool PlayForComboClear(LabConfig config)
        {
            if (config == null || !config.HapticsEnabled) return false;
            LastKind = LabHapticKind.ComboClear;
            PlayCount++;
#if UNITY_IOS && !UNITY_EDITOR
            if (isActiveAndEnabled) StartCoroutine(PulseRoutine(comboClearPulses, comboClearGapSeconds));
#endif
            return true;
        }

        /// <summary>콤보 실패(틀린 색 / 시간 초과): 약 2.5초 동안 크게 이어지는 진동 (#301에서 2 → 2.5초).</summary>
        public bool PlayForComboFail(LabConfig config)
        {
            if (config == null || !config.HapticsEnabled) return false;
            LastKind = LabHapticKind.ComboFail;
            PlayCount++;
#if UNITY_IOS && !UNITY_EDITOR
            C6Lab_PlayContinuousHaptic(comboFailSeconds, 1f, .45f);
#endif
            return true;
        }

#if UNITY_IOS && !UNITY_EDITOR
        private IEnumerator PulseRoutine(int pulses, float gap)
        {
            for (int i = 0; i < pulses; i++)
            {
                if (i > 0) yield return new WaitForSecondsRealtime(gap);
                C6Lab_PlayImpactHaptic((int)LabHapticStyle.Heavy, 1f);
            }
        }
#endif

        /// <summary>
        /// Host 스냅샷에서 콤보 결과가 새로 나왔는지 본다. 같은 결과는 한 번만 울리고,
        /// 새 라운드/세션이 시작되면 처음부터 다시 센다. 접속 직후 이미 끝난 결과는 울리지 않는다.
        /// </summary>
        private void Update()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            if (snapshot == null || snapshot.phase != LabPhase.Playing) return;
            if (snapshot.sessionId != comboSession || snapshot.round != comboRound
                || snapshot.comboResultCount < comboResultCount)
            {
                comboSession = snapshot.sessionId;
                comboRound = snapshot.round;
                comboResultCount = snapshot.comboResultCount;
                return;
            }
            if (snapshot.comboResultCount == comboResultCount) return;
            comboResultCount = snapshot.comboResultCount;
            if (snapshot.comboResult == LabComboResult.Success) PlayForComboClear(Config);
            else if (snapshot.comboResult == LabComboResult.WrongColor || snapshot.comboResult == LabComboResult.Timeout)
                PlayForComboFail(Config);
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

        [DllImport("__Internal")]
        private static extern void C6Lab_PlayContinuousHaptic(float seconds, float intensity, float sharpness);
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
