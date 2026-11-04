using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 (#477). 오프닝이 끝나면 방 목록 전에 이 기기 혼자 3자리 연습 판을 연다. 나(불이)는 가운데, 왼쪽 묵이와 오른쪽
    /// 금옥이는 화면에만 있는 가상 이웃이다. 앱을 켤 때마다 첫 게임 시작에 한 번이며 저장하지 않는다(오프닝과 같은 규칙).
    /// 끝나거나 그만두거나 준비에 실패하면 결과·Game Over 없이 Home의 방 목록으로 간다.
    /// </summary>
    public sealed partial class GameSession
    {
        private const string TutorialPreparingText = "튜토리얼 준비 중…";
        private static bool tutorialConsumed;
        private bool tutorialRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTutorialRun() => tutorialConsumed = false;

        /// <summary>This run is the practice board, from its loading screen until it leaves for the room list.</summary>
        public bool IsTutorial => tutorialRun;

        /// <summary>Whether this app run still owes its one tutorial.</summary>
        public static bool TutorialPending => !tutorialConsumed;

        /// <summary>Count the tutorial as seen for this app run, so the opening leads straight to the room list.</summary>
        public static void SkipTutorialThisRun() => tutorialConsumed = true;

        /// <summary>What the opening hands over to: the tutorial once per app run, otherwise the room list.</summary>
        public void StartTutorialOrRooms()
        {
            if (!TryStartTutorial()) EnterRoomBrowser();
        }

        /// <summary>Open the practice board from Home. <paramref name="again"/> replays it in the same run (testing).</summary>
        public bool TryStartTutorial(bool again = false)
        {
            if (tutorialConsumed && !again || IsCreatingRoom || state.phase != GameFlowPhase.Home) return false;
            tutorialConsumed = true;
            if (!network.PrepareTutorialGame())
            {
                if (network.IsConnected) network.Stop();
                return false;
            }
            tutorialRun = true;
            hostMode = GameMode.Story;
            hostFlow.EnterLobby(GameMode.Story);
            if (hostFlow.BeginRun(network.ConnectedPlayerIds))
            {
                PrepareStage();
                return true;
            }
            FinishTutorial();
            return true;
        }

        /// <summary>Leave the practice board straight for Home's room list, with no result or Game Over.</summary>
        public void FinishTutorial()
        {
            if (!tutorialRun) return;
            tutorialRun = false;
            goingHome = true;
            network.Stop();
            network.UnbindBattleReferences();
            ResetRoomFlow();
            SetLocalState(new GameFlowState());
            goingHome = false;
            EnterRoomBrowser();
        }

        /// <summary>The tutorial's closing fade (#477): the persistent cover darkens Battle and lifts once Home is drawn.</summary>
        public void SetTutorialFade(float alpha)
        {
            if (tutorialRun && sceneFade != null) sceneFade.Set(alpha, true);
        }
    }
}
