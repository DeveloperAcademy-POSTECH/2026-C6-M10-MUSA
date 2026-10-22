using System;
using System.Collections.Generic;
using System.Linq;

namespace C6Lab
{
    /// <summary>
    /// 결과 대시보드의 칭호 종류 (화면에 붙는 순서).
    /// 딜왕: 요괴에게 준 피해 최다 · 명중왕: 던진 공 중 맞힌 비율 최고 · 구슬 패스왕: 구슬 전달 최다 · 북 패스왕: 북 전달 최다 ·
    /// 막타: 요괴를 쓰러뜨린 한 방(승리 시에만) · 북 독점: 북 점유율 최고 (점유 시간으로 비교)
    /// </summary>
    public enum LabResultTitle { MostDamage, MostAccurate, MostPassed, MostDrumPassed, LastHit, MostDrum }

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

    /// <summary>한 스테이지의 결과 기록: 그 스테이지 플레이어 기록(복사본)과 플레이 시간.</summary>
    public sealed class LabResultStage
    {
        public readonly LabPlayerState[] Players;
        public readonly double PlayedSeconds;

        public LabResultStage(IEnumerable<LabPlayerState> players, double playedSeconds)
        {
            Players = (players ?? Array.Empty<LabPlayerState>()).Where(p => p != null).Select(p => p.Copy()).ToArray();
            PlayedSeconds = Math.Max(0d, playedSeconds);
        }
    }

    /// <summary>
    /// 전체 게임 대시보드 계산 결과. 칭호 값은 스테이지 평균이고(명중왕은 전체 맞힌 수 / 전체 던진 수,
    /// 막타는 횟수), StageCount는 평균을 낸 스테이지 수다.
    /// </summary>
    public sealed class LabResultOverall
    {
        public readonly IReadOnlyList<LabResultAward> Awards;
        public readonly int StageCount;
        /// <summary>스테이지 평균 플레이 시간. 북 점유율의 분모(평균 점유 시간 / 이 값 = 전체 점유 / 전체 시간).</summary>
        public readonly double AveragePlayedSeconds;

        public LabResultOverall(IReadOnlyList<LabResultAward> awards, int stageCount, double averagePlayedSeconds)
        {
            Awards = awards ?? Array.Empty<LabResultAward>();
            StageCount = stageCount;
            AveragePlayedSeconds = averagePlayedSeconds;
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
                case LabResultTitle.MostAccurate: return AccuracyPercent(player);
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
        /// 명중왕은 화면에 보이는 정수 %가 같으면 맞힌 수가 많은 사람이 받고, 그것도 같으면 공동이다.
        /// </summary>
        public static IReadOnlyList<LabResultAward> Awards(IEnumerable<LabPlayerState> players, bool victory = false)
            => AwardsBy(players, victory, ValueOf);

        /// <summary>Awards와 같은 규칙으로, 항목 값만 value로 바꿔 매긴다 (전체 대시보드의 막타 횟수 등).</summary>
        private static IReadOnlyList<LabResultAward> AwardsBy(IEnumerable<LabPlayerState> players, bool victory,
            Func<LabPlayerState, LabResultTitle, int> value)
        {
            var list = (players ?? Array.Empty<LabPlayerState>()).Where(p => p != null).OrderBy(p => p.seat).ToArray();
            var awards = new List<LabResultAward>();
            if (list.Length == 0) return awards;
            foreach (LabResultTitle title in Enum.GetValues(typeof(LabResultTitle)))
            {
                if (title == LabResultTitle.LastHit && !victory) continue;
                int best = list.Max(p => value(p, title));
                if (best <= 0) continue;
                LabPlayerState[] winners = list.Where(p => value(p, title) == best).ToArray();
                if (title == LabResultTitle.MostAccurate)
                {
                    int mostHits = winners.Max(p => p.hitTotal);
                    winners = winners.Where(p => p.hitTotal == mostHits).ToArray();
                }
                awards.Add(new LabResultAward(title, best, winners));
            }
            return awards;
        }

        /// <summary>
        /// 전체 게임 대시보드: 스테이지 기록들을 모아 칭호를 다시 매긴다. 스테이지 수로 나눈 평균(반올림 정수)을
        /// 화면에 보이는 값으로 비교하므로, 보이는 값이 같으면 공동이다. 어떤 스테이지에 없던 사람은 그 스테이지를 0으로 친다.
        /// 명중왕은 전체 맞힌 수 / 전체 던진 수, 막타는 막타를 친 횟수, 북 독점은 평균 점유 시간으로 비교한다.
        /// 돌려주는 칭호의 Winners는 평균값을 담은 기록이다 (hitTotal·throwTotal만 전체 합계).
        /// </summary>
        public static LabResultOverall Overall(IEnumerable<LabResultStage> stages)
        {
            LabResultStage[] list = (stages ?? Array.Empty<LabResultStage>()).Where(s => s != null).ToArray();
            int n = list.Length;
            if (n == 0) return new LabResultOverall(Array.Empty<LabResultAward>(), 0, 0d);
            var sums = new Dictionary<ulong, OverallSum>();
            foreach (LabResultStage stage in list)
                foreach (LabPlayerState p in stage.Players)
                {
                    if (!sums.TryGetValue(p.id, out OverallSum sum)) sums[p.id] = sum = new OverallSum { id = p.id, seat = p.seat };
                    sum.damage += p.damageTotal;
                    sum.passed += p.passedTotal;
                    sum.drumPassed += p.drumPassedTotal;
                    sum.drumSeconds += p.drumSeconds;
                    sum.hits += p.hitTotal;
                    sum.throws += p.throwTotal;
                    if (p.lastHit) sum.lastHits++;
                }
            var lastHits = new Dictionary<ulong, int>();
            var averaged = new List<LabPlayerState>();
            foreach (OverallSum sum in sums.Values)
            {
                lastHits[sum.id] = sum.lastHits;
                averaged.Add(new LabPlayerState
                {
                    id = sum.id, seat = sum.seat,
                    damageTotal = Average(sum.damage, n), passedTotal = Average(sum.passed, n),
                    drumPassedTotal = Average(sum.drumPassed, n), drumSeconds = sum.drumSeconds / n,
                    hitTotal = (int)Math.Min(int.MaxValue, sum.hits), throwTotal = (int)Math.Min(int.MaxValue, sum.throws),
                    lastHit = sum.lastHits > 0
                });
            }
            IReadOnlyList<LabResultAward> awards = AwardsBy(averaged, true, (p, title) =>
                title == LabResultTitle.LastHit ? lastHits[p.id] : ValueOf(p, title));
            return new LabResultOverall(awards, n, list.Sum(s => s.PlayedSeconds) / n);
        }

        /// <summary>
        /// 전체 대시보드 기록 칸. 딜왕 "40 (2회 타격)"(둘 다 스테이지 평균) · 명중왕 "60%" ·
        /// 구슬 패스왕·북 패스왕 "5회"(평균) · 막타 "2회"(횟수) · 북 독점 "20초 · 33%"(평균 시간 · 전체 점유율).
        /// </summary>
        public static string OverallRecord(LabResultAward award, LabResultOverall overall)
        {
            if (award.Winners == null || award.Winners.Length == 0 || overall == null || overall.StageCount <= 0) return "";
            switch (award.Title)
            {
                case LabResultTitle.MostDamage:
                    return award.Value + " (" + string.Join("·", award.Winners.Select(p => Average(p.hitTotal, overall.StageCount)))
                        + "회 타격)";
                case LabResultTitle.LastHit: return award.Value + "회";
                default: return Record(award, overall.AveragePlayedSeconds);
            }
        }

        private sealed class OverallSum
        {
            public ulong id;
            public int seat;
            public long damage, passed, drumPassed, hits, throws;
            public double drumSeconds;
            public int lastHits;
        }

        private static int Average(long sum, int count) => count <= 0 ? 0
            : (int)Math.Min(int.MaxValue, Math.Round((double)sum / count, MidpointRounding.AwayFromZero));

        public static string Label(LabResultTitle title)
        {
            switch (title)
            {
                case LabResultTitle.MostDamage: return "딜왕";
                case LabResultTitle.MostAccurate: return "명중왕";
                case LabResultTitle.MostPassed: return "구슬 패스왕";
                case LabResultTitle.MostDrumPassed: return "북 패스왕";
                case LabResultTitle.LastHit: return "막타";
                case LabResultTitle.MostDrum: return "북 독점";
                default: return title.ToString();
            }
        }

        /// <summary>
        /// 명중률: 던진 공(throwTotal) 중 요괴에 맞힌 공(hitTotal)의 비율(0~100, 반올림). 던진 공이 없으면 0.
        /// </summary>
        public static int AccuracyPercent(LabPlayerState player)
        {
            if (player == null || player.throwTotal <= 0 || player.hitTotal <= 0) return 0;
            double rate = Math.Min(1d, (double)player.hitTotal / player.throwTotal);
            return (int)Math.Round(rate * 100d, MidpointRounding.AwayFromZero);
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
        /// 명중왕 "60%" · 구슬 패스왕·북 패스왕 "5회" · 북 독점 "12초 · 40%" · 막타는 빈칸.
        /// </summary>
        public static string Record(LabResultAward award, double playedSeconds)
        {
            if (award.Winners == null || award.Winners.Length == 0) return "";
            switch (award.Title)
            {
                case LabResultTitle.MostDamage:
                    return award.Value + " (" + string.Join("·", award.Winners.Select(p => p.hitTotal)) + "회 타격)";
                case LabResultTitle.MostAccurate: return award.Value + "%";
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
