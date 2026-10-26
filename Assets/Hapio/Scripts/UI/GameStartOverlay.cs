using System;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// Scene-independent presentation of the authoritative start gate. GameSession owns
    /// readiness, shared time and lifetime; this view never runs its own countdown clock.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
    public sealed class GameStartOverlay : MonoBehaviour
    {
        public const string ResourcePath = "Hapio/GameStartOverlay";
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private UnityEngine.UI.Image background;
        [SerializeField] private UnityEngine.UI.Text caption;
        [SerializeField] private Color preparingBackground = new Color(.045f, .07f, .095f, 1f);
        [SerializeField] private Color countdownBackground = new Color(0f, 0f, 0f, .25f);
        [SerializeField] private Color textColor = new Color(.98f, .96f, .9f, 1f);
        [SerializeField, Min(12)] private int preparingFontSize = 24;
        [SerializeField, Min(12)] private int countdownFontSize = 88;
        [SerializeField, Min(12)] private int startedFontSize = 56;

        private enum DisplayMode { Hidden, Preparing, Countdown, Started }
        private DisplayMode mode;
        private int lastReady = -1, lastTotal = -1, lastNumber = -1;
        public bool Visible => visibility != null && visibility.alpha > 0f;
        public bool BlocksInput => Visible && visibility.blocksRaycasts;
        public string DisplayText => caption == null ? string.Empty : caption.text;

        public static GameStartOverlay Create(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
                throw new InvalidOperationException("The saved Hapio start overlay prefab is missing.");
            GameObject created = Instantiate(prefab, parent, false);
            created.name = "GameStartOverlay";
            var view = created.GetComponent<GameStartOverlay>();
            if (view == null || !view.HasReferences())
            {
                Destroy(created);
                throw new InvalidOperationException("The saved Hapio start overlay references are incomplete.");
            }
            view.Hide();
            return view;
        }

        private void Awake()
        {
            if (!HasReferences())
                throw new InvalidOperationException("The saved Hapio start overlay references are incomplete.");
            Hide();
        }

        private bool HasReferences() => visibility != null && background != null && caption != null
            && caption.font != null && visibility.gameObject == gameObject
            && background.transform.IsChildOf(transform) && caption.transform.IsChildOf(transform);

        public void ShowPreparing(int ready, int total)
        {
            total = Mathf.Max(1, total);
            ready = Mathf.Clamp(ready, 0, total);
            if (mode == DisplayMode.Preparing && lastReady == ready && lastTotal == total) return;
            lastReady = ready; lastTotal = total;
            Apply(DisplayMode.Preparing, "전투 준비 중…\n\n준비 " + ready + "/" + total,
                preparingFontSize, preparingBackground, true);
        }

        public void ShowCountdown(int seconds)
        {
            if (seconds < 1 || seconds > 3) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (mode == DisplayMode.Countdown && lastNumber == seconds) return;
            lastNumber = seconds;
            Apply(DisplayMode.Countdown, seconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                countdownFontSize, countdownBackground, true);
        }

        public void ShowStarted()
        {
            if (mode == DisplayMode.Started) return;
            Apply(DisplayMode.Started, "시작!", startedFontSize, Color.clear, false);
        }

        public void Hide()
        {
            mode = DisplayMode.Hidden;
            if (visibility == null) return;
            visibility.alpha = 0f;
            visibility.blocksRaycasts = visibility.interactable = false;
            if (background != null) background.raycastTarget = false;
            if (caption != null) caption.text = string.Empty;
        }

        private void Apply(DisplayMode value, string text, int fontSize, Color color, bool block)
        {
            mode = value;
            visibility.alpha = 1f;
            visibility.blocksRaycasts = block;
            visibility.interactable = false;
            background.color = color;
            background.raycastTarget = block;
            caption.text = text;
            caption.fontSize = fontSize;
            caption.color = textColor;
            caption.raycastTarget = false;
        }
    }
}
