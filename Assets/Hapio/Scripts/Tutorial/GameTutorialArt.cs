using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 (#477) 그림·글꼴·연출 값. Resources/Hapio/Tutorial/TutorialArt 하나를 전투 화면이 읽는다.
    /// 크기·위치는 전투 HUD와 같은 기준 화면(390 × 844) 단위다.
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialArt", menuName = "Hapio/Tutorial Art")]
    public sealed class GameTutorialArt : ScriptableObject
    {
        public const string ResourcePath = "Hapio/Tutorial/TutorialArt";

        [Header("자리별 캐릭터 얼굴 (P1·P2·P3 동그라미 대신)")]
        [Tooltip("아래 가운데 = 나 (P2)")] public Sprite bul;
        [Tooltip("왼쪽 이웃 (P3)")] public Sprite muk;
        [Tooltip("오른쪽 이웃 (P1)")] public Sprite geumok;
        [Tooltip("아래 내 얼굴을 원래 동그라미보다 몇 배로")] [Min(.5f)] public float localPortraitScale = 1.2f;
        [Tooltip("양옆 이웃 얼굴을 원래 동그라미보다 몇 배로")] [Min(.5f)] public float neighborPortraitScale = 1.8f;
        [Tooltip("얼굴이 화면 가장자리에서 떨어지는 최소 거리. 원래 화면 끝에 걸쳐 있던 이웃 얼굴은 이만큼 안쪽으로 옮겨 다 보이게 한다.")]
        [Min(0f)] public float portraitScreenMargin = 6f;

        [Header("말풍선")]
        [Tooltip("꼬리가 왼쪽 아래에 있는 그림. 오른쪽 사람은 좌우를 뒤집어 쓴다.")] public Sprite bubble;
        public Vector2 bubbleSize = new Vector2(236f, 150f);
        [Tooltip("그림 안에서 글자가 들어갈 여백 (왼쪽, 아래, 오른쪽, 위 · 그림 크기 비율). 아래는 꼬리 높이를 포함한다.")]
        public Vector4 textInset = new Vector4(.08f, .27f, .08f, .11f);
        [Tooltip("꼬리 끝 위치 (그림 왼쪽 아래 기준 비율). 말풍선은 이 점에서 커진다.")]
        public Vector2 tailTip = new Vector2(.045f, .02f);
        [Tooltip("꼬리 끝을 얼굴 모서리에서 얼마나 띄울지 (바깥쪽 +x, 위 +y)")]
        public Vector2 tailGap = new Vector2(2f, 2f);
        [Tooltip("말풍선이 화면 가장자리에서 떨어지는 최소 거리")] [Min(0f)] public float screenMargin = 8f;
        public Font dialogueFont;
        [Min(8)] public int maxFontSize = 17;
        [Min(8)] public int minFontSize = 12;
        public Color textColor = new Color(.12f, .1f, .09f, 1f);

        [Header("말풍선 등장 (살짝 커졌다가 제자리)")]
        [Min(.01f)] public float popSeconds = .28f;
        [Min(1f)] public float popOvershoot = 1.1f;

        [Header("화면 어둡게 · 밝히기")]
        public Color dimColor = new Color(0f, 0f, 0f, .62f);
        [Min(.01f)] public float dimFadeSeconds = .25f;
        [Tooltip("동그랗게 밝히는 곳(박·북)을 대상보다 몇 배 크게")] [Min(1f)] public float spotlightPadding = 1.2f;
        [Tooltip("막대처럼 길쭉하게 밝히는 곳(기운 막대)을 대상보다 사방으로 얼마나 크게")] [Min(0f)] public float barSpotlightPadding = 8f;
        [Tooltip("밝힌 곳 가장자리의 번짐 (둥근 모서리 반지름 비율)")] [Range(.01f, .5f)] public float spotlightSoftness = .16f;
        [Tooltip("밝히는 곳이 바뀔 때 옮겨 가는 빠르기 (클수록 빠름)")] [Min(1f)] public float spotlightFollow = 12f;

        [Header("안내 띠 (동작을 기다릴 때)")]
        public Font bannerFont;
        public Color bannerTextColor = Color.black;
        public Color bannerColor = new Color(.98f, .95f, .88f, .94f);
        [Tooltip("띠 가운데가 화면 위에서 얼마나 내려와 있는지 (화면 높이 비율)")] [Range(0f, 1f)] public float bannerTopFraction = .3f;
        [Min(10f)] public float bannerHeight = 46f;
        [Min(8)] public int bannerFontSize = 20;
        [Tooltip("동작을 해냈을 때 띠 아래에 잠깐 뜨는 \"성공!\"")] public Color successColor = new Color(.78f, .15f, .1f, 1f);
        [Min(8)] public int successFontSize = 28;
        [Tooltip("동작을 해낸 뒤 다음 장면까지 (\"성공!\"이 보이는 시간)")] [Min(0f)] public float afterActionSeconds = .9f;
        [Tooltip("기운이 다 차오른 뒤 다음 장면까지")] [Min(0f)] public float afterRefillSeconds = .4f;
        [Tooltip("북을 다 친 뒤 기운이 이만큼만 살짝 차고 멈춘다 (북 한 번 비용 대비). 1보다 작아 여전히 부족하고, 기운 부족 빨간 빛이 이 부분에 보인다.")]
        [Range(.05f, .95f)] public float shortStaminaShareOfTap = .5f;

        [Header("손 그림 (누르는 곳을 가리킴)")]
        [Tooltip("손가락이 왼쪽 위를 가리키는 그림")] public Sprite hand;
        public Vector2 handSize = new Vector2(64f, 64f);
        [Tooltip("그림 안에서 손가락 끝 (왼쪽 아래 기준 비율)")] public Vector2 handTip = new Vector2(.09f, .93f);
        [Tooltip("쉬는 동안 손가락 끝이 누를 곳에서 떨어져 있는 거리. 누를 때 이만큼 다가간다.")]
        public Vector2 handRestOffset = new Vector2(12f, -12f);
        [Tooltip("한 번 누르는 데 걸리는 시간")] [Min(.2f)] public float handTapSeconds = .9f;
        [Tooltip("누르는 순간 손 크기")] [Range(.5f, 1f)] public float handPressScale = .9f;

        [Header("진행")]
        [Tooltip("판이 보인 뒤 첫 대사까지")] [Min(0f)] public float firstLineDelay = .6f;
        [Tooltip("대사가 뜬 뒤 다음 대사로 넘길 수 있을 때까지 (연타 방지)")] [Min(0f)] public float tapCooldown = .2f;
        [Tooltip("마지막 대사 뒤 어두워지는 시간")] [Min(.01f)] public float exitFadeSeconds = .6f;

        public static GameTutorialArt Load() => Resources.Load<GameTutorialArt>(ResourcePath);
    }
}
