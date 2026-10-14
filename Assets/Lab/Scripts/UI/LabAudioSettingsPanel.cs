using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 일시정지 카드 안, 맨 아래 버튼 밑에 BGM·효과음 크기 슬라이더를 코드로 붙인다 (장면 수정 없음).
    /// 들어갈 자리만큼 카드를 위아래로 조금 늘리고, 카드와 같은 색을 쓴다.
    /// 일시정지 메뉴는 모든 참가자에게 보이므로, 각자 자기 기기의 소리 크기를 정할 수 있다.
    /// 효과음 슬라이더를 움직이면 장구 소리로 바로 들어볼 수 있다.
    /// BGM은 일시정지 중엔 멈춰 있으므로, 다시 시작할 때 바뀐 크기로 들린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabAudioSettingsPanel : MonoBehaviour
    {
        // 일시정지 카드 버튼과 같은 자리·폭 (카드 왼쪽 위 기준, 기준 해상도 390×844)
        public float left = 29f;
        public float width = 276f;
        public float firstRowTop = 315f;   // DEV SETTINGS 버튼 아래 (버튼 끝 299 + 간격)
        public float rowHeight = 36f;
        public float rowGap = 6f;
        public float cardGrow = 66f;       // 카드를 늘릴 높이 (위아래로 반씩)

        // 카드 색 (장면의 일시정지 카드와 맞춤)
        public Color textColor = new Color(.169f, .137f, .153f, 1f);   // 제목 글씨
        public Color fillColor = new Color(.565f, .337f, .247f, 1f);   // 버튼 갈색
        public Color trackColor = new Color(.867f, .733f, .565f, 1f);  // 카드 테두리 베이지
        public int fontSize = 16;
        [Min(0f)] public float previewInterval = .3f;

        private GameObject pauseMenu;
        private LabGenerateSound previewSound;
        private Font font;
        private Slider bgmSlider;
        private Slider sfxSlider;
        private Text bgmValue;
        private Text sfxValue;
        private float lastPreviewTime = -10f;

        public void Bind(GameObject menu, LabGenerateSound preview)
        {
            pauseMenu = menu;
            previewSound = preview;
        }

        private void Start()
        {
            if (pauseMenu == null) return;
            var card = pauseMenu.transform.Find("PausePanel") as RectTransform;
            if (card == null)
            {
                Debug.LogWarning("C6_LAB_AUDIO_PANEL: PausePanel not found under the pause menu.");
                return;
            }
            Text existing = card.GetComponentInChildren<Text>(true);
            font = existing != null ? existing.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 카드(테두리)를 위아래로 반씩 늘려 가운데를 유지하고, 안쪽 밝은 면은 아래로 늘린다.
            card.offsetMin += new Vector2(0f, -cardGrow * .5f);
            card.offsetMax += new Vector2(0f, cardGrow * .5f);
            var inner = card.Find("Inner") as RectTransform;
            if (inner != null) inner.offsetMin += new Vector2(0f, -cardGrow);

            bgmSlider = CreateRow(card, "BGM", firstRowTop, out bgmValue);
            sfxSlider = CreateRow(card, "SFX", firstRowTop + rowHeight + rowGap, out sfxValue);

            bgmSlider.SetValueWithoutNotify(LabAudioSettings.BgmVolume);
            sfxSlider.SetValueWithoutNotify(LabAudioSettings.SfxVolume);
            bgmSlider.onValueChanged.AddListener(value =>
            {
                LabAudioSettings.BgmVolume = value;
                RefreshLabels();
            });
            sfxSlider.onValueChanged.AddListener(value =>
            {
                LabAudioSettings.SfxVolume = value;
                RefreshLabels();
                PlayPreview();
            });
            RefreshLabels();
        }

        private void PlayPreview()
        {
            if (previewSound == null || Time.unscaledTime - lastPreviewTime < previewInterval) return;
            lastPreviewTime = Time.unscaledTime;
            previewSound.PlayRandom();
        }

        private void RefreshLabels()
        {
            if (bgmValue != null) bgmValue.text = Mathf.RoundToInt(bgmSlider.value * 100f) + "%";
            if (sfxValue != null) sfxValue.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
        }

        private Slider CreateRow(RectTransform card, string title, float top, out Text valueText)
        {
            var rowObject = new GameObject(title + "VolumeRow", typeof(RectTransform));
            var row = (RectTransform)rowObject.transform;
            row.SetParent(card, false);
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(left, -top);
            row.sizeDelta = new Vector2(width, rowHeight);

            CreateText(row, title, 0f, .18f, TextAnchor.MiddleLeft);
            valueText = CreateText(row, "", .82f, 1f, TextAnchor.MiddleRight);

            GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = title + "Slider";
            var sliderRect = (RectTransform)sliderObject.transform;
            sliderRect.SetParent(row, false);
            sliderRect.anchorMin = new Vector2(.2f, 0f);
            sliderRect.anchorMax = new Vector2(.8f, 1f);
            sliderRect.offsetMin = sliderRect.offsetMax = Vector2.zero;

            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            // 가는 막대 + 잡기 쉬운 손잡이. 줄 전체 높이에서 터치가 된다.
            SetBar(sliderObject.transform.Find("Background") as RectTransform, trackColor);
            SetBar(sliderObject.transform.Find("Fill Area") as RectTransform, null);
            if (slider.fillRect != null) slider.fillRect.GetComponent<Image>().color = fillColor;
            if (slider.handleRect != null)
            {
                slider.handleRect.sizeDelta = new Vector2(14f, -10f);
                slider.handleRect.GetComponent<Image>().color = fillColor;
            }
            ColorBlock colors = slider.colors;
            colors.highlightedColor = colors.pressedColor = colors.selectedColor = Color.white;
            slider.colors = colors;
            return slider;
        }

        private static void SetBar(RectTransform bar, Color? color)
        {
            if (bar == null) return;
            bar.anchorMin = new Vector2(bar.anchorMin.x, .38f);
            bar.anchorMax = new Vector2(bar.anchorMax.x, .62f);
            bar.offsetMin = new Vector2(bar.offsetMin.x, 0f);
            bar.offsetMax = new Vector2(bar.offsetMax.x, 0f);
            if (color.HasValue) bar.GetComponent<Image>().color = color.Value;
        }

        private Text CreateText(RectTransform parent, string content, float xMin, float xMax, TextAnchor alignment)
        {
            var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(xMin, 0f);
            rect.anchorMax = new Vector2(xMax, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = textColor;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
    }
}
