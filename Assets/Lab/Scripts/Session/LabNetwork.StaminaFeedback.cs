using System;

namespace C6Lab
{
    public sealed partial class LabNetwork
    {
        /// <summary>Once per settled local generation receipt that actually spent stamina;
        /// never a delta inferred from a refilling gauge, replayed snapshot, or free supply.</summary>
        public event Action<float> LocalStaminaSpent;

        private void NotifyLocalStaminaSpent(LabNetworkPacket request, LabActionReceipt receipt, bool purged)
        {
            if (purged || request == null || receipt == null || !receipt.accepted
                || request.type != "generate" || config == null || !(config.GenerateCost > 0f)) return;
            LocalStaminaSpent?.Invoke(config.GenerateCost);
        }
    }
}
