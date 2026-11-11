using UnityEngine;
namespace Hapio
{
    /// <summary>Authoring preview; intentionally owns no GameSession or network transitions.</summary>
    public sealed class StageStoryShowcase : MonoBehaviour
    {
        public StoryPlayer player;
        public bool previewAsHost = true;
        public StorySequence[] groups;
        public UnityEngine.UI.Text selectionLabel;
        private StoryCutView previous;
        public void PlayGroup(int index)
        {
            if (player == null || groups == null || index < 0 || index >= groups.Length) return;
            player.Play(groups[index]);
            if (selectionLabel != null) selectionLabel.text = groups[index].name;
        }
        private void Update()
        {
            if (player == null) return;
            var clear = player.CurrentView != null ? player.CurrentView.GetComponent<StoryStageClearControls>() : null;
            if (clear != null) clear.Present(player.IsReading, previewAsHost, true);
            if (player.CurrentView == previous) return;
            previous = player.CurrentView;
            if (previous == null) return;
            if (clear != null) clear.nextSeal.onClick.AddListener(player.RequestNext);
            var actions = previous.GetComponent<StoryResultActions>();
            if (actions == null) return;
            actions.onRetry.AddListener(RetryFailure);
            actions.onExit.AddListener(BackToAll);
        }
        private void RetryFailure() => PlayGroup(5);
        private void BackToAll() => PlayGroup(0);
    }
}
