using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// 장면이 바뀌어도 남는 한지 구름 덮개 (#635). 오프닝이 끝나 튜토리얼로 넘어갈 때 화면을 덮고, 튜토리얼이 준비될 때까지
    /// 덮인 채로 기다렸다가 걷힌다. 덮개 자체는 <see cref="GameCloudCover"/>이고, 여기서는 세션이 하나를 갖고 언제 걷을지 정한다.
    /// - 튜토리얼로 갔으면: 튜토리얼을 할 수 있게 된 뒤(첫 대사를 띄울 수 있을 때) 걷힌다.
    /// - 방 목록으로 갔으면(튜토리얼을 이미 했거나 준비에 실패): 장면을 불러오는 중이 아니면 바로 걷힌다.
    /// - 준비가 너무 오래 걸리면(15초) 그래도 걷어, 구름 아래에 갇히지 않는다.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>The longest the clouds wait for the next screen before lifting anyway.</summary>
        public const float CloudHoldLimitSeconds = 15f;

        private GameCloudCover cloudCover;
        private bool liftCloudsOnceShown;
        private float liftCloudsAskedAt;

        /// <summary>The persistent cloud cover, or null when its art is missing (the game then goes on without clouds).</summary>
        public GameCloudCover CloudCover => cloudCover;
        /// <summary>The clouds are on the screen (closing, covering or lifting); nothing under them can be touched.</summary>
        public bool CloudsUp => cloudCover != null && cloudCover.IsUp;
        /// <summary>The clouds are waiting for the next screen before they lift.</summary>
        public bool CloudsWaitForNextScreen => liftCloudsOnceShown && CloudsUp;

        /// <summary>The practice board can be played: its first line may show (the same moment its plain cover goes).</summary>
        private bool TutorialPlayable => tutorialRun && state.phase == GameFlowPhase.Playing && countdownCommitted && !network.GameStartHeld;

        /// <summary>What came after the clouds can be seen: no scene is loading, and a practice board is playable.</summary>
        private bool NextScreenShown => !IsLoadingScene && (!tutorialRun || TutorialPlayable);

        /// <summary>
        /// The opening's hand-over (#635): keep the clouds over the screen until what came next can be seen, then lift them.
        /// Called once that next step (the tutorial or the room list) has begun.
        /// </summary>
        public void LiftCloudsOnceShown()
        {
            if (!CloudsUp) return;
            liftCloudsOnceShown = true;
            liftCloudsAskedAt = Time.unscaledTime;
        }

        private void UpdateCloudCover()
        {
            if (!liftCloudsOnceShown) return;
            if (!CloudsUp) { liftCloudsOnceShown = false; return; }
            if (!NextScreenShown && Time.unscaledTime - liftCloudsAskedAt < CloudHoldLimitSeconds) return;
            liftCloudsOnceShown = false;
            cloudCover.Open();
        }
    }
}
