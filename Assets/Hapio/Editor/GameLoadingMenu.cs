using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>
    /// 로딩 화면 (#478) 미리보기: Play 중 어느 화면에서든 새 로딩 화면을 띄워 0% → 100%를 고르지 않은 걸음으로 채우고,
    /// 잠깐 멈췄다 닫는다. 걸리는 시간은 LoadingArt의 previewSeconds. 실제 화면 전환에 붙이는 것은 다음 단계다.
    /// </summary>
    public static class GameLoadingMenu
    {
        private const string PreviewPath = "Hapio/Loading/Preview Loading Screen (Play Mode)";
        private const string HolderName = "Loading Screen Preview";

        [MenuItem(PreviewPath)]
        private static void Preview()
        {
            GameLoadingArt art = GameLoadingArt.Load();
            if (art == null) { Debug.LogWarning("HAPIO_LOADING_MENU: Resources/" + GameLoadingArt.ResourcePath + " is missing."); return; }
            GameObject holder = GameObject.Find(HolderName);
            if (holder == null)
            {
                holder = new GameObject(HolderName);
                Object.DontDestroyOnLoad(holder);
            }
            GameLoadingView view = holder.GetComponentInChildren<GameLoadingView>(true);
            if (view == null) view = GameLoadingView.Create(holder.transform, art);
            view.PlayPreview(art.previewSeconds);
        }

        [MenuItem(PreviewPath, true)]
        private static bool CanPreview() => Application.isPlaying;
    }
}
