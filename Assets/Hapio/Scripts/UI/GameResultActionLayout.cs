using UnityEngine;

namespace Hapio
{
    /// <summary>One result action group in the story page's authored coordinate system.</summary>
    [RequireComponent(typeof(RectTransform))]
    [DefaultExecutionOrder(2000)]
    public sealed class GameResultActionLayout : MonoBehaviour
    {
        public StoryPlayer player;
        public UnityEngine.UI.Button advanceButton, viewerButton, exitButton, overallButton;
        public UnityEngine.UI.Text statusText;
        [Header("402 x 874 page units")]
        [Min(0)] public float horizontalMargin = 24;
        [Min(0)] public float bottomPadding = 12;
        [Min(24)] public float primaryHeight = 40;
        [Min(24)] public float secondaryHeight = 36;
        [Min(0)] public float rowGap = 6;
        [Min(16)] public float statusHeight = 32;
        public Rect failureStatusBounds = new Rect(32, 632, 338, 58);
        private readonly UnityEngine.UI.Button[] secondary = new UnityEngine.UI.Button[3];
        private void LateUpdate() => Apply();

        public void Apply()
        {
            var root = (RectTransform)transform;
            var page = transform.parent as RectTransform;
            if (page == null) return;
            float width = page.rect.width > 0 ? page.rect.width : 402;
            root.anchorMin = Vector2.zero; root.anchorMax = new Vector2(1, 0);
            root.pivot = new Vector2(.5f, 0); root.anchoredPosition = Vector2.zero;
            root.localScale = Vector3.one;
            bool failed = player != null && player.CurrentView != null
                && player.CurrentView.GetComponentInChildren<StorySealGauge>(true) != null;
            if (failed)
            {
                // Authored Retry/Exit already occupy y721..819. The status belongs in the empty gap above them.
                Rect bounds = failureStatusBounds;
                float height = page.rect.height > 0 ? page.rect.height : 874;
                float bottom = height - bounds.yMax;
                Set(root, new Vector2(0, bottom + bounds.height));
                if (statusText != null) Place(statusText.rectTransform, bounds.x, bottom, bounds.width, bounds.height);
                return;
            }
            float y = bottomPadding + SafeBottom(page);
            float usable = Mathf.Max(1, width - horizontalMargin * 2);
            if (Shown(advanceButton))
            {
                Place((RectTransform)advanceButton.transform, horizontalMargin, y, usable, primaryHeight);
                y += primaryHeight + rowGap;
            }
            secondary[0] = viewerButton; secondary[1] = exitButton; secondary[2] = overallButton;
            int count = 0;
            for (int i = 0; i < secondary.Length; i++) if (Shown(secondary[i])) count++;
            if (count > 0)
            {
                float cell = Mathf.Max(1, (usable - rowGap * (count - 1)) / count);
                int column = 0;
                for (int i = 0; i < secondary.Length; i++)
                {
                    if (!Shown(secondary[i])) continue;
                    Place((RectTransform)secondary[i].transform, horizontalMargin + column++ * (cell + rowGap), y, cell, secondaryHeight);
                }
                y += secondaryHeight + rowGap;
            }
            if (statusText != null) Place(statusText.rectTransform, horizontalMargin, y, usable, statusHeight);
            Set(root, new Vector2(0, y + statusHeight));
        }

        private static bool Shown(UnityEngine.UI.Button button) => button != null && button.gameObject.activeSelf;

        private static float SafeBottom(RectTransform page)
        {
            if (!Application.isPlaying || Screen.width <= 0 || Screen.height <= 0) return 0;
            Canvas canvas = page.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(page,
                new Vector2(Screen.width * .5f, Screen.safeArea.yMin), camera, out Vector2 local)) return 0;
            return Mathf.Max(0, local.y - page.rect.yMin);
        }

        private static void Set(RectTransform rect, Vector2 size)
        {
            if (rect.sizeDelta != size) rect.sizeDelta = size;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }
    }
}
