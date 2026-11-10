using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 아트팀 폭탄 실험실: 에디터 창(C6 Lab/Art/폭탄 실험실)에서 버튼을 누르면 내 판에 박 폭탄을 바로 떨어뜨린다.
    /// 게임과 똑같은 길(경고 → 요괴가 던짐 → 위에서 떨어짐 → 도화선 → 폭발)로 나온다. Host이고 전투 중일 때만 된다.
    /// 도화선 길이를 주면 그 폭탄 하나만 그 시간에 터진다(설정은 바뀌지 않음).
    /// </summary>
    public sealed partial class LabNetwork
    {
        public bool ArtLabReady => IsHost && model != null && latest != null && latest.phase == LabPhase.Playing;
        /// <summary>지금 판의 원래 폭탄 도화선 길이(초). 전투 중이 아니면 0.</summary>
        public float ArtLabGameFuseSeconds => ArtLabReady ? model.ArtLabFuseSeconds : 0f;

        /// <summary>
        /// 내 판의 <paramref name="landing"/>(0~1)에 잠금 <paramref name="lockSwipes"/>번짜리 박 폭탄을 떨어뜨린다.
        /// <paramref name="fuseSeconds"/>가 0보다 크면 이 폭탄만 그 시간 뒤에 터진다.
        /// </summary>
        public bool ArtLabDropGourdBomb(Vector2 landing, int lockSwipes, float fuseSeconds = -1f)
        {
            if (!ArtLabReady) return false;
            double now = battleClock.Now(Time.realtimeSinceStartupAsDouble);
            if (!model.TutorialDropGourdBomb(LocalPlayerId, landing, lockSwipes, now, out LabOrbState bomb)) return false;
            if (fuseSeconds > 0f && bomb != null) model.ArtLabSetBombFuse(bomb.id, now, fuseSeconds);
            PublishSnapshot(true);
            return true;
        }
    }
}
