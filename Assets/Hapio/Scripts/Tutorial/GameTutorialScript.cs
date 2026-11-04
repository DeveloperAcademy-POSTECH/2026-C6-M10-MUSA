using System;
using System.Collections.Generic;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 (#477)에서 말하는 사람. 자리와 같다: 나(불이)는 아래 가운데 P2, 금옥이는 오른쪽 이웃 P1(앞 번호),
    /// 묵이는 왼쪽 이웃 P3(다음 번호). 말풍선은 말하는 사람 자리의 얼굴에서 나온다.
    /// </summary>
    public enum GameTutorialSpeaker { Bul, Muk, Geumok }

    /// <summary>대사가 나오는 동안 화면을 어둡게 하고 밝혀 둘 곳. None이면 어둡게 하지 않는다.</summary>
    public enum GameTutorialFocus { None, Gourd }

    /// <summary>한 번 누를 때마다 넘어가는 대사 한 줄. 줄바꿈(\n)은 말풍선 안에서 그대로 줄을 바꾼다.</summary>
    public readonly struct GameTutorialLine
    {
        public readonly GameTutorialSpeaker Speaker;
        public readonly string Text;
        public readonly GameTutorialFocus Focus;

        public GameTutorialLine(GameTutorialSpeaker speaker, string text, GameTutorialFocus focus = GameTutorialFocus.None)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("A tutorial line needs words.", nameof(text));
            Speaker = speaker;
            Text = text;
            Focus = focus;
        }
    }

    /// <summary>튜토리얼 한 단계: 이름과 대사들. 동작을 기다리는 단계는 다음 작업에서 붙는다.</summary>
    public sealed class GameTutorialStep
    {
        public readonly string Name;
        public readonly IReadOnlyList<GameTutorialLine> Lines;

        public GameTutorialStep(string name, params GameTutorialLine[] lines)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A tutorial step needs a name.", nameof(name));
            if (lines == null || lines.Length == 0) throw new ArgumentException("A tutorial step needs lines.", nameof(lines));
            Name = name;
            Lines = Array.AsReadOnly((GameTutorialLine[])lines.Clone());
        }
    }

    /// <summary>
    /// 확정 대본 (#477, 2026-11-04). 오프닝 마지막 "얼른 요괴 잡으러 가자!" 바로 뒤에서 금옥이가 붙잡으며 시작한다.
    /// 아이들끼리는 반말, 한 말풍선은 두 줄 이내.
    /// </summary>
    public static class GameTutorialScript
    {
        /// <summary>전투 화면 위쪽 "STAGE 1 / 3 · 구미호" 자리에 대신 쓰는 글자.</summary>
        public const string StageLabel = "튜토리얼";

        private static GameTutorialLine Bul(string text, GameTutorialFocus focus = GameTutorialFocus.None) =>
            new GameTutorialLine(GameTutorialSpeaker.Bul, text, focus);
        private static GameTutorialLine Geumok(string text, GameTutorialFocus focus = GameTutorialFocus.None) =>
            new GameTutorialLine(GameTutorialSpeaker.Geumok, text, focus);

        /// <summary>0단계 · 목표: 박을 오행의 기운으로 빈틈없이 칠하면 요괴를 가둔다.</summary>
        public static readonly GameTutorialStep Goal = new GameTutorialStep("0단계 · 목표",
            Geumok("잠깐! 무작정 가면 안 돼."),
            Geumok("사용법부터 익히고 가자."),
            Bul("에이~ 그냥 던지면 되는 거 아냐?"),
            Geumok("그러다 또 사고 칠래?"),
            Geumok("마침 박 속에 작은 요괴가 남아 있네.\n얘로 연습하자!"),
            Geumok("봉인박을 오행의 기운으로\n빈틈없이 칠하면 요괴를 가둘 수 있어.", GameTutorialFocus.Gourd));

        /// <summary>순서대로 진행하는 단계들. 마지막 단계가 끝나면 어두워지며 방 목록으로 간다.</summary>
        public static readonly IReadOnlyList<GameTutorialStep> Steps = Array.AsReadOnly(new[] { Goal });

        /// <summary>
        /// The seat (P number - 1) each speaker sits in. A player's right neighbor is the previous seat and the left
        /// neighbor the next one (<see cref="C6Lab.LabPlayerSlots.NeighborSeat"/>), so 금옥이 on the right is P1.
        /// </summary>
        public static int SeatOf(GameTutorialSpeaker speaker) => speaker == GameTutorialSpeaker.Geumok ? 0
            : speaker == GameTutorialSpeaker.Bul ? 1 : 2;
    }
}
