using UnityEditor;

namespace Hapio.Editor
{
    /// <summary>
    /// #479 지귀 실험실: 에디터에서 DEV · SOLO로 시작할 때 몇 스테이지부터 할지 고른다.
    /// 값은 이 컴퓨터의 EditorPrefs에만 저장되어 저장소/빌드에 들어가지 않는다. 기본 1이면 지금과 같다.
    /// </summary>
    public static class GameDeveloperSoloStartMenu
    {
        private const string Root = "Hapio/DEV SOLO 시작 스테이지/";
        private const string One = Root + "1 구미호 (기본)", Two = Root + "2 야광귀", Three = Root + "3 지귀";

        [MenuItem(One, false, 200)] private static void StartOne() => Set(1);
        [MenuItem(Two, false, 201)] private static void StartTwo() => Set(2);
        [MenuItem(Three, false, 202)] private static void StartThree() => Set(3);

        [MenuItem(One, true)] private static bool CheckOne() => Check(One, 1);
        [MenuItem(Two, true)] private static bool CheckTwo() => Check(Two, 2);
        [MenuItem(Three, true)] private static bool CheckThree() => Check(Three, 3);

        private static int Current => EditorPrefs.GetInt(GameSession.DeveloperSoloStartStageKey, 1);
        private static void Set(int stage) => EditorPrefs.SetInt(GameSession.DeveloperSoloStartStageKey, stage);
        private static bool Check(string path, int stage)
        {
            Menu.SetChecked(path, Current == stage);
            return true;
        }
    }
}
