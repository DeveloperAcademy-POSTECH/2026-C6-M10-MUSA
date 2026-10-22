using System;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 2:2 청홍 땅따먹기 (#281). 4명(또는 DEV 솔로)이고 설정이 켜져 있을 때만 쓴다. 요괴 HP 대신 박 전체 칸 대비
    /// 팀 색 칸 비율로 승패를 정한다: 한 팀이 모든 칸을 차지하면 바로, 아니면 제한 시간이 끝날 때 더 많은 팀이 이기고
    /// 같으면 무승부다. 두 경우 모두 판은 Victory로 끝나고 teamWinner가 결과를 담는다.
    /// </summary>
    public sealed partial class LabModel
    {
        private const uint TeamDrumSalt = 0x7EA3D2A1u;
        private bool teamMode;
        private LabTeam teamWinner;
        private readonly int[] feverHits = new int[3];
        private readonly double[] feverStartedAt = new double[3], feverUntil = new double[3];
        private uint teamCountsRevision;
        private bool teamCountsValid;
        private int blueCells, redCells;

        /// <summary>이번 판이 2:2 땅따먹기인지.</summary>
        public bool TeamTerritory => teamMode;

        /// <summary>설정이 켜져 있어도 4명 판과 DEV 솔로에서만 팀전이 된다. 다른 인원은 기존 규칙 그대로다.</summary>
        public static bool TeamModeFor(LabConfig config, int seats) =>
            config != null && config.TeamTerritoryEnabled && (seats == LabTeams.Players || seats == 1);

        private void ResetTeam()
        {
            teamWinner = LabTeam.None;
            Array.Clear(feverHits, 0, feverHits.Length);
            for (int i = 0; i < feverUntil.Length; i++) feverStartedAt[i] = feverUntil[i] = -1d;
            teamCountsValid = false;
            blueCells = redCells = 0;
        }

        /// <summary>솔로는 혼자 청팀이다.</summary>
        private LabTeam TeamOf(ulong player) =>
            !teamMode || !players.TryGetValue(player, out var state) ? LabTeam.None
            : activeSeatCount == 1 ? LabTeam.Blue : LabTeams.TeamOfSeat(state.seat);

        private bool FeverActive(LabTeam team, double now) => team != LabTeam.None && feverUntil[(int)team] > now;

        /// <summary>
        /// 결합 구슬의 색. 팀전에서는 언제나 우리 팀 색이고, DEV 솔로는 덮어쓰기를 확인할 수 있게 청·홍을 번갈아 낸다.
        /// </summary>
        private LabOrbElement CombinedElementFor(ulong sender)
        {
            if (!teamMode) return ElementFor(seed, combinedCount++);
            if (activeSeatCount == 1) return combinedCount++ % 2 == 0 ? LabOrbElement.Water : LabOrbElement.Fire;
            combinedCount++;
            return LabTeams.ElementOf(TeamOf(sender));
        }

        /// <summary>팀마다 북 하나를 팀원 가운데 무작위 한 명의 판 가운데에 둔다. 솔로는 하나다.</summary>
        private void PlaceTeamDrums(double now)
        {
            int[] chosen = activeSeatCount == 1 ? new[] { 0 }
                : new[] { 0, 2 }.Select(first => first + (int)(DrawFor(seed ^ TeamDrumSalt, (ulong)first, 0u) % 2UL)).ToArray();
            foreach (int seat in chosen)
            {
                var drum = new LabOrbState
                {
                    id = NewOrbId(), owner = seats[seat], kind = LabOrbKind.Drum,
                    x = DrumStartX[0], y = .5f, createdAt = now, revision = ++revision
                };
                drums.Add(drum.id, drum);
            }
        }

        /// <summary>
        /// 맞힌 얼룩 모양만 상대 색을 덮어쓰고 빈 칸은 평소처럼 칠한다. 피버 게이지를 채우고, 한 팀이 모든 칸을
        /// 차지했으면 바로 끝낸다.
        /// </summary>
        private void ConfirmTeamHit(LabPlayerState attacker, LabOrbElement element, Vector3 paintDirection, float share,
            double now)
        {
            if (LabTeams.TeamOfElement(element) != LabTeam.None
                && paintSurface.RegisterTerritoryHit(paintDirection, PaintColorFor(element), attacker.id,
                    attacker.seat + 1, share, out _, out int stolen))
                attacker.stolenCells = (int)Math.Min(int.MaxValue, (long)attacker.stolenCells + stolen);
            AddFeverHit(TeamOf(attacker.id), now);
            SettleTeamSweep();
        }

        private void AddFeverHit(LabTeam team, double now)
        {
            if (team == LabTeam.None || rules.TeamFeverHits <= 0 || FeverActive(team, now)) return;
            int index = (int)team;
            if (++feverHits[index] < rules.TeamFeverHits) return;
            feverHits[index] = 0;
            feverStartedAt[index] = now;
            feverUntil[index] = now + rules.TeamFeverSeconds;
            LabOrbElement element = LabTeams.ElementOf(team);
            // 피버가 시작되면 팀 판의 음·양과 결합 구슬이 모두 팀 색 결합 구슬이 된다. 아이템·북·던진 구슬은 그대로다.
            foreach (var orb in orbs.Values)
            {
                if (orb.inFlight || orb.jigwiSupply || TeamOf(orb.owner) != team) continue;
                if (!orb.kind.IsRaw() && !(orb.kind == LabOrbKind.Combined && orb.element != element)) continue;
                orb.kind = LabOrbKind.Combined;
                orb.element = element;
                orb.fever = true;
                orb.revision = ++revision;
            }
        }

        /// <summary>피버 중 생성은 음·양 대신 우리 팀 색 결합 구슬을 낸다. 아이템은 그대로 나온다.</summary>
        private void ApplyFeverGeneration(LabOrbState orb, ulong sender, double now)
        {
            LabTeam team = TeamOf(sender);
            if (!orb.kind.IsRaw() || !FeverActive(team, now)) return;
            orb.kind = LabOrbKind.Combined;
            orb.element = LabTeams.ElementOf(team);
            orb.fever = true;
        }

        private void RefreshTeamCounts()
        {
            if (teamCountsValid && teamCountsRevision == paintSurface.Revision) return;
            teamCountsValid = true;
            teamCountsRevision = paintSurface.Revision;
            blueCells = paintSurface.CountColor(LabPaintSurface.Opaque(PaintColorFor(LabOrbElement.Water)));
            redCells = paintSurface.CountColor(LabPaintSurface.Opaque(PaintColorFor(LabOrbElement.Fire)));
            int[] painters = paintSurface.CountPainterSlots();
            foreach (var player in players.Values)
                player.paintedCells = player.seat + 1 < painters.Length ? painters[player.seat + 1] : 0;
        }

        /// <summary>한 팀이 박의 모든 칸을 차지하면 그 팀이 바로 이긴다.</summary>
        private void SettleTeamSweep()
        {
            if (!teamMode || phase != LabPhase.Playing) return;
            RefreshTeamCounts();
            int cells = paintSurface.CellCount;
            if (blueCells == cells) EndTeamBattle(LabTeam.Blue);
            else if (redCells == cells) EndTeamBattle(LabTeam.Red);
        }

        /// <summary>제한 시간이 끝나면 박 전체 칸 대비 팀 칸이 많은 쪽이 이긴다. 같으면 무승부다.</summary>
        private void EndTeamBattleAtDeadline()
        {
            RefreshTeamCounts();
            EndTeamBattle(blueCells > redCells ? LabTeam.Blue : redCells > blueCells ? LabTeam.Red : LabTeam.None);
        }

        private void EndTeamBattle(LabTeam winner)
        {
            teamWinner = winner;
            phase = LabPhase.Victory;
        }

        private void FillTeamSnapshot(LabSnapshot snapshot)
        {
            if (!teamMode) return;
            RefreshTeamCounts();
            snapshot.teamTerritory = true;
            snapshot.blueCells = blueCells;
            snapshot.redCells = redCells;
            snapshot.teamWinner = phase == LabPhase.Victory ? teamWinner : LabTeam.None;
            snapshot.blueFeverHits = feverHits[(int)LabTeam.Blue];
            snapshot.redFeverHits = feverHits[(int)LabTeam.Red];
            bool blueOn = FeverActive(LabTeam.Blue, snapshot.serverTime), redOn = FeverActive(LabTeam.Red, snapshot.serverTime);
            snapshot.blueFeverStartedAt = blueOn ? feverStartedAt[(int)LabTeam.Blue] : -1d;
            snapshot.blueFeverUntil = blueOn ? feverUntil[(int)LabTeam.Blue] : -1d;
            snapshot.redFeverStartedAt = redOn ? feverStartedAt[(int)LabTeam.Red] : -1d;
            snapshot.redFeverUntil = redOn ? feverUntil[(int)LabTeam.Red] : -1d;
            // 결과 기록(칠한 넓이)은 위 RefreshTeamCounts가 채운 뒤에 복사한다.
            snapshot.players = players.Values.OrderBy(player => player.seat).Select(player => player.Copy()).ToArray();
        }
    }
}
