using C6Lab;

namespace Hapio
{
    public sealed partial class GameSession
    {
        /// <summary>The selected catalog stage. Not a seat, route position, or local developer override.</summary>
        public int SelectedDemoStage { get; private set; }

        private bool SetSelectedDemoStage(int stage)
        {
            if (!LabGameBuildVariant.IsStaffStageDemo || stage < 1 || stage > 3 || goingHome
                || state.phase != GameFlowPhase.Home || !network.CanChooseConnection
                || !hostFlow.ConfigureDemoStage(stage)) return false;
            SelectedDemoStage = stage;
            return true;
        }

        private void ClearDemoSelectionAfterLeave()
        {
            if (!LabGameBuildVariant.IsStaffStageDemo) return;
            SelectedDemoStage = 0;
            hostFlow.ConfigureDemoStage(0);
            if (network.CanChooseConnection) network.SetDemoStageIdentity(0);
            roomBrowserVisible = false;
        }

        /// <summary>Only a deliberate menu action returns the completed demo to its staff screen.</summary>
        public void ReturnToDemoSelection()
        {
            if (!LabGameBuildVariant.IsStaffStageDemo) return;
            GoHome();
        }
    }
}
