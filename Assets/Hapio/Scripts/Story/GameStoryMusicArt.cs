using System;
using UnityEngine;

namespace Hapio
{
    /// <summary>#629 박 사고 컷(14~16)에서 정해진 순간에 나는 효과음 하나.</summary>
    [Serializable]
    public struct GameStoryTimedSound
    {
        [Tooltip("효과음")] public AudioClip clip;
        [Tooltip("박이 떨어지기 시작한 순간(Break Cut 시작)부터 몇 초 뒤에 낼지")] [Min(0f)] public float seconds;
        [Tooltip("크기 (게임 설정의 효과음 볼륨에 곱한다)")] [Range(0f, 1f)] public float volume;
        [Tooltip("빠르기 (1 = 원래. 낮추면 느리고 낮게, 높이면 빠르고 높게)")] [Range(.3f, 2f)] public float speed;
    }

    /// <summary>#629 오프닝 컷 하나에서 나는 효과음(컷 에셋 첫 쪽의 소리)의 크기·빠르기.</summary>
    [Serializable]
    public struct GameStoryCutSound
    {
        [Tooltip("컷 이름 (예: OP_001)")] public string cutId;
        [Tooltip("메모: 이 컷에서 나는 소리 (보기용)")] public string note;
        [Tooltip("크기 (컷 소리 원래 크기에 곱한다, 0 = 끔)")] [Range(0f, 1f)] public float volume;
        [Tooltip("빠르기 (1 = 원래. 낮추면 느리고 낮게, 높이면 빠르고 높게)")] [Range(.3f, 2f)] public float speed;
    }

    /// <summary>
    /// #629 타이틀·오프닝 음악 곡과 값. Resources/Hapio/Story/StoryMusicArt 하나를 읽는다. Play 중에 바꿔도 바로 들린다.
    /// - 평화 곡: 타이틀 화면부터 오프닝 앞부분까지 끊기지 않고 이어지고, 방 목록에서도 나온다.
    /// - 박이 떨어지는 컷(Break Cut Id)이 시작되면 평화 곡이 테이프가 멈추듯 느려지며 메아리만 남고,
    ///   조금 뒤 깨진 이후 곡이 들어온다.
    /// </summary>
    [CreateAssetMenu(fileName = "StoryMusicArt", menuName = "Hapio/Story Music Art")]
    public sealed class GameStoryMusicArt : ScriptableObject
    {
        public const string ResourcePath = "Hapio/Story/StoryMusicArt";

        [Header("곡")]
        [Tooltip("타이틀 ~ 박이 떨어지기 전, 그리고 방 목록 곡 (반복)")] public AudioClip calmMusic;
        [Tooltip("평화 곡 크기 (게임 설정의 BGM 볼륨에 곱한다)")] [Range(0f, 1f)] public float calmVolume = 1f;
        [Tooltip("평화 곡 빠르기 (1 = 원래. 낮추면 느리고 낮게, 높이면 빠르고 높게)")] [Range(.3f, 2f)] public float calmSpeed = 1f;
        [Tooltip("타이틀·방 목록에서는 평화 곡을 이만큼만 틀어 터치 먹물 소리가 잘 들리게 한다 (Calm Volume에 곱한다)")]
        [Range(0f, 1f)] public float titleVolume = .32f;
        [Tooltip("시작을 눌러 오프닝이 열리면 작던 타이틀 곡이 원래 크기까지 커지는 시간 (초, 0~1 전체 기준)")]
        [Min(0f)] public float titleToOpeningSeconds = 1.5f;
        [Tooltip("박이 깨진 뒤 오프닝 끝까지 나오는 곡 (반복)")] public AudioClip brokenMusic;
        [Tooltip("깨진 이후 곡 크기 (게임 설정의 BGM 볼륨에 곱한다)")] [Range(0f, 1f)] public float brokenVolume = .6f;
        [Tooltip("깨진 이후 곡 빠르기. 1보다 작으면 테이프를 늦추듯 더 느리고 더 낮아진다 (0.85 ≈ 15% 느리고 반음 약 3개 낮게)")]
        [Range(.3f, 1.5f)] public float brokenPitch = .85f;
        [Tooltip("대기실(자리 고르기)에서도 평화 곡을 계속 틀지")] public bool playInLobby = true;

        [Header("전환")]
        [Tooltip("이 컷이 시작되는 순간 평화 곡이 멈추기 시작한다 (새 28컷에서 박이 떨어지기 시작하는 컷)")] public string breakCutId = "OP_014";
        [Tooltip("평화 곡이 테이프처럼 느려져 완전히 멈출 때까지 (초). 기본은 컷 14(2.3초) + 컷 15에서 박이 바닥에 닿는 2.6초")]
        [Min(.1f)] public float tapeStopSeconds = 4.9f;
        [Tooltip("느려지는 모양. 클수록 처음엔 천천히, 끝으로 갈수록 급하게 멈춘다")] [Range(.5f, 4f)] public float tapeStopCurve = 1.6f;
        [Tooltip("멈추기 시작한 뒤 깨진 이후 곡이 들어올 때까지 (초)")] [Min(0f)] public float brokenDelaySeconds = 7.4f;
        [Tooltip("깨진 이후 곡이 스며들 듯 커지는 시간 (초)")] [Min(0f)] public float brokenFadeInSeconds = 1.2f;

        [Header("메아리 (멈추는 동안)")]
        [Tooltip("메아리 간격 (밀리초)")] [Range(10f, 2000f)] public float echoDelayMs = 330f;
        [Tooltip("메아리가 한 번 돌아올 때마다 남는 크기 (클수록 길게 남는다)")] [Range(0f, 1f)] public float echoDecay = .6f;
        [Tooltip("메아리 소리 크기")] [Range(0f, 1f)] public float echoWet = .7f;
        [Tooltip("평화 곡이 멈춘 뒤에도 메아리가 울리도록 기다리는 시간 (초)")] [Min(0f)] public float echoTailSeconds = 6f;

        [Header("박 사고 효과음 (컷 14~16은 자동으로 흘러가서 컷 효과음 칸 대신 여기서 정확한 순간에 낸다)")]
        [Tooltip("기본: 0초 덜컹 / 4.4초 떨어져 와장창(와장창이 파일 0.5초 지점이라 박이 바닥에 닿는 4.9초에 맞음) / 5.95초 요괴 탈출")]
        public GameStoryTimedSound[] breakSounds = new GameStoryTimedSound[0];

        [Header("오프닝 컷 효과음 (컷마다 크기·빠르기. 표에 없는 컷은 원래대로)")]
        public GameStoryCutSound[] cutSounds = new GameStoryCutSound[0];

        [Header("켜고 끄기")]
        [Tooltip("타이틀·방 목록에서 곡이 들어오는 시간 (초)")] [Min(0f)] public float fadeInSeconds = .5f;
        [Tooltip("튜토리얼·전투로 넘어가거나 스토리를 건너뛸 때 곡이 사라지는 시간 (초)")] [Min(0f)] public float fadeOutSeconds = 1f;

        public static GameStoryMusicArt Load() => Resources.Load<GameStoryMusicArt>(ResourcePath);

        /// <summary>빠르기 값 정리. 0 이하(새로 추가한 빈 칸)는 원래 빠르기 1로 본다.</summary>
        public static float Speed(float speed) => speed <= 0f || float.IsNaN(speed) ? 1f : Mathf.Clamp(speed, .3f, 2f);
        public float CalmSpeed => Speed(calmSpeed);
        public float BrokenSpeed => Speed(brokenPitch);

        /// <summary>이 컷 효과음의 (크기 배율, 빠르기). 표에 없는 컷은 (1, 1).</summary>
        public Vector2 CutSoundTuning(string cutId)
        {
            if (cutSounds != null && !string.IsNullOrEmpty(cutId))
                foreach (GameStoryCutSound row in cutSounds)
                    if (row.cutId == cutId) return new Vector2(Mathf.Clamp01(row.volume), Speed(row.speed));
            return Vector2.one;
        }

        /// <summary>테이프 멈춤 진행(0~1)에서의 재생 속도. 1에서 시작해 0에서 멈춘다.</summary>
        public float TapeSpeedAt(float progress) => Mathf.Pow(1f - Mathf.Clamp01(progress), tapeStopCurve);

        /// <summary>테이프 멈춤 진행(0~1)에서의 크기. 느려지는 동안 함께 작아져 멈출 때 0이 된다.</summary>
        public static float TapeLoudnessAt(float progress) => 1f - Mathf.Pow(Mathf.Clamp01(progress), .9f);
    }
}
