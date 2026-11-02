using System;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// 방에 들어갈 때 Home → Lobby 화면 전환을 가리는 막 (#418). GameSession이 만들어 씬이 바뀌어도 함께 남는다.
    /// 화면이 어두워지는 동안에만 눌림을 막고, 밝아지는 동안에는 새 화면을 바로 누를 수 있다. 시간은 GameSession이 정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSceneFade : MonoBehaviour
    {
        // 시작 안내(GameStartOverlay 30000)보다 위.
        public const int SortingOrder = 30100;
        private CanvasGroup group;
        private UnityEngine.UI.Image cover;

        public float Alpha => group != null && gameObject.activeSelf ? group.alpha : 0f;
        public bool Visible => Alpha > 0f;
        public bool BlocksInput => Visible && group.blocksRaycasts;

        public static GameSceneFade Create(Transform parent, Color color)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("GameSceneFade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup),
                typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            var view = root.AddComponent<GameSceneFade>();
            view.group = root.GetComponent<CanvasGroup>();
            view.group.interactable = false;
            var image = new GameObject("Cover", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            image.transform.SetParent(root.transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            view.cover = image.GetComponent<UnityEngine.UI.Image>();
            view.cover.color = color;
            view.Set(0f, false);
            return view;
        }

        /// <summary>막의 진하기(0 = 안 보임, 1 = 다 가림)와 눌림을 막을지. 0이면 꺼 둔다.</summary>
        public void Set(float alpha, bool blockInput)
        {
            alpha = Mathf.Clamp01(alpha);
            group.alpha = alpha;
            group.blocksRaycasts = blockInput && alpha > 0f;
            cover.raycastTarget = group.blocksRaycasts;
            if (gameObject.activeSelf != alpha > 0f) gameObject.SetActive(alpha > 0f);
        }
    }
}
