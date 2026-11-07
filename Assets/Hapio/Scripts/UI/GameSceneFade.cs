using System;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// 방에 들어갈 때 Home → Lobby 화면 전환을 가리는 막 (#418). GameSession이 만들어 씬이 바뀌어도 함께 남는다.
    /// 화면이 어두워지는 동안에만 눌림을 막고, 밝아지는 동안에는 막 자체가 눌림을 막지 않는다. 시간은 GameSession이 정한다.
    /// 판 밖의 씬 전환(방 들어가기·나오기 포함)에서는 그 위의 공용 로딩 화면(#478, 정렬 30150)이 닫히기 시작할 때까지 눌림을 막는다.
    /// 마지막 일격 다시보기 뒤 결과로 넘어갈 때도 같은 막을 한지색으로 쓰고, 다른 참가자를 기다리는 동안 문구를 띄운다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSceneFade : MonoBehaviour
    {
        // 시작 안내(GameStartOverlay 30000)보다 위.
        public const int SortingOrder = 30100;
        private CanvasGroup group;
        private UnityEngine.UI.Image cover;
        private UnityEngine.UI.Text status;

        public float Alpha => group != null && gameObject.activeSelf ? group.alpha : 0f;
        public bool Visible => Alpha > 0f;
        public bool BlocksInput => Visible && group.blocksRaycasts;
        public Color CoverColor => cover != null ? cover.color : Color.clear;
        public string Status => status != null && status.gameObject.activeSelf ? status.text : null;

        /// <summary>막의 색. 투명도는 <see cref="Set"/>가 정한다.</summary>
        public void SetColor(Color color)
        {
            if (cover == null) return;
            color.a = 1f;
            cover.color = color;
        }

        /// <summary>막 가운데 안내 문구. 비우면 숨긴다. 글꼴이 없으면 기본 글꼴을 쓴다.</summary>
        public void SetStatus(string words, Font font = null)
        {
            if (string.IsNullOrEmpty(words))
            {
                if (status != null) status.gameObject.SetActive(false);
                return;
            }
            if (status == null)
            {
                var label = new GameObject("Status", typeof(RectTransform), typeof(UnityEngine.UI.Text));
                label.transform.SetParent(transform, false);
                var rect = (RectTransform)label.transform;
                rect.anchorMin = new Vector2(0f, .45f); rect.anchorMax = new Vector2(1f, .55f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                status = label.GetComponent<UnityEngine.UI.Text>();
                status.alignment = TextAnchor.MiddleCenter;
                status.fontSize = 28;
                status.raycastTarget = false;
                status.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            if (font != null) status.font = font;
            else if (status.font == null) status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Color ink = cover != null && cover.color.grayscale > .5f ? new Color(.25f, .2f, .15f, .85f) : new Color(1f, 1f, 1f, .85f);
            status.color = ink;
            status.text = words;
            status.gameObject.SetActive(true);
        }

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
