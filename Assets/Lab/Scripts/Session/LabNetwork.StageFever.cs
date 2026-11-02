namespace C6Lab
{
    /// <summary>
    /// 세 스테이지 피버(#441)를 모든 기기가 같은 시각에 알아보는 곳. 스냅샷 모양과 접속 버전은 바꾸지 않고, 스테이지마다
    /// 이미 스냅샷에 실려 오는 값만 읽는다. Host 규칙(LabModel.StageFeverStartsAt)과 같은 시각이다.
    /// - 구미호 (#334): feverTimeAt (Host 시각)
    /// - 야광귀 (#343): 전투가 흐른 시간(playedSeconds)이 시간표 방해 종료를 지난 때 (YaguangFeverElapsed)
    /// - 지귀 (#332): jigwiAB(또는 jigwiStage)의 fever와 cutoffAt (Host 시각)
    /// 피버 화면(LabYaguangFeverHud, LabYaguangFeverStaminaGlow, LabYaguangFeverDrumCue)이 이것을 본다.
    /// 피버 곡(LabBattleBgm)은 #441에서 바꾸지 않아 지금처럼 야광귀/구미호 값을 따로 본다.
    /// </summary>
    public sealed partial class LabNetwork
    {
        /// <summary>
        /// 이 스냅샷에서 스테이지 피버가 시작된 지 몇 초가 지났는지. 피버가 아니면(2:2, Lab 단판, 전투 아님, 아직 전) -1.
        /// </summary>
        public double StageFeverElapsed(LabSnapshot snapshot)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || !gameFlowEnabled || snapshot.teamTerritory) return -1d;
            return StageFeverElapsed(snapshot, gameStageYokai, YaguangFeverElapsed(snapshot));
        }

        /// <summary>
        /// 스테이지별 피버 경과 초 (검사에서 스테이지를 정해 부를 수 있게 공개한다). yaguangElapsed는 야광귀 계산 결과.
        /// </summary>
        public static double StageFeverElapsed(LabSnapshot snapshot, LabStageYokai stage, double yaguangElapsed)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.teamTerritory) return -1d;
            double now = snapshot.serverTime;
            switch (stage)
            {
                case LabStageYokai.Yaguang:
                    return yaguangElapsed;
                case LabStageYokai.Gumiho:
                    return snapshot.FeverTimeOn(now) ? System.Math.Max(0d, now - snapshot.feverTimeAt) : -1d;
                case LabStageYokai.Jigwi:
                    bool ab = snapshot.jigwiAB != null && snapshot.jigwiAB.enabled;
                    bool fever = ab ? snapshot.jigwiAB.fever : snapshot.jigwiStage != null && snapshot.jigwiStage.enabled
                        && snapshot.jigwiStage.fever;
                    double cutoff = ab ? snapshot.jigwiAB.cutoffAt : snapshot.jigwiStage != null ? snapshot.jigwiStage.cutoffAt : -1d;
                    return fever && cutoff >= 0d ? System.Math.Max(0d, now - cutoff) : -1d;
                default:
                    return -1d;
            }
        }

        /// <summary>지금 스테이지 피버인지 (#441)</summary>
        public bool StageFeverOn => StageFeverElapsed(latest) >= 0d;
    }
}
