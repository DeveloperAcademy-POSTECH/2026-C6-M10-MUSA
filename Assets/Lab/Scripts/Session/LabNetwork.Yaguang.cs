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
        public void RequestFlashShakeRelease()
        {
            if (latest == null || latest.phase != LabPhase.Playing || latest.paused) return;
            Request(new LabNetworkPacket { type = FlashShakePacket, requestId = ++requestSequence });
        }
    }
}
