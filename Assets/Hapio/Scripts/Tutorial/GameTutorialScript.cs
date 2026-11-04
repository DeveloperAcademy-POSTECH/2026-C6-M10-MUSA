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
        Progress
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
        ThrowOrb
    }

    /// <summary>한 장면이 뜰 때 함께 보여 주는 연출.</summary>
    public enum GameTutorialCue { None, StaminaShort }

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

        /// <summary>순서대로 진행하는 단계들. 마지막 단계가 끝나면 어두워지며 방 목록으로 간다.</summary>
        public static readonly IReadOnlyList<GameTutorialStep> Steps = Array.AsReadOnly(new[] { Goal, Drum, Combine, Throw });

        /// <summary>
        /// The seat (P number - 1) each speaker sits in. A player's right neighbor is the previous seat and the left
        /// neighbor the next one (<see cref="C6Lab.LabPlayerSlots.NeighborSeat"/>), so 금옥이 on the right is P1.
        /// </summary>
        public static int SeatOf(GameTutorialSpeaker speaker) => speaker == GameTutorialSpeaker.Geumok ? 0
            : speaker == GameTutorialSpeaker.Bul ? 1 : 2;
    }
}
