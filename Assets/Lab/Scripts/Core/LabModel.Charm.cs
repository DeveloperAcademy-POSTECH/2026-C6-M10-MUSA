using System;
using System.Collections.Generic;
using System.Linq;

namespace C6Lab
{
    /// <summary>
    /// 매혹 대상 (#251, #305, #664). 보통은 한 명이고, 다섯 명 이상인 방은 두 명(Rules.CharmTargetsAtFive)이 동시에 걸린다.
    /// 두 명은 서로 옆자리가 아니어서, 매혹을 풀어 줄 양옆 사람이 걸린 사람이 되는 일이 없다. 다섯 명이면 두 사람 사이에 낀
    /// 한 사람이 두 사람 모두의 옆이 되어, 두 사람의 말풍선을 각각 정해 둔 횟수만큼 눌러야 한다. 예고·시작은 함께이고 끝은 따로다:
    /// 양옆이 다 누른 사람은 먼저 깨어나고, 아무도 못 깨운 사람은 정해 둔 시간에 풀린다.
    /// </summary>
    public sealed partial class LabModel
    {
        /// <summary>How many are charmed at once in a room of this many players: one, or Rules.CharmTargetsAtFive in five or more.</summary>
        public int CharmTargetCount(int roomPlayers) =>
            roomPlayers >= 5 && rules != null ? Math.Max(1, Math.Min(2, rules.CharmTargetsAtFive)) : 1;

        /// <summary>
        /// The players to charm: the drawn one, and in a room of five another drawn from the rest who is not beside them.
        /// </summary>
        private List<ulong> CharmTargetsFrom(List<ulong> candidates, ulong first, uint index, uint targetSalt)
        {
            var chosen = new List<ulong> { first };
            if (CharmTargetCount(activeSeatCount) < 2 || !players.TryGetValue(first, out LabPlayerState firstState)) return chosen;
            List<ulong> apart = candidates.Where(id => id != first && players.TryGetValue(id, out LabPlayerState state)
                && !SeatsBeside(state.seat, firstState.seat)).ToList();
            if (apart.Count > 0)
                chosen.Add(apart[(int)(DrawFor(seed ^ targetSalt ^ SecondCharmSalt, SabotageDrawKey, index) % (ulong)apart.Count)]);
            return chosen;
        }

        private const uint SecondCharmSalt = 0x5ec0c4a7u;

        private bool SeatsBeside(int a, int b) =>
            activeSeatCount > 1 && a != b && ((a + 1) % activeSeatCount == b || (b + 1) % activeSeatCount == a);

        /// <summary>The same one effect/notification/statistics path as DispatchGimmick, for one charm on one or two players.</summary>
        private void DispatchCharm(List<ulong> targets, double at)
        {
            StartCharm(targets, at);
            foreach (ulong target in targets) sabotages.Add(new LabSabotage(LabSabotageKind.Charm, target, at));
            gimmickStats.Add(LabSabotageKind.Charm);
        }

        /// <summary>
        /// 매혹 (#251): 이 사람들은 정해 둔 시간 동안 헤롱헤롱하고, #664부터는 판 전체가 멈춘다. #305: 예고 시간만큼 먼저 모두에게
        /// 알리고 그 뒤에 함께 걸린다 (예고 0이면 바로).
        /// </summary>
        private void StartCharm(IReadOnlyList<ulong> targets, double at)
        {
            charmTargets.Clear();
            charmEnds.Clear();
            charmWarnedAt = at;
            charmStartedAt = at + rules.CharmWarningSeconds;
            charmUntil = charmStartedAt + rules.CharmSeconds;
            foreach (ulong target in targets)
                if (!charmEnds.ContainsKey(target))
                {
                    charmTargets.Add(target);
                    charmEnds[target] = charmUntil;
                }
            charmTaps = 0;
            charmHelperTaps.Clear();
        }

        /// <summary>Every charm ends now (the fever start): nobody is charmed any more.</summary>
        private void EndAllCharms(double at)
        {
            foreach (ulong target in charmTargets) charmEnds[target] = Math.Min(charmEnds[target], at);
            charmUntil = at;
        }

        /// <summary>The players beside this charmed one, in seat order: two, or one in a two-player room, or none solo (#305).</summary>
        private ulong[] CharmHelpersOf(ulong target)
        {
            if (!players.TryGetValue(target, out var charmed) || activeSeatCount < 2) return Array.Empty<ulong>();
            int seat = charmed.seat;
            ulong before = seats[(seat + activeSeatCount - 1) % activeSeatCount];
            ulong after = seats[(seat + 1) % activeSeatCount];
            return before == after ? new[] { before } : new[] { before, after };
        }

        /// <summary>One row per (charmed player still charmed, player beside them) for the snapshot, in charm then seat order.</summary>
        private T[] CharmHelperRows<T>(double now, Func<(ulong target, ulong helper), T> pick)
        {
            if (!CharmOn(now)) return Array.Empty<T>();
            var rows = new List<T>();
            foreach (ulong target in charmTargets)
            {
                if (!Charmed(target, now)) continue;
                foreach (ulong helper in CharmHelpersOf(target)) rows.Add(pick((target, helper)));
            }
            return rows.ToArray();
        }

        /// <summary>
        /// 매혹 (#251, #305, #664): 걸린 사람(target)의 양옆 사람이 그 사람의 하트 말풍선을 누른 한 번. 양옆 사람이 각자 정해 둔
        /// 횟수를 채워야 그 사람이 풀린다 (둘이 하는 판은 상대 한 사람). 두 사람 사이에 낀 사람은 두 사람 몫을 따로 센다. 먼저 다
        /// 채운 사람의 더 누르기는 세지 않는다. 양옆이 아닌 사람, 걸린 사람 자신, 예고 중이거나 매혹이 없을 때, 깨우기가 꺼져
        /// 있을 때는 받지 않는다. 이 탭으로 깨어났으면 woken에 그 사람을, 아니면 null을 돌려준다 (Host의 플레이어 ID도 0이므로
        /// 구분한다).
        /// </summary>
        public bool TryCharmTap(ulong sender, ulong target, double now, out ulong? woken)
        {
            woken = null;
            if (!TryAdvance(now, out _) || !CharmOn(now) || rules.CharmRescueTaps <= 0 || !Charmed(target, now)
                || Charmed(sender, now) || Array.IndexOf(CharmHelpersOf(target), sender) < 0)
                return false;
            var key = (target, sender);
            charmHelperTaps.TryGetValue(key, out int taps);
            if (taps >= rules.CharmRescueTaps) return true;
            charmHelperTaps[key] = taps + 1;
            charmTaps++;
            foreach (ulong helper in CharmHelpersOf(target))
                if (!charmHelperTaps.TryGetValue((target, helper), out int done) || done < rules.CharmRescueTaps) return true;
            charmEnds[target] = now;
            charmUntil = charmTargets.Max(charmed => charmEnds[charmed]);
            woken = target;
            return true;
        }

        /// <summary>
        /// A tap that names nobody: it goes to the first charmed player beside the sender who still waits for their taps
        /// (or, when they have finished every one, to the first beside them).
        /// </summary>
        public bool TryCharmTap(ulong sender, double now, out ulong? woken)
        {
            woken = null;
            if (!TryAdvance(now, out _)) return false;
            ulong? beside = null;
            foreach (ulong target in charmTargets)
            {
                if (!Charmed(target, now) || Array.IndexOf(CharmHelpersOf(target), sender) < 0) continue;
                if (!charmHelperTaps.TryGetValue((target, sender), out int taps) || taps < rules.CharmRescueTaps)
                    return TryCharmTap(sender, target, now, out woken);
                beside ??= target;
            }
            return beside.HasValue && TryCharmTap(sender, beside.Value, now, out woken);
        }
    }
}
