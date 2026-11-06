using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>
    /// 튜토리얼 (#477)을 Play 중 Home(타이틀·방 목록)에서 오프닝 없이 바로 연다. 같은 실행에서 다시 보는 것도 된다.
    /// 앱에서는 오프닝이 끝날 때 한 번만 열린다.
    /// </summary>
    public static class GameTutorialMenu
    {
        private const string PlayNowPath = "Hapio/Tutorial/Play Tutorial Now (Play Mode)";

        [MenuItem(PlayNowPath)]
        private static void PlayNow()
        {
            GameSession session = GameSession.Instance;
            if (session == null || !session.TryStartTutorial(again: true))
                Debug.LogWarning("HAPIO_TUTORIAL_MENU: open Home in Play mode first (the title or the room list, not inside a room).");
        }

        [MenuItem(PlayNowPath, true)]
        private static bool CanPlayNow() => Application.isPlaying && GameSession.Instance != null;
    }
}
