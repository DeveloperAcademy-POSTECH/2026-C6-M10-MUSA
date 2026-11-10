using UnityEngine;

namespace C6Lab
{
    /// <summary>A local display preference in DEV panels, outside the Host's shared numeric settings.</summary>
    public sealed class LabNoticeSettingsRow : MonoBehaviour
    {
        public const string RowName = "LocalNoticeTextSetting";
        public UnityEngine.UI.Button ToggleButton { get; private set; }
        private UnityEngine.UI.Text stateLabel;

        public static LabNoticeSettingsRow Attach(GameObject panel, UnityEngine.UI.InputField template,
            UnityEngine.UI.Button style)
        {
            if (!LabNoticeSettings.AvailableInThisBuild || panel == null || template == null || style == null
                || template.transform.parent == null || template.transform.parent.parent == null) return null;
            Transform content = template.transform.parent.parent;
            if (!content.IsChildOf(panel.transform)) return null;
            Transform existing = content.Find(RowName);
            if (existing != null)
            {
                var attached = existing.GetComponent<LabNoticeSettingsRow>();
                if (attached != null) attached.Refresh();
                return attached;
            }
            var labelTransform = template.transform.parent.Find("Label")
                ?? template.transform.parent.Find("SettingLabel");
            var labelStyle = labelTransform != null ? labelTransform.GetComponent<UnityEngine.UI.Text>() : null;
            if (labelStyle == null) return null;

            var row = new GameObject(RowName, typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
            row.layer = content.gameObject.layer;
            row.transform.SetParent(content, false);
            row.transform.SetAsFirstSibling();
            var layout = row.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = 40f;
            layout.flexibleWidth = 1f;
            var rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = new Vector2(0f, 1f); rowRect.anchorMax = Vector2.one;
            rowRect.pivot = new Vector2(.5f, 1f); rowRect.sizeDelta = new Vector2(0f, 40f);

            var label = Text(row.transform, "Label", labelStyle);
            label.text = "안내 문구 표시 (로컬·즉시)";
            Place(label.rectTransform, 0f, .76f, 0f, 0f);

            var buttonObject = new GameObject("Toggle", typeof(RectTransform), typeof(UnityEngine.UI.Button));
            buttonObject.layer = row.layer;
            buttonObject.transform.SetParent(row.transform, false);
            Place((RectTransform)buttonObject.transform, .79f, 1f, 4f, -4f);
            var button = buttonObject.GetComponent<UnityEngine.UI.Button>();
            button.transition = style.transition;
            button.colors = style.colors;
            button.spriteState = style.spriteState;
            var sourceImage = style.targetGraphic as UnityEngine.UI.Image;
            if (sourceImage == null) sourceImage = style.GetComponent<UnityEngine.UI.Image>();
            if (sourceImage != null)
            {
                var image = buttonObject.AddComponent<UnityEngine.UI.Image>();
                image.sprite = sourceImage.sprite;
                image.type = sourceImage.type;
                image.color = sourceImage.color;
                image.material = sourceImage.material;
                image.raycastTarget = true;
                button.targetGraphic = image;
            }
            var stateStyle = style.GetComponentInChildren<UnityEngine.UI.Text>(true) ?? labelStyle;
            var state = Text(buttonObject.transform, "State", stateStyle);
            Place(state.rectTransform, 0f, 1f, 0f, 0f);
            state.alignment = TextAnchor.MiddleCenter;
            if (button.targetGraphic == null) { state.raycastTarget = true; button.targetGraphic = state; }
            var result = row.AddComponent<LabNoticeSettingsRow>();
            result.ToggleButton = button;
            result.stateLabel = state;
            button.onClick.AddListener(result.Toggle);
            result.Refresh();
            return result;
        }

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            if (ToggleButton == null || stateLabel == null) return;
            ToggleButton.interactable = LabNoticeSettings.AvailableInThisBuild;
            stateLabel.text = LabNoticeSettings.Enabled ? "ON" : "OFF";
        }

        private void Toggle()
        {
            LabNoticeSettings.SetEnabled(!LabNoticeSettings.Enabled);
            Refresh();
        }

        private static UnityEngine.UI.Text Text(Transform parent, string name, UnityEngine.UI.Text style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<UnityEngine.UI.Text>();
            text.font = style.font; text.fontSize = style.fontSize; text.fontStyle = style.fontStyle;
            text.color = style.color; text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(8, style.fontSize);
            text.resizeTextMaxSize = style.fontSize;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = new Vector2(left, 0f); rect.anchorMax = new Vector2(right, 1f);
            rect.offsetMin = new Vector2(0f, bottom); rect.offsetMax = new Vector2(0f, top);
        }
    }
}
