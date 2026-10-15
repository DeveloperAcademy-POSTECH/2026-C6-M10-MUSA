using System;
using System.Collections.Generic;
using System.Linq;

namespace C6Lab
{
    /// <summary>결과 대시보드의 MVP 칭호 종류. (생성은 누구나 하는 일이라 비교하지 않는다)</summary>
    public enum LabResultTitle { MostCombined, MostItems, MostPassed }

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
                case LabResultTitle.MostCombined: return player.combinedTotal;
                case LabResultTitle.MostItems: return player.ItemsUsed;
                case LabResultTitle.MostPassed: return player.passedTotal;
                default: return 0;
            }
        }

        /// <summary>
        /// 항목별 1등. 동점이면 모두 받는다. 아무도 기록이 없는(0) 항목은 칭호를 주지 않는다.
        /// 좌석 순서(P1, P2…)로 정렬해 돌려준다.
        /// </summary>
        public static IReadOnlyList<LabResultAward> Awards(IEnumerable<LabPlayerState> players)
        {
            var list = (players ?? Array.Empty<LabPlayerState>()).Where(p => p != null).OrderBy(p => p.seat).ToArray();
            var awards = new List<LabResultAward>();
            if (list.Length == 0) return awards;
            foreach (LabResultTitle title in Enum.GetValues(typeof(LabResultTitle)))
            {
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
                case LabResultTitle.MostCombined: return "결합왕";
                case LabResultTitle.MostItems: return "아이템왕";
                case LabResultTitle.MostPassed: return "패스왕";
                default: return title.ToString();
            }
        }

        /// <summary>플레이 시간 중 북을 가지고 있던 비율(0~100, 반올림). 플레이 시간이 0이면 0.</summary>
        public static int DrumSharePercent(LabPlayerState player, double playedSeconds)
        {
            if (player == null || playedSeconds <= 0d || player.drumSeconds <= 0d) return 0;
            return (int)Math.Round(Math.Min(1d, player.drumSeconds / playedSeconds) * 100d, MidpointRounding.AwayFromZero);
        }

        /// <summary>북 점유 칸 글자: "12초" 아래 줄에 "40%"</summary>
        public static string DrumText(LabPlayerState player, double playedSeconds) =>
            player == null ? "" : (int)Math.Round(player.drumSeconds, MidpointRounding.AwayFromZero) + "초\n"
                + DrumSharePercent(player, playedSeconds) + "%";

        /// <summary>좌석 번호로 만든 플레이어 이름 (P1~P5). 화면의 이웃 표시와 같은 규칙.</summary>
        public static string PlayerName(LabPlayerState player) => player == null ? "P—" : "P" + (player.seat + 1);
    }
}
