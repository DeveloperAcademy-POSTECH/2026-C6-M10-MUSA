using UnityEngine;

namespace Hapio
{
    public sealed class GameOverCharacterView : MonoBehaviour
    {
        public RectTransform design;
        public GameObject bul, geumok;
        public UnityEngine.UI.Text dialogue;
        public UnityEngine.UI.Button homeButton;
        private bool selected;
        private UnityEngine.UI.Text homeCaption;
        private string originalHomeCaption;
        private UnityEngine.UI.RawImage parchment;
        private readonly Vector3[] canvasCorners = new Vector3[4];
        private void Awake() => GameResultStrokes.Apply(this);

        public void Present(bool canReturnHome, bool staffRetry = false)
        {
            if (homeCaption == null)
            {
                homeCaption = homeButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (homeCaption != null) originalHomeCaption = homeCaption.text;
            }
            if (homeCaption != null) homeCaption.text = staffRetry ? "다시 도전" : originalHomeCaption;
            if (!selected)
            {
                bool useGeumok = Random.Range(0, 2) == 1;
                bul.SetActive(!useGeumok);
                geumok.SetActive(useGeumok);
                dialogue.text = useGeumok ? "지금, 누가 나갔지?" : "얼른 다시 돌아와!";
                selected = true;
            }
            homeButton.interactable = canReturnHome;
        }

        private void LateUpdate()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            float factor = canvas != null ? canvas.scaleFactor : 1f;
            Rect safe = Screen.safeArea;
            design.localScale = Vector3.one * Mathf.Min(safe.width / factor / 402f, safe.height / factor / 874f);
            CoverCanvasBackground();
        }

        // Content fits the safe area; the opaque paper must cover the entire display.
        // Keep the Figma crop at 402x874, enlarging the same texture uniformly only when needed.
        public void CoverCanvasBackground()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            if (parchment == null) parchment = design.Find("Parchment").GetComponent<UnityEngine.UI.RawImage>();
            if (parchment.maskable)
            {
                parchment.maskable = false;
                parchment.RecalculateClipping();
            }
            ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(canvasCorners);
            Vector2 centre = new Vector2(77f, 83.5f);
            float scale = 1f;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in canvasCorners)
            {
                Vector3 local = design.InverseTransformPoint(corner);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
                scale = Mathf.Max(scale, Mathf.Abs(local.x - centre.x) / 352f,
                    Mathf.Abs(local.y - centre.y) / 520.5f);
            }
            // Clouds crop at the display edge, not at the inset content rectangle.
            Rect bounds = design.rect;
            design.GetComponent<UnityEngine.UI.RectMask2D>().padding = new Vector4(
                min.x - bounds.xMin, min.y - bounds.yMin, bounds.xMax - max.x, bounds.yMax - max.y);
            var rect = parchment.rectTransform;
            rect.sizeDelta = new Vector2(704f, 1041f) * scale;
            rect.anchoredPosition = new Vector2(-74f - 352f * (scale - 1f), 167f + 520.5f * (scale - 1f));
        }
    }
}
