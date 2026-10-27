namespace C6Lab
{
    /// <summary>
    /// 야광귀 피버타임 (#343)을 모든 기기가 같은 시각에 알아보는 곳. 스냅샷 모양은 바꾸지 않는다: 게임의 야광귀 스테이지에서
    /// 전투가 흐른 시간(playedSeconds, 일시정지 제외)이 공유 설정의 시간표 방해 종료(기본 100초)를 지나면 피버다.
    /// Host의 스태미나 2배(LabModel.YaguangFeverRecoveryMultiplier)도 같은 시각에 시작한다.
    /// </summary>
    public sealed partial class LabNetwork
    {
        /// <summary>
        /// 이 스냅샷에서 야광귀 피버가 시작된 지 몇 초가 지났는지. 피버가 아니면(다른 스테이지·2:2·Lab 단판·전투 아님) -1.
        /// </summary>
        public double YaguangFeverElapsed(LabSnapshot snapshot)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || !gameFlowEnabled
                || gameStageYokai != LabStageYokai.Yaguang || snapshot.teamTerritory || config == null) return -1d;
            double end = config.YaguangTimelineSabotageEndSeconds;
            if (end <= 0d || snapshot.playedSeconds < end) return -1d;
            return snapshot.playedSeconds - end;
        }

        /// <summary>지금 야광귀 피버타임인지 (#343)</summary>
        public bool YaguangFeverOn => YaguangFeverElapsed(latest) >= 0d;
    }
}
