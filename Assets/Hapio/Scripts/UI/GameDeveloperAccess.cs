using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Development entry only; preserves the existing scoped settings and Host authority.</summary>
    public static class GameDeveloperAccess
    {
        // App Store / non-development players do not install the secret input surface at all.
        public static bool AvailableInThisBuild
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        public static void BindMenu(GameScreen screen)
        {
            if (screen.developerButton != null) screen.developerButton.gameObject.SetActive(false);
            if (!AvailableInThisBuild) return;
            GameDeveloperTapTarget.Attach(screen.gameObject, () => CanOpenMenu(screen), screen.OpenDeveloper);
        }

        private static bool CanOpenMenu(GameScreen screen)
        {
            GameSession session = GameSession.Instance;
            if (screen == null || !screen.isActiveAndEnabled || session == null || session.Network == null
                || !session.Network.DeveloperBuildAvailable || session.IsLoadingScene || session.IsLoadingScreenUp
                || session.IsCreatingRoom || GameOpeningController.IsPresenting
                || (screen.developerPanel != null && screen.developerPanel.activeInHierarchy)) return false;
            bool menu = screen.kind == GameScreenKind.Home && session.State.phase == GameFlowPhase.Home
                || screen.kind == GameScreenKind.Lobby && session.State.phase == GameFlowPhase.Lobby;
            return menu && (session.Network.CanChooseConnection
                || session.Network.IsHost && session.Network.Snapshot == null);
        }

        public static void BindBattle(GameBattleBindings bindings)
        {
            if (bindings.hud != null) bindings.hud.developerSettingsHidden = true;
            if (!AvailableInThisBuild) return;
            GameDeveloperTapTarget.Attach(bindings.gameObject, () => CanOpenBattle(bindings), () =>
            {
                if (!CanOpenBattle(bindings)) return;
                LabNetwork network = GameSession.Instance.Network;
                // Host pause publishes synchronously; no DEV form opens against an unpaused live round.
                if (!network.IsBattlePaused && !network.PauseBattle()) return;
                if (bindings.developerMode == null || !bindings.developerMode.CanOpen) return;
                bindings.developerMode.Open();
                if (bindings.developerMode.panel != null)
                    bindings.developerMode.panel.transform.SetAsLastSibling();
            });
        }

        private static bool CanOpenBattle(GameBattleBindings bindings)
        {
            GameSession session = GameSession.Instance;
            if (bindings == null || !bindings.isActiveAndEnabled || session == null || session.Network == null
                || !session.Network.DeveloperBuildAvailable || !session.Network.IsHost || session.IsTutorial
                || session.IsLoadingScene || session.IsLoadingScreenUp || session.IsPreparingBattle
                || session.State.phase != GameFlowPhase.Playing || bindings.developerMode == null
                || bindings.developerMode.panel == null || bindings.developerMode.panel.activeInHierarchy) return false;
            return !session.Network.GameStartHeld && (session.Network.CanPauseBattle || session.Network.IsBattlePaused);
        }

        public static void MoveSoloIntoSettings(GameScreen screen, Button solo, Button territorySolo)
        {
            if (screen == null || screen.developerPanel == null) return;
            PlaceSolo(solo, screen.developerPanel.transform, .5f, .71f, "SOLO");
            PlaceSolo(territorySolo, screen.developerPanel.transform, .73f, .95f, "2:2 SOLO");
            Transform title = screen.developerPanel.transform.Find("Title");
            if (title != null && title.TryGetComponent(out Text heading))
            {
                heading.text = "개발 설정";
                ((RectTransform)title).anchorMax = new Vector2(.46f, .97f);
            }
        }

        private static void PlaceSolo(Button button, Transform parent, float left, float right, string label)
        {
            if (button == null) return;
            button.transform.SetParent(parent, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(left, .915f); rect.anchorMax = new Vector2(right, .96f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) { text.text = label; text.resizeTextForBestFit = true; text.resizeTextMinSize = 8; text.resizeTextMaxSize = 16; }
            button.gameObject.SetActive(false);
        }
    }
}
