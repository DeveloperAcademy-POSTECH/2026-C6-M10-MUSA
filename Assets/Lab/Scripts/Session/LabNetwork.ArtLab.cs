using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 아트팀 폭탄 실험실: 에디터 창(C6 Lab/Art/폭탄 실험실)에서 버튼을 누르면 내 판에 박 폭탄을 바로 떨어뜨린다.
    /// 게임과 똑같은 길(경고 → 요괴가 던짐 → 위에서 떨어짐 → 도화선 → 폭발)로 나온다. Host이고 전투 중일 때만 된다.
    /// </summary>
    public sealed partial class LabNetwork
    {
        public bool ArtLabReady => IsHost && model != null && latest != null && latest.phase == LabPhase.Playing;

        /// <summary>내 판의 <paramref name="landing"/>(0~1)에 잠금 <paramref name="lockSwipes"/>번짜리 박 폭탄을 떨어뜨린다.</summary>
        public bool ArtLabDropGourdBomb(Vector2 landing, int lockSwipes)
        {
            if (!ArtLabReady || !model.TutorialDropGourdBomb(LocalPlayerId, landing, lockSwipes,
                battleClock.Now(Time.realtimeSinceStartupAsDouble), out _)) return false;
            PublishSnapshot(true);
            return true;
        }
    }
}
