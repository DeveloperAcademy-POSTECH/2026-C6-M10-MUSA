using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 기믹 안내 문구 (#328): 매혹·여우 구슬을 어떻게 풀지 알려 주는 두 줄 글자. 매혹 예고 (#305)와 같은 자리·글꼴·색으로
    /// 뜨고, 처음 보일 때 톡 튀어나와 살짝 두근거린다. 화면 표시만 하며 터치를 막지 않는다.
    /// </summary>
    public sealed class LabGuideText
    {
        /// <summary>매혹 예고와 같은 자리 (Canvas 비율: 박 위).</summary>
        public static readonly Vector2 DefaultAnchor = new Vector2(.5f, .6f);
        public const float DefaultSize = 20f;
        private const float PopSeconds = .2f, PulseHz = 2f;
        private static readonly Color TextColor = new Color(.55f, .1f, .16f);

        private readonly Text text;
        private float age = -1f;

        private LabGuideText(Text text) { this.text = text; }

        public RectTransform Rect => text.rectTransform;
        public bool Showing => text != null && text.gameObject.activeInHierarchy;
        public string Shown => text != null ? text.text : string.Empty;

        /// <summary>Canvas 위의 layer 아래에 숨긴 채로 만든다. 글꼴은 Canvas에 이미 있는 글자의 것을 쓴다.</summary>
        public static LabGuideText Create(RectTransform layer, Font font, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = layer.gameObject.layer;
            rect.SetParent(layer, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = rect.anchorMax = DefaultAnchor;
            // Nearly the 390-wide reference screen; a long line shrinks to fit rather than running off it.
            rect.sizeDelta = new Vector2(370f, DefaultSize * 3.2f);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.RoundToInt(DefaultSize);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = Mathf.RoundToInt(DefaultSize);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.lineSpacing = 1.1f;
            text.raycastTarget = false;
            text.color = TextColor;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, .95f);
            outline.effectDistance = new Vector2(2f, -2f);
            rect.gameObject.SetActive(false);
            return new LabGuideText(text);
        }

        /// <summary>The first font already on this Canvas, so the guide matches the battle screen.</summary>
        public static Font FontOf(Canvas canvas)
        {
            Text[] existing = canvas != null ? canvas.GetComponentsInChildren<Text>(true) : new Text[0];
            for (int i = 0; i < existing.Length; i++)
                if (existing[i].font != null) return existing[i].font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>Shows or hides the words this frame; a new showing pops in, then it beats gently.</summary>
        public void Draw(bool shown, string words)
        {
            if (text == null) return;
            text.gameObject.SetActive(shown);
            if (!shown)
            {
                age = -1f;
                return;
            }
            text.text = words;
            age = age < 0f ? 0f : age + Time.deltaTime;
            float pop = age < PopSeconds ? Mathf.Lerp(.5f, 1f, age / PopSeconds)
                : 1f + .04f * Mathf.Sin((age - PopSeconds) * PulseHz * 2f * Mathf.PI);
            text.rectTransform.localScale = Vector3.one * pop;
        }
    }
}
