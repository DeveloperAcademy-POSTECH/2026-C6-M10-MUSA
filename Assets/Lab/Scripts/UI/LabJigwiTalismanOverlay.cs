using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace C6Lab
{
    /// <summary>
    /// #479 지귀 부적: A가 시작되면 아직 부적을 안 쓴 사람의 전투 영역(박 쪽)에 세로 한지 부적을 띄우고, 쓰는 동안 조합판 조작(구슬 끌기·북·던지기)을 막는다.
    /// 부적의 점선 "물"을 손가락으로 따라 쓰면(획 순서/손 떼기 자유, 점선 밖은 무시) 지나간 점이 붉게 칠해지고,
    /// 정해 둔 비율이 칠해지면 이 기기가 Host에 한 번 알린다. 불 끄기와 보상은 Host가 정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabJigwiTalismanOverlay : MonoBehaviour
    {
        [Tooltip("조합판 영역 안쪽 여백 (판 크기 대비). 부적 종이가 판 가장자리에서 이만큼 떨어진다.")]
        [SerializeField, Range(0f, .2f)] private float margin = .04f;
        [Tooltip("한지 무늬 위에 곱하는 색. 흰색이면 한지 그대로.")]
        [SerializeField] private Color paperColor = new Color(1f, 1f, 1f, .98f);
        [Tooltip("부적 종이의 가로/세로 비율. 작을수록 좁고 길다.")]
        [SerializeField, Range(.35f, 1f)] private float paperAspect = .55f;
        [Tooltip("#540: 부적은 조합판 위에 놓인다. 조합판 위쪽(둥근 테두리)에서 비워 둘 높이 (조합판 높이 대비).")]
        [SerializeField, Range(0f, .4f)] private float topInset = .07f;
        [Tooltip("#540: 조합판 아래쪽(내 이름·기운 막대)에서 비워 둘 높이 (조합판 높이 대비).")]
        [SerializeField, Range(0f, .4f)] private float bottomInset = .13f;
        [SerializeField] private Color inkColor = new Color(.72f, .11f, .07f, 1f);
        [Tooltip("#533: 아직 안 쓴 글자 획 색. 둥근 점이 겹치므로 불투명으로 둔다(반투명이면 겹친 곳이 얼룩진다).")]
        [SerializeField] private Color guideColor = new Color(.79f, .67f, .4f, 1f);
        [Tooltip("#479/#533: 손가락이 지나가 칠해진 획 색. #533부터 수채화 파랑(반투명 점이 겹쳐 물감처럼 고인다). 알파가 낮을수록 묽다.")]
        [SerializeField] private Color writtenColor = new Color(.16f, .47f, .93f, .5f);
        [Tooltip("#533: 수채화 점마다 색이 조금씩 흔들리는 정도 (0이면 모두 같은 색).")]
        [SerializeField, Range(0f, .3f)] private float watercolorJitter = .12f;
        [Tooltip("#533: 완성으로 보는 기준 — 한 획 안에서 연달아 비어도 되는 점 수. 1이면 점 하나 빈 곳(붓 굵기에 가려 안 보임)까지만 봐준다. 획 끝도 이 기준을 따른다.")]
        [SerializeField, Range(0, 4)] private int maxStrokeGap = 1;
        [Tooltip("#533: 한 획을 이만큼 쓰면 깜빡이는 안내 점이 다음 획으로 넘어간다 (0~1). 칠하기 자체는 순서와 상관없다.")]
        [SerializeField, Range(.3f, 1f)] private float strokeAdvance = .8f;
        [Tooltip("#533: 글자에서 이만큼(글자 칸 대비) 떨어진 곳을 문지르면 '글자 밖'으로 센다.")]
        [SerializeField, Range(.05f, .3f)] private float offDistance = .11f;
        [Tooltip("#533: 글자 밖을 문지른 길이(글자 칸 폭 = 1)가 이만큼 쌓이면 '번졌다'며 처음부터 다시 쓴다. 아무렇게나 문지르기 막기. 클수록 너그럽다.")]
        [SerializeField, Range(.5f, 6f)] private float smudgeBudget = 2f;
        [Tooltip("#533: 번졌을 때 안내 문구를 바꿔 보여 주는 시간(초).")]
        [SerializeField, Range(.3f, 3f)] private float smudgeHintSeconds = 1.4f;
        [Tooltip("#533: 다음에 쓸 곳을 알려 주는 깜빡이는 점 색.")]
        [SerializeField] private Color nextMarkerColor = new Color(.16f, .47f, .93f, .9f);
        [Tooltip("#533: 흐린 글자의 경계선 색 (칠하기 전부터 글자 모양이 보이게).")]
        [SerializeField] private Color ghostOutlineColor = new Color(.62f, .45f, .22f, 1f);
        [Tooltip("#533: 흐린 글자 경계선 두께 (화면 픽셀).")]
        [SerializeField, Range(0f, 6f)] private float ghostOutlineWidth = 2.5f;
        [Tooltip("#533: 아직 안 쓴 \"물\" 글자(실제 글꼴)를 흐리게 깔아 두는 색.")]
        [SerializeField] private Color ghostColor = new Color(.88f, .76f, .48f, 1f);
        [Tooltip("#533: 먹 붓 굵기 (글자 칸 대비). 지나간 곳의 글자 획을 이만큼 채운다(글자 모양 밖으로는 안 번진다). 판정 거리와는 따로다.")]
        [SerializeField, Range(.06f, .3f)] private float brushWidth = .16f;
        [Tooltip("#533: 다 쓰면 금빛이 획 순서대로 글자를 훑고 지나가는 시간(초). 빈 곳도 이때 채워진다. 솔·도·시가 나는 동안 훑는다.")]
        [SerializeField, Range(.1f, 3f)] private float sweepSeconds = 1.5f;
        [Tooltip("#533 완성 가락 한 박자 (초).")]
        [SerializeField, Range(.1f, .8f)] private float beatSeconds = .3f;
        [Tooltip("#533 완성 가락 네 음이 나는 박자 (0부터). 기본 0·2·4·8 = 솔 (쉼) 도 (쉼) 시 (쉼 쉼 쉼) 라. 마지막 음과 함께 도장이 찍힌다.")]
        [SerializeField] private int[] noteBeats = { 0, 2, 4, 8 };
        [Header("#533 못 다 쓴 부적 (A가 끝날 때까지 못 쓰면 지귀 불에 타 버림)")]
        [Tooltip("#540: A가 끝나기 이만큼(초) 전부터 부적이 겉(가장자리)에서부터 안쪽으로 타들어 간다. A보다 길면 A가 시작하자마자(0초부터). 끝나는 순간 한가운데까지 다 탄다. 0이면 끔.")]
        [SerializeField, Range(0f, 15f)] private float burnWarnSeconds = 15f;
        [Tooltip("#540: 쓰는 동안에는 이만큼(0~1, 1 = 한가운데)까지만 탄다. 글자에 닿지 않는 가장자리에서 머물러 쓰기를 방해하지 않고, 다 못 쓰고 끝나면 그때 한가운데까지 마저 탄다.")]
        [SerializeField, Range(0f, 1f)] private float burnWriteLimit = .14f;
        [Tooltip("#540: 타들어 가는 빠르기 곡선. 1이면 일정하게, 클수록 처음엔 아주 천천히 타다가 끝으로 갈수록 빨라진다.")]
        [SerializeField, Range(1f, 4f)] private float burnEase = 1.2f;
        [Tooltip("#540: A가 끝나기 이만큼(초) 남았을 때부터 1초마다 톡 하는 진동.")]
        [SerializeField, Range(0f, 10f)] private float warnHapticSeconds = 3f;
        [Tooltip("#540: 타들어 가는 불 테두리 두께 (부적 폭 대비).")]
        [SerializeField, Range(.01f, .15f)] private float burnEdgeWidth = .045f;
        [Tooltip("예고 동안 1초마다 톡 하는 진동 세기 (0이면 끔).")]
        [SerializeField, Range(0f, 1f)] private float warnHapticIntensity = .3f;
        [Tooltip("#540: 다 못 쓰고 A가 끝난 뒤, 남은 불길이 마저 다 태우는 시간(초).")]
        [SerializeField, Range(.1f, 3f)] private float burnSeconds = .3f;
        [Tooltip("다 탄 재가 흩어지며 사라지는 시간(초).")]
        [SerializeField, Range(.1f, 2f)] private float ashSeconds = .45f;
        [Tooltip("탄 자리 색 (그을린 한지).")]
        [SerializeField] private Color charColor = new Color(.13f, .07f, .05f, .92f);
        [Tooltip("타들어 가는 불 테두리 색.")]
        [SerializeField] private Color emberColor = new Color(1f, .5f, .12f, 1f);
        [Tooltip("#540: 타들어 가는 동안 나는 타닥타닥 소리 (Resources/Audio 폴더, 반복). 타들어 갈수록 커진다.")]
        [SerializeField] private string crackleFolder = "JigwiTalismanCrackle";
        [SerializeField, Range(0f, 1f)] private float crackleVolume = .35f;
        [Tooltip("타는 동안 지글지글 이어지는 진동 세기 (0이면 끔).")]
        [SerializeField, Range(0f, 1f)] private float burnHapticIntensity = .5f;
        [Header("#533 도장 (완성하면 \"쾅\" 찍힘)")]
        [Tooltip("#540 도장 글자. 비우면(기본) 글자 대신 물방울과 물결 그림을 찍는다. 글자를 쓰면 그림 대신 글자(두 글자면 줄바꿈 \\n으로 세로).")]
        [SerializeField] private string stampText = "";
        [SerializeField] private Color stampColor = new Color(.78f, .09f, .07f, .95f);
        [Tooltip("도장 크기 (종이 폭 대비).")]
        [SerializeField, Range(.15f, .6f)] private float stampSize = .34f;
        [Tooltip("도장 자리 (종이 안 0~1, 아래가 0). 부적 오른쪽 아래에 글자와 살짝 겹치게.")]
        [SerializeField] private Vector2 stampAnchor = new Vector2(.7f, .17f);
        [Tooltip("도장 기울기 (도).")]
        [SerializeField, Range(-30f, 30f)] private float stampTilt = -7f;
        [Tooltip("도장이 내려와 찍히기까지 (초). 강세 음 순간에 닿는다.")]
        [SerializeField, Range(.05f, .5f)] private float stampDropSeconds = .14f;
        [Tooltip("#533: 훑기가 끝난 순간 부적이 \"쾅\" 하고 커지는 정도 (종이 크기 대비).")]
        [SerializeField, Range(0f, .25f)] private float burstPunch = .09f;
        [Tooltip("#533: 징을 친 뒤 부적이 \"웅~\" 울리며 떨리는 세기 (종이 높이 대비). 징 울림처럼 천천히 잦아든다.")]
        [SerializeField, Range(0f, .05f)] private float burstShake = .014f;
        [Tooltip("#533: 징 울림 떨림이 잦아드는 빠르기. 클수록 빨리 멎는다.")]
        [SerializeField, Range(.5f, 6f)] private float humDecay = 1.8f;
        [Tooltip("#533: 징 울림 떨림/빛 맥박의 빠르기 (초당 번).")]
        [SerializeField, Range(1f, 20f)] private float humHz = 7f;
        [Tooltip("#533: 글자에서 퍼져 나가는 빛 고리 하나가 퍼지는 시간(초).")]
        [SerializeField, Range(.1f, 2f)] private float ringSeconds = .9f;
        [Tooltip("#533: 징 울림처럼 차례로 퍼지는 빛 고리 수.")]
        [SerializeField, Range(1, 5)] private int ringCount = 3;
        [Tooltip("#533: 빛 고리 사이 간격(초).")]
        [SerializeField, Range(.05f, .8f)] private float ringGapSeconds = .28f;
        [Header("#533 소리 (Resources/Audio 아래 폴더 이름, 폴더 안 파일 중 무작위. 비우면 소리 없음)")]
        [Tooltip("글자를 칠하는 동안 이어지는 연필 사각 소리. 파일 중 무작위(같은 파일 연속 X)에 높낮이·크기를 조금씩 흔든다.")]
        [SerializeField] private string writeSoundFolder = "JigwiTalismanWrite";
        [SerializeField, Range(0f, 1f)] private float writeSoundVolume = .6f;
        [Tooltip("연필 소리 높낮이 흔들림 (0.08 = ±8%).")]
        [SerializeField, Range(0f, .3f)] private float writePitchJitter = .08f;
        [Tooltip("연필 소리 크기 흔들림 (0.2 = ±20%).")]
        [SerializeField, Range(0f, .5f)] private float writeVolumeJitter = .2f;
        [Tooltip("앞 연필 소리가 이만큼(0~1) 지나면 계속 칠할 때 다음 소리를 잇는다. 작을수록 촘촘하다.")]
        [SerializeField, Range(.3f, 1f)] private float writeChainAt = .7f;
        [Tooltip("#533 완성 가락: \"둥 둥 둥 .. 둥!\" 네 음. 폴더 안 파일을 이름순으로 1~4음에 쓴다. 파일이 4개보다 적으면 있는 파일을 높낮이를 올려(0/2/4/7반음) 채운다.")]
        [SerializeField] private string noteSoundFolder = "ComboHit";
        [Tooltip("앞 세 음 크기.")]
        [SerializeField, Range(0f, 1f)] private float noteSoundVolume = .7f;
        [Tooltip("마지막 강세 음(\"둥!\") 크기.")]
        [SerializeField, Range(0f, 1f)] private float accentSoundVolume = 1f;
        [Tooltip("(가락과 따로) 금빛이 획을 훑기 시작할 때 덧낼 소리. 비우면 없음. 예: JigwiTalismanSweep(가야금 띠리링을 조금 느리게). 예: FiveColorHit, CharmRelease")]
        [SerializeField] private string sweepSoundFolder = "";
        [SerializeField, Range(0f, 1f)] private float sweepSoundVolume = .7f;
        [Tooltip("(가락과 따로) \"쾅\" 순간 덧낼 소리. 기본 JigwiTalismanSplash(시원한 물벼락). 비우면 없음. 예: JigwiTalismanChime(긴 여운), YaguangShakeBellFinal")]
        [SerializeField] private string burstSoundFolder = "JigwiTalismanSplash";
        [Header("#540 지귀 목소리 (A/B 예고가 뜨는 순간, 각 기기에서)")]
        [Tooltip("A(부적) 예고 때 지귀 대사 폴더. 예: \"다 태워버리겠어!\". 비우면 없음.")]
        [SerializeField] private string voiceAFolder = "JigwiVoiceA";
        [Tooltip("B(물구슬 동시 명중) 예고 때 지귀 대사 폴더. 예: \"물방울로는 어림도 없지!\". 비우면 없음.")]
        [SerializeField] private string voiceBFolder = "JigwiVoiceB";
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
        [Tooltip("#540 불덩이가 타는 소리(반복). 불씨가 붙을 때부터 불덩이가 커질수록 점점 커진다. 비우면 없음.")]
        [SerializeField] private string fireLoopFolder = "JigwiFireballLoop";
        [Tooltip("불씨가 막 붙었을 때 타는 소리 크기.")]
        [SerializeField, Range(0f, 1f)] private float fireLoopVolume = .12f;
        [Tooltip("A가 끝나는 순간(불덩이가 가장 클 때)의 타는 소리 크기.")]
        [SerializeField, Range(0f, 1f)] private float fireLoopWarnVolume = 1f;
        [Tooltip("타는 소리가 커지고 줄어드는 빠르기(초).")]
        [SerializeField, Range(.05f, 1f)] private float fireLoopFadeSeconds = .25f;
        [SerializeField, Range(0f, 1f)] private float burstSoundVolume = 1f;
        [Tooltip("번졌을 때. 예: Miss, ComboFail")]
        [SerializeField] private string smudgeSoundFolder = "Miss";
        [SerializeField, Range(0f, 1f)] private float smudgeSoundVolume = .8f;
        [Header("#533 진동 (아이폰 빌드에서만, 설정의 진동 끄기를 따름)")]
        [Tooltip("글자를 칠할 때 사각사각 짧은 진동 세기. 0이면 끔.")]
        [SerializeField, Range(0f, 1f)] private float writeHapticIntensity = .35f;
        [Tooltip("칠하는 진동 사이 최소 간격(초). 너무 자주 울리지 않게.")]
        [SerializeField, Range(.02f, .3f)] private float writeHapticGapSeconds = .07f;
        [Tooltip("\"쾅\" 진동: 센 탭 한 번 뒤 징 울림처럼 이만큼 이어지는 진동(초). 0이면 탭만.")]
        [SerializeField, Range(0f, 2f)] private float burstHapticSeconds = 1f;
        [Tooltip("이어지는 징 진동 세기 (0~1).")]
        [SerializeField, Range(0f, 1f)] private float burstHapticIntensity = .6f;
        [Tooltip("이어지는 징 진동 날카로움 (0 = 둥글고 묵직).")]
        [SerializeField, Range(0f, 1f)] private float burstHapticSharpness = .15f;
        [Tooltip("번졌을 때 진동 세기. 0이면 끔.")]
        [SerializeField, Range(0f, 1f)] private float smudgeHapticIntensity = .9f;
        [Header("#540 완성 흐름: 불 꺼짐 → 숨 고르기 → 금빛 훑기·가락 → 도장 → 접혀 부적 구슬로 → 판으로 떨어짐")]
        [Tooltip("다 쓰면 타던 가장자리에 김이 오르며 불이 꺼지는 시간(초).")]
        [SerializeField, Range(0f, 1.5f)] private float extinguishSeconds = .45f;
        [Tooltip("불이 꺼진 뒤 가락이 시작하기 전 숨 고르는 시간(초).")]
        [SerializeField, Range(0f, 1f)] private float breathSeconds = .25f;
        [Tooltip("#540 3D 불덩이: 예고 때 박 위에 불씨가 붙는 \"화르륵\" 소리 폴더. 비우면 없음.")]
        [SerializeField] private string igniteSoundFolder = "JigwiFireballIgnite";
        [SerializeField, Range(0f, 1f)] private float igniteSoundVolume = .8f;
        [Tooltip("#540 3D 불덩이: 못 쓰고 끝나 불덩이가 조합판으로 떨어질 때 소리 폴더. 비우면 없음.")]
        [SerializeField] private string fallSoundFolder = "JigwiFireballFall";
        [SerializeField, Range(0f, 1f)] private float fallSoundVolume = .9f;
        [Tooltip("#540 못 쓰고 끝나 불덩이가 떨어질 때 지귀 목소리(\"모두 재가 되어라!\") 폴더. 비우면 없음.")]
        [SerializeField] private string failVoiceFolder = "JigwiVoiceAFail";
        [Tooltip("#540 불덩이가 조합판에 닿아 불이 확 번질 때 소리 폴더. 비우면 없음.")]
        [SerializeField] private string spreadSoundFolder = "JigwiFireSpread";
        [SerializeField, Range(0f, 1f)] private float spreadSoundVolume = 1f;
        [Tooltip("#540 부적 구슬이 판에 닿을 때 \"퐁\" 소리 폴더. 비우면 없음.")]
        [SerializeField] private string landSoundFolder = "JigwiTalismanLand";
        [SerializeField, Range(0f, 1f)] private float landSoundVolume = .9f;
        [Tooltip("#509/#540: 도장이 찍힌 뒤 빛난 채 머무는 시간(초). LabConfig '부적 보상 지연'이 0보다 크면 그 값에 맞춰 자동으로 정해져 이 값은 쓰지 않는다.")]
        [SerializeField, Range(.1f, 3f)] private float glowSeconds = .6f;
        [Tooltip("#540: 부적이 접히며 부적 구슬로 오므라드는 시간(초).")]
        [SerializeField, Range(.1f, 1.5f)] private float foldSeconds = .45f;
        [Tooltip("#540: 부적 구슬이 조합판 위로 툭 떨어지는 시간(초). Host가 보상 구슬을 주는 때(LabConfig '부적 보상 지연')와 맞춘다.")]
        [SerializeField, Range(.1f, 1.5f)] private float fallSeconds = .35f;
        [Tooltip("#509: 다 쓴 글씨가 빛날 때의 색 (금빛).")]
        [SerializeField] private Color glowColor = new Color(1f, .86f, .32f, 1f);
        [Tooltip("#533: \"물\" 글자의 가로 폭이 부적 종이 폭에서 차지하는 비율. 클수록 글자가 크다(세로는 종이 높이의 72%까지).")]
        [SerializeField, Range(.4f, .95f)] private float glyphWidthShare = .82f;
        [Tooltip("#533: 글자 획 굵기 (글자 칸 대비). 점끼리 겹쳐 이어진 굵은 획으로 보인다. 판정 거리와는 따로다.")]
        [SerializeField, Range(.03f, .15f)] private float strokeWidth = .085f;
        /// <summary>요청이 사라졌을 때 다시 보내기까지 (초)</summary>
        private const float ResendSeconds = 1f;
        private CanvasGroup paperGroup;
        private float finishAge = -1f;
        private Vector3 finishFrom;

        private LabNetwork source;
        private RectTransform root, paper, frame, glyphArea;
        private UnityEngine.UI.RawImage paperImage;
        private UnityEngine.UI.Image frameImage;
        private static Texture2D hanji;
        private static Sprite roundDot;
        /// <summary>#533: 신라문화체 굵은 "물"의 글자 모양 범위 (판정 점과 같은 글자 칸 0~1 좌표, 위가 1). 점선에 맞춰 잰 값.</summary>
        private static readonly Rect GlyphInkRect = Rect.MinMaxRect(.149f, .081f, .848f, .92f);
        /// <summary>#533: 흐린 "물" 글자(보통 Text). 먹 붓 점을 글자 모양 안에만 보이게 가리는 Mask 그림이기도 하다.</summary>
        private UnityEngine.UI.Text ghost, ghostEdge;
        private UnityEngine.UI.Outline ghostOutline;
        private static Sprite watercolorDot;
        private Color[] inkColors = Array.Empty<Color>();
        private const int GhostFontSize = 200;
        private readonly TextGenerator ghostGenerator = new TextGenerator();
        /// <summary>#533: 실제 글꼴 글자 위에 먹으로 쓰는 모양인지 (신라문화체가 없으면 예전 점선 모양).</summary>
        public bool InkMode => ghost != null;
        private UnityEngine.UI.Image[] dots = Array.Empty<UnityEngine.UI.Image>();
        private UnityEngine.UI.Text hint;
        private Font font;
        private bool blocking, hasLast;
        private Vector2 last;
        private string writtenRound;
        private float sentAt;
        private readonly LabJigwiTalismanStroke stroke = new LabJigwiTalismanStroke(LabJigwiTalismanStroke.Water, LabJigwiTalismanStroke.WaterStrokes);
        private UnityEngine.UI.Image nextMarker;
        private UnityEngine.UI.Image[] burstRings = Array.Empty<UnityEngine.UI.Image>();
        private static Sprite ringSprite;
        /// <summary>#533: 점마다 획 순서상 위치 (0~1). 완성 때 금빛이 이 순서로 훑는다.</summary>
        private float[] strokeRank = Array.Empty<float>();
        private AudioSource sfx, scratch, fireLoop, voice;
        private readonly HashSet<string> voicedRounds = new HashSet<string>();
        /// <summary>#540: 지귀 예고 대사를 낸 횟수와 마지막 대사 종류(A/B). 테스트용.</summary>
        public int VoiceCount { get; private set; }
        public string LastVoice { get; private set; }
        private float fireLoopLevel;
        /// <summary>#540: 지금 깔리는 타는 소리 크기 (0~1, 설정 크기 곱하기 전). 테스트용.</summary>
        public float FireLoopTarget { get; private set; }
        private readonly AudioSource[] notePlayers = new AudioSource[4];
        private int notesPlayed;
        [Tooltip("#533 완성 가락 음높이 (파일 원래 음에서 반음 수). 기본 ComboHit는 라(A4)라서 솔 도 시 라 = -2, +3, +2, 0. 파일이 4개 이상이면 쓰지 않는다.")]
        [SerializeField] private int[] noteSemitones = { -2, 3, 2, 0 };
        private float burnAge = -1f;
        // #540 effects that live outside the paper (board dim, landing splash, flying embers) and the 3D fireball.
        private UnityEngine.UI.Image boardDim, landRing;
        private LabJigwiFireball fireball;
        private LabJigwiFireball.Phase fireballSeen;
        private LabJigwiScreenPresentation presentation;
        private UnityEngine.UI.Image[] landDrops = Array.Empty<UnityEngine.UI.Image>(), flyEmbers = Array.Empty<UnityEngine.UI.Image>();
        private float dimLevel, landAge = -1f, flyAge = -1f;
        private Vector2 landAt, flyFrom;
        private bool landed;
        public float BoardDim => dimLevel;
        private float extinguishFrom;
        private bool sweepCued;
        // #540: at completion the flames and sparks freeze where they are and fade (no steam).
        private Color[] frozenFlames = Array.Empty<Color>(), frozenSparks = Array.Empty<Color>();
        private UnityEngine.UI.Image foldOrb, foldSeal;
        private UnityEngine.UI.Image scorch;
        private Texture2D burnTexture;
        private Color32[] burnPixels;
        private float[] burnDistance;
        private int burnWidth, burnHeight;
        private float burnShownFront = -1f;
        private AudioSource crackle;
        private float crackleLevel;
        /// <summary>#540: 지금 타들어 간 정도 (0 = 안 탐, 1 = 한가운데까지). 테스트용.</summary>
        public float BurnFront { get; private set; }
        private int lastWarnTick = -1;
        /// <summary>#533: A가 끝나 가는 정도 (0 = 아직, 1 = 끝나는 순간). 부적 가장자리 그을림 예고에 쓴다.</summary>
        public float WarnLevel { get; private set; }
        /// <summary>#540: 지금 A가 끝나기까지 남은 시간(초). 부적이 없으면 무한대.</summary>
        public float TimeLeft { get; private set; } = float.PositiveInfinity;
        private Vector3 burnFrom;
        private RectTransform emberBand;
        private UnityEngine.UI.Image[] flames = Array.Empty<UnityEngine.UI.Image>();
        private UnityEngine.UI.Image[] sparks = Array.Empty<UnityEngine.UI.Image>();
        private Sprite[] flameFrames;
        /// <summary>#533: 못 다 쓴 부적이 타는 중인지. 이때 조합판은 이미 풀려 있다.</summary>
        public bool Burning => burnAge >= 0f;
        public float BurnTotalSeconds => burnSeconds + ashSeconds;

        /// <summary>
        /// #540: 타닥타닥 크기 — 겉에서부터 타들어 가는 동안 탄 만큼 커지고, 끝나서 마저 다 탈 때 가장 크며, 재가 되면 잦아든다.
        /// 다 써서 완성하거나 일시정지 중이면 없다.
        /// </summary>
        public float CrackleTarget(bool paused)
        {
            if (Burning)
            {
                float ash = Mathf.Clamp01((burnAge - burnSeconds) / Mathf.Max(.01f, ashSeconds));
                return crackleVolume * (1f - ash);
            }
            if (!Visible || paused || Finishing) return 0f;
            return crackleVolume * Mathf.Clamp01(WarnLevel * 1.5f);
        }
        /// <summary>#533: 못 다 써서 탄 횟수 (테스트용).</summary>
        public int BurnCount { get; private set; }
        private float burnStartFront;
        private RectTransform stampRoot;
        private UnityEngine.UI.Image stampImage;
        private UnityEngine.UI.Text stampLabel;
        private UnityEngine.UI.Image stampEmblem;
        private static Sprite stampEmblemSprite;
        private UnityEngine.UI.Image[] stampSpecks = Array.Empty<UnityEngine.UI.Image>();
        private static Sprite stampSprite;
        /// <summary>#533: 도장이 찍혀 보이는지 (테스트용).</summary>
        public bool StampVisible => stampRoot != null && stampRoot.gameObject.activeSelf;
        private AudioClip[] writeClips;
        private string writeClipsFolder;
        private int lastWriteClip = -1;
        /// <summary>#533: 테스트용 — 연필 소리를 낸 횟수.</summary>
        public int WriteSoundCount { get; private set; }
        private float lastWriteHaptic = -1f;
        private bool burstPlayed;
        /// <summary>#533: 테스트용 — 마지막으로 낸 효과 이름(Sweep/Burst/Smudge)과 횟수.</summary>
        public string LastCue { get; private set; }
        public int CueCount { get; private set; }
        private readonly List<int> fresh = new List<int>();

        /// <summary>부적이 보이는지</summary>
        public bool Visible { get; private set; }
        /// <summary>지금 덮고 있는 A 회차 id (보이지 않으면 null)</summary>
        public string RoundId { get; private set; }
        /// <summary>판 조작을 막아야 하는지 (보일 때만)</summary>
        public bool WantsBoardBlocked => Visible;
        public RectTransform Paper => paper;
        /// <summary>점선 중 칠해진 비율 (0~1)</summary>
        public float Coverage => stroke.Coverage;
        /// <summary>이 화면에서 부적을 다 쓴 횟수 (요청을 보낸 횟수, 다시 보낸 것은 빼고)</summary>
        public int CompletedCount { get; private set; }
        /// <summary>#533: 글자 밖을 너무 문질러 처음부터 다시 쓰게 된 횟수.</summary>
        public int SmudgeCount { get; private set; }
        private const string HintText = "손가락으로 '물'을 적어\n부적을 완성하세요.";
        private const string SmudgeText = "번졌어요!\n글자를 따라 다시 써 주세요.";
        private float smudgeHintUntil = -1f;
        public LabJigwiTalismanStroke Stroke => stroke;
        /// <summary>#509: 다 쓴 부적이 빛나며 사라지는 중인지. 이때 조합판은 이미 풀려 있다.</summary>
        public bool Finishing => finishAge >= 0f;
        private float NoteAt(int index) => Mathf.Max(.01f, beatSeconds)
            * (noteBeats != null && index < noteBeats.Length ? Mathf.Max(0, noteBeats[index]) : index * 2);
        private float AccentAt => Mathf.Max(.01f, NoteAt(3));
        /// <summary>#533: 마지막 강세 음(도장)이 나는 때 (완성 연출 시작부터 초).</summary>
        public float AccentSeconds => IntroSeconds + AccentAt;
        /// <summary>#540: 다 쓴 뒤 가락이 시작하기까지 (불 꺼짐 + 숨 고르기).</summary>
        public float IntroSeconds => Mathf.Max(0f, extinguishSeconds) + Mathf.Max(0f, breathSeconds);
        /// <summary>#540: 다 쓴 뒤 부적 구슬이 판에 닿기까지. LabConfig의 부적 보상 지연과 같아야 한다.</summary>
        public float FinishSeconds => IntroSeconds + AccentAt + HoldSeconds + foldSeconds + fallSeconds;
        /// <summary>
        /// #540 (L9): 도장 뒤 빛난 채 머무는 시간. LabConfig의 부적 보상 지연(Host)이 있으면 그 값에 맞춰 늘고 줄어,
        /// 부적 구슬이 판에 닿는 때와 보상 구슬이 생기는 때가 늘 같다. 설정은 그 값 하나만 바꾸면 된다.
        /// </summary>
        public float HoldSeconds
        {
            get
            {
                float delay = source != null && source.config != null ? source.config.JigwiTalismanRewardDelaySeconds : 0f;
                if (delay <= 0f) return glowSeconds;
                return Mathf.Max(.1f, delay - (IntroSeconds + AccentAt + foldSeconds + fallSeconds));
            }
        }
        /// <summary>#509: 사라지는 동안의 부적 불투명도 (1 → 0).</summary>
        public float PaperAlpha => paperGroup != null ? paperGroup.alpha : 1f;

        public void Bind(LabNetwork network, Font labelFont)
        {
            source = network;
            if (labelFont != null) font = labelFont;
            EnsureWidgets();
            if (labelFont != null && hint != null) hint.font = labelFont;
            if (ghost != null && IsShilla(labelFont) && ghost.font != labelFont)
            {
                ghost.font = labelFont;
                if (ghostEdge != null) ghostEdge.font = labelFont;
                LayoutGlyph();
            }
        }

        /// <summary>아직 부적을 안 쓴 내 자리가 있는 진행 중인 A. 없으면 null.</summary>
        public static LabJigwiABRound PendingTalisman(LabSnapshot snapshot, ulong localPlayer, double now)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.jigwiAB?.enabled != true) return null;
            LabJigwiABRound round = snapshot.jigwiAB.Active(now);
            if (round == null || round.kind != LabJigwiABKind.PersonalBurn || !round.Active(now)) return null;
            foreach (LabJigwiABPlayerFire seat in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                if (seat != null && seat.playerId == localPlayer) return seat.talismanAt < 0d ? round : null;
            return null;
        }

        private void LateUpdate()
        {
            if (source == null) { Present(null, ulong.MaxValue, 0d); return; }
            LabSnapshot snapshot = source.Snapshot;
            Present(snapshot, source.LocalPlayerId, source.JigwiABPresentationTime);
            if (Finishing) AdvanceFinish(Time.unscaledDeltaTime);
            if (Burning) AdvanceBurn(Time.unscaledDeltaTime);
            bool paused = snapshot == null || snapshot.paused || source.GameStartHeld || source.BattlePresentationHeld;
            DriveFireball(snapshot);
            UpdateFireLoop(!paused, Time.unscaledDeltaTime);
            UpdateCrackle(CrackleTarget(paused), Time.unscaledDeltaTime);
            Camera boardView = source.board != null ? source.board.ViewCamera : null;
            DrawEffects(boardView, Time.unscaledDeltaTime);
            if (Visible)
            {
                FitToBoard(source.board != null ? source.board.ViewCamera : null);
                bool held = paused;
                if (held) hasLast = false; else ReadPointer();
                UpdateNextMarker(Time.unscaledTime);
                DrawWarning(Time.unscaledTime);
                if (smudgeHintUntil >= 0f && Time.unscaledTime >= smudgeHintUntil) RestoreHint();
            }
            ResendIfLost(snapshot);
        }

        /// <summary>테스트와 LateUpdate가 같이 쓰는 표시 진입점.</summary>
        public void Present(LabSnapshot snapshot, ulong localPlayer, double now)
        {
            EnsureWidgets();
            ObserveTaunts(snapshot, now);
            LabJigwiABRound round = PendingTalisman(snapshot, localPlayer, now);
            // #533: the A ran out before this seat finished — Jigwi's fire burns the unfinished talisman away.
            if (Visible && round == null && !Finishing && !Burning && RanOut(snapshot, RoundId, localPlayer, now)) BeginBurn();
            // A new talisman cuts a still-fading (or still-burning) old one short.
            if (round != null && round.id != writtenRound && Finishing) EndFinish();
            if (round != null && round.id != writtenRound && Burning) EndBurn();
            if (round?.id != RoundId && round?.id != writtenRound && !Finishing && !Burning) ResetWriting();
            // A talisman this screen already finished stays hidden while the Host confirms it.
            Visible = round != null && round.id != writtenRound;
            RoundId = Visible ? round.id : null;
            OmenLevel = Visible || Finishing || Burning ? 0f : WarningOmen(snapshot, localPlayer, now);
            float left = Visible ? (float)(round.endsAt - now) : float.PositiveInfinity;
            TimeLeft = left;
            WarnLevel = 0f;
            if (Visible && burnWarnSeconds > 0f)
            {
                // #540: from the A's start (or burnWarnSeconds before its end), slow at first and faster near the end.
                double start = Math.Max(round.activeAt, round.endsAt - burnWarnSeconds);
                double span = Math.Max(.01d, round.endsAt - start);
                float linear = Mathf.Clamp01((float)((now - start) / span));
                WarnLevel = Mathf.Pow(linear, Mathf.Max(1f, burnEase));
            }
            if (paper != null) paper.gameObject.SetActive(Visible || Finishing || Burning);
            if (!Visible) hasLast = false;
            SetBoardBlocked(Visible);
        }

        /// <summary>
        /// #540 지귀 목소리: A/B 예고가 뜨는 순간 한 번씩 대사를 낸다(공유 연출 시계 기준이라 기기마다 같은 때).
        /// 늦게 들어와 이미 지난 예고는 다시 틀지 않는다.
        /// </summary>
        private void ObserveTaunts(LabSnapshot snapshot, double now)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.jigwiAB?.enabled != true || snapshot.jigwiAB.rounds == null) return;
            foreach (LabJigwiABRound item in snapshot.jigwiAB.rounds)
            {
                if (item == null || item.id == null || now < item.warningAt || now - item.warningAt > .6d) continue;
                if (item.resolution == LabJigwiABResolution.Skipped || item.resolution == LabJigwiABResolution.Cancelled) continue;
                if (!voicedRounds.Add(item.id)) continue;
                bool a = item.kind == LabJigwiABKind.PersonalBurn;
                PlayVoice(a ? "A" : "B", a ? voiceAFolder : voiceBFolder);
            }
        }

        private void PlayVoice(string name, string folder)
        {
            VoiceCount++; LastVoice = name;
            if (string.IsNullOrEmpty(folder) || voiceVolume <= 0f) return;
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/" + folder);
            if (clips == null || clips.Length == 0) return;
            if (voice == null)
            {
                voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false; voice.spatialBlend = 0f;
            }
            voice.Stop();
            voice.clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            voice.volume = voiceVolume * LabAudioSettings.SfxVolume;
            voice.Play();
        }

        /// <summary>#533: 이 회차 A가 (시간이 다 돼) 끝났는데 내 자리 부적은 아직 안 쓴 상태인지. 게임이 끝난 경우는 빼고.</summary>
        private static bool RanOut(LabSnapshot snapshot, string roundId, ulong localPlayer, double now)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.jigwiAB?.rounds == null || roundId == null) return false;
            foreach (LabJigwiABRound round in snapshot.jigwiAB.rounds)
            {
                if (round == null || round.id != roundId) continue;
                if (round.Active(now)) return false;
                foreach (LabJigwiABPlayerFire seat in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                    if (seat != null && seat.playerId == localPlayer) return seat.talismanAt < 0d;
                return false;
            }
            return false;
        }

        /// <summary>
        /// #540 실패 예고: A가 끝나기 몇 초 전부터 부적이 겉(가장자리)에서부터 안쪽으로 타들어 간다. 끝나는 순간 한가운데까지 다 탄다.
        /// 타는 경계는 들쭉날쭉하게 빛나고, 그 위로 불꽃이 일렁이며 불티가 날린다. 1초마다 약한 진동이 톡.
        /// </summary>
        private void DrawWarning(float time)
        {
            if (Burning) return;
            float level = Finishing ? 0f : WarnLevel;
            // #540: while writing, the fire only eats the margin outside the letter so it never hides the strokes.
            DrawEdgeBurn(level * burnWriteLimit, time, level);
            int tick = level > 0f && TimeLeft <= warnHapticSeconds ? Mathf.FloorToInt(time) : -1;
            if (tick >= 0 && tick != lastWarnTick)
            {
                lastWarnTick = tick;
                LabHaptics.PlayDirect(Config, LabHapticStyle.Light, warnHapticIntensity * (.5f + .5f * level));
            }
            if (tick < 0) lastWarnTick = -1;
        }

        /// <summary>
        /// #540 겉에서부터 타들어 가기: front(0~1, 1 = 한가운데)까지 그을린 자국, 그 경계에 빛나는 불 테두리, 테두리를 따라 불꽃과 불티.
        /// 그을린 모양은 미리 잰 "가장자리에서 떨어진 정도"(들쭉날쭉한 결 포함)로 정해서 매번 같다. strength는 불꽃·불티 세기.
        /// </summary>
        private void DrawEdgeBurn(float front, float time, float strength)
        {
            BurnFront = Mathf.Max(0f, front);
            bool on = front > 0f;
            if (scorch != null)
            {
                if (scorch.gameObject.activeSelf != on) scorch.gameObject.SetActive(on);
                if (on) PaintBurnTexture(front);
            }
            bool flamesOn = on && front < 1f && paper != null;
            if (emberBand != null && emberBand.gameObject.activeSelf != flamesOn) emberBand.gameObject.SetActive(flamesOn);
            if (paper == null) return;
            Rect r = paper.rect;
            float inset = Mathf.Clamp01(front) * r.width * .5f;
            float w = Mathf.Max(0f, r.width - inset * 2f), h = Mathf.Max(0f, r.height - inset * 2f);
            if (flamesOn)
                for (int k = 0; k < flames.Length; k++)
                {
                    if (flames[k] == null) continue;
                    if (flameFrames != null && flameFrames.Length > 0)
                        flames[k].sprite = flameFrames[(int)(time * 14f + k * 2) % flameFrames.Length];
                    float flicker = .75f + .35f * Mathf.Sin(time * (17f + k * 3f) + k);
                    float size = r.width * .13f * flicker * Mathf.Lerp(.6f, 1f, strength);
                    RectTransform f = flames[k].rectTransform;
                    f.anchorMin = f.anchorMax = new Vector2(.5f, .5f);
                    f.sizeDelta = new Vector2(size * .8f, size);
                    f.anchoredPosition = Perimeter((k + .5f) / flames.Length + time * .02f, w, h) + Vector2.up * size * .3f;
                    flames[k].color = new Color(1f, 1f, 1f, .95f);
                }
            for (int k = 0; k < sparks.Length; k++)
            {
                if (sparks[k] == null) continue;
                bool lit = flamesOn && k < Mathf.CeilToInt(sparks.Length * Mathf.Clamp01(strength + .2f));
                if (sparks[k].gameObject.activeSelf != lit) sparks[k].gameObject.SetActive(lit);
                if (!lit) continue;
                float cycle = time / .8f + k * .37f, t = Mathf.Repeat(cycle, 1f);
                RectTransform sp = sparks[k].rectTransform;
                sp.anchorMin = sp.anchorMax = new Vector2(.5f, .5f);
                Vector2 from = Perimeter((k * .618034f + Mathf.Floor(cycle) * .29f) % 1f, w, h);
                sp.anchoredPosition = from + new Vector2(Mathf.Sin(t * 5f + k) * r.width * .03f, t * r.height * .14f);
                float size = r.width * (.016f + .01f * ((k * .414f) % 1f)) * (1f - t * .5f);
                sp.sizeDelta = new Vector2(size, size);
                Color c = Color.Lerp(new Color(1f, .9f, .5f, 1f), emberColor, t); c.a = 1f - t; sparks[k].color = c;
            }
        }

        /// <summary>#540: 가운데 기준 w×h 직사각형 둘레 위 한 점 (s = 0~1, 둘레를 한 바퀴).</summary>
        private static Vector2 Perimeter(float s, float w, float h)
        {
            float total = Mathf.Max(.001f, 2f * (w + h)), d = Mathf.Repeat(s, 1f) * total;
            float hw = w * .5f, hh = h * .5f;
            if (d < w) return new Vector2(-hw + d, hh);
            d -= w; if (d < h) return new Vector2(hw, hh - d);
            d -= h; if (d < w) return new Vector2(hw - d, -hh);
            d -= w; return new Vector2(-hw, -hh + d);
        }

        /// <summary>#540: 부적 크기의 "가장자리에서 떨어진 정도" 지도 (0 = 가장자리, 1 = 한가운데, 들쭉날쭉한 결 포함). 한 번만 잰다.</summary>
        private void BuildBurnMap()
        {
            burnWidth = 96; burnHeight = Mathf.RoundToInt(96f / Mathf.Max(.2f, paperAspect));
            burnDistance = new float[burnWidth * burnHeight];
            burnPixels = new Color32[burnDistance.Length];
            float half = burnWidth * .5f;
            for (int y = 0; y < burnHeight; y++)
                for (int x = 0; x < burnWidth; x++)
                {
                    float dx = Mathf.Min(x + .5f, burnWidth - x - .5f), dy = Mathf.Min(y + .5f, burnHeight - y - .5f);
                    float d = Mathf.Min(dx, dy) / half;
                    float noise = Mathf.PerlinNoise(x * .11f + 3f, y * .11f + 17f) * .7f + Mathf.PerlinNoise(x * .35f + 9f, y * .35f) * .3f;
                    burnDistance[y * burnWidth + x] = Mathf.Clamp01(d * .9f + (noise - .5f) * .16f + .02f);
                }
            burnTexture = new Texture2D(burnWidth, burnHeight, TextureFormat.RGBA32, false)
            { name = "Talisman Edge Burn (#540 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            burnShownFront = -1f;
        }

        private void PaintBurnTexture(float front)
        {
            if (burnTexture == null || burnDistance == null) return;
            if (Mathf.Abs(front - burnShownFront) < .003f) return;
            burnShownFront = front;
            float band = Mathf.Max(.005f, burnEdgeWidth), halo = band * .6f;
            Color32 clear = new Color32(0, 0, 0, 0), hot = new Color(1f, .88f, .45f, 1f);
            for (int i = 0; i < burnDistance.Length; i++)
            {
                float d = burnDistance[i];
                if (d < front - band)
                {
                    float shade = .85f + .15f * ((i * 7919) % 97) / 97f;
                    Color c = charColor; c.r *= shade; c.g *= shade; c.b *= shade;
                    burnPixels[i] = c;
                }
                else if (d < front)
                    burnPixels[i] = Color.Lerp(hot, emberColor, (front - d) / band);
                else if (d < front + halo)
                {
                    Color c = emberColor; c.a = .45f * (1f - (d - front) / halo);
                    burnPixels[i] = c;
                }
                else burnPixels[i] = clear;
            }
            burnTexture.SetPixels32(burnPixels);
            burnTexture.Apply(false, false);
        }

        /// <summary>
        /// #540 불덩이가 타는 소리를 깐다. 불씨가 붙으면 작게 시작해 불덩이가 커질수록 점점 커지고(초조하게),
        /// 다 쓰면 불과 함께 멈추며, 떨어질 때는 가장 크게. 반복 소리라 시작 위치를 매번 달리 한다.
        /// </summary>
        private void UpdateFireLoop(bool running, float dt)
        {
            FireLoopTarget = running ? FireballLoudness(fireballSeen, fireballProgress, fireLoopVolume, fireLoopWarnVolume) : 0f;
            float step = dt / Mathf.Max(.01f, fireLoopFadeSeconds);
            fireLoopLevel = Mathf.MoveTowards(fireLoopLevel, FireLoopTarget, step);
            if (fireLoopLevel <= 0f)
            {
                if (fireLoop != null && fireLoop.isPlaying) fireLoop.Stop();
                return;
            }
            if (fireLoop == null)
            {
                AudioClip clip = string.IsNullOrEmpty(fireLoopFolder) ? null
                    : Array.Find(Resources.LoadAll<AudioClip>("Audio/" + fireLoopFolder), value => value != null);
                if (clip == null) return;
                fireLoop = gameObject.AddComponent<AudioSource>();
                fireLoop.playOnAwake = false; fireLoop.spatialBlend = 0f; fireLoop.loop = true; fireLoop.clip = clip;
            }
            if (!fireLoop.isPlaying)
            {
                fireLoop.time = UnityEngine.Random.Range(0f, Mathf.Max(0f, fireLoop.clip.length - .05f));
                fireLoop.Play();
            }
            fireLoop.volume = fireLoopLevel * LabAudioSettings.SfxVolume;
        }

        /// <summary>#540: 타들어 가는 동안 타닥타닥 소리. 타들어 갈수록 커지고, 다 타서 재가 되면 잦아든다.</summary>
        private void UpdateCrackle(float target, float dt)
        {
            crackleLevel = Mathf.MoveTowards(crackleLevel, target, dt / .2f);
            if (crackleLevel <= 0f)
            {
                if (crackle != null && crackle.isPlaying) crackle.Stop();
                return;
            }
            if (crackle == null)
            {
                AudioClip clip = string.IsNullOrEmpty(crackleFolder) ? null
                    : Array.Find(Resources.LoadAll<AudioClip>("Audio/" + crackleFolder), value => value != null);
                if (clip == null) return;
                crackle = gameObject.AddComponent<AudioSource>();
                crackle.playOnAwake = false; crackle.spatialBlend = 0f; crackle.loop = true; crackle.clip = clip;
            }
            if (!crackle.isPlaying)
            {
                crackle.time = UnityEngine.Random.Range(0f, Mathf.Max(0f, crackle.clip.length - .05f));
                crackle.Play();
            }
            crackle.volume = crackleLevel * LabAudioSettings.SfxVolume;
        }

        /// <summary>
        /// #540 부적 밖 연출: (V3) 예고·판이 막히는 동안 판이 살짝 어두워짐, (V8) 부적 구슬이 판에 닿을 때 물 튐,
        /// (V10) 못 다 쓴 부적이 탈 때 불티가 양옆(타는 구슬 쪽)으로 날아감.
        /// </summary>
        public void DrawEffects(Camera boardView, float dt)
        {
            if (paper == null || root == null) return;
            if (!Finishing && !Burning) paper.localScale = Vector3.one;
            // V3: the board dims while the talisman holds it.
            float dimTarget = Visible || Burning ? 1f : OmenLevel;
            dimLevel = Mathf.MoveTowards(dimLevel, dimTarget, dt * 3f);
            if (boardDim != null)
            {
                bool on = dimLevel > .001f;
                if (boardDim.gameObject.activeSelf != on) boardDim.gameObject.SetActive(on);
                if (on)
                {
                    Rect board = boardView != null ? boardView.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height * .46f);
                    PlaceScreenRect(boardDim.rectTransform, board);
                    boardDim.color = new Color(.05f, .02f, .02f, .32f * dimLevel);
                }
            }
            AdvanceLanding(dt);
            AdvanceFlyEmbers(dt);
        }

        /// <summary>화면 픽셀 사각형을 root 기준 위치·크기로 옮긴다.</summary>
        private void PlaceScreenRect(RectTransform target, Rect pixels)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pixels.min, eye, out Vector2 low)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pixels.max, eye, out Vector2 high)) return;
            target.anchorMin = target.anchorMax = target.pivot = new Vector2(.5f, .5f);
            target.localPosition = new Vector3((low.x + high.x) * .5f, (low.y + high.y) * .5f, 0f);
            target.sizeDelta = new Vector2(Mathf.Abs(high.x - low.x), Mathf.Abs(high.y - low.y));
        }

        /// <summary>#540 (V8): 부적 구슬이 판에 닿는 자리에서 물이 튄다 (둥근 물결 + 물방울).</summary>
        private void BeginLanding()
        {
            landAge = 0f;
            landAt = paper != null ? (Vector2)paper.localPosition : Vector2.zero;
            Cue("Land", landSoundFolder, landSoundVolume);
            AdvanceLanding(0f);
        }

        private void AdvanceLanding(float dt)
        {
            if (landAge < 0f) return;
            landAge += dt;
            const float life = .55f;
            float t = landAge / life;
            bool on = t < 1f;
            float unit = paper != null ? paper.rect.width : 100f;
            if (landRing != null)
            {
                if (landRing.gameObject.activeSelf != on) landRing.gameObject.SetActive(on);
                if (on)
                {
                    float grow = 1f - (1f - t) * (1f - t);
                    landRing.rectTransform.localPosition = landAt;
                    landRing.rectTransform.sizeDelta = Vector2.one * unit * (.2f + .7f * grow);
                    landRing.color = new Color(.35f, .75f, 1f, .9f * (1f - t));
                }
            }
            for (int k = 0; k < landDrops.Length; k++)
            {
                if (landDrops[k] == null) continue;
                if (landDrops[k].gameObject.activeSelf != on) landDrops[k].gameObject.SetActive(on);
                if (!on) continue;
                float angle = (k + .5f) / landDrops.Length * Mathf.PI + Mathf.PI * .05f;
                Vector2 v = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * unit * (.45f + .2f * (k % 3));
                landDrops[k].rectTransform.localPosition = landAt + v * t + Vector2.down * unit * 1.2f * t * t;
                float size = unit * .05f * (1f - t * .5f);
                landDrops[k].rectTransform.sizeDelta = new Vector2(size, size);
                landDrops[k].color = new Color(.45f, .82f, 1f, 1f - t);
            }
            if (!on) landAge = -1f;
        }

        /// <summary>#540 (V10): 부적이 탈 때 불티가 양옆(타는 구슬 쪽)으로 포물선을 그리며 날아간다.</summary>
        private void BeginFlyEmbers()
        {
            flyAge = 0f;
            flyFrom = paper != null ? (Vector2)paper.localPosition : Vector2.zero;
            AdvanceFlyEmbers(0f);
        }

        private void AdvanceFlyEmbers(float dt)
        {
            if (flyAge < 0f) return;
            flyAge += dt;
            const float life = .8f;
            float unit = paper != null ? paper.rect.width : 100f, height = paper != null ? paper.rect.height : 200f;
            bool any = false;
            for (int k = 0; k < flyEmbers.Length; k++)
            {
                if (flyEmbers[k] == null) continue;
                float t = (flyAge - (k % 4) * .08f) / life;
                bool on = t >= 0f && t < 1f;
                any |= t < 1f;
                if (flyEmbers[k].gameObject.activeSelf != on) flyEmbers[k].gameObject.SetActive(on);
                if (!on) continue;
                float side = k % 2 == 0 ? -1f : 1f;
                float startY = (((k * .618f) % 1f) - .5f) * height * .8f;
                Vector2 start = flyFrom + new Vector2(side * unit * .5f, startY);
                Vector2 reach = new Vector2(side * unit * (.55f + .25f * ((k * .414f) % 1f)), unit * .35f);
                flyEmbers[k].rectTransform.localPosition = start + new Vector2(reach.x * t, reach.y * t - unit * 1.1f * t * t);
                float size = unit * (.035f + .02f * ((k * .27f) % 1f)) * (1f - t * .4f);
                flyEmbers[k].rectTransform.sizeDelta = new Vector2(size, size);
                Color c = Color.Lerp(new Color(1f, .92f, .55f, 1f), emberColor, t); c.a = 1f - t * t; flyEmbers[k].color = c;
            }
            if (!any) flyAge = -1f;
        }

        private void BeginBurn()
        {
            if (paper == null) return;
            BurnCount++;
            burnAge = 0f; burnFrom = paper.localPosition;
            paper.gameObject.SetActive(true);
            if (hint != null) hint.gameObject.SetActive(false);
            if (nextMarker != null) nextMarker.gameObject.SetActive(false);
            LastCue = "Burn"; CueCount++;
            BeginFlyEmbers();
            burnStartFront = Mathf.Clamp01(BurnFront);
            LabHaptics.PlayDirect(Config, LabHapticStyle.Medium, .7f, burnSeconds, burnHapticIntensity, .65f);
            DrawBurn();
        }

        /// <summary>#533: LateUpdate와 테스트가 같이 쓰는 타기 진행.</summary>
        public void AdvanceBurn(float seconds)
        {
            if (!Burning || float.IsNaN(seconds) || seconds < 0f) return;
            burnAge += seconds;
            if (burnAge >= BurnTotalSeconds) { EndBurn(); return; }
            DrawBurn();
        }

        /// <summary>
        /// #540 못 다 쓴 부적: 겉에서부터 타들어 가던 불이 남은 곳까지 마저 다 태우고(쓰다 만 물빛 획도 불이 닿으면 잿빛),
        /// 다 탄 부적은 재가 되어 살짝 가라앉으며 흩어진다. 타닥타닥 소리는 재가 되며 잦아든다.
        /// </summary>
        private void DrawBurn()
        {
            float t = Mathf.Clamp01(burnAge / Mathf.Max(.01f, burnSeconds));
            float front = Mathf.Lerp(burnStartFront, 1.08f, t);
            float ash = Mathf.Clamp01((burnAge - burnSeconds) / Mathf.Max(.01f, ashSeconds));
            Rect r = paper.rect;
            DrawEdgeBurn(front, burnAge + 100f, 1f - ash);
            // Written strokes the fire has reached turn to ash.
            Color ashColor = new Color(.32f, .3f, .3f, .85f);
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null || !stroke.IsCovered(i)) continue;
                float burnt = Mathf.Clamp01((front - DotBurnDistance(i)) / .06f);
                dots[i].color = Color.Lerp(InkColor(i), ashColor, burnt);
            }
            if (ghost != null) ghost.color = Color.Lerp(ghostColor, new Color(.35f, .27f, .2f, ghostColor.a), Mathf.Clamp01(front));
            float sink = ash * ash;
            if (paperGroup != null) paperGroup.alpha = 1f - sink;
            paper.localScale = Vector3.one * (1f - .04f * sink);
            float tremble = front < 1f ? r.height * .004f : 0f;
            paper.localPosition = burnFrom + new Vector3(Mathf.Sin(burnAge * 53f) * tremble, -r.height * .04f * sink, 0f);
        }

        /// <summary>#540: 글자 점 자리가 가장자리에서 떨어진 정도 (타들어 가는 지도에서 읽음).</summary>
        private float DotBurnDistance(int index)
        {
            if (burnDistance == null || glyphArea == null || paper == null) return 1f;
            Rect r = paper.rect; float side = glyphArea.rect.width;
            Vector2 local = glyphArea.anchoredPosition + (stroke.Samples[index] - new Vector2(.5f, .5f)) * side;
            float u = Mathf.Clamp01(local.x / Mathf.Max(1f, r.width) + .5f), v = Mathf.Clamp01(local.y / Mathf.Max(1f, r.height) + .5f);
            int x = Mathf.Clamp((int)(u * burnWidth), 0, burnWidth - 1), y = Mathf.Clamp((int)(v * burnHeight), 0, burnHeight - 1);
            return burnDistance[y * burnWidth + x];
        }

        private void EndBurn()
        {
            if (!Burning) return;
            burnAge = -1f;
            if (paper == null) return;
            if (paperGroup != null) paperGroup.alpha = 1f;
            paper.localScale = Vector3.one; paper.localPosition = burnFrom;
            if (scorch != null) scorch.gameObject.SetActive(false);
            if (emberBand != null) emberBand.gameObject.SetActive(false);
            foreach (UnityEngine.UI.Image spark in sparks) if (spark != null) spark.gameObject.SetActive(false);
            if (ghost != null) ghost.color = ghostColor;
            if (hint != null) hint.gameObject.SetActive(true);
            ResetWriting();
            paper.gameObject.SetActive(Visible);
        }

        /// <summary>
        /// 부적 위 한 획 조각을 칠한다. from/to는 "물" 글자 칸 기준 0~1 좌표다. 다 쓰면 Host에 알리고 부적을 내린다.
        /// LateUpdate의 손가락/마우스 입력과 테스트가 같이 쓴다.
        /// </summary>
        public void PaintNormalized(Vector2 from, Vector2 to)
        {
            if (!Visible) return;
            fresh.Clear();
            // #533: rubbing far from the letter (coin-scratch scribbling) smudges the talisman; it starts over.
            stroke.AddOffGuide(from, to, offDistance);
            if (stroke.OffLength > smudgeBudget) { Smudge(); return; }
            stroke.StrokeAdvance = strokeAdvance;
            stroke.Paint(from, to, Tolerance, fresh);
            for (int i = 0; i < fresh.Count; i++)
                if (fresh[i] < dots.Length && dots[fresh[i]] != null) ShowWritten(dots[fresh[i]], fresh[i]);
            if (fresh.Count > 0) Scratch();
            if (fresh.Count > 0 && Time.unscaledTime - lastWriteHaptic >= writeHapticGapSeconds)
            {
                lastWriteHaptic = Time.unscaledTime;
                LabHaptics.PlayDirect(Config, LabHapticStyle.Light, writeHapticIntensity);
            }
            // #533: finish only when the letter LOOKS whole — every stroke written end to end with no visible gap —
            // not merely when enough dots are counted (that used to finish with the ㄹ tail still blank).
            if (stroke.Coverage + 1e-5f < Required || !stroke.LooksComplete(maxStrokeGap)) return;
            writtenRound = RoundId;
            CompletedCount++;
            Send();
            Visible = false; RoundId = null; hasLast = false;
            SetBoardBlocked(false);
            // #509: the written "물" glows, then the talisman slowly fades away (the board is already free).
            BeginFinish();
        }

        private void ShowWritten(UnityEngine.UI.Image dot, int index)
        {
            dot.color = InkColor(index);
            if (InkMode) dot.gameObject.SetActive(true);
        }

        /// <summary>#533: 지금 써야 할 획의 다음 빈 점에서 깜빡이는 표시. 획 순서를 알려 줄 뿐, 다른 곳을 칠해도 된다.</summary>
        private void UpdateNextMarker(float time)
        {
            if (nextMarker == null) return;
            int next = stroke.NextPoint;
            bool show = Visible && next >= 0;
            if (nextMarker.gameObject.activeSelf != show) nextMarker.gameObject.SetActive(show);
            if (!show) return;
            RectTransform m = nextMarker.rectTransform;
            m.anchorMin = m.anchorMax = stroke.Samples[next];
            m.anchoredPosition = Vector2.zero;
            float pulse = .5f + .5f * Mathf.Sin(time * 6f);
            m.localScale = Vector3.one * (.75f + .35f * pulse);
            Color c = nextMarkerColor; c.a *= .45f + .55f * pulse; nextMarker.color = c;
        }

        private void BuildStrokeRank()
        {
            strokeRank = new float[dots.Length];
            int total = Mathf.Max(1, stroke.Samples.Length - 1), n = 0;
            for (int s = 0; s < stroke.StrokeCount; s++)
                foreach (int index in stroke.StrokePoints(s))
                    if (index < strokeRank.Length) strokeRank[index] = n++ / (float)total;
        }

        /// <summary>
        /// #540: 도장 안 그림 (런타임에 한 번 만든다). 낙관처럼 전서체(篆書) "水" 모양을 그림으로 판다 — 가운데 물결치는 긴 획과
        /// 양옆 두 개씩 굽은 획. 글꼴이 아니라 그림이라 어느 기기에서나 같고, 인주가 고르지 않게 묻은 결은 테두리와 같다.
        /// </summary>
        internal static Sprite StampEmblemSprite()
        {
            if (stampEmblemSprite != null) return stampEmblemSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Talisman Stamp Emblem (#540 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            var centre = new Vector2[25];
            for (int i = 0; i < centre.Length; i++)
            {
                float v = .08f + i * .84f / (centre.Length - 1);
                centre[i] = new Vector2(.5f + .05f * Mathf.Sin(v * Mathf.PI * 3f), v);
            }
            Vector2[] leftTop = { new Vector2(.38f, .86f), new Vector2(.28f, .8f), new Vector2(.22f, .7f), new Vector2(.23f, .58f), new Vector2(.29f, .52f) };
            Vector2[] leftLow = { new Vector2(.38f, .46f), new Vector2(.28f, .4f), new Vector2(.22f, .3f), new Vector2(.23f, .18f), new Vector2(.29f, .12f) };
            Vector2[][] strokes = { centre, leftTop, leftLow, Mirror(leftTop), Mirror(leftLow) };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + .5f) / size, (y + .5f) / size);
                    float nearest = float.MaxValue;
                    foreach (Vector2[] line in strokes)
                        for (int i = 1; i < line.Length; i++) nearest = Mathf.Min(nearest, SegmentDistance(p, line[i - 1], line[i]));
                    // Carved strokes with slightly worn edges.
                    float wear = .006f * Mathf.PerlinNoise(x * .4f + 7f, y * .4f + 3f);
                    float ink = Mathf.Clamp01((.047f - wear - nearest) * size * .6f);
                    float grain = Mathf.PerlinNoise(x * .35f + 23f, y * .35f + 5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, ink * Mathf.Clamp01(.55f + grain * .9f));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            stampEmblemSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            stampEmblemSprite.hideFlags = HideFlags.DontSave;
            return stampEmblemSprite;
        }

        private static Vector2[] Mirror(Vector2[] line)
        {
            var mirrored = new Vector2[line.Length];
            for (int i = 0; i < line.Length; i++) mirrored[i] = new Vector2(1f - line[i].x, line[i].y);
            return mirrored;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude <= 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>#533: 도장 테두리 무늬 (런타임에 한 번 만든다). 굵은 네모 테두리에 인주가 고르지 않게 묻은 결.</summary>
        internal static Sprite StampSprite()
        {
            if (stampSprite != null) return stampSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Talisman Stamp (#533 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size, v = (y + .5f) / size;
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    float wobble = .012f * Mathf.PerlinNoise(x * .21f + 3f, y * .21f + 9f);
                    float outer = Mathf.Clamp01((edge - .02f - wobble) * size * .5f);
                    float inner = Mathf.Clamp01((.14f + wobble - edge) * size * .5f);
                    float grain = Mathf.PerlinNoise(x * .35f + 17f, y * .35f + 41f);
                    float ink = outer * inner * Mathf.Clamp01(.55f + grain * .9f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(ink));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            stampSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            stampSprite.hideFlags = HideFlags.DontSave;
            return stampSprite;
        }

        /// <summary>#533: 빛 고리 무늬 (런타임에 한 번 만든다). 가운데가 빈 부드러운 고리.</summary>
        private static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Talisman Burst Ring (#533 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            float half = size * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(half, half)) / half;
                    float band = Mathf.Clamp01(1f - Mathf.Abs(r - .82f) / .14f);
                    float glow = Mathf.Clamp01(1f - Mathf.Abs(r - .8f) / .4f) * .35f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(band * band + glow) * (r < 1f ? 1f : 0f));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            ringSprite.hideFlags = HideFlags.DontSave;
            return ringSprite;
        }

        private Color InkColor(int index) => InkMode && index < inkColors.Length ? inkColors[index] : writtenColor;

        /// <summary>#533: 점마다 정해 둔(매번 같은) 수채화 색 흔들림. 물감이 고르게 안 먹은 느낌.</summary>
        private void BuildInkColors()
        {
            inkColors = new Color[dots.Length];
            var random = new System.Random(533);
            Color.RGBToHSV(writtenColor, out float h, out float sat, out float v);
            for (int i = 0; i < inkColors.Length; i++)
            {
                float a = (float)random.NextDouble() * 2f - 1f, b = (float)random.NextDouble() * 2f - 1f, c = (float)random.NextDouble() * 2f - 1f;
                Color tint = Color.HSVToRGB(Mathf.Repeat(h + a * watercolorJitter * .12f, 1f),
                    Mathf.Clamp01(sat + b * watercolorJitter * .5f), Mathf.Clamp01(v + c * watercolorJitter * .4f));
                tint.a = Mathf.Clamp01(writtenColor.a * (1f + c * watercolorJitter * 1.5f));
                inkColors[i] = tint;
            }
        }

        private void BeginFinish()
        {
            if (paper == null) return;
            finishAge = 0f; finishFrom = paper.localPosition;
            paper.gameObject.SetActive(true);
            if (hint != null) hint.gameObject.SetActive(false);
            if (nextMarker != null) nextMarker.gameObject.SetActive(false);
            if (stampRoot != null) stampRoot.gameObject.SetActive(false);
            // #540: the fire stops dead — the flames and sparks freeze right where they are, then quietly fade
            // with the scorch (no steam).
            frozenFlames = Freeze(flames); frozenSparks = Freeze(sparks);
            extinguishFrom = BurnFront;
            burstPlayed = false; notesPlayed = 0; sweepCued = false; landed = false;
            DrawFinish();
        }

        /// <summary>#509: LateUpdate와 테스트가 같이 쓰는 빛남/사라짐 진행.</summary>
        public void AdvanceFinish(float seconds)
        {
            if (!Finishing || float.IsNaN(seconds) || seconds < 0f) return;
            finishAge += seconds;
            if (finishAge >= FinishSeconds) { EndFinish(); return; }
            DrawFinish();
        }

        /// <summary>
        /// #533 완성 연출: ① 금빛이 획 순서대로 글자를 훑으며 빈 곳까지 채움 → ② "쾅": 부적이 커졌다 돌아오며 떨리고,
        /// 글자에서 빛 고리가 퍼지고, 흐린 글자·경계선도 금빛으로 → ③ 빛난 채 잠깐 머문 뒤 위로 떠오르며 사라짐.
        /// </summary>
        private void DrawFinish()
        {
            DrawExtinguish(finishAge);
            // After the fire is out and a breath, the melody and the gold sweep begin.
            float t = finishAge - IntroSeconds;
            if (t >= 0f && !sweepCued) { sweepCued = true; Cue("Sweep", sweepSoundFolder, sweepSoundVolume); }
            float sweep = Mathf.Max(.01f, sweepSeconds);
            float sweepT = t < 0f ? 0f : Mathf.Clamp01(t / sweep);
            // 솔 (rest) 도 (rest) 시 (rest rest rest) 라! — the last, accented note lands with the seal.
            while (t >= 0f && notesPlayed < 3 && t >= NoteAt(notesPlayed)) PlayNote(notesPlayed++);
            float after = t - AccentAt;
            DrawStamp(after);
            if (after >= 0f && !burstPlayed)
            {
                burstPlayed = true;
                PlayNote(3);
                Cue("Burst", burstSoundFolder, burstSoundVolume);
                LabHaptics.PlayDirect(Config, LabHapticStyle.Heavy, 1f, burstHapticSeconds, burstHapticIntensity, burstHapticSharpness);
            }
            float burst = after < 0f ? 0f : Mathf.Clamp01(after / .35f);
            float bump = after < 0f ? 0f : Mathf.Sin(Mathf.PI * burst);
            // #540: after the glow the talisman folds into a talisman orb, which then drops down to the board.
            float hold = HoldSeconds;
            float fold = Mathf.Clamp01((after - hold) / Mathf.Max(.01f, foldSeconds));
            float fall = Mathf.Clamp01((after - hold - foldSeconds) / Mathf.Max(.01f, fallSeconds));
            if (fall >= 1f && !landed) { landed = true; BeginLanding(); }
            float folded = fold * fold * (3f - 2f * fold);
            Color gold = new Color(glowColor.r, glowColor.g, glowColor.b, 1f);
            // #533 gong hum: after the strike everything trembles and pulses, dying away like a gong's ring.
            float ring = after < 0f ? 0f : Mathf.Exp(-after * humDecay);
            float wave = Mathf.Sin(after * humHz * 2f * Mathf.PI);
            Color hot = Color.Lerp(gold, new Color(1f, .99f, .88f, 1f), .5f + .5f * wave);
            Color shine = Color.Lerp(gold, hot, ring);
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                bool written = stroke.IsCovered(i);
                if (!InkMode && !written) continue;
                float rank = i < strokeRank.Length ? strokeRank[i] : 0f;
                float lit = Mathf.Clamp01((sweepT * 1.12f - rank) / .12f);
                if (lit <= 0f)
                {
                    // Not reached yet: written ink stays as written, gaps stay empty.
                    if (written) { dots[i].color = InkColor(i); dots[i].rectTransform.localScale = Vector3.one; }
                    continue;
                }
                if (InkMode && !dots[i].gameObject.activeSelf) dots[i].gameObject.SetActive(true);
                dots[i].color = Color.Lerp(InkColor(i), shine, lit);
                float pop = 1f + .35f * Mathf.Sin(Mathf.PI * lit) * (1f - lit * .5f) + .12f * bump + .06f * ring * wave;
                dots[i].rectTransform.localScale = Vector3.one * pop;
            }
            if (ghost != null) ghost.color = Color.Lerp(ghostColor, new Color(1f, .94f, .62f, ghostColor.a), burst);
            if (ghostEdge != null)
            {
                Color edge = Color.Lerp(ghostOutlineColor, gold, burst);
                ghostEdge.color = edge;
                if (ghostOutline != null) ghostOutline.effectColor = edge;
            }
            DrawRings(after, gold);
            if (paperImage != null) paperImage.color = Color.Lerp(paperColor, new Color(1f, .97f, .85f, paperColor.a), Mathf.Max(burst * .8f, sweepT * .3f));
            if (paperGroup != null) paperGroup.alpha = 1f - fall * .9f;
            // #533/#540: the seal's slam punches the talisman; then it crumples into an orb and drops to the board.
            float paperScale = (1f + burstPunch * bump) * Mathf.Lerp(1f, .1f, folded);
            paper.localScale = Vector3.one * paperScale;
            paper.localRotation = Quaternion.Euler(0f, 0f, folded * 12f);
            float shake = burstShake * paper.rect.height * ring * (1f - folded);
            var jitter = new Vector3(wave * shake, Mathf.Cos(after * humHz * 1.37f * 2f * Mathf.PI) * shake * .35f, 0f);
            // #540: the talisman lies on the board, so the orb only drops a little onto the board surface.
            paper.localPosition = finishFrom + jitter + Vector3.down * (fall * fall * paper.rect.height * .2f);
            DrawFoldOrb(folded, paperScale, gold);
        }

        /// <summary>
        /// #533 도장: 강세 음 stampDropSeconds 전에 크고 옅게 나타나 기울며 내려와 강세 음 순간 "쾅" 닿고, 살짝 눌렸다 돌아온다.
        /// 닿는 순간 붉은 인주 방울이 둘레에 튄다. 그 뒤로는 찍힌 채 부적과 함께 사라진다.
        /// </summary>
        private void DrawStamp(float after)
        {
            if (stampRoot == null) return;
            float drop = Mathf.Max(.01f, stampDropSeconds);
            float t = (after + drop) / drop; // 0 → 1 while descending, ≥ 1 once stamped
            bool show = t >= 0f;
            if (stampRoot.gameObject.activeSelf != show) stampRoot.gameObject.SetActive(show);
            if (!show) return;
            float scale, alpha;
            if (t < 1f)
            {
                float fall = t * t; // accelerate into the paper
                scale = Mathf.Lerp(2.4f, 1f, fall);
                alpha = Mathf.Lerp(.15f, 1f, fall);
            }
            else
            {
                float settle = Mathf.Clamp01(after / .22f);
                scale = 1f - .08f * Mathf.Sin(Mathf.PI * settle) * (1f - settle);
                alpha = 1f;
            }
            stampRoot.localScale = Vector3.one * scale;
            stampRoot.localRotation = Quaternion.Euler(0f, 0f, stampTilt + (t < 1f ? (1f - t) * -10f : 0f));
            Color c = stampColor; c.a *= alpha;
            if (stampImage != null) stampImage.color = c;
            if (stampLabel != null) stampLabel.color = c;
            if (stampEmblem != null) stampEmblem.color = c;
            float splat = after < 0f ? 0f : Mathf.Clamp01(after / .12f);
            for (int k = 0; k < stampSpecks.Length; k++)
            {
                if (stampSpecks[k] == null) continue;
                bool on = after >= 0f;
                if (stampSpecks[k].gameObject.activeSelf != on) stampSpecks[k].gameObject.SetActive(on);
                if (!on) continue;
                stampSpecks[k].rectTransform.localScale = Vector3.one * (.3f + .7f * splat);
                Color speck = stampColor; speck.a *= .85f; stampSpecks[k].color = speck;
            }
        }

        /// <summary>#533: 징 울림처럼 빛 고리가 차례로 퍼진다. 뒤 고리일수록 옅고 크게.</summary>
        private void DrawRings(float after, Color gold)
        {
            for (int k = 0; k < burstRings.Length; k++)
            {
                UnityEngine.UI.Image ringImage = burstRings[k];
                if (ringImage == null) continue;
                float local = after - k * ringGapSeconds;
                float t = after < 0f || local < 0f ? -1f : local / Mathf.Max(.01f, ringSeconds);
                bool show = k < ringCount && t >= 0f && t < 1f;
                if (ringImage.gameObject.activeSelf != show) ringImage.gameObject.SetActive(show);
                if (!show) continue;
                float grow = 1f - (1f - t) * (1f - t);
                ringImage.rectTransform.localScale = Vector3.one * (.35f + (1.9f + .5f * k) * grow);
                Color c = gold; c.a = (1f - t) * .9f / (1f + k * .6f); ringImage.color = c;
            }
        }

        /// <summary>
        /// #540 불 꺼짐: 다 쓴 순간 불꽃과 불티가 그 자리에 그대로 멈추고(흔들림·움직임 없음), 그을음과 함께 조용히 옅어진다. 김은 없다.
        /// </summary>
        private void DrawExtinguish(float age)
        {
            float e = Mathf.Clamp01(age / Mathf.Max(.01f, extinguishSeconds));
            bool cooling = extinguishFrom > 0f && e < 1f;
            if (scorch != null)
            {
                if (scorch.gameObject.activeSelf != cooling) scorch.gameObject.SetActive(cooling);
                if (cooling) scorch.color = new Color(1f, 1f, 1f, 1f - e);
            }
            float keep = 1f - e * e * (3f - 2f * e);
            Fade(flames, frozenFlames, keep);
            Fade(sparks, frozenSparks, keep);
            if (e >= 1f && emberBand != null && emberBand.gameObject.activeSelf) emberBand.gameObject.SetActive(false);
        }

        private static Color[] Freeze(UnityEngine.UI.Image[] parts)
        {
            var colors = new Color[parts.Length];
            for (int k = 0; k < parts.Length; k++)
                colors[k] = parts[k] != null && parts[k].gameObject.activeInHierarchy ? parts[k].color : Color.clear;
            return colors;
        }

        private static void Fade(UnityEngine.UI.Image[] parts, Color[] from, float keep)
        {
            for (int k = 0; k < parts.Length && k < from.Length; k++)
            {
                if (parts[k] == null || from[k].a <= 0f) continue;
                Color c = from[k]; c.a *= keep; parts[k].color = c;
            }
        }

        /// <summary>#540: 오므라드는 부적 가운데에서 금빛 부적 구슬이 자라난다 (종이가 작아져도 구슬은 제 크기).</summary>
        private void DrawFoldOrb(float folded, float paperScale, Color gold)
        {
            if (foldOrb == null) return;
            bool on = folded > 0f;
            if (foldOrb.gameObject.activeSelf != on) foldOrb.gameObject.SetActive(on);
            if (!on || paper == null) return;
            float size = paper.rect.width * .3f;
            foldOrb.rectTransform.sizeDelta = new Vector2(size, size);
            foldOrb.rectTransform.anchoredPosition = Vector2.zero;
            foldOrb.rectTransform.localScale = Vector3.one * (folded / Mathf.Max(.05f, paperScale));
            foldOrb.color = new Color(gold.r, gold.g, gold.b, folded);
            if (foldSeal != null) foldSeal.color = new Color(stampColor.r, stampColor.g, stampColor.b, folded);
        }

        private void EndFinish()
        {
            finishAge = -1f;
            if (paper == null) return;
            if (paperGroup != null) paperGroup.alpha = 1f;
            paper.localScale = Vector3.one; paper.localPosition = finishFrom; paper.localRotation = Quaternion.identity;
            if (foldOrb != null) foldOrb.gameObject.SetActive(false);
            if (emberBand != null) emberBand.gameObject.SetActive(false);
            foreach (UnityEngine.UI.Image spark in sparks) if (spark != null) spark.gameObject.SetActive(false);
            frozenFlames = frozenSparks = Array.Empty<Color>();
            if (scorch != null) { scorch.color = Color.white; scorch.gameObject.SetActive(false); }
            if (paperImage != null) paperImage.color = paperColor;
            if (hint != null) hint.gameObject.SetActive(true);
            for (int i = 0; i < dots.Length; i++) if (dots[i] != null) dots[i].rectTransform.localScale = Vector3.one;
            if (ghost != null) ghost.color = ghostColor;
            if (ghostEdge != null) ghostEdge.color = ghostOutlineColor;
            if (ghostOutline != null) ghostOutline.effectColor = ghostOutlineColor;
            foreach (UnityEngine.UI.Image ringImage in burstRings) if (ringImage != null) ringImage.gameObject.SetActive(false);
            if (stampRoot != null) stampRoot.gameObject.SetActive(false);
            paper.gameObject.SetActive(Visible);
        }

        private LabConfig Config => source != null ? source.config : null;

        /// <summary>
        /// #533: 칠하는 동안 연필 사각 소리를 잇는다. 앞 소리가 거의 끝났을 때만 다음 소리를 내서 겹쳐 쌓이지 않고,
        /// 손을 멈추면 지금 소리까지만 난다. 매번 다른 파일·높낮이·크기로 단조롭지 않게.
        /// </summary>
        private void Scratch()
        {
            if (string.IsNullOrEmpty(writeSoundFolder) || writeSoundVolume <= 0f) return;
            if (writeClips == null || writeClipsFolder != writeSoundFolder)
            {
                writeClipsFolder = writeSoundFolder;
                writeClips = Resources.LoadAll<AudioClip>("Audio/" + writeSoundFolder);
                lastWriteClip = -1;
            }
            if (writeClips == null || writeClips.Length == 0) return;
            if (scratch == null)
            {
                scratch = gameObject.AddComponent<AudioSource>();
                scratch.playOnAwake = false; scratch.spatialBlend = 0f; scratch.loop = false;
            }
            if (scratch.isPlaying && scratch.clip != null && scratch.time < scratch.clip.length * writeChainAt) return;
            int index = UnityEngine.Random.Range(0, writeClips.Length);
            if (writeClips.Length > 1 && index == lastWriteClip)
                index = (index + 1 + UnityEngine.Random.Range(0, writeClips.Length - 1)) % writeClips.Length;
            lastWriteClip = index;
            scratch.clip = writeClips[index];
            scratch.pitch = 1f + UnityEngine.Random.Range(-writePitchJitter, writePitchJitter);
            scratch.volume = Mathf.Clamp01(writeSoundVolume * (1f + UnityEngine.Random.Range(-writeVolumeJitter, writeVolumeJitter)))
                * LabAudioSettings.SfxVolume;
            scratch.Play();
            WriteSoundCount++;
        }

        /// <summary>#533: 완성 가락의 index번째 음 (0~2 = "둥", 3 = 강세 "둥!"). 음마다 따로 재생해 앞 음 여운이 끊기지 않는다.</summary>
        private void PlayNote(int index)
        {
            NotesPlayedCount++;
            if (string.IsNullOrEmpty(noteSoundFolder)) return;
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/" + noteSoundFolder);
            if (clips == null || clips.Length == 0) return;
            Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
            AudioClip clip; float pitch = 1f;
            if (clips.Length >= 4) clip = clips[index];
            else
            {
                clip = clips[index % clips.Length];
                int semitone = noteSemitones != null && index < noteSemitones.Length ? noteSemitones[index] : 0;
                pitch = Mathf.Pow(2f, semitone / 12f);
            }
            AudioSource player = notePlayers[index];
            if (player == null)
            {
                player = notePlayers[index] = gameObject.AddComponent<AudioSource>();
                player.playOnAwake = false; player.spatialBlend = 0f;
            }
            player.pitch = pitch;
            player.PlayOneShot(clip, (index == 3 ? accentSoundVolume : noteSoundVolume) * LabAudioSettings.SfxVolume);
        }

        /// <summary>#533: 테스트용 — 완성 가락 음을 낸 횟수.</summary>
        public int NotesPlayedCount { get; private set; }

        /// <summary>#533: Resources/Audio/폴더의 소리 하나를 이 기기에서만 낸다(파일이 없으면 조용히 넘어감).</summary>
        private void Cue(string name, string folder, float volume)
        {
            LastCue = name; CueCount++;
            if (string.IsNullOrEmpty(folder) || volume <= 0f) return;
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/" + folder);
            if (clips == null || clips.Length == 0) return;
            if (sfx == null)
            {
                sfx = gameObject.AddComponent<AudioSource>();
                sfx.playOnAwake = false; sfx.spatialBlend = 0f;
            }
            sfx.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)], volume * LabAudioSettings.SfxVolume);
        }

        private float Tolerance => source != null && source.config != null ? source.config.JigwiTalismanTolerance : .07f;
        private float Required => source != null && source.config != null ? source.config.JigwiTalismanCoverage : .9f;

        private void Send()
        {
            sentAt = Time.unscaledTime;
            if (source != null && writtenRound != null) source.RequestJigwiTalismanDone(writtenRound);
        }

        private void ResendIfLost(LabSnapshot snapshot)
        {
            if (writtenRound == null || source == null || Time.unscaledTime - sentAt < ResendSeconds) return;
            LabJigwiABRound round = PendingTalisman(snapshot, source.LocalPlayerId, source.JigwiABPresentationTime);
            if (round != null && round.id == writtenRound) Send();
        }

        private void ReadPointer()
        {
            Vector2 screen = default; bool pressed = false, began = false;
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && touch.press.isPressed)
            { screen = touch.position.ReadValue(); pressed = true; began = touch.press.wasPressedThisFrame; }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            { screen = Mouse.current.position.ReadValue(); pressed = true; began = Mouse.current.leftButton.wasPressedThisFrame; }
            if (!pressed || !TryNormalize(screen, out Vector2 point)) { hasLast = false; return; }
            if (began || !hasLast) last = point;
            Vector2 from = last;
            last = point; hasLast = true;
            PaintNormalized(from, point);
        }

        private bool TryNormalize(Vector2 screen, out Vector2 point)
        {
            point = default;
            if (glyphArea == null) return false;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(glyphArea, screen, eye, out Vector2 local)) return false;
            Rect rect = glyphArea.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;
            point = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
            return true;
        }

        private void Smudge()
        {
            SmudgeCount++;
            ResetWriting();
            Cue("Smudge", smudgeSoundFolder, smudgeSoundVolume);
            LabHaptics.PlayDirect(Config, LabHapticStyle.Rigid, smudgeHapticIntensity);
            if (hint == null) return;
            hint.text = SmudgeText;
            smudgeHintUntil = Time.unscaledTime + smudgeHintSeconds;
        }

        private void RestoreHint()
        {
            smudgeHintUntil = -1f;
            if (hint != null) hint.text = HintText;
        }

        private void ResetWriting()
        {
            stroke.Clear(); hasLast = false; writtenRound = null;
            RestoreHint();
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                if (InkMode) { dots[i].gameObject.SetActive(false); dots[i].color = InkColor(i); }
                else dots[i].color = guideColor;
            }
        }

        private void SetBoardBlocked(bool value)
        {
            if (blocking == value) return;
            blocking = value;
            if (source != null && source.board != null) source.board.SetUiInputBlocked(value);
        }

        /// <summary>#540: 0 → 1 across the A warning when a talisman is coming for this seat (0 otherwise). Dims the board.</summary>
        public float OmenLevel { get; private set; }

        private static float WarningOmen(LabSnapshot snapshot, ulong localPlayer, double now)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.jigwiAB?.rounds == null) return 0f;
            foreach (LabJigwiABRound item in snapshot.jigwiAB.rounds)
            {
                if (item == null || item.kind != LabJigwiABKind.PersonalBurn || !item.Warning(now)) continue;
                foreach (LabJigwiABPlayerFire seat in item.players ?? Array.Empty<LabJigwiABPlayerFire>())
                    if (seat != null && seat.playerId == localPlayer && seat.talismanAt < 0d)
                        return Mathf.Clamp01((float)((now - item.warningAt) / Math.Max(.01d, item.activeAt - item.warningAt)));
            }
            return 0f;
        }

        /// <summary>
        /// #540 "다 태워버리겠어" 3D 불덩이(`LabJigwiFireball`): 예고에 박 위에 불씨가 붙고, 쓰는 동안 커지고, 다 쓰면 그대로 멈춰
        /// 사라지고, 못 쓰면 조합판으로 떨어진다. 불씨가 붙을 때 "톡", 떨어질 때 "쿵" 진동과 소리.
        /// </summary>
        /// <summary>#540: 불덩이 단계·진행 → 불 소리 크기 (start = 불씨, full = 가장 클 때).</summary>
        public static float FireballLoudness(LabJigwiFireball.Phase phase, float progress, float start, float full)
        {
            switch (phase)
            {
                case LabJigwiFireball.Phase.Kindle: return start * Mathf.Clamp01(progress * 2f);
                case LabJigwiFireball.Phase.Grow: return Mathf.Lerp(start, full, Mathf.Pow(Mathf.Clamp01(progress), 1.3f));
                case LabJigwiFireball.Phase.Fall: return full;
                default: return 0f;
            }
        }

        private float fireballProgress, spreadAt = -1f;

        private void DriveFireball(LabSnapshot snapshot)
        {
            if (!Application.isPlaying || source == null) return;
            if (fireball == null) fireball = LabJigwiFireball.Create();
            double now = source.JigwiABPresentationTime;
            LabJigwiFireball.Phase phase = LabJigwiFireball.Evaluate(snapshot, source.LocalPlayerId, now, out float progress, out float size);
            if (presentation == null) presentation = GetComponentInParent<LabJigwiScreenPresentation>();
            fireball.Show(phase, progress, size, source.target, presentation != null ? presentation.battleCamera : null, (float)now);
            fireballProgress = progress;
            // The fireball lands on the board: fire spreads over it with a whoosh and a heavy thud.
            if (spreadAt >= 0f && Time.unscaledTime >= spreadAt)
            {
                spreadAt = -1f;
                Cue("Spread", spreadSoundFolder, spreadSoundVolume);
                LabHaptics.PlayDirect(Config, LabHapticStyle.Heavy, 1f, .6f, .8f, .4f);
            }
            if (phase != fireballSeen)
            {
                if (phase == LabJigwiFireball.Phase.Kindle)
                {
                    Cue("Ignite", igniteSoundFolder, igniteSoundVolume);
                    LabHaptics.PlayDirect(Config, LabHapticStyle.Light, .55f);
                }
                else if (phase == LabJigwiFireball.Phase.Fall)
                {
                    Cue("Fall", fallSoundFolder, fallSoundVolume);
                    PlayVoice("AFail", failVoiceFolder);
                    LabHaptics.PlayDirect(Config, LabHapticStyle.Heavy, .9f);
                    spreadAt = Time.unscaledTime + (1f - progress) * LabJigwiFireball.FallSeconds;
                }
                fireballSeen = phase;
            }
        }

        /// <summary>
        /// #540: 부적은 조합판 위에 놓인다. 판 안쪽(위 둥근 테두리와 아래 이름·기운 막대는 비움) 가운데에 세로로 긴 부적을 둔다.
        /// 화면 픽셀 기준 부적 자리를 돌려준다.
        /// </summary>
        private Rect PaperScreenRect(Camera boardCamera)
        {
            Rect board = boardCamera != null ? boardCamera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height * .46f);
            float inset = Mathf.Min(board.width, board.height) * margin;
            float bottom = board.yMin + board.height * bottomInset, top = board.yMax - board.height * topInset;
            float height = Mathf.Max(1f, top - bottom);
            float width = Mathf.Min(board.width - inset * 2f, height * paperAspect);
            return new Rect(board.center.x - width * .5f, bottom, width, height);
        }

        private void FitToBoard(Camera boardCamera)
        {
            if (paper == null || root == null) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            Rect pixels = PaperScreenRect(boardCamera);
            Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pixels.min, eye, out Vector2 low)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pixels.max, eye, out Vector2 high)) return;
            // Local points are relative to the root's pivot, which is what localPosition uses.
            paper.anchorMin = paper.anchorMax = paper.pivot = new Vector2(.5f, .5f);
            paper.localPosition = new Vector3((low.x + high.x) * .5f, (low.y + high.y) * .5f, 0f);
            paper.sizeDelta = new Vector2(Mathf.Abs(high.x - low.x), Mathf.Abs(high.y - low.y));
            LayoutGlyph();
        }

        /// <summary>
        /// "물" 칸은 늘 정사각형이다. #533: 칸 크기를 칸 전체가 아니라 실제 글자 획이 놓인 범위에 맞춰,
        /// 글자가 종이 폭의 glyphWidthShare만큼 차지하게 키우고 글자 가운데를 안내 문구 아래에 둔다.
        /// 판정 좌표(칸 기준 0~1)는 그대로라 점선과 판정은 함께 커진다.
        /// </summary>
        private void LayoutGlyph()
        {
            if (glyphArea == null || paper == null) return;
            Vector2 size = paper.rect.size;
            // #533: with the real glyph the letter's own outline sets the size; the old dotted look pads by its stroke width.
            Rect bounds = InkMode ? GlyphInkRect : SampleBounds(stroke.Samples);
            float pad = InkMode ? 0f : strokeWidth;
            float spanX = Mathf.Max(.05f, bounds.width + pad), spanY = Mathf.Max(.05f, bounds.height + pad);
            float side = Mathf.Max(1f, Mathf.Min(size.x * glyphWidthShare / spanX, size.y * .72f / spanY));
            glyphArea.anchorMin = glyphArea.anchorMax = glyphArea.pivot = new Vector2(.5f, .5f);
            // Put the letter's own centre (not the box centre) a little below the paper centre, under the hint.
            Vector2 centre = bounds.center;
            glyphArea.anchoredPosition = new Vector2((.5f - centre.x) * side, -size.y * .06f + (.5f - centre.y) * side);
            glyphArea.sizeDelta = new Vector2(side, side);
            float dot = Mathf.Max(4f, side * (InkMode ? brushWidth : strokeWidth));
            if (nextMarker != null) nextMarker.rectTransform.sizeDelta = Vector2.one * Mathf.Max(6f, side * .07f);
            float seal = Mathf.Max(8f, size.x * stampSize);
            if (stampRoot != null)
            {
                stampRoot.anchorMin = stampRoot.anchorMax = stampAnchor;
                stampRoot.anchoredPosition = Vector2.zero;
                stampRoot.sizeDelta = new Vector2(seal, seal);
                // #540: one syllable fills the seal; two stacked syllables share it.
                int lines = string.IsNullOrEmpty(stampText) ? 1 : stampText.Split('\n').Length;
                if (stampLabel != null) stampLabel.fontSize = Mathf.Max(8, Mathf.RoundToInt(seal * (lines > 1 ? .36f : .58f)));
                for (int k = 0; k < stampSpecks.Length; k++)
                {
                    if (stampSpecks[k] == null) continue;
                    // Fixed splatter around the seal (same every time).
                    float angle = k * 2.399963f + .7f, reach = .62f + .18f * ((k * .618034f) % 1f);
                    stampSpecks[k].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * seal * reach;
                    float speck = seal * (.035f + .03f * ((k * .414214f) % 1f));
                    stampSpecks[k].rectTransform.sizeDelta = new Vector2(speck, speck);
                }
            }
            foreach (UnityEngine.UI.Image ringImage in burstRings)
                if (ringImage != null) { ringImage.rectTransform.sizeDelta = Vector2.one * seal * 1.3f; ringImage.rectTransform.anchoredPosition = Vector2.zero; }
            if (InkMode && FitGhost(side, dot)) return;
            for (int i = 0; i < dots.Length; i++)
                if (dots[i] != null) dots[i].rectTransform.sizeDelta = new Vector2(dot, dot);
        }

        /// <summary>
        /// #533: 흐린 "물" Text의 실제 글자 사각형을 재서, 그 사각형이 GlyphInkRect(판정 점과 같은 칸 좌표)에 꼭 맞게
        /// Text를 늘이고 옮긴다. 먹 붓 점은 Text의 자식이라 같은 비율을 되돌려 놓는다.
        /// </summary>
        private bool FitGhost(float side, float dot)
        {
            RectTransform g = ghost.rectTransform;
            g.anchorMin = g.anchorMax = g.pivot = new Vector2(.5f, .5f);
            g.sizeDelta = new Vector2(GhostFontSize * 2f, GhostFontSize * 2f);
            TextGenerationSettings settings = ghost.GetGenerationSettings(g.rect.size);
            if (!ghostGenerator.Populate(ghost.text, settings) || ghostGenerator.vertexCount < 4) return false;
            IList<UIVertex> verts = ghostGenerator.verts;
            float unit = 1f / Mathf.Max(.0001f, ghost.pixelsPerUnit);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++) { Vector2 v = verts[i].position * unit; min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
            Vector2 quad = max - min;
            if (quad.x <= .001f || quad.y <= .001f) return false;
            var target = Rect.MinMaxRect((GlyphInkRect.xMin - .5f) * side, (GlyphInkRect.yMin - .5f) * side,
                (GlyphInkRect.xMax - .5f) * side, (GlyphInkRect.yMax - .5f) * side);
            var scale = new Vector2(target.width / quad.x, target.height / quad.y);
            Vector2 offset = target.center - Vector2.Scale((min + max) * .5f, scale);
            g.localScale = new Vector3(scale.x, scale.y, 1f);
            g.anchoredPosition = offset;
            if (ghostEdge != null)
            {
                RectTransform e = ghostEdge.rectTransform;
                e.anchorMin = e.anchorMax = e.pivot = g.pivot; e.sizeDelta = g.sizeDelta;
                e.localScale = g.localScale; e.anchoredPosition = g.anchoredPosition;
                // The outline offset is in the text's own units, so undo the scale to keep it a steady screen width.
                if (ghostOutline != null) ghostOutline.effectDistance = new Vector2(ghostOutlineWidth / scale.x, -ghostOutlineWidth / scale.y);
            }
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                Vector2 at = (stroke.Samples[i] - new Vector2(.5f, .5f)) * side;
                RectTransform d = dots[i].rectTransform;
                d.anchorMin = d.anchorMax = new Vector2(.5f, .5f);
                d.anchoredPosition = new Vector2((at.x - offset.x) / scale.x, (at.y - offset.y) / scale.y);
                d.sizeDelta = new Vector2(dot / scale.x, dot / scale.y);
            }
            return true;
        }

        private void EnsureWidgets()
        {
            if (paper != null) return;
            root = transform as RectTransform;
            if (root == null) return;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            paper = NewRect("Talisman Paper", root);
            paper.sizeDelta = new Vector2(200f, 360f);
            paperGroup = paper.gameObject.AddComponent<CanvasGroup>();
            paperGroup.blocksRaycasts = false; paperGroup.interactable = false;
            paperImage = paper.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            paperImage.texture = HanjiTexture(); paperImage.color = paperColor; paperImage.raycastTarget = false;
            frame = NewRect("Talisman Frame", paper);
            Stretch(frame, .035f);
            frameImage = frame.gameObject.AddComponent<UnityEngine.UI.Image>();
            frameImage.color = new Color(0f, 0f, 0f, 0f); frameImage.raycastTarget = false;
            var outline = frame.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = inkColor; outline.effectDistance = new Vector2(3f, -3f);
            hint = NewText("Talisman Hint", paper, HintText, 22, inkColor);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(.08f, .83f); hintRect.anchorMax = new Vector2(.92f, .96f);
            hintRect.offsetMin = hintRect.offsetMax = Vector2.zero;
            hint.resizeTextForBestFit = true; hint.resizeTextMinSize = 10; hint.resizeTextMaxSize = 28;
            glyphArea = NewRect("Talisman Glyph (물)", paper);
            // #533: the real Shilla Culture "물" lies faintly on the paper and masks the ink, so written ink only shows
            // inside the letter's shape (a brush, not sausage dots). Without the font it falls back to the dotted guide.
            RectTransform dotParent = glyphArea;
            if (IsShilla(font))
            {
                // #533: a copy underneath carries the outline, so the letter's edge shows before any writing
                // and the watercolour (masked by the ghost on top) never paints over it.
                ghostEdge = NewText("Talisman Ghost Edge (물)", glyphArea, "물", GhostFontSize, ghostOutlineColor);
                ghostEdge.horizontalOverflow = HorizontalWrapMode.Overflow; ghostEdge.verticalOverflow = VerticalWrapMode.Overflow;
                ghostEdge.alignment = TextAnchor.MiddleCenter;
                ghostOutline = ghostEdge.gameObject.AddComponent<UnityEngine.UI.Outline>();
                ghostOutline.effectColor = ghostOutlineColor; ghostOutline.useGraphicAlpha = true;
                ghost = NewText("Talisman Ghost Glyph (물)", glyphArea, "물", GhostFontSize, ghostColor);
                ghost.horizontalOverflow = HorizontalWrapMode.Overflow; ghost.verticalOverflow = VerticalWrapMode.Overflow;
                ghost.alignment = TextAnchor.MiddleCenter;
                ghost.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
                dotParent = ghost.rectTransform;
            }
            dots = new UnityEngine.UI.Image[stroke.Samples.Length];
            for (int i = 0; i < dots.Length; i++)
            {
                var dot = NewRect("Dot " + i, dotParent).gameObject.AddComponent<UnityEngine.UI.Image>();
                dot.raycastTarget = false; dot.sprite = InkMode ? WatercolorDot() : RoundDot();
                // In ink mode FitGhost places the dots inside the scaled glyph.
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = stroke.Samples[i];
                dot.rectTransform.anchoredPosition = Vector2.zero;
                dot.color = InkMode ? writtenColor : guideColor;
                if (InkMode) dot.gameObject.SetActive(false);
                dots[i] = dot;
            }
            BuildInkColors();
            // #533: drawn last so it sits above the ghost letter and the ink.
            nextMarker = NewRect("Talisman Next Stroke", glyphArea).gameObject.AddComponent<UnityEngine.UI.Image>();
            nextMarker.raycastTarget = false; nextMarker.sprite = RoundDot(); nextMarker.color = nextMarkerColor;
            nextMarker.gameObject.SetActive(false);
            // #533: the red seal that slams onto the finished talisman, with ink specks and light rings from where it lands.
            stampRoot = NewRect("Talisman Stamp", paper);
            stampRoot.pivot = new Vector2(.5f, .5f);
            stampSpecks = new UnityEngine.UI.Image[9];
            for (int k = 0; k < stampSpecks.Length; k++)
            {
                var speck = NewRect("Stamp Speck " + k, stampRoot).gameObject.AddComponent<UnityEngine.UI.Image>();
                speck.raycastTarget = false; speck.sprite = WatercolorDot();
                speck.rectTransform.anchorMin = speck.rectTransform.anchorMax = new Vector2(.5f, .5f);
                speck.gameObject.SetActive(false);
                stampSpecks[k] = speck;
            }
            stampImage = NewRect("Stamp Seal", stampRoot).gameObject.AddComponent<UnityEngine.UI.Image>();
            stampImage.raycastTarget = false; stampImage.sprite = StampSprite();
            Stretch(stampImage.rectTransform, 0f);
            stampLabel = NewText("Stamp Text", stampRoot, stampText, 0, stampColor);
            Stretch(stampLabel.rectTransform, .1f);
            stampLabel.lineSpacing = .85f;
            stampLabel.horizontalOverflow = HorizontalWrapMode.Overflow; stampLabel.verticalOverflow = VerticalWrapMode.Overflow;
            // #540: by default the seal carries a picture (a water drop over waves) instead of words.
            stampEmblem = NewRect("Stamp Emblem", stampRoot).gameObject.AddComponent<UnityEngine.UI.Image>();
            stampEmblem.raycastTarget = false; stampEmblem.sprite = StampEmblemSprite();
            Stretch(stampEmblem.rectTransform, .1f);
            bool words = !string.IsNullOrEmpty(stampText);
            stampLabel.gameObject.SetActive(words); stampEmblem.gameObject.SetActive(!words);
            burstRings = new UnityEngine.UI.Image[5];
            for (int k = 0; k < burstRings.Length; k++)
            {
                var ringImage = NewRect("Talisman Burst Ring " + k, stampRoot).gameObject.AddComponent<UnityEngine.UI.Image>();
                ringImage.raycastTarget = false; ringImage.sprite = RingSprite();
                ringImage.rectTransform.anchorMin = ringImage.rectTransform.anchorMax = new Vector2(.5f, .5f);
                ringImage.gameObject.SetActive(false);
                burstRings[k] = ringImage;
            }
            stampRoot.gameObject.SetActive(false);
            // #533: the burn layers sit on top of everything on the paper; the scorched edge comes first.
            // #540: the paper burns in from its edges — a runtime texture repainted as the fire creeps inward.
            BuildBurnMap();
            scorch = NewRect("Talisman Edge Burn", paper).gameObject.AddComponent<UnityEngine.UI.Image>();
            scorch.raycastTarget = false;
            scorch.sprite = Sprite.Create(burnTexture, new Rect(0f, 0f, burnWidth, burnHeight), new Vector2(.5f, .5f), 100f);
            scorch.sprite.hideFlags = HideFlags.DontSave;
            Stretch(scorch.rectTransform, 0f);
            scorch.gameObject.SetActive(false);
            emberBand = NewRect("Talisman Burn Flames", paper);
            Stretch(emberBand, 0f);
            flameFrames = Resources.LoadAll<Sprite>("Bomb/BlastFire");
            if (flameFrames != null) Array.Sort(flameFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            flames = new UnityEngine.UI.Image[6];
            for (int k = 0; k < flames.Length; k++)
            {
                var flame = NewRect("Burn Flame " + k, emberBand).gameObject.AddComponent<UnityEngine.UI.Image>();
                flame.raycastTarget = false; flame.preserveAspect = true;
                flame.sprite = flameFrames != null && flameFrames.Length > 0 ? flameFrames[0] : WatercolorDot();
                flames[k] = flame;
            }
            emberBand.gameObject.SetActive(false);
            sparks = new UnityEngine.UI.Image[14];
            for (int k = 0; k < sparks.Length; k++)
            {
                var spark = NewRect("Burn Spark " + k, emberBand).gameObject.AddComponent<UnityEngine.UI.Image>();
                spark.raycastTarget = false; spark.sprite = RoundDot();
                spark.gameObject.SetActive(false);
                sparks[k] = spark;
            }
            // #540: the talisman orb the folded talisman becomes.
            foldOrb = NewRect("Talisman Orb", paper).gameObject.AddComponent<UnityEngine.UI.Image>();
            foldOrb.raycastTarget = false; foldOrb.sprite = RoundDot();
            foldOrb.rectTransform.anchorMin = foldOrb.rectTransform.anchorMax = new Vector2(.5f, .5f);
            foldSeal = NewRect("Talisman Orb Seal", foldOrb.rectTransform).gameObject.AddComponent<UnityEngine.UI.Image>();
            foldSeal.raycastTarget = false; foldSeal.sprite = StampEmblemSprite();
            Stretch(foldSeal.rectTransform, .2f);
            foldOrb.gameObject.SetActive(false);
            // #540: effects outside the paper. The board dim sits behind everything; the rest sit on top.
            boardDim = NewRect("Talisman Board Dim", root).gameObject.AddComponent<UnityEngine.UI.Image>();
            boardDim.raycastTarget = false; boardDim.transform.SetAsFirstSibling(); boardDim.gameObject.SetActive(false);
            landRing = NewRect("Talisman Landing Ring", root).gameObject.AddComponent<UnityEngine.UI.Image>();
            landRing.raycastTarget = false; landRing.sprite = RingSprite();
            landRing.rectTransform.anchorMin = landRing.rectTransform.anchorMax = landRing.rectTransform.pivot = new Vector2(.5f, .5f);
            landRing.gameObject.SetActive(false);
            landDrops = new UnityEngine.UI.Image[8];
            for (int k = 0; k < landDrops.Length; k++)
            {
                var drop = NewRect("Talisman Landing Drop " + k, root).gameObject.AddComponent<UnityEngine.UI.Image>();
                drop.raycastTarget = false; drop.sprite = RoundDot();
                drop.rectTransform.anchorMin = drop.rectTransform.anchorMax = drop.rectTransform.pivot = new Vector2(.5f, .5f);
                drop.gameObject.SetActive(false);
                landDrops[k] = drop;
            }
            flyEmbers = new UnityEngine.UI.Image[12];
            for (int k = 0; k < flyEmbers.Length; k++)
            {
                var ember = NewRect("Talisman Flying Ember " + k, root).gameObject.AddComponent<UnityEngine.UI.Image>();
                ember.raycastTarget = false; ember.sprite = RoundDot();
                ember.rectTransform.anchorMin = ember.rectTransform.anchorMax = ember.rectTransform.pivot = new Vector2(.5f, .5f);
                ember.gameObject.SetActive(false);
                flyEmbers[k] = ember;
            }
            BuildStrokeRank();
            LayoutGlyph();
            paper.gameObject.SetActive(false);
        }

        /// <summary>
        /// 한지 질감 (런타임에 한 번 만든다, 에셋 없음): 노란 괴황지 바탕에 얼룩, 가는 섬유, 가장자리 바램.
        /// </summary>
        public static Texture2D HanjiTexture()
        {
            if (hanji != null) return hanji;
            const int width = 256, height = 512;
            var light = new Color(.98f, .88f, .52f); var dark = new Color(.86f, .7f, .34f);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float mottle = Mathf.PerlinNoise(x * .03f + 7f, y * .03f + 3f) * .65f
                        + Mathf.PerlinNoise(x * .13f + 31f, y * .13f + 17f) * .35f;
                    float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y)) / 26f);
                    Color c = Color.Lerp(dark, light, mottle);
                    pixels[y * width + x] = c * Mathf.Lerp(.8f, 1f, edge);
                }
            var random = new System.Random(479);
            for (int i = 0; i < 900; i++)
            {
                float px = (float)random.NextDouble() * width, py = (float)random.NextDouble() * height;
                float angle = (float)(random.NextDouble() * Math.PI * 2d), length = 5f + (float)random.NextDouble() * 20f;
                Color fiber = random.NextDouble() < .6d ? new Color(1f, .96f, .78f) : new Color(.78f, .6f, .28f);
                float strength = .18f + (float)random.NextDouble() * .25f;
                for (float t = 0f; t < length; t += .7f)
                {
                    int fx = Mathf.RoundToInt(px + Mathf.Cos(angle) * t), fy = Mathf.RoundToInt(py + Mathf.Sin(angle) * t);
                    if (fx < 0 || fy < 0 || fx >= width || fy >= height) break;
                    int index = fy * width + fx;
                    pixels[index] = Color.Lerp(pixels[index], fiber, strength);
                }
            }
            hanji = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "Talisman Hanji (#479 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < pixels.Length; i++) pixels[i].a = 1f;
            hanji.SetPixels(pixels); hanji.Apply(false, true);
            return hanji;
        }

        private static bool IsShilla(Font candidate) =>
            candidate != null && candidate.name.IndexOf("Shilla", StringComparison.OrdinalIgnoreCase) >= 0;

        private static Rect SampleBounds(Vector2[] samples)
        {
            if (samples == null || samples.Length == 0) return new Rect(0f, 0f, 1f, 1f);
            Vector2 min = samples[0], max = samples[0];
            for (int i = 1; i < samples.Length; i++) { min = Vector2.Min(min, samples[i]); max = Vector2.Max(max, samples[i]); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>#533: 획 점용 둥근 무늬 (런타임에 한 번 만든다). 둥근 점이 겹쳐 매끈한 굵은 획이 된다.</summary>
        /// <summary>
        /// #533: 수채화 점. 가운데는 묽고 가장자리가 조금 진하며(물감이 마르며 테두리에 고이는 느낌), 테두리가 살짝 울퉁불퉁하다.
        /// </summary>
        private static Sprite WatercolorDot()
        {
            if (watercolorDot != null) return watercolorDot;
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Talisman Watercolor Dot (#533 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            float half = size * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x + .5f - half, y + .5f - half) / half;
                    float angle = Mathf.Atan2(d.y, d.x);
                    float edge = .86f + .07f * Mathf.Sin(angle * 5f + 1.3f) + .05f * Mathf.Sin(angle * 11f + .4f);
                    float r = d.magnitude / edge;
                    float body = .55f + .45f * Mathf.SmoothStep(.55f, .95f, r);
                    float cut = Mathf.Clamp01((1f - r) * half * .6f);
                    float grain = .85f + .15f * Mathf.PerlinNoise(x * .18f + 11f, y * .18f + 5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(body * cut * grain));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            watercolorDot = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            watercolorDot.hideFlags = HideFlags.DontSave;
            return watercolorDot;
        }

        private static Sprite RoundDot()
        {
            if (roundDot != null) return roundDot;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Talisman Round Dot (#533 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            float r = size * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            roundDot = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            roundDot.hideFlags = HideFlags.DontSave;
            return roundDot;
        }

        private UnityEngine.UI.Text NewText(string name, RectTransform parent, string value, int size, Color color)
        {
            var text = NewRect(name, parent).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font; text.text = value; text.color = color; text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleCenter; if (size > 0) text.fontSize = size;
            return text;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = new Vector2(inset, inset); rect.anchorMax = new Vector2(1f - inset, 1f - inset);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void OnDisable() { if (fireball != null) fireball.Hide(); fireballSeen = LabJigwiFireball.Phase.None; SetBoardBlocked(false); Visible = false; RoundId = null; hasLast = false; EndFinish(); EndBurn(); if (paper != null) paper.gameObject.SetActive(false); }
        private void OnDestroy() { SetBoardBlocked(false); if (fireball != null) Destroy(fireball.gameObject); }
    }

    /// <summary>
    /// #479 부적 "물"의 점선. 신라문화체 굵은 "물"을 그린 뒤 획의 가운데 선만 남겨 일정 간격(글자 칸의 2.8%)으로 고른 점이다
    /// (글자 칸 0~1, 위가 1). 지나간 선분에서 여유 거리 안에 든 점을 칠하며, 획 순서와 손 떼기는 따지지 않는다.
    /// </summary>
    public sealed class LabJigwiTalismanStroke
    {
        /// <summary>신라문화체(ShillaCultureBold) "물"의 가운데 선 점들</summary>
        public static readonly Vector2[] Water =
        {
            new Vector2(0.514f, 0.122f), new Vector2(0.549f, 0.124f), new Vector2(0.467f, 0.145f), new Vector2(0.575f, 0.138f), new Vector2(0.603f, 0.148f), new Vector2(0.446f, 0.164f),
            new Vector2(0.630f, 0.156f), new Vector2(0.657f, 0.166f), new Vector2(0.685f, 0.177f), new Vector2(0.425f, 0.195f), new Vector2(0.711f, 0.188f), new Vector2(0.738f, 0.195f),
            new Vector2(0.415f, 0.224f), new Vector2(0.407f, 0.254f), new Vector2(0.486f, 0.264f), new Vector2(0.370f, 0.271f), new Vector2(0.433f, 0.266f), new Vector2(0.515f, 0.269f),
            new Vector2(0.544f, 0.274f), new Vector2(0.574f, 0.280f), new Vector2(0.601f, 0.288f), new Vector2(0.630f, 0.290f), new Vector2(0.616f, 0.314f), new Vector2(0.627f, 0.340f),
            new Vector2(0.640f, 0.369f), new Vector2(0.397f, 0.379f), new Vector2(0.426f, 0.382f), new Vector2(0.456f, 0.385f), new Vector2(0.485f, 0.389f), new Vector2(0.651f, 0.400f),
            new Vector2(0.680f, 0.401f), new Vector2(0.509f, 0.408f), new Vector2(0.538f, 0.410f), new Vector2(0.567f, 0.410f), new Vector2(0.596f, 0.411f), new Vector2(0.625f, 0.413f),
            new Vector2(0.522f, 0.434f), new Vector2(0.494f, 0.453f), new Vector2(0.522f, 0.464f), new Vector2(0.551f, 0.460f), new Vector2(0.215f, 0.484f), new Vector2(0.244f, 0.487f),
            new Vector2(0.273f, 0.492f), new Vector2(0.522f, 0.494f), new Vector2(0.302f, 0.497f), new Vector2(0.330f, 0.503f), new Vector2(0.357f, 0.511f), new Vector2(0.386f, 0.516f),
            new Vector2(0.415f, 0.521f), new Vector2(0.444f, 0.524f), new Vector2(0.498f, 0.524f), new Vector2(0.535f, 0.519f), new Vector2(0.554f, 0.542f), new Vector2(0.648f, 0.540f),
            new Vector2(0.580f, 0.555f), new Vector2(0.607f, 0.563f), new Vector2(0.832f, 0.565f), new Vector2(0.637f, 0.568f), new Vector2(0.666f, 0.569f), new Vector2(0.695f, 0.574f),
            new Vector2(0.788f, 0.574f), new Vector2(0.722f, 0.584f), new Vector2(0.751f, 0.581f), new Vector2(0.527f, 0.644f), new Vector2(0.428f, 0.665f), new Vector2(0.486f, 0.665f),
            new Vector2(0.457f, 0.668f), new Vector2(0.528f, 0.673f), new Vector2(0.404f, 0.684f), new Vector2(0.556f, 0.681f), new Vector2(0.585f, 0.684f), new Vector2(0.536f, 0.704f),
            new Vector2(0.389f, 0.715f), new Vector2(0.544f, 0.734f), new Vector2(0.376f, 0.744f), new Vector2(0.556f, 0.760f), new Vector2(0.372f, 0.775f), new Vector2(0.570f, 0.784f),
            new Vector2(0.360f, 0.804f), new Vector2(0.580f, 0.813f), new Vector2(0.351f, 0.834f), new Vector2(0.475f, 0.839f), new Vector2(0.586f, 0.844f), new Vector2(0.502f, 0.849f),
            new Vector2(0.338f, 0.863f), new Vector2(0.530f, 0.859f), new Vector2(0.557f, 0.865f), new Vector2(0.601f, 0.868f)
        };

        /// <summary>
        /// #533 "물"의 획 순서. 각 획은 Water 점 번호를 쓰는 방향대로 나열한다.
        /// ㅁ(왼쪽 세로 → 위·오른쪽 → 아래) → ㅜ(가로 → 세로) → ㄹ(위·오른쪽 → 가운데 → 왼쪽·아래).
        /// </summary>
        public static readonly int[][] WaterStrokes =
        {
            new[] { 84, 80, 78, 76, 74, 72, 68 },
            new[] { 81, 83, 85, 86, 87, 82, 79, 77, 75, 73, 71 },
            new[] { 64, 66, 65, 63, 67, 69, 70 },
            new[] { 40, 41, 42, 44, 45, 46, 47, 48, 49, 50, 52, 54, 55, 53, 57, 58, 59, 61, 62, 60, 56 },
            new[] { 51, 43, 38, 39, 37, 36 },
            new[] { 25, 26, 27, 28, 31, 32, 33, 34, 35, 29, 30, 24, 23, 22 },
            new[] { 16, 14, 17, 18, 19, 20, 21 },
            new[] { 15, 13, 12, 9, 5, 2, 0, 1, 3, 4, 6, 7, 8, 10, 11 }
        };

        public readonly Vector2[] Samples;
        private readonly bool[] covered;
        private readonly int[][] strokes;
        private readonly int[] strokeOf, strokeCovered;
        public int CoveredCount { get; private set; }
        public float Coverage => Samples.Length == 0 ? 0f : CoveredCount / (float)Samples.Length;
        /// <summary>#533: 안내할 획 번호 (아직 StrokeAdvance만큼 쓰지 않은 첫 획). EnforceOrder면 이보다 뒤 획은 칠해지지 않는다.</summary>
        public int CurrentStroke { get; private set; }
        public int StrokeCount => strokes.Length;
        /// <summary>#533: 한 획을 이 비율만큼 쓰면 다음 획이 열린다.</summary>
        public float StrokeAdvance { get; set; } = .8f;
        /// <summary>#533: 켜면 안내 획보다 뒤 획은 칠해지지 않는다. 직접 해 보니 너무 엄격해 기본은 끔(순서는 안내만).</summary>
        public bool EnforceOrder { get; set; }

        /// <summary>strokes가 없으면 획 순서 없이 아무 점이나 칠한다.</summary>
        public LabJigwiTalismanStroke(Vector2[] samples, int[][] strokeOrder = null)
        {
            Samples = (Vector2[])samples.Clone();
            covered = new bool[Samples.Length];
            strokeOf = new int[Samples.Length];
            var listed = new bool[Samples.Length];
            var built = new List<int[]>();
            if (strokeOrder != null)
                foreach (int[] order in strokeOrder)
                {
                    var keep = new List<int>();
                    foreach (int index in order ?? Array.Empty<int>())
                        if (index >= 0 && index < Samples.Length && !listed[index]) { listed[index] = true; keep.Add(index); }
                    if (keep.Count > 0) built.Add(keep.ToArray());
                }
            // Points no stroke lists join the last stroke, so a guide is always finishable.
            var rest = new List<int>();
            for (int i = 0; i < Samples.Length; i++) if (!listed[i]) rest.Add(i);
            if (rest.Count > 0 || built.Count == 0) built.Add(rest.ToArray());
            strokes = built.ToArray();
            for (int s = 0; s < strokes.Length; s++) foreach (int index in strokes[s]) strokeOf[index] = s;
            strokeCovered = new int[strokes.Length];
        }

        public bool IsCovered(int index) => covered[index];
        public int StrokeOf(int index) => strokeOf[index];
        public int[] StrokePoints(int stroke) => (int[])strokes[stroke].Clone();

        /// <summary>#533: 지금 획에서 쓰는 방향으로 다음에 칠할 빈 점 번호. 다 썼으면 -1.</summary>
        public int NextPoint
        {
            get
            {
                for (int s = CurrentStroke; s < strokes.Length; s++)
                    foreach (int index in strokes[s]) if (!covered[index]) return index;
                return -1;
            }
        }

        /// <summary>#533: 글자에서 떨어진 곳을 문지른 길이의 합 (글자 칸 폭 = 1).</summary>
        public float OffLength { get; private set; }

        /// <summary>
        /// #533: 선분 from→to 중 어느 점에서도 offDistance보다 먼 부분의 길이를 OffLength에 더한다(잘게 나눠 잰다). 더한 길이를 돌려준다.
        /// </summary>
        public float AddOffGuide(Vector2 from, Vector2 to, float offDistance)
        {
            if (!Finite(from) || !Finite(to) || !(offDistance > 0f) || Samples.Length == 0) return 0f;
            float length = Vector2.Distance(from, to);
            if (length <= 1e-6f) return 0f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(length / .02f), 1, 200);
            float piece = length / steps, limit = offDistance * offDistance, added = 0f;
            for (int k = 0; k < steps; k++)
            {
                Vector2 at = Vector2.Lerp(from, to, (k + .5f) / steps);
                bool near = false;
                for (int i = 0; i < Samples.Length && !near; i++) near = (Samples[i] - at).sqrMagnitude <= limit;
                if (!near) added += piece;
            }
            OffLength += added;
            return added;
        }

        /// <summary>#533: 획 하나에서 쓰는 방향으로 연달아 빈 점이 가장 길게 이어진 수 (획 끝의 빈 곳도 센다).</summary>
        public int LongestGap(int strokeIndex)
        {
            int longest = 0, run = 0;
            foreach (int index in strokes[strokeIndex])
            {
                run = covered[index] ? 0 : run + 1;
                if (run > longest) longest = run;
            }
            return longest;
        }

        /// <summary>#533: 모든 획에서 연달아 빈 점이 maxGap 이하 — 눈으로 봐서 글자가 끊긴 곳 없이 다 쓰였는지.</summary>
        public bool LooksComplete(int maxGap)
        {
            for (int s = 0; s < strokes.Length; s++) if (LongestGap(s) > maxGap) return false;
            return true;
        }

        public void Clear()
        {
            Array.Clear(covered, 0, covered.Length); CoveredCount = 0; OffLength = 0f;
            Array.Clear(strokeCovered, 0, strokeCovered.Length); CurrentStroke = 0;
        }

        /// <summary>선분 from→to에서 tolerance 안에 든 점을 칠하고, 새로 칠한 점 번호를 fresh에 담는다. #533: EnforceOrder일 때만 아직 열리지 않은 획을 건너뛴다.</summary>
        public void Paint(Vector2 from, Vector2 to, float tolerance, List<int> fresh = null)
        {
            if (!Finite(from) || !Finite(to) || !(tolerance > 0f)) return;
            float limit = tolerance * tolerance;
            for (int i = 0; i < Samples.Length; i++)
            {
                if (covered[i] || (EnforceOrder && strokeOf[i] > CurrentStroke) || DistanceSquared(Samples[i], from, to) > limit) continue;
                covered[i] = true; CoveredCount++; strokeCovered[strokeOf[i]]++;
                fresh?.Add(i);
                Advance();
            }
        }

        private void Advance()
        {
            float need = Mathf.Clamp01(StrokeAdvance);
            while (CurrentStroke < strokes.Length - 1
                   && strokeCovered[CurrentStroke] >= Mathf.CeilToInt(strokes[CurrentStroke].Length * need - 1e-4f)) CurrentStroke++;
        }

        private static bool Finite(Vector2 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);

        private static float DistanceSquared(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float length = ab.sqrMagnitude;
            float t = length <= 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / length);
            return (a + ab * t - p).sqrMagnitude;
        }
    }
}
