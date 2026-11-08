using UnityEngine;
namespace Hapio
{
    /// <summary>Authoring preview; intentionally owns no GameSession or network transitions.</summary>
    public sealed class StageStoryShowcase : MonoBehaviour
    {
        public StoryPlayer player;
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
            if (player == null || player.CurrentView == previous) return;
            previous = player.CurrentView;
            if (previous == null) return;
            var actions = previous.GetComponent<StoryResultActions>();
            if (actions == null) return;
            actions.onRetry.AddListener(RetryFailure);
            actions.onExit.AddListener(BackToAll);
        }
        private void RetryFailure() => PlayGroup(5);
        private void BackToAll() => PlayGroup(0);
    }
}
