using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #551 지귀 B 3-2-1: 지목된 사람이 자기 특수 물구슬을 손가락으로 잡고 있는지 Host에 알리고, "지금!" 전에는 그 구슬이
    /// 던져지지 않게 막는다. 모두 잡고 있으면 Host가 3-2-1을 시작하고(스냅샷 countdownAt), 잡은 손을 놓으면 취소된다.
    /// 판정은 모두 Host(`LabModel.TrySetJigwiBReady`)가 한다.
    /// </summary>
    public sealed partial class LabNetwork
    {
        /// <summary>#551 잡고 있음/놓음 요청 패킷 종류. firstId에 B 회차 id, accepted에 잡고 있는지.</summary>
        public const string JigwiBReadyPacket = "jigwiBReady";
        private string jigwiBReadyRound;
        private bool jigwiBReadySent;
        private float jigwiBReadySentAt = float.NegativeInfinity;
        /// <summary>#551 나무 물통: 당기기 요청 패킷 종류. firstId에 B 회차 id.</summary>
        public const string JigwiBPullPacket = "jigwiBPull";
        private string jigwiBucketPulledRound; // the bucket round this device already poured
        private float jigwiBucketPullSentAt = float.NegativeInfinity;

        /// <summary>#551 나무 물통: 지금 내가 참여 중인 물통 B (아니면 null). 화면이 손잡이/밧줄을 그릴 때 쓴다.</summary>
        public LabJigwiABRound MyJigwiBucket
        {
            get { LabJigwiABRound round = MyJigwiB(JigwiABPresentationTime); return round != null && round.bucket ? round : null; }
        }

        /// <summary>
        /// #551 나무 물통 (개별): 폰을 좌우로 3번 출렁여 내 양동이를 쏟았다. B가 진행 중일 때 한 번만 Host에 보낸다. 보냈으면 true.
        /// </summary>
        public bool RequestJigwiBucketPull()
        {
            if (!IsConnected || !CanProcessBattleActions || latest == null || latest.paused) return false;
            LabJigwiABRound round = MyJigwiBucket;
            if (round == null || jigwiBucketPulledRound == round.id) return false;
            jigwiBucketPulledRound = round.id;
            SendJigwiBucketPull(round.id);
            return true;
        }
        /// <summary>#551 나무 물통: 이 기기가 이 회차 양동이를 이미 쏟았는지(Host 스냅샷이 오기 전에도).</summary>
        public bool JigwiBucketPulledThisCount(LabJigwiABRound round) => round != null && jigwiBucketPulledRound == round.id;
        private void SendJigwiBucketPull(string roundId)
        {
            jigwiBucketPullSentAt = Time.unscaledTime;
            Request(new LabNetworkPacket { type = JigwiBPullPacket, requestId = ++requestSequence, firstId = roundId });
        }

        /// <summary>#696 연기 보상: 연기를 다 걷어 냈다고 Host에 알리는 패킷 종류. firstId에 연기 회차 id.</summary>
        public const string JigwiSmokeClearPacket = "jigwiSmokeClear";
        private string jigwiSmokeClearSentRound;

        /// <summary>
        /// #696: 이 기기 화면에서 지귀 연기를 닦기 시작했다(파편이 연기 밑에 깔려 닦는 동안 드러난다).
        /// 연기 회차마다 한 번 Host에 보낸다(파편 판정은 Host). 보냈으면 true.
        /// </summary>
        public bool RequestJigwiSmokeClear(string roundId)
        {
            if (!IsConnected || !CanProcessBattleActions || latest == null || latest.paused || string.IsNullOrEmpty(roundId)
                || jigwiSmokeClearSentRound == roundId) return false;
            jigwiSmokeClearSentRound = roundId;
            Request(new LabNetworkPacket { type = JigwiSmokeClearPacket, requestId = ++requestSequence, firstId = roundId });
            return true;
        }

        /// <summary>#551: 이 기기 사람이 지목된, 지금 진행 중인 B (아니면 null).</summary>
        private LabJigwiABRound MyJigwiB(double now)
        {
            if (latest == null || latest.phase != LabPhase.Playing || latest.jigwiAB?.enabled != true) return null;
            LabJigwiABRound round = latest.jigwiAB.Active(now);
            if (round == null || round.kind != LabJigwiABKind.GroupFire || !round.Active(now)
                || round.syncWindowSeconds <= 0f && !round.bucket) return null; // the countdown rule runs only with the new window
            foreach (var seat in round.players ?? System.Array.Empty<LabJigwiABPlayerFire>())
                if (seat != null && seat.playerId == LocalPlayerId) return round;
            return null;
        }

        /// <summary>#551: 이 구슬이 이번 B에서 내가 받은 특수 물구슬인지.</summary>
        private bool IsMyJigwiBWater(LabJigwiABRound round, string orbId)
        {
            if (round == null || string.IsNullOrEmpty(orbId) || latest?.orbs == null) return false;
            foreach (var orb in latest.orbs)
                if (orb != null && orb.id == orbId) return orb.jigwiABSupplyEventId == round.id && orb.originOwner == LocalPlayerId;
            return false;
        }

        /// <summary>#551 board gate: before "지금!" my special Water only slides (no throw); everything else throws as usual.</summary>
        private bool JigwiBThrowAllowed(string orbId)
        {
            double now = JigwiABPresentationTime;
            LabJigwiABRound round = MyJigwiB(now);
            if (round == null || !IsMyJigwiBWater(round, orbId)) return true;
            return round.countdownAt >= 0d && now >= round.GoAt;
        }

        /// <summary>
        /// #551: 매 프레임, 내 특수 물구슬을 잡고 있는지가 바뀌면 Host에 한 번 알린다. 스냅샷과 어긋나 있으면(유실)
        /// 0.5초마다 다시 보낸다. "지금!"이 지나면 더 보내지 않는다.
        /// </summary>
        private void UpdateJigwiBReady()
        {
            if (board == null || !IsConnected || !CanProcessBattleActions || latest == null || latest.paused) return;
            double now = JigwiABPresentationTime;
            LabJigwiABRound round = MyJigwiB(now);
            if (round != null && round.bucket) { ResendJigwiBucketPull(round, now); return; } // the bucket has no holding
            if (round == null || round.countdownAt >= 0d && now >= round.GoAt) { jigwiBReadyRound = null; return; }
            bool holding = IsMyJigwiBWater(round, board.HeldOrbId);
            bool hostThinks = false;
            foreach (var seat in round.players)
                if (seat != null && seat.playerId == LocalPlayerId) hostThinks = seat.readyAt >= 0d;
            bool changed = jigwiBReadyRound != round.id || jigwiBReadySent != holding;
            bool resend = holding != hostThinks && Time.unscaledTime - jigwiBReadySentAt >= .5f;
            if (!changed && !resend) return;
            jigwiBReadyRound = round.id; jigwiBReadySent = holding; jigwiBReadySentAt = Time.unscaledTime;
            if (holding == hostThinks) return; // already agreed (e.g. after a reconnect)
            Request(new LabNetworkPacket { type = JigwiBReadyPacket, requestId = ++requestSequence, firstId = round.id, accepted = holding });
        }

        /// <summary>#551: a pour the Host has not counted yet is sent again every 0.2 s while B runs.</summary>
        private void ResendJigwiBucketPull(LabJigwiABRound round, double now)
        {
            if (jigwiBucketPulledRound != round.id || Time.unscaledTime - jigwiBucketPullSentAt < .2f) return;
            foreach (var seat in round.players)
                if (seat != null && seat.playerId == LocalPlayerId && seat.launchedAt < 0d) { SendJigwiBucketPull(round.id); return; }
        }
    }
}
