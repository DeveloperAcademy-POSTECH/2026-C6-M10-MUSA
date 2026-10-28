using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Preview-only title gate. The game's room/session navigation remains with GameLobbyView.</summary>
    public sealed class StoryOpeningTitle : MonoBehaviour
    {
        public StoryPlayer player;
        public GameObject titlePage;
        public GameObject controls;
        public Button beginButton;
        private void Awake() { if (player != null) player.playOnStart = false; }
        private void OnEnable() { if (beginButton != null) beginButton.onClick.AddListener(Begin); }
        private void Start() => ShowTitle();
        private void OnDisable() { if (beginButton != null) beginButton.onClick.RemoveListener(Begin); }
        public void ShowTitle()
        {
            if (player != null) player.Cancel();
            if (titlePage != null) titlePage.SetActive(true);
            if (controls != null) controls.SetActive(false);
        }
        public void Begin()
        {
            if (player == null || !player.Play(player.sequence)) return;
            if (titlePage != null) titlePage.SetActive(false);
            if (controls != null) controls.SetActive(true);
        }
    }
}
