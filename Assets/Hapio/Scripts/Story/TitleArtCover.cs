using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// 타이틀 그림이 화면을 꽉 채우게 한다 (#467). 타이틀이 놓인 페이지는 402x874 설계를 화면 안에 다 들어가게 줄이므로,
    /// 비율이 다른 폰에서는 양옆이나 위아래에 뒤 배경이 비치는 얇은 띠(흰선)가 남았다. 그림은 설계 배치 그대로 두고
    /// 화면 가운데를 기준으로 화면을 덮을 때까지만 키운다(넘치는 가장자리는 잘린다). 같은 페이지의 버튼은 그대로 화면 안에 있다.
    /// GameLobbyView가 LateUpdate에서 페이지 배율을 정한 뒤에 계산한다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class TitleArtCover : MonoBehaviour
    {
        public Vector2 referenceSize = new Vector2(402, 874);
        private void OnEnable() => Apply();
        private void LateUpdate() => Apply();

        public void Apply()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (parent == null || canvas == null || referenceSize.x <= 0 || referenceSize.y <= 0) return;
            var screen = (RectTransform)canvas.rootCanvas.transform;
            float parentScale = screen.lossyScale.x > 0f ? parent.lossyScale.x / screen.lossyScale.x : 0f;
            if (parentScale <= 0f) return;
            Vector2 view = screen.rect.size;
            float cover = Mathf.Max(view.x / referenceSize.x, view.y / referenceSize.y);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = referenceSize;
            rect.localScale = Vector3.one * (cover / parentScale);
            rect.position = screen.TransformPoint(screen.rect.center);
        }
    }
}
