using System;
using System.Collections.Generic;
using System.Linq;

namespace C6Lab
{
    /// <summary>
    /// 결과 대시보드의 칭호 종류 (화면에 붙는 순서).
    /// 딜왕: 요괴에게 준 피해 최다 · 구슬 패스왕: 구슬 전달 최다 · 북 패스왕: 북 전달 최다 ·
    /// 막타: 요괴를 쓰러뜨린 한 방(승리 시에만) · 북 독점: 북 점유율 최고 (점유 시간으로 비교)
    /// </summary>
    public enum LabResultTitle { MostDamage, MostPassed, MostDrumPassed, LastHit, MostDrum }

    /// <summary>한 칭호의 결과. 동점이면 Winners에 여러 명이 들어간다.</summary>
    public readonly struct LabResultAward
    {
        public readonly LabResultTitle Title;
        public readonly int Value;
        public readonly LabPlayerState[] Winners;

        public LabResultAward(LabResultTitle title, int value, LabPlayerState[] winners)
        {
            Title = title;
            Value = value;
            Winners = winners;
        }
    }

    /// <summary>
    /// 결과 대시보드 계산. 화면과 분리해 두어 디자인이 바뀌어도 그대로 쓴다.
    /// </summary>
    public static class LabResultStats
    {
        public static int ValueOf(LabPlayerState player, LabResultTitle title)
        {
            if (player == null) return 0;
            switch (title)
            {
                case LabResultTitle.MostDamage: return player.damageTotal;
                case LabResultTitle.MostPassed: return player.passedTotal;
                case LabResultTitle.MostDrumPassed: return player.drumPassedTotal;
                case LabResultTitle.LastHit: return player.lastHit ? 1 : 0;
                // 화면에 보이는 정수 초로 비교해서, 같은 초로 보이면 공동 칭호
                case LabResultTitle.MostDrum: return (int)Math.Round(player.drumSeconds, MidpointRounding.AwayFromZero);
                default: return 0;
            }
        }

        /// <summary>
        /// 항목별 1등. 동점이면 모두 받는다. 아무도 기록이 없는(0) 항목은 칭호를 주지 않는다.
        /// 막타는 승리했을 때만 준다. 좌석 순서(P1, P2…)로 정렬해 돌려준다.
        /// </summary>
        public static IReadOnlyList<LabResultAward> Awards(IEnumerable<LabPlayerState> players, bool victory = false)
        {
            var list = (players ?? Array.Empty<LabPlayerState>()).Where(p => p != null).OrderBy(p => p.seat).ToArray();
            var awards = new List<LabResultAward>();
            if (list.Length == 0) return awards;
            foreach (LabResultTitle title in Enum.GetValues(typeof(LabResultTitle)))
            {
                if (title == LabResultTitle.LastHit && !victory) continue;
                int best = list.Max(p => ValueOf(p, title));
                if (best <= 0) continue;
                awards.Add(new LabResultAward(title, best, list.Where(p => ValueOf(p, title) == best).ToArray()));
            }
            return awards;
        }

        public static string Label(LabResultTitle title)
        {
            switch (title)
            {
                case LabResultTitle.MostDamage: return "딜왕";
                case LabResultTitle.MostPassed: return "구슬 패스왕";
                case LabResultTitle.MostDrumPassed: return "북 패스왕";
                case LabResultTitle.LastHit: return "막타";
                case LabResultTitle.MostDrum: return "북 독점";
                default: return title.ToString();
            }
        }

        /// <summary>북 점유율: 플레이 시간 중 북을 점유한 비율(0~100, 반올림). 플레이 시간이 0이면 0.</summary>
        public static int DrumSharePercent(LabPlayerState player, double playedSeconds)
        {
            if (player == null || playedSeconds <= 0d || player.drumSeconds <= 0d) return 0;
            return (int)Math.Round(Math.Min(1d, player.drumSeconds / playedSeconds) * 100d, MidpointRounding.AwayFromZero);
        }

        /// <summary>북 점유 글자: 점유 시간 · 북 점유율 (예: "12초 · 40%")</summary>
        public static string DrumText(LabPlayerState player, double playedSeconds) =>
            player == null ? "" : (int)Math.Round(player.drumSeconds, MidpointRounding.AwayFromZero) + "초 · "
                + DrumSharePercent(player, playedSeconds) + "%";

        /// <summary>칭호를 받은 사람들 (예: "P1, P3"). 동점이면 좌석 순서로 모두 적는다. 내 줄은 화면에서 색으로 구분한다.</summary>
        public static string WinnerNames(LabResultAward award) =>
            award.Winners == null ? "" : string.Join(", ", award.Winners.Select(PlayerName));

        /// <summary>
        /// 칭호 줄의 기록 칸. 딜왕 "40 (2회 타격)" (동점이면 "60 (2·3회 타격)", 이름 순서와 같음) ·
        /// 구슬 패스왕·북 패스왕 "5회" · 북 독점 "12초 · 40%" · 막타는 빈칸.
        /// </summary>
        public static string Record(LabResultAward award, double playedSeconds)
        {
            if (award.Winners == null || award.Winners.Length == 0) return "";
            switch (award.Title)
            {
                case LabResultTitle.MostDamage:
                    return award.Value + " (" + string.Join("·", award.Winners.Select(p => p.hitTotal)) + "회 타격)";
                case LabResultTitle.MostPassed:
                case LabResultTitle.MostDrumPassed: return award.Value + "회";
                case LabResultTitle.MostDrum: return DrumText(award.Winners[0], playedSeconds);
                default: return "";
            }
        }

        /// <summary>좌석 번호로 만든 플레이어 이름 (P1~P5). 화면의 이웃 표시와 같은 규칙.</summary>
        public static string PlayerName(LabPlayerState player) => player == null ? "P—" : "P" + (player.seat + 1);
    }
}
