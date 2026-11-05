using System;
using System.Collections.Generic;
using System.Linq;
using C6Lab;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 (#477)에서 말하는 사람. 자리와 같다: 나(불이)는 아래 가운데 P2, 금옥이는 오른쪽 이웃 P1(앞 번호),
    /// 묵이는 왼쪽 이웃 P3(다음 번호). 말풍선은 말하는 사람 자리의 얼굴에서 나온다.
    /// </summary>
    public enum GameTutorialSpeaker { Bul, Muk, Geumok }

    /// <summary>화면을 어둡게 하고 밝혀 둘 곳. None이면 어둡게 하지 않는다.</summary>
    public enum GameTutorialFocus
    {
        None, Gourd, Drum, Stamina,
        /// <summary>내 판의 검은 음구슬 두 개만 (흰 양구슬은 빼고). 둘을 잇는 방향으로 기울어진 알약 모양으로 밝힌다.</summary>
        Yins,
        /// <summary>합칠 음구슬 하나와 양구슬 하나 (양구슬에 가장 가까운 음구슬).</summary>
        Pair,
        /// <summary>위쪽 박 칠하기 진행률 막대와 그 옆 % 글자.</summary>
        Progress,
        /// <summary>내 판의 폭탄 (자물쇠 숫자가 보이게).</summary>
        Bomb,
        /// <summary>오른쪽 위 북 위치 지도(미니맵). 둥근 네모로 밝히고, 말풍선이 지도를 가리면 얼굴 아래로 내려 단다.</summary>
        Minimap
    }

    /// <summary>다음으로 넘어가기 전에 기다리는 일. None은 화면을 눌러 넘기는 대사다.</summary>
    public enum GameTutorialAction
    {
        None,
        /// <summary>북을 Count번 눌러 구슬을 만든다. 북은 가운데에 고정되고 다른 손동작은 막힌다.</summary>
        TapDrum,
        /// <summary>멈춰 둔 기운 회복을 풀고 막대가 다 차기를 기다린다. 화면은 누를 수 없다.</summary>
        RefillStamina,
        /// <summary>음구슬을 끌어 양구슬 위에 놓아 Count번 합친다. 구슬만 잡을 수 있고 북·던지기는 막힌다.</summary>
        CombineOrbs,
        /// <summary>
        /// 오행구슬을 위로 밀어 박에 맞힌다. 던지기만 열린다. 빗나가면 금옥이가 "다시 해 봐!"라고 하고 같은 구슬이
        /// 위에서 떨어져 다시 생긴다. 맞힐 때까지 기다린다.
        /// </summary>
        ThrowOrb,
        /// <summary>
        /// 북을 오른쪽 끝으로 밀어 금옥이에게 넘긴다. 북 옮기기만 열린다. 왼쪽(묵이)으로 가면 묵이가 "이건 금옥이한테 줘."
        /// 하고 바로 돌려준다. 금옥이가 받으면 금옥이 화면 미니 창에 북이 들어오는 모습이 뜬다.
        /// </summary>
        PassDrum,
        /// <summary>
        /// 잠긴 폭탄을 오른쪽 끝으로 밀어 금옥이에게 넘긴다 (자물쇠 2→1). 구슬 옮기기만 열리고, 폭탄은 오른쪽으로만 넘어간다
        /// (왼쪽은 되튕김). 폭탄은 무거워 더 세게 밀어야 해서 손 안내가 더 빠르다.
        /// </summary>
        PassBomb,
        /// <summary>
        /// 금옥이가 돌려준 폭탄(자물쇠 1→0, 무지개색)이 내 판에 들어와 멈출 때까지 기다린다. 폭탄은 아직 열리지 않는다.
        /// 화면은 누를 수 없다. 다음 대사 "풀렸다!"와 함께 공격 구슬 두 개로 열린다.
        /// </summary>
        WaitBombBack,
        /// <summary>
        /// 요괴가 내 판 오른쪽 끝을 막는다(실제 박의 넘기기 막기). 막힌 가장자리가 빨갛게 세 번 번쩍이는 동안 기다리고,
        /// 그다음 대사가 뜬다. 화면은 누를 수 없다. 막기는 시간이 흐르지 않고, 북을 묵이에게 넘길 때 풀린다.
        /// </summary>
        WatchBlock,
        /// <summary>
        /// 북을 왼쪽 끝으로 밀어 묵이에게 넘긴다. 북 옮기기만 열린다. 오른쪽은 막혀 있어 북이 튕겨 나온다.
        /// 묵이가 받으면 막기가 풀린다.
        /// </summary>
        PassDrumLeft
    }

    /// <summary>한 장면이 뜰 때 함께 보여 주는 연출.</summary>
    public enum GameTutorialCue
    {
        None, StaminaShort,
        /// <summary>금옥이가 만든 양구슬과 받은 북을 오른쪽에서 함께 돌려준다 (4단계).</summary>
        GiveBack,
        /// <summary>박이 내 판에 자물쇠 2짜리 폭탄을 떨어뜨린다 (5단계, 박 폭탄 경고·요괴 연출과 함께).</summary>
        DropBomb,
        /// <summary>
        /// 금옥이가 받은 폭탄을 "앗 뜨거!" 하며 도로 넘겨준다. 넘어오며 자물쇠가 0이 되어 무지개색이 되지만, 내 판에 멈춰도
        /// 아직 열리지 않는다 (5단계).
        /// </summary>
        ReturnBomb,
        /// <summary>"풀렸다!": 멈춰 기다리던 무지개 폭탄이 이 대사와 함께 공격 구슬 두 개로 열린다 (5단계).</summary>
        OpenBomb
    }

    /// <summary>
    /// 튜토리얼 한 장면. 대사(Action None)는 말하는 사람 자리에서 말풍선으로 뜨고 화면을 누르면 넘어간다.
    /// 동작 장면은 말풍선 대신 검은 신라문화체 안내 띠(Text)와 손 그림을 보여 주고 그 동작이 끝나기를 기다린다.
    /// </summary>
    public readonly struct GameTutorialBeat
    {
        public readonly GameTutorialSpeaker Speaker;
        /// <summary>A line's words, or an action's guide banner (empty for none). \n breaks the line.</summary>
        public readonly string Text;
        public readonly GameTutorialFocus Focus;
        public readonly GameTutorialAction Action;
        /// <summary>How many times the action is done (TapDrum: taps).</summary>
        public readonly int Count;
        public readonly GameTutorialCue Cue;

        private GameTutorialBeat(GameTutorialSpeaker speaker, string text, GameTutorialFocus focus,
            GameTutorialAction action, int count, GameTutorialCue cue)
        {
            Speaker = speaker;
            Text = text ?? string.Empty;
            Focus = focus;
            Action = action;
            Count = count;
            Cue = cue;
        }

        public bool IsLine => Action == GameTutorialAction.None;

        public static GameTutorialBeat Line(GameTutorialSpeaker speaker, string text,
            GameTutorialFocus focus = GameTutorialFocus.None, GameTutorialCue cue = GameTutorialCue.None)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("A tutorial line needs words.", nameof(text));
            return new GameTutorialBeat(speaker, text, focus, GameTutorialAction.None, 0, cue);
        }

        public static GameTutorialBeat Do(GameTutorialAction action, string banner, GameTutorialFocus focus, int count = 1)
        {
            if (action == GameTutorialAction.None) throw new ArgumentException("Use Line for a line.", nameof(action));
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            return new GameTutorialBeat(GameTutorialSpeaker.Bul, banner, focus, action, count, GameTutorialCue.None);
        }
    }

    /// <summary>튜토리얼 한 단계: 이름과 장면들.</summary>
    public sealed class GameTutorialStep
    {
        public readonly string Name;
        public readonly IReadOnlyList<GameTutorialBeat> Beats;

        public GameTutorialStep(string name, params GameTutorialBeat[] beats)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A tutorial step needs a name.", nameof(name));
            if (beats == null || beats.Length == 0) throw new ArgumentException("A tutorial step needs beats.", nameof(beats));
            Name = name;
            Beats = Array.AsReadOnly((GameTutorialBeat[])beats.Clone());
        }

        /// <summary>Only the spoken lines, in order.</summary>
        public IReadOnlyList<GameTutorialBeat> Lines => Beats.Where(beat => beat.IsLine).ToArray();
    }

    /// <summary>
    /// 확정 대본 (#477, 2026-11-04). 오프닝 마지막 "얼른 요괴 잡으러 가자!" 바로 뒤에서 금옥이가 붙잡으며 시작한다.
    /// 아이들끼리는 반말, 한 말풍선은 두 줄 이내. 안내 띠는 "~하시오"체.
    /// </summary>
    public static class GameTutorialScript
    {
        /// <summary>전투 화면 위쪽 "STAGE 1 / 3 · 구미호" 자리에 대신 쓰는 글자.</summary>
        public const string StageLabel = "튜토리얼";
        /// <summary>동작을 해냈을 때 안내 띠 아래에 잠깐 뜨는 글자.</summary>
        public const string SuccessText = "성공!";

        /// <summary>얼굴 아래에 쓰는 이름 (사용자 시안: 묵이·금옥·불이).</summary>
        public static string NameOf(GameTutorialSpeaker speaker) =>
            speaker == GameTutorialSpeaker.Muk ? "묵이" : speaker == GameTutorialSpeaker.Geumok ? "금옥" : "불이";

        /// <summary>Who sits in a seat (P number - 1): the reverse of <see cref="SeatOf"/>.</summary>
        public static GameTutorialSpeaker SpeakerAt(int seat) =>
            seat == SeatOf(GameTutorialSpeaker.Geumok) ? GameTutorialSpeaker.Geumok
            : seat == SeatOf(GameTutorialSpeaker.Muk) ? GameTutorialSpeaker.Muk : GameTutorialSpeaker.Bul;

        /// <summary>
        /// 건너뛰기: 튜토리얼 화면을 한 번이라도 누르면 왼쪽 위에 이 버튼이 서서히 나타난다. 누르면 아래 경고 창이 뜬다.
        /// "계속 배우기"는 하던 곳부터 잇고, "건너뛰기"는 마지막 장면 뒤와 같이 어두워지며 방 목록으로 간다.
        /// </summary>
        public const string SkipButtonLabel = "건너뛰기";
        public const string SkipTitle = "튜토리얼을 건너뛰겠소?";
        public const string SkipBody = "튜토리얼을 건너뛰면 구슬 전달과 조합 등\n기본 조작을 익히지 못해\n협동 플레이에 어려움이 있을 수 있소.";
        public const string SkipStay = "계속 배우기";
        public const string SkipLeave = "건너뛰기";
        /// <summary>튜토리얼 일시정지 메뉴에 더하는 버튼: 연습 판을 처음부터 다시 연다. 메뉴의 다른 버튼(CONTINUE·QUIT RUN)과 같은 말투.</summary>
        public const string RestartLabel = "RESTART";

        /// <summary>1단계에서 북을 세 번 치면 나오는 구슬: 음 2개·양 1개라 2단계에서 한 쌍을 합치고 음 하나가 남는다.</summary>
        public static readonly IReadOnlyList<LabOrbKind> DrumResults =
            Array.AsReadOnly(new[] { LabOrbKind.Yin, LabOrbKind.Yang, LabOrbKind.Yin });

        /// <summary>
        /// 세 구슬이 북에서 나가는 방향(바로 위에서 몇 도, 오른쪽 +)과 멈출 때까지 가는 거리(판 너비 비율). 매번 같은 자리에 선다
        /// (사용자 시안 2026-11-04): 첫 음은 북 왼쪽, 양은 북 오른쪽(같은 높이), 둘째 음은 양의 왼쪽 위.
        /// 2단계는 양에 붙어 있는 둘째 음을 양 위로 끌어 놓고, 왼쪽 음은 4단계까지 남는다.
        /// </summary>
        public static readonly IReadOnlyList<(float angleFromUp, float distance)> DrumLaunches = Array.AsReadOnly(new[]
        {
            (-79.1f, .36f), (74.8f, .259f), (38.5f, .25f)
        });

        /// <summary>
        /// How far from the drum's center a new orb starts, in board widths: the board begins its body just outside the
        /// drum (drum radius + orb radius) so the two never start overlapped.
        /// </summary>
        public static float DrumClearance(float drumRadius, float orbRadius, float boardWidth) =>
            boardWidth > 0f ? (UnityEngine.Mathf.Max(0f, drumRadius) + UnityEngine.Mathf.Max(0f, orbRadius)) / boardWidth : 0f;

        /// <summary>The slide left for an orb to rest <paramref name="fromDrumCenter"/> away after leaving the drum's edge.</summary>
        public static float SlideAfterDrum(float fromDrumCenter, float clearance) =>
            UnityEngine.Mathf.Max(.01f, fromDrumCenter - UnityEngine.Mathf.Max(0f, clearance));

        /// <summary>
        /// The slide with <paramref name="extra"/> board widths more (so the orbs visibly fly out), but never so far that,
        /// leaving a drum in the middle of the board at <paramref name="angleFromUp"/>, the orb's center comes nearer than
        /// <paramref name="edgeMargin"/> to a side edge.
        /// </summary>
        public static float SlideAfterDrum(float fromDrumCenter, float clearance, float extra, float angleFromUp, float edgeMargin)
        {
            float slide = SlideAfterDrum(fromDrumCenter, clearance) + UnityEngine.Mathf.Max(0f, extra);
            float side = UnityEngine.Mathf.Abs(UnityEngine.Mathf.Sin(angleFromUp * UnityEngine.Mathf.Deg2Rad));
            if (side > .0001f)
            {
                float room = (.5f - UnityEngine.Mathf.Clamp(edgeMargin, 0f, .5f)) / side - UnityEngine.Mathf.Max(0f, clearance);
                slide = UnityEngine.Mathf.Min(slide, UnityEngine.Mathf.Max(.01f, room));
            }
            return slide;
        }

        /// <summary>
        /// The launch that stops an orb <paramref name="distance"/> board widths away in that direction, when the floor
        /// slows it by <paramref name="deceleration"/> board widths per second²: speed² = 2 × deceleration × distance.
        /// </summary>
        public static UnityEngine.Vector2 LaunchVelocity(float angleFromUp, float distance, float deceleration)
        {
            float radians = angleFromUp * UnityEngine.Mathf.Deg2Rad;
            float speed = UnityEngine.Mathf.Sqrt(2f * UnityEngine.Mathf.Max(0f, deceleration) * UnityEngine.Mathf.Max(0f, distance));
            return new UnityEngine.Vector2(UnityEngine.Mathf.Sin(radians), UnityEngine.Mathf.Cos(radians)) * speed;
        }

        private static GameTutorialBeat Bul(string text, GameTutorialFocus focus = GameTutorialFocus.None) =>
            GameTutorialBeat.Line(GameTutorialSpeaker.Bul, text, focus);
        private static GameTutorialBeat Geumok(string text, GameTutorialFocus focus = GameTutorialFocus.None,
            GameTutorialCue cue = GameTutorialCue.None) => GameTutorialBeat.Line(GameTutorialSpeaker.Geumok, text, focus, cue);
        private static GameTutorialBeat Muk(string text) => GameTutorialBeat.Line(GameTutorialSpeaker.Muk, text);

        /// <summary>0단계 · 목표: 박을 오행의 기운으로 빈틈없이 칠하면 요괴를 가둔다.</summary>
        public static readonly GameTutorialStep Goal = new GameTutorialStep("0단계 · 목표",
            Geumok("잠깐! 무작정 가면 안 돼."),
            Geumok("사용법부터 익히고 가자."),
            Bul("에이~ 그냥 던지면 되는 거 아냐?"),
            Geumok("그러다 또 사고 칠래?"),
            Geumok("마침 박 속에 작은 요괴가 남아 있네.\n얘로 연습하자!"),
            Geumok("봉인박을 오행의 기운으로\n빈틈없이 칠하면 요괴를 가둘 수 있어.", GameTutorialFocus.Gourd));

        /// <summary>
        /// 1단계 · 북 치기: 가운데 고정된 북을 세 번 눌러 음·양·음을 만든다. 기운은 딱 세 번 칠 만큼이라 바닥나고,
        /// 기운 막대만 밝힌 채 부족 연출을 보여 준 뒤 회복을 풀어 다시 차오르는 것을 보여 준다.
        /// </summary>
        public static readonly GameTutorialStep Drum = new GameTutorialStep("1단계 · 북 치기",
            Geumok("먼저 북을 쳐 봐.", GameTutorialFocus.Drum),
            Geumok("음구슬이나 양구슬이 튀어나올 거야.", GameTutorialFocus.Drum),
            GameTutorialBeat.Do(GameTutorialAction.TapDrum, "북을 눌러 구슬을 만드시오", GameTutorialFocus.Drum,
                GameStageSettings.TutorialDrumTaps),
            Bul("오, 나온다 나와!"),
            Geumok("북을 칠 때마다 기운이 줄어.", GameTutorialFocus.Stamina),
            Geumok("바닥나면 잠깐 숨을 골라야 해.", GameTutorialFocus.Stamina, GameTutorialCue.StaminaShort),
            GameTutorialBeat.Do(GameTutorialAction.RefillStamina, string.Empty, GameTutorialFocus.Stamina));

        /// <summary>2단계에서 음·양을 합치면 나오는 오행: 화(빨강). 3단계에서 이 빨간 구슬을 던진다.</summary>
        public const LabOrbElement CombineResult = LabOrbElement.Fire;

        /// <summary>
        /// 2단계 · 조합: 음구슬 하나를 끌어 양구슬 위에 놓으면 화(빨강) 오행구슬이 된다. 남은 음구슬 하나는 4단계에서 쓴다.
        /// 합친 뒤에는 어둡게 하지 않아 빨간 구슬과 남은 음구슬이 함께 보인다.
        /// </summary>
        public static readonly GameTutorialStep Combine = new GameTutorialStep("2단계 · 조합",
            Geumok("검은 음구슬과 하얀 양구슬을\n맞붙여 봐.", GameTutorialFocus.Pair),
            Bul("검은 거 두 개는 안 돼?", GameTutorialFocus.Yins),
            Geumok("같은 색끼리는 안 붙어!", GameTutorialFocus.Yins),
            GameTutorialBeat.Do(GameTutorialAction.CombineOrbs, "음구슬을 끌어 양구슬 위에 놓으시오", GameTutorialFocus.Pair),
            Bul("우와, 색이 변했어!"),
            Geumok("그게 오행구슬이야."),
            Geumok("화·수·목·금·토 중 하나가 나와."));

        /// <summary>3단계에서 빗나갔을 때 금옥이가 하는 말. 말풍선은 잠깐 뜨고, 화면을 막지 않아 바로 다시 던질 수 있다.</summary>
        public const string RetryLine = "다시 해 봐!";
        public const GameTutorialSpeaker RetrySpeaker = GameTutorialSpeaker.Geumok;
        /// <summary>
        /// 이스터에그: 이만큼째 빗나가면 금옥이가 "다시 해 봐!" 대신 이 말을 먼저 한다. 그다음 "다시 해 봐!"가 이어서 뜨고,
        /// 그 뒤로 빗나가면 다시 "다시 해 봐!"만 나온다 (한 번만).
        /// </summary>
        public const int TeaseMisses = 5;
        public const string TeaseLine = "......\n너 일부러 그러는 거지?";

        /// <summary>The first word after the <paramref name="misses"/>th miss: the tease once, at the fifth, else "다시 해 봐!".</summary>
        public static string MissLine(int misses) => misses == TeaseMisses ? TeaseLine : RetryLine;
        /// <summary>빗나간 빨간 구슬이 다시 떨어지는 자리 (판 0..1): 오른쪽, 북과 남은 음구슬을 피해서.</summary>
        public static readonly UnityEngine.Vector2 RetryLanding = new UnityEngine.Vector2(.72f, .6f);

        /// <summary>
        /// 3단계 · 던지기: 2단계에서 만든 빨간(화) 오행구슬을 위로 밀어 박에 맞힌다. 빗나가면 다시 준다.
        /// 맞히면 박에 색이 묻고, 위쪽 진행률 막대만 밝혀 100%가 목표임을 보여 준다.
        /// </summary>
        public static readonly GameTutorialStep Throw = new GameTutorialStep("3단계 · 던지기",
            Geumok("이제 오행구슬을 위로 휙!\n박을 향해 던져 봐."),
            GameTutorialBeat.Do(GameTutorialAction.ThrowOrb, "빨간 구슬을 위로 밀어 박에 던지시오", GameTutorialFocus.None),
            Bul("맞았다! 박에 색이 묻었어!"),
            Geumok("박이 100% 칠해질 때까지\n계속 칠하면 돼!", GameTutorialFocus.Progress));

        /// <summary>4단계에서 북을 왼쪽(묵이)으로 넘겼을 때 묵이가 하며 바로 돌려주는 말.</summary>
        public const string WrongSideLine = "이건 금옥이한테 줘.";
        public const GameTutorialSpeaker WrongSideSpeaker = GameTutorialSpeaker.Muk;
        /// <summary>금옥이가 돌려주는 양구슬·북이 내 판 오른쪽 끝으로 들어오는 높이 (판 0..1).</summary>
        public const float GiveBackYangHeight = .62f, GiveBackDrumHeight = .5f;
        /// <summary>돌려받은 양구슬이 오른쪽 끝에서 미끄러져 들어와 멈추는 거리 (판 너비 비율).</summary>
        public const float GiveBackSlide = .22f;

        /// <summary>
        /// 4단계 · 넘기기: 북은 몇 개 없어 같이 쓴다. 북을 오른쪽 끝으로 밀어 금옥이에게 넘기면, 금옥이가 고맙다며 자기가 만든
        /// 양구슬과 북을 함께 돌려준다. 남은 음과 받은 양은 짝이 안 맞는 구슬을 친구에게 넘기는 이유가 된다.
        /// </summary>
        public static readonly GameTutorialStep Pass = new GameTutorialStep("4단계 · 넘기기",
            Bul("나만 너무 열심히 하고 있는 거 아니야?"),
            Geumok("맞아! 너가 북을 줘야지... \n북은 몇 개 없어서 같이 써야 해! 옆으로 밀면 넘어가. "),
            GameTutorialBeat.Do(GameTutorialAction.PassDrum, "북을 오른쪽 끝으로 밀어 금옥이에게 넘기시오", GameTutorialFocus.None),
            Geumok("고마워!\n대신 내가 만든 구슬과 북 줄게~", cue: GameTutorialCue.GiveBack),
            Geumok("짝이 안 맞는 구슬은\n이렇게 친구한테 넘기면 돼."),
            Geumok("지금 북이 어디에 있는지는\n우측 지도에서 볼 수 있어!", GameTutorialFocus.Minimap),
            Bul("오호! 누가 북을 안 주고 있는지\n실시간으로 확인할 수 있겠군!", GameTutorialFocus.Minimap));

        /// <summary>5단계 폭탄의 자물쇠 수: 내가 넘기면 1, 금옥이가 돌려주면 0이 되어 내 판에서 열린다.</summary>
        public const int BombLocks = 2;
        /// <summary>
        /// 폭탄이 떨어지는 자리 (판 0..1): 북보다 위·왼쪽. 오른쪽 끝으로 미는 길이 금옥이 얼굴(위 오른쪽 구석)과
        /// 돌려받은 양구슬(오른쪽 0.62 높이) 사이로 난다.
        /// </summary>
        public static readonly UnityEngine.Vector2 BombLanding = new UnityEngine.Vector2(.42f, .72f);

        /// <summary>
        /// 5단계 · 폭탄: 요괴가 박에서 자물쇠 2짜리 폭탄을 떨어뜨린다. 오른쪽 금옥이에게 넘기면(2→1) 금옥이가 "앗 뜨거!" 하며
        /// 도로 넘겨주고(1→0), 풀린 폭탄은 내 판에서 공격 구슬 두 개로 열린다.
        /// </summary>
        public static readonly GameTutorialStep Bomb = new GameTutorialStep("5단계 · 폭탄",
            GameTutorialBeat.Line(GameTutorialSpeaker.Bul, "으악! 박에서 뭐가 떨어졌어!", cue: GameTutorialCue.DropBomb),
            Geumok("요괴가 던진 폭탄이야!\n적힌 숫자만큼 옆으로 넘기면 풀려.", GameTutorialFocus.Bomb),
            GameTutorialBeat.Do(GameTutorialAction.PassBomb, "폭탄을 오른쪽 금옥이에게 넘기시오", GameTutorialFocus.None),
            Geumok("앗 뜨거! 도로 받아!", cue: GameTutorialCue.ReturnBomb),
            GameTutorialBeat.Do(GameTutorialAction.WaitBombBack, string.Empty, GameTutorialFocus.None),
            Geumok("풀렸다!", cue: GameTutorialCue.OpenBomb),
            Geumok("풀린 폭탄은 오행 구슬 두 개가 돼."),
            Bul("가만히 냅두면 터지니까\n얼른얼른 넘겨야겠네!"));

        /// <summary>6단계에서 요괴가 막는 쪽: 내 판 오른쪽(금옥이 쪽).</summary>
        public const int BlockedSide = 1;

        /// <summary>
        /// 6단계 · 막기 + 마무리: 요괴가 오른쪽을 막아 가장자리가 빨갛게 번쩍인 뒤 대사가 뜬다. 막힌 쪽으로 밀면 튕겨 나오고,
        /// 반대쪽 묵이에게 북을 넘기면 막기가 풀린다. 넷이 한마디씩 하고 어두워지며 방 목록으로 간다.
        /// </summary>
        public static readonly GameTutorialStep Block = new GameTutorialStep("6단계 · 막기",
            GameTutorialBeat.Do(GameTutorialAction.WatchBlock, string.Empty, GameTutorialFocus.None),
            Bul("어? 오른쪽이 빨개졌어!"),
            Geumok("요괴가 오른쪽을 막은 것 같아!"),
            Geumok("막힌 쪽은 튕겨 나와.\n반대쪽 묵이한테 북을 넘겨!"),
            Muk("드디어 내 차례가 왔네\n기다리느라 힘들었어..."),
            GameTutorialBeat.Do(GameTutorialAction.PassDrumLeft, "북을 왼쪽 끝으로 밀어 묵이에게 넘기시오", GameTutorialFocus.None),
            Muk("받았어. 고마워."),
            Geumok("좋아, 이 정도면\n잡을 수 있겠어!"),
            Geumok("앞으로 나올 요괴들이 어떤 방해를 할지 모르니,\n조심하자!"),
            Bul("이번엔 진짜다!\n다 같이 가자!"),
            Muk("가보자!"));

        /// <summary>순서대로 진행하는 단계들. 마지막 단계가 끝나면 어두워지며 방 목록으로 간다.</summary>
        public static readonly IReadOnlyList<GameTutorialStep> Steps =
            Array.AsReadOnly(new[] { Goal, Drum, Combine, Throw, Pass, Bomb, Block });

        /// <summary>
        /// The seat (P number - 1) each speaker sits in. A player's right neighbor is the previous seat and the left
        /// neighbor the next one (<see cref="C6Lab.LabPlayerSlots.NeighborSeat"/>), so 금옥이 on the right is P1.
        /// </summary>
        public static int SeatOf(GameTutorialSpeaker speaker) => speaker == GameTutorialSpeaker.Geumok ? 0
            : speaker == GameTutorialSpeaker.Bul ? 1 : 2;
    }
}
