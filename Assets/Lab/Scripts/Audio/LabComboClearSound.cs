using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 박 색상 콤보를 성공하면(`CLEAR!`) 추임새 "좋다"를 낸다 (#271). 팀 전체의 성공이라 모든 기기에서 난다.
    /// 실패하면(틀린 색·시간 초과) 실패 소리를 낸다 (#338, 역시 모든 기기). 실패 소리는 Resources/Audio/ComboFail 폴더.
    /// Host 스냅샷의 콤보 결과 순번이 새로 늘었고 결과가 성공일 때 한 번만 낸다. 새 라운드·세션이면 처음부터 다시 세고,
    /// 접속 직후 이미 끝난 결과는 내지 않는다(LabHaptics와 같은 방식). 소리 파일은 Resources/Audio/ComboClear 폴더의
    /// 파일을 쓰고(여러 개면 무작위), 없으면 조용히 넘어간다. 크기는 volume × 일시정지 메뉴 효과음 설정.
    /// #664: 구미호 여우구슬 판정도 같다. 제한 시간 안에 다 지우면 "좋다", 시간이 다 되면 실패 소리 (판정 순번이 새로 늘 때 한 번).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabComboClearSound : MonoBehaviour
    {
        public const string ClipFolder = "Audio/ComboClear";
        public const string FailClipFolder = "Audio/ComboFail";

        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("콤보 실패 소리 크기 (#338)")]
        [Range(0f, 1f)] public float failVolume = 1f;

        private AudioClip[] clips, failClips;
        private AudioSource source;
        private LabNetwork network;
        private string session;
        private uint round;
        private uint resultCount;
        private int foxRoundCount;

        public int ClipCount => clips == null ? 0 : clips.Length;
        public int FailClipCount => failClips == null ? 0 : failClips.Length;
        /// <summary>실패 소리를 낸 횟수 (테스트·확인용). 소리 파일이 없어도 실패를 알아챈 횟수는 센다.</summary>
        public int FailPlayCount { get; private set; }
        /// <summary>낸 횟수 (테스트·확인용). 소리 파일이 없어도 성공을 알아챈 횟수는 센다.</summary>
        public int PlayCount { get; private set; }

        public void Bind(LabNetwork target) => network = target;

        private void Awake()
        {
            clips = Resources.LoadAll<AudioClip>(ClipFolder);
            failClips = Resources.LoadAll<AudioClip>(FailClipFolder);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 기준 소리
        }

        private void Update()
        {
            if (network != null) Observe(network.Snapshot);
        }

        /// <summary>스냅샷에서 새 콤보 성공을 찾으면 소리를 내고 true를 돌려준다.</summary>
        public bool Observe(LabSnapshot snapshot)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing) return false;
            if (snapshot.sessionId != session || snapshot.round != round || snapshot.comboResultCount < resultCount
                || snapshot.foxRoundCount < foxRoundCount)
            {
                session = snapshot.sessionId;
                round = snapshot.round;
                resultCount = snapshot.comboResultCount;
                foxRoundCount = snapshot.foxRoundCount;
                return false;
            }
            bool cleared = false;
            // #664 여우구슬 판정: 다 지우면 성공(1), 시간이 다 되면 실패(-1)
            if (snapshot.foxRoundCount != foxRoundCount)
            {
                foxRoundCount = snapshot.foxRoundCount;
                cleared = PlayResult(System.Math.Sign(snapshot.foxRoundResult));
            }
            if (snapshot.comboResultCount == resultCount) return cleared;
            resultCount = snapshot.comboResultCount;
            bool failed = snapshot.comboResult == LabComboResult.WrongColor || snapshot.comboResult == LabComboResult.Timeout;
            return PlayResult(failed ? -1 : snapshot.comboResult == LabComboResult.Success ? 1 : 0) || cleared;
        }

        /// <summary>성공(1)이면 "좋다", 실패(-1)면 실패 소리를 낸다. 성공이면 true.</summary>
        private bool PlayResult(int result)
        {
            if (result < 0)
            {
                FailPlayCount++;
                if (failClips != null && failClips.Length > 0 && source != null)
                    source.PlayOneShot(failClips[Random.Range(0, failClips.Length)], failVolume * LabAudioSettings.SfxVolume);
                return false;
            }
            if (result == 0) return false;
            PlayCount++;
            if (clips != null && clips.Length > 0 && source != null)
                source.PlayOneShot(clips[Random.Range(0, clips.Length)], volume * LabAudioSettings.SfxVolume);
            return true;
        }
    }
}
