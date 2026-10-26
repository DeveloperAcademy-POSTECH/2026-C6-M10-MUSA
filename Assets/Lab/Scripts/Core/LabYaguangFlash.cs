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
        /// 이 사람이 지금 눈부신지 (#301): 마지막 섬광을 맞았고, 눈부심 시간 안이고, 그 뒤 요괴 얼굴을 맞혀 풀지 않았다.
        /// 회복 중은 눈부신 것으로 치지 않는다. Host의 `LabModel.FlashGlared`와 같은 규칙을 스냅샷으로 계산한다.
        /// </summary>
        public static bool Glared(LabSnapshot snapshot, double now, ulong player, float glareSeconds) =>
            snapshot != null && snapshot.phase == LabPhase.Playing && snapshot.LastFlashHit(player)
            && snapshot.flashLastAt >= 0d && now >= snapshot.flashLastAt && now < snapshot.flashLastAt + glareSeconds
            && snapshot.FlashReleasedAt(player) < 0d;

        /// <summary>한 명이라도 지금 눈부신지 (#301). 이동안 박 속 요괴가 모든 기기에서 나와 있다.</summary>
        public static bool AnyGlared(LabSnapshot snapshot, double now, float glareSeconds)
        {
            if (snapshot == null || snapshot.players == null) return false;
            foreach (LabPlayerState player in snapshot.players)
                if (player != null && Glared(snapshot, now, player.id, glareSeconds)) return true;
            return false;
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
