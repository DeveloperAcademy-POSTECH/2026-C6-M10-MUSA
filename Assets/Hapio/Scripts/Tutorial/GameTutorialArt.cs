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
        [Tooltip("구슬이 얼굴·기운 막대를 가리지 않도록 세우는 보이지 않는 벽을 그것들보다 사방으로 얼마나 크게")]
        [Min(0f)] public float keepOutPadding = 4f;

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
        [Range(.05f, .95f)] public float shortStaminaShareOfTap = .25f;

        [Header("손 그림 (누르는 곳을 가리킴)")]
        [Tooltip("손가락이 왼쪽 위를 가리키는 그림")] public Sprite hand;
        public Vector2 handSize = new Vector2(64f, 64f);
        [Tooltip("그림 안에서 손가락 끝 (왼쪽 아래 기준 비율)")] public Vector2 handTip = new Vector2(.09f, .93f);
        [Tooltip("쉬는 동안 손가락 끝이 누를 곳에서 떨어져 있는 거리. 누를 때 이만큼 다가간다.")]
        public Vector2 handRestOffset = new Vector2(12f, -12f);
        [Tooltip("한 번 누르는 데 걸리는 시간")] [Min(.2f)] public float handTapSeconds = .9f;
        [Tooltip("누르는 순간 손 크기")] [Range(.5f, 1f)] public float handPressScale = .9f;

        [Tooltip("끌어 놓기 안내(2단계)에서 손이 한 번 끌고 떼는 데 걸리는 시간")] [Min(.4f)] public float handDragSeconds = 1.6f;
        [Tooltip("던지기 안내(3단계)에서 손이 한 번 쓸어 올리는 주기")] [Min(.4f)] public float handSwipeSeconds = 1.4f;
        [Tooltip("던지기 안내 화살표·손이 구슬에서 박 쪽으로 가는 길이")] [Min(10f)] public float throwGuideLength = 150f;
        [Tooltip("던지기를 기다리는 동안 안내 띠 가운데의 높이 (화면 위에서 화면 높이 비율). 박을 가리지 않게 판 바로 위.")]
        [Range(0f, 1f)] public float throwBannerTopFraction = .47f;
        [Tooltip("빗나갔을 때 \"다시 해 봐!\" 말풍선이 떠 있는 시간")] [Min(.3f)] public float remarkSeconds = 2f;
        [Tooltip("다섯 번째로 빗나갔을 때 \"너 일부러 그러는 거지?\" 말풍선이 떠 있는 시간. 구슬을 잡아도 사라지지 않고, 끝나면 \"다시 해 봐!\"가 이어서 뜬다.")]
        [Min(.3f)] public float teaseSeconds = 2.4f;
        [Tooltip("진행률 막대를 밝히는 곳을 막대보다 사방으로 얼마나 크게")] [Min(0f)] public float progressSpotlightPadding = 8f;

        [Header("북에서 나오는 구슬 (1단계)")]
        [Tooltip("세 구슬이 시안 자리보다 더 미끄러지는 거리 (판 너비 비율). 클수록 북에서 멀리 튀어나간다.")]
        [Range(0f, .3f)] public float drumLaunchExtra = .06f;
        [Tooltip("구슬 중심이 판 옆 끝에서 떨어져 멈추는 최소 거리 (판 너비 비율). 더 미끄러져도 이 안쪽에서 멈춘다.")]
        [Range(.05f, .4f)] public float drumLaunchEdgeMargin = .1f;

        [Header("금옥이 화면 미니 창 (4단계: 넘긴 북을 받는 모습)")]
        [Tooltip("비워 두면 북 미니맵의 태극 북 그림을 쓴다")] public Sprite mirrorDrum;
        public Vector2 mirrorSize = new Vector2(150f, 100f);
        [Min(.5f)] public float mirrorSeconds = 1.8f;
        public Color mirrorColor = new Color(.96f, .91f, .8f, 1f);
        public Color mirrorBorderColor = new Color(.33f, .24f, .17f, 1f);
        [Min(10f)] public float mirrorDrumSize = 46f;
        [Min(10f)] public float mirrorFaceSize = 42f;

        [Header("돌려주기 (4·5단계)")]
        [Tooltip("\"고마워!\" 대사가 뜬 뒤 금옥이가 양구슬을 돌려줄 때까지")] [Min(0f)] public float giveBackDelay = .35f;
        [Tooltip("양구슬 뒤 북이 돌아올 때까지")] [Min(0f)] public float giveBackGap = .35f;
        [Tooltip("엉뚱한 쪽(묵이)으로 넘긴 북을 묵이가 돌려줄 때까지")] [Min(0f)] public float wrongSideReturnDelay = .8f;
        [Tooltip("\"앗 뜨거! 도로 받아!\" 대사가 뜬 뒤 금옥이가 폭탄을 돌려줄 때까지")] [Min(0f)] public float bombReturnDelay = .35f;
        [Tooltip("폭탄 넘기기 안내(5단계)에서 손이 한 번 끄는 데 걸리는 시간. 폭탄은 무거워 더 세게 밀어야 해서 북보다 빠르다.")]
        [Min(.3f)] public float bombHandDragSeconds = 1.1f;

        [Header("막기 (6단계)")]
        [Tooltip("요괴가 오른쪽을 막은 뒤 첫 대사까지 가장자리가 번쩍이는 시간. 막힌 가장자리는 처음에 세 번 번쩍인다 (1.05초).")]
        [Min(0f)] public float blockWarningSeconds = C6Lab.LabOrbEdgeBlock.OpeningSeconds;

        [Header("화살표 (끌어 놓을 방향)")]
        public Color arrowColor = new Color(.36f, .66f, .29f, 1f);
        [Min(1f)] public float arrowWidth = 7f;
        [Min(1f)] public float arrowHeadLength = 18f;
        [Min(1f)] public float arrowHeadWidth = 22f;
        [Tooltip("화살표 양 끝을 구슬 가장자리에서 띄우는 거리")] [Min(0f)] public float arrowGap = 2f;
        [Tooltip("구슬을 밝히는 곳을 구슬보다 사방으로 얼마나 크게")] [Min(0f)] public float orbSpotlightPadding = 14f;

        [Header("진행")]
        [Tooltip("판이 보인 뒤 첫 대사까지")] [Min(0f)] public float firstLineDelay = .6f;
        [Tooltip("대사가 뜬 뒤 다음 대사로 넘길 수 있을 때까지 (연타 방지)")] [Min(0f)] public float tapCooldown = .2f;
        [Tooltip("마지막 대사 뒤 어두워지는 시간")] [Min(.01f)] public float exitFadeSeconds = .6f;

        public static GameTutorialArt Load() => Resources.Load<GameTutorialArt>(ResourcePath);
    }
}
