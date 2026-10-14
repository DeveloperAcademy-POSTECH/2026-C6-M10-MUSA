using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 일시정지 메뉴 아래에 BGM·효과음 크기 슬라이더를 코드로 붙인다 (장면 수정 없음).
    /// 일시정지 메뉴는 모든 참가자에게 보이므로, 각자 자기 기기의 소리 크기를 정할 수 있다.
    /// 효과음 슬라이더를 움직이면 장구 소리로 바로 들어볼 수 있다.
    /// BGM은 일시정지 중엔 멈춰 있으므로, 다시 시작할 때 바뀐 크기로 들린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabAudioSettingsPanel : MonoBehaviour
    {
        // 일시정지 메뉴 화면에서 패널이 차지하는 위치 (가로·세로 비율). 일시정지 박스 바로 아래.
        public Vector2 anchorMin = new Vector2(.0846f, .15f);
        public Vector2 anchorMax = new Vector2(.9154f, .275f);
        public Color panelColor = new Color(0f, 0f, 0f, .55f);
        public Color fillColor = new Color(.96f, .72f, .26f, 1f);
        public int fontSize = 18;
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
            Text existing = pauseMenu.GetComponentInChildren<Text>(true);
            font = existing != null ? existing.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var panel = new GameObject("SoundSettingsPanel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(pauseMenu.transform, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = panelColor;

            bgmSlider = CreateRow(rect, "BGM", 1, out bgmValue);
            sfxSlider = CreateRow(rect, "SFX", 0, out sfxValue);

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

        /// <summary>row 1 = 위쪽 줄, 0 = 아래쪽 줄.</summary>
        private Slider CreateRow(RectTransform parent, string title, int row, out Text valueText)
        {
            var rowObject = new GameObject(title + "Row", typeof(RectTransform));
            var rowRect = (RectTransform)rowObject.transform;
            rowRect.SetParent(parent, false);
            rowRect.anchorMin = new Vector2(0f, row * .5f);
            rowRect.anchorMax = new Vector2(1f, row * .5f + .5f);
            rowRect.offsetMin = new Vector2(16f, 0f);
            rowRect.offsetMax = new Vector2(-16f, 0f);

            CreateText(rowRect, title, 0f, .2f, TextAnchor.MiddleLeft);
            valueText = CreateText(rowRect, "", .82f, 1f, TextAnchor.MiddleRight);

            GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = title + "Slider";
            var sliderRect = (RectTransform)sliderObject.transform;
            sliderRect.SetParent(rowRect, false);
            sliderRect.anchorMin = new Vector2(.22f, .3f);
            sliderRect.anchorMax = new Vector2(.8f, .7f);
            sliderRect.offsetMin = sliderRect.offsetMax = Vector2.zero;

            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            if (slider.fillRect != null)
                slider.fillRect.GetComponent<Image>().color = fillColor;
            Transform background = sliderObject.transform.Find("Background");
            if (background != null)
                background.GetComponent<Image>().color = new Color(1f, 1f, 1f, .25f);
            if (slider.handleRect != null)
            {
                // 손가락으로 잡기 쉽게 손잡이를 조금 키운다.
                slider.handleRect.sizeDelta = new Vector2(24f, 12f);
                slider.handleRect.GetComponent<Image>().color = Color.white;
            }
            return slider;
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
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
    }
}
