using System;

namespace C6Lab
{
    public sealed partial class LabNetwork
    {
        /// <summary>Once per settled local generation receipt that actually spent stamina;
        /// never a delta inferred from a refilling gauge, replayed snapshot, or free supply.</summary>
        public event Action<float> LocalStaminaSpent;
        /// <summary>
        /// 내가 북을 눌렀는데 Host가 스태미나 부족으로 생성을 거절했을 때 한 번 (#418). 부족 표시(게이지 흔들림·빨간 빛)와
        /// 효과음이 듣는다. 보유 한도·다른 이유의 거절, 지난 라운드의 늦은 응답에는 오지 않는다.
        /// </summary>
        public event Action LocalStaminaShort;

        private void NotifyLocalStaminaSpent(LabNetworkPacket request, LabActionReceipt receipt, bool purged)
        {
            if (IsStaminaShort(request?.type, receipt, purged)) LocalStaminaShort?.Invoke();
            if (purged || request == null || receipt == null || !receipt.accepted
                || request.type != "generate" || config == null || !(config.GenerateCost > 0f)) return;
            LocalStaminaSpent?.Invoke(config.GenerateCost);
        }

        /// <summary>이 응답이 내 생성 요청(requestType "generate")을 스태미나 부족으로 거절한 것인지 (#418).</summary>
        public static bool IsStaminaShort(string requestType, LabActionReceipt receipt, bool purged) =>
            !purged && requestType == "generate" && receipt != null && !receipt.accepted
            && receipt.reason == LabModel.InsufficientStaminaReason;
    }
}
