using System;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// 로딩 화면 (#478) 그림·글꼴·값. Resources/Hapio/Loading/LoadingArt 하나를 로딩 화면이 읽는다.
    /// 크기·위치는 다른 화면과 같은 기준 화면(390 × 844 pt) 단위다.
    /// </summary>
    [CreateAssetMenu(fileName = "LoadingArt", menuName = "Hapio/Loading Art")]
    public sealed class GameLoadingArt : ScriptableObject
    {
        public const string ResourcePath = "Hapio/Loading/LoadingArt";

        /// <summary>달리는 캐릭터 한 명: 제자리 달리기 그림들과 그 크기·자리.</summary>
        [Serializable]
        public sealed class Runner
        {
            public string name = "불이";
            [Tooltip("달리기 한 바퀴를 이루는 그림들, 차례대로")] public Sprite[] frames = Array.Empty<Sprite>();
            [Tooltip("그림 한 장을 보여 주는 시간. 6장 × 0.14초 ≈ 0.84초에 한 바퀴 (클수록 느리게 달림). Play 중에 바꿔도 바로 보인다.")] [Min(.02f)] public float frameSeconds = .14f;
            [Tooltip("발이 땅에 닿는 그림 번호(0부터). 여기서 몸이 살짝 납작해지고, 100%가 되면 이 그림에서 멈춘다.")]
            public int[] landingFrames = { 2, 5 };
            [Tooltip("이 캐릭터만 키우거나 줄이기 (1 = 기본 높이)")] [Min(.1f)] public float scale = 1f;
            [Tooltip("이 캐릭터만 옮기기 (pt, 오른쪽 +x, 위 +y)")] public Vector2 offset = Vector2.zero;
            [Tooltip("그림별 앞머리 끝 위치 (그림의 왼쪽 위가 0,0). 바람 연출을 쓰지 않으면 비워 둔다.")]
            public Vector2[] forelockTips = Array.Empty<Vector2>();

            public bool Usable => frames != null && frames.Length > 0 && Array.TrueForAll(frames, frame => frame != null);
        }

        [Header("배경 (시안 #F9E5C9 위에 한지 결)")]
        public Color backgroundColor = new Color(.976f, .898f, .788f, 1f);
        [Tooltip("배경색 위에 반투명으로 겹치는 한지 결 그림")] public Texture paperTexture;
        [Range(0f, 1f)] public float paperStrength = .55f;

        [Header("달리는 캐릭터 (로딩 화면이 뜰 때마다 무작위로 한 명)")]
        public Runner[] runners = Array.Empty<Runner>();
        [Tooltip("캐릭터 그림 높이")] [Min(10f)] public float runnerHeight = 182f;
        [Tooltip("캐릭터 가운데가 화면 위에서 얼마나 내려와 있는지 (화면 높이 비율)")] [Range(0f, 1f)] public float runnerCenterFromTop = .356f;
        [Tooltip("발이 닿을 때 몸이 납작해지는 정도 (0 = 없음)")] [Range(0f, .2f)] public float landingSquash = .04f;
        [Tooltip("납작해졌다 돌아오는 시간")] [Min(.01f)] public float landingSquashSeconds = .08f;

        [Header("\"로딩중\" 글자")]
        public string caption = "로딩중";
        public Font captionFont;
        [Min(8)] public int captionFontSize = 20;
        public Color captionColor = Color.black;
        [Tooltip("캐릭터 아래 끝에서 글자 위 끝까지")] public float captionGapBelowRunner = 4f;

        [Header("진행 막대")]
        [Min(20f)] public float barWidth = 262f;
        [Min(4f)] public float barHeight = 16f;
        [Tooltip("막대 테두리 두께")] [Min(0f)] public float barBorder = 1.5f;
        [Tooltip("차오르는 부분이 막대 안쪽에서 떨어지는 거리")] [Min(0f)] public float fillInset = 2.5f;
        public Color trackEdgeColor = new Color(.808f, .765f, .702f, 1f);
        public Color trackColor = new Color(.890f, .847f, .792f, 1f);
        public Color fillColor = new Color(.424f, .275f, .149f, 1f);
        [Tooltip("\"로딩중\" 아래 끝과 % 말풍선 위 끝 사이 (둘이 겹치지 않게). 막대는 그 아래에 자동으로 놓인다.")]
        [Min(0f)] public float bubbleGapBelowCaption = 8f;

        [Header("% 말풍선")]
        public Vector2 bubbleSize = new Vector2(36f, 20f);
        [Tooltip("꼬리 길이 (말풍선 아래로)")] [Min(0f)] public float bubbleTail = 5f;
        [Min(0f)] public float bubbleTailWidth = 8f;
        [Min(0f)] public float bubbleCorner = 6f;
        [Tooltip("꼬리 끝과 막대 위 끝 사이")] public float bubbleGapAboveBar = 2f;
        public Color bubbleColor = new Color(.569f, .404f, .271f, 1f);
        public Font percentFont;
        [Min(6)] public int percentFontSize = 11;
        public Color percentColor = Color.white;
        [Tooltip("움직이는 빠르기(초당 막대 폭)마다 말풍선이 뒤로 기우는 각도")] [Min(0f)] public float bubbleTiltPerSpeed = 60f;
        [Tooltip("가장 많이 기우는 각도")] [Range(0f, 45f)] public float bubbleMaxTilt = 14f;
        [Tooltip("기울기가 바뀌는 빠르기 (클수록 빨리 따라감)")] [Min(.1f)] public float bubbleTiltFollow = 10f;

        [Header("준비 인원 (첫 전투 준비 때 막대 아래 \"준비 n/m\")")]
        [Min(6)] public int detailFontSize = 13;
        public Color detailColor = new Color(.36f, .27f, .2f, 1f);
        [Tooltip("막대 아래 끝에서 글자 위 끝까지")] public float detailGapBelowBar = 10f;

        [Header("진행 표시")]
        [Tooltip("보이는 진행이 실제 진행을 따라가는 빠르기 (클수록 빨리 따라감). 뒤로는 가지 않는다.")]
        [Min(.1f)] public float displayFollow = 10f;
        [Tooltip("보이는 진행이 1초에 갈 수 있는 최대 거리 (막대 폭 비율). 바로 끝나는 전환도 0→100%가 한 번 차 보인다.")]
        [Min(.05f)] public float displayMaxSpeed = 1.6f;

        [Header("보이고 닫히기 (GameSession이 띄울 때)")]
        [Tooltip("아무리 짧은 전환도 이만큼은 보인다 (사용자 결정 1초)")] [Min(0f)] public float minimumSeconds = 1f;
        [Tooltip("100%가 된 뒤 닫히기 전까지 100%를 보여 주는 시간")] [Min(0f)] public float fullHoldSeconds = .2f;
        [Tooltip("닫힐 때 사라지는 시간")] [Min(0f)] public float fadeOutSeconds = .15f;

        [Header("미리보기 (Editor Play 중 Hapio 메뉴)")]
        [Min(.5f)] public float previewSeconds = 5f;
        [Tooltip("100%가 된 뒤 미리보기 화면이 닫히기까지")] [Min(0f)] public float previewHoldSeconds = .8f;

        public static GameLoadingArt Load() => Resources.Load<GameLoadingArt>(ResourcePath);
    }
}
