using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace C6Lab.Experiments
{
    /// <summary>Saved prefab references. The opaque modal blocks the existing result buttons underneath it.</summary>
    public sealed class GourdResultViewerUI : MonoBehaviour
    {
        public Button entryButton;
        public GameObject viewerPanel;
        public RawImage previewImage;
        public Button closeButton, resetButton;
        public Text statusText;
        public GameObject experimentTag;
        [Range(128, 2048)] public int previewTextureSize = 768;
        private GourdViewerGestureSurface gestures;
        public bool HasRequiredReferences => entryButton != null && viewerPanel != null && previewImage != null
            && closeButton != null && resetButton != null && statusText != null;
        public bool IsShowing => viewerPanel != null && viewerPanel.activeSelf;

        public void Bind(Action open, Action close, Action reset, Action<Vector2> rotate, Action<float> zoom)
        {
            entryButton.onClick.AddListener(() => open());
            closeButton.onClick.AddListener(() => close());
            resetButton.onClick.AddListener(() => { gestures?.ResetGesture(); reset(); });
            gestures = previewImage.gameObject.GetComponent<GourdViewerGestureSurface>();
            if (gestures == null) gestures = previewImage.gameObject.AddComponent<GourdViewerGestureSurface>();
            gestures.Bind(rotate, zoom);
            Hide();
            SetEntryVisible(false);
        }
        public void SetEntryVisible(bool visible) { if (entryButton != null) entryButton.gameObject.SetActive(visible); }
        public void Show(RenderTexture texture)
        {
            previewImage.texture = texture;
            statusText.text = "100% PAINTED · LOCAL VIEW";
            gestures.ResetGesture();
            viewerPanel.SetActive(true);
            SetEntryVisible(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        public void Hide()
        {
            if (previewImage != null) previewImage.texture = null;
            if (viewerPanel != null) viewerPanel.SetActive(false);
            gestures?.ResetGesture();
        }
    }

}
