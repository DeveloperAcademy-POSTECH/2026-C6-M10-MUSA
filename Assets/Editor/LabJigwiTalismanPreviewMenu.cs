using UnityEditor;
using UnityEngine;

namespace C6Lab.Editor
{
    /// <summary>
    /// #595 공용 부적 완성 미리보기: 혼자서도 N명이 차례로 부적을 쓰고(아이콘이 하나씩 켜짐) 모두 완성되는 장면
    /// (부적이 박으로 날아감 → "물" 글자 → 폭포 + 흔들림 + 소리)을 본다. 지귀 전투 중 Play에서만. 화면만 보여 주고 박은 칠하지 않는다.
    /// </summary>
    public static class LabJigwiTalismanPreviewMenu
    {
        private const string Root = "C6 Lab/Jigwi/공용 부적 완성 미리보기/";

        [MenuItem(Root + "2명")] private static void Two() => Preview(2);
        [MenuItem(Root + "3명")] private static void Three() => Preview(3);
        [MenuItem(Root + "4명")] private static void Four() => Preview(4);
        [MenuItem(Root + "5명")] private static void Five() => Preview(5);

        [MenuItem(Root + "2명", true)] [MenuItem(Root + "3명", true)] [MenuItem(Root + "4명", true)] [MenuItem(Root + "5명", true)]
        private static bool CanPreview() => EditorApplication.isPlaying;

        private static void Preview(int players)
        {
            var view = Object.FindFirstObjectByType<LabJigwiScreenPresentation>();
            if (view == null || !view.isActiveAndEnabled)
            {
                Debug.LogWarning("공용 부적 미리보기: 지귀 전투 화면에서 눌러 주세요 (Hapio DEV SOLO 3 지귀).");
                return;
            }
            view.PreviewCommonTalisman(players);
        }
    }
}
