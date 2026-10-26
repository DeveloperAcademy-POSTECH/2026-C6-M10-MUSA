using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 야광귀 섬광 (#268)의 화면 계산. Host 스냅샷의 섬광 시각·대상과 DEV 시간 값으로, 이 기기 플레이어가 지금
    /// 얼마나 눈부신지(0~1)와 예고가 얼마나 차올랐는지(0~1)를 정한다. 판정·물리에는 쓰지 않는다.
    /// </summary>
    public static class LabYaguangFlash
    {
        /// <summary>
        /// 판 구슬·아이템이 하얗게 보이는 정도. 섬광을 맞은 사람만 번쩍인 순간부터 눈부심 동안 1이고,
        /// 회복 동안 부드럽게 0으로 돌아온다. 전투 중이 아니거나 섬광이 없었으면 0.
        /// 눈부신 동안 요괴 얼굴을 맞혀 풀었으면(#301) 그 시각에 눈부심이 끝나고 거기서부터 회복한다.
        /// </summary>
        public static float GlareAmount(LabSnapshot snapshot, double now, ulong player, float glareSeconds,
            float recoverySeconds)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || !snapshot.LastFlashHit(player)
                || snapshot.flashLastAt < 0d) return 0f;
            double since = now - snapshot.flashLastAt;
            if (since < 0d) return 0f;
            double glareEnds = snapshot.flashLastAt + glareSeconds;
            double released = snapshot.FlashReleasedAt(player);
            if (released >= 0d && released < glareEnds) glareEnds = released;
            if (now < glareEnds) return 1f;
            if (recoverySeconds <= 0f) return 0f;
            float u = (float)((now - glareEnds) / recoverySeconds);
            if (u >= 1f) return 0f;
            return 1f - u * u * (3f - 2f * u);
        }

        /// <summary>
        /// 예고가 차오른 정도. 이 사람이 다음 섬광 대상이고 예고 중이면 0에서 1로 오르고, 아니면 0.
        /// </summary>
        public static float WarningAmount(LabSnapshot snapshot, double now, ulong player)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || !snapshot.FlashWarningOn(now)
                || !snapshot.FlashTargets(player)) return 0f;
            double length = snapshot.flashAt - snapshot.flashWarningAt;
            return length > 0d ? Mathf.Clamp01((float)((now - snapshot.flashWarningAt) / length)) : 1f;
        }
    }
}
