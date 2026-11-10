using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Authoring-only fixture, attached exclusively to SealRunSummaryPreview.unity.</summary>
    public sealed class SealRunSummaryPreview : MonoBehaviour
    {
        public StoryPlayer player;
        [Range(1, 2)] public int samplePlayer = 1;
        private void OnEnable() { if (player != null) player.ViewChanged += Bind; }
        private void OnDisable() { if (player != null) player.ViewChanged -= Bind; }
        private void Bind(StoryCutView view)
        {
            var summary = view.GetComponent<SealRunSummary>();
            if (summary == null) return;
            var stages = new[] { Stage(84,30), Stage(130,27), Stage(117,19) };
            summary.Bind(stages, (ulong)samplePlayer);
        }
        private static LabResultStage Stage(double seconds, int mine) => new LabResultStage(new[] {
            new LabPlayerState {id=1, damageTotal=mine}, new LabPlayerState {id=2, damageTotal=100-mine}
        }, seconds);
    }
}
