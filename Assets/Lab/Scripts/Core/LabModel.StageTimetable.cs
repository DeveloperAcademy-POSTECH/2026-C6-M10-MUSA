using System;
using System.Linq;

namespace C6Lab
{
    /// <summary>
    /// #737 2R 야광귀 / 3R 지귀 시간표 다듬기. 시간표 자체(LabModel.YaguangSchedule, LabModel.JigwiAB)는 그대로 두고,
    /// 그 위에 규칙을 얹는다. 1R 구미호와 2:2에는 쓰지 않는다.
    /// - 초반 겹침 막기: 초반(전투 시작 뒤 stageEarlyPhaseSeconds 전)에 고유 기믹(예고 포함)이 진행 중이면, 공통 기믹(박 폭탄 /
    ///   넘기기 막기)은 그것이 화면에서 다 끝나고 stageEarlyCommonGapSeconds 뒤에 온다. 중반부터는 겹쳐도 그대로 온다.
    /// </summary>
    public sealed partial class LabModel
    {
        private double timetableStartAt, timetableEarlyUntil = double.NegativeInfinity, timetableEarlyGap;
        // 끝을 아직 몰라(미션/콤보 진행 중) 붙잡아 둔 공통 기믹의 원래 시각. 없으면 NaN.
        private double heldEarlyCommonAt = double.NaN;

        /// <summary>이 판에 #737 규칙을 쓰는지 (2R 야광귀 시간표 또는 3R 지귀 시간표).</summary>
        private bool TimetableRulesOn => !teamMode && (rules.YaguangTimeline || jigwiABEnabled);

        private void ResetStageTimetable(LabConfig config, double now)
        {
            timetableStartAt = now;
            heldEarlyCommonAt = double.NaN;
            float early = config != null ? config.StageEarlyPhaseSeconds : 0f;
            timetableEarlyUntil = early > 0f ? now + early : double.NegativeInfinity;
            timetableEarlyGap = config != null ? config.StageEarlyCommonGapSeconds : 1d;
        }

        /// <summary>
        /// #737: 이 시각(at)에 오려던 공통 기믹을 미뤄야 하면 nextCommonGimmickAt을 미룬 시각(끝을 모르면 +∞, 붙잡아 둠)으로
        /// 바꾸고 true. 미루지 않으면 false.
        /// </summary>
        private bool HoldEarlyCommon(double at)
        {
            if (!TimetableRulesOn || !(at < timetableEarlyUntil)) return false;
            double busy = UniqueBusyUntil(at);
            if (!(busy > at)) return false;
            if (double.IsPositiveInfinity(busy))
            {
                heldEarlyCommonAt = at;
                nextCommonGimmickAt = double.PositiveInfinity;
            }
            else nextCommonGimmickAt = busy + timetableEarlyGap;
            return true;
        }

        /// <summary>#737: 붙잡아 둔 공통 기믹은 고유 기믹의 끝이 정해지면 그 끝 + 간격으로 다시 잡는다 (방해 끝 전까지만).</summary>
        private void ReleaseHeldEarlyCommon(double until)
        {
            if (double.IsNaN(heldEarlyCommonAt)) return;
            // 3R은 미션마다 끝난 시각이 남아 있어 붙잡은 때의 미션이 끝난 시각을 정확히 안다. 2R 콤보는 지금 상태만 있어서
            // 끝났다고 보인 이 시각을 끝으로 본다.
            double busy = jigwiABEnabled ? UniqueBusyUntil(heldEarlyCommonAt) : UniqueBusyUntil(until);
            if (double.IsPositiveInfinity(busy)) return;
            double at = Math.Max(heldEarlyCommonAt, busy + timetableEarlyGap);
            heldEarlyCommonAt = double.NaN;
            if (at < TimetableCutoffAt) nextCommonGimmickAt = Math.Min(nextCommonGimmickAt, at);
        }

        /// <summary>방해가 끝나는 시각 (2R 야광귀 시간표 끝 / 3R 지귀 피버 시작).</summary>
        private double TimetableCutoffAt => jigwiABEnabled ? jigwiStageCutoffAt
            : rules.YaguangTimeline ? yaguangTimelineEndAt : double.PositiveInfinity;

        /// <summary>
        /// #737: at 시각에 고유 기믹(예고 포함)이 화면에 걸려 있으면 그것이 다 끝나는 시각, 끝을 아직 모르면 +∞, 없으면 at.
        /// 3R은 미션의 보상/벌칙 연출(물 폭포, 판 구슬 탐)까지, 2R은 섬광의 하얀 화면과 회복까지 본다.
        /// </summary>
        private double UniqueBusyUntil(double at)
        {
            double until = at;
            if (jigwiABEnabled)
            {
                foreach (var item in jigwiABRounds)
                {
                    if (at < item.warningAt || item.resolution == LabJigwiABResolution.Cancelled
                        || item.resolution == LabJigwiABResolution.Skipped) continue;
                    if (item.resolution == LabJigwiABResolution.None) return double.PositiveInfinity;
                    until = Math.Max(until, JigwiMissionDoneAt(item));
                }
                return until;
            }
            if (rules.YaguangTimeline)
            {
                if (combo.Active || comboWarned || flashWarned) return double.PositiveInfinity;
                if (flashCount > 0) until = Math.Max(until, lastFlashAt + rules.FlashGlareSeconds + rules.FlashRecoverySeconds);
            }
            return until;
        }
    }
}
