using System;
using System.Collections.Generic;
using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Local presentation of the authoritative, successful stage snapshots.</summary>
    public sealed class SealRunSummary : MonoBehaviour
    {
        public Text[] stageTimes;
        public Text[] contributions;
        public Text totalTime;
        public Text appraisal;
        public static readonly string[] Appraisals = {
            "흔들림 없는 솜씨가\n참으로 훌륭했소.",
            "요괴를 다루는 손길이\n예사롭지 않았소.",
            "매서운 기세에도\n물러서지 않았구려.",
            "재빠른 손놀림으로\n요괴를 잠재웠소.",
            "끝까지 포기하지 않는\n그 의지가 빛났소.",
            "날카로운 눈썰미가\n봉인의 힘이 되었소.",
            "위기의 순간에도\n평정심을 잃지 않았소.",
            "그대의 뛰어난 솜씨에\n요괴도 꼼짝 못 했소.",
            "한 치의 망설임 없는\n인상적인 봉인이었소.",
            "그대의 침착한 손놀림이\n매우 돋보였소."
        };
        private int appraisalIndex = -1;

        public void Bind(IReadOnlyList<LabResultStage> stages, ulong localPlayerId)
        {
            double total = 0;
            bool complete = stages != null && stages.Count == 3;
            for (int i = 0; i < 3; i++)
            {
                LabResultStage stage = stages != null && i < stages.Count ? stages[i] : null;
                if (stageTimes != null && i < stageTimes.Length && stageTimes[i] != null)
                    stageTimes[i].text = stage == null ? "—" : FormatTime(stage.PlayedSeconds);
                if (contributions != null && i < contributions.Length && contributions[i] != null)
                    contributions[i].text = Contribution(stage, localPlayerId);
                if (stage != null) total += ValidSeconds(stage.PlayedSeconds);
                else complete = false;
            }
            if (totalTime != null) totalTime.text = complete ? FormatTime(total) : "—";
            if (appraisalIndex < 0) appraisalIndex = UnityEngine.Random.Range(0, Appraisals.Length);
            if (appraisal != null) appraisal.text = Appraisals[appraisalIndex];
        }

        private static double ValidSeconds(double seconds) => double.IsNaN(seconds) || double.IsInfinity(seconds) ? 0 : Math.Max(0, seconds);
        public static string FormatTime(double seconds)
        {
            long whole = (long)Math.Min(long.MaxValue / 2d, Math.Floor(ValidSeconds(seconds)));
            return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }
        public static string Contribution(LabResultStage stage, ulong localPlayerId)
        {
            if (stage == null || stage.Players == null) return "—";
            double total = 0, mine = 0;
            bool found = false;
            foreach (var player in stage.Players)
            {
                if (player == null) continue;
                // Judgment cells are equal-area. Count newly painted cells, including funded growth,
                // rather than hit score or the ownership still visible after drying/overpainting.
                long painted = Math.Max(0L, player.paintedCellsTotal);
                total += painted;
                if (player.id == localPlayerId) { mine += painted; found = true; }
            }
            if (!found) return "—";
            return (total == 0 ? 0 : (int)Math.Round(100d * mine / total, MidpointRounding.AwayFromZero)).ToString() + "%";
        }
    }
}
