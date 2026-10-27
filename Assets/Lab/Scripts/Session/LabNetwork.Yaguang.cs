using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 야광귀 섬광 해제 (#301): 눈부신 사람이 폰을 정해진 횟수만큼 흔들면 그 기기가 Host에 한 번 알린다.
    /// 흔든 횟수는 그 사람 기기가 세고(하얀 막이 조금씩 걷히는 것도 그 화면에서만), 해제 여부는 Host가
    /// `LabModel.TryFlashShakeRelease`로 확정해 스냅샷의 `flashReleasedAt`으로 모두에게 알린다.
    /// </summary>
    public sealed partial class LabNetwork
    {
        /// <summary>흔들어 풀기 요청 패킷 종류</summary>
        public const string FlashShakePacket = "flashShake";

        /// <summary>이 기기 플레이어가 폰을 다 흔들었다고 Host에 알린다. 전투 중이 아니면 보내지 않는다.</summary>
        public bool RequestFlashShakeRelease()
        {
            if (!IsConnected || !CanProcessBattleActions || latest == null
                || latest.phase != LabPhase.Playing || latest.paused || latest.flashCount == 0) return false;
            Request(new LabNetworkPacket { type = FlashShakePacket, requestId = ++requestSequence,
                flashGeneration = latest.flashCount });
            return true;
        }

        /// <summary>섬광 파훼 보상 요청 패킷 종류 (#329)</summary>
        public const string FlashBonusPacket = "flashBonus";

        /// <summary>
        /// 이 기기 플레이어가 풀린 뒤에도 끊기지 않고 최대 횟수까지 흔들었다고 Host에 알린다 (#329). Host가 이번 섬광에서
        /// 한 번만 황금알을 준다. 전투 중이 아니면 보내지 않는다.
        /// </summary>
        public bool RequestFlashGoldenBonus()
        {
            if (!IsConnected || !CanProcessBattleActions || latest == null
                || latest.phase != LabPhase.Playing || latest.paused || latest.flashCount == 0) return false;
            Request(new LabNetworkPacket { type = FlashBonusPacket, requestId = ++requestSequence,
                flashGeneration = latest.flashCount });
            return true;
        }

        /// <summary>내 판에 황금알이 새로 생겼다 (#329). 받은 개수. 진동·소리가 듣는다.</summary>
        public event System.Action<int> GoldenEggsReceived;
        /// <summary>내가 던진 황금알이 박에 맞았다 (#329)</summary>
        public event System.Action LocalGoldenHit;
        private readonly System.Collections.Generic.HashSet<string> knownGoldenEggs =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>스냅샷의 내 판에서 처음 보는 황금알 수를 세어, 있으면 한 번 알린다.</summary>
        private void NoticeGoldenEggs(LabSnapshot snapshot)
        {
            if (snapshot?.orbs == null) return;
            int fresh = 0;
            foreach (LabOrbState orb in snapshot.orbs)
                if (orb != null && orb.golden && orb.owner == LocalPlayerId && knownGoldenEggs.Add(orb.id)
                    && orb.originOwner == LocalPlayerId && snapshot.serverTime - orb.createdAt < 2d) fresh++;
            if (fresh > 0) GoldenEggsReceived?.Invoke(fresh);
        }

        /// <summary>황금알 명중 (#329): 맞은 자리에 금빛 반짝이를 터뜨리고, 던진 사람이면 진동한다.</summary>
        private void PresentGoldenHit(Vector3 targetLocalPoint, ulong owner)
        {
            if (target == null) return;
            Transform hitbox = target.Hitbox != null ? target.Hitbox.transform : target.transform;
            LabGoldenSparkle.Burst(hitbox.TransformPoint(targetLocalPoint), config != null ? config.OrbRadius : .3f,
                config != null ? config.YaguangGoldenEggColor : new Color(1f, .78f, .16f));
            if (owner == LocalPlayerId) LocalGoldenHit?.Invoke();
        }
    }
}
