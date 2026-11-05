using C6Lab;

namespace Hapio
{
    public sealed partial class GameSession
    {
        // Battle uses LabDeveloperMode rather than the Home/Lobby GameScreen. Remember
        // its Host-only live event too, so the next stage retains these two cosmetic values.
        private void OnBattleClayAppearanceChanged(float thickness,float gloss)
        {
            if(network==null || !network.IsHost || state.phase!=GameFlowPhase.Playing
                || !CanEditDeveloperSettings || stageSettings==null)return;
            float[] values=stageSettings.Capture(0);
            if(values[LabConfig.ClayThicknessField]==thickness && values[LabConfig.ClayGlossField]==gloss)return;
            values[LabConfig.ClayThicknessField]=thickness;values[LabConfig.ClayGlossField]=gloss;
            if(!stageSettings.TryApply(0,values,out string error)){UiNotice=error;return;}
            Changed?.Invoke();
        }

        /// <summary>Persist only these two cosmetic fields; other form edits still require APPLY.</summary>
        public bool PreviewClayAppearance(float thickness, float gloss, out string error)
        {
            error = "Only the Host can adjust clay appearance in Home, the lobby or a paused battle.";
            if (!CanEditDeveloperSettings || stageSettings == null || !LabConfig.ValidClayAppearance(thickness, gloss)) return false;
            float[] before = stageSettings.Capture(0);
            float[] values = (float[])before.Clone();
            values[LabConfig.ClayThicknessField] = thickness;
            values[LabConfig.ClayGlossField] = gloss;
            if (!stageSettings.TryApply(0, values, out error)) return false;
            if (state.phase == GameFlowPhase.Playing && !network.TryPreviewClayAppearance(thickness, gloss))
            {
                stageSettings.TryApply(0, before, out _);
                error = "Clay preview is available after the Host pauses the battle.";
                return false;
            }
            error = string.Empty;
            return true;
        }
    }
}
