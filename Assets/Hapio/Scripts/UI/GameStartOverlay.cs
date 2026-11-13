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
        [Header("시작·끝 문구 (#717): 봉인 시작! · 봉인 성공! · 시간 초과! · 경기 종료!")]
        [Tooltip("시작·끝 문구 글자 색 (시안의 진홍). \"시간 초과!\"만 아래 파란색")]
        [SerializeField] private Color finishedTextColor = new Color32(0x8A, 0x12, 0x06, 0xFF);
        [Tooltip("\"시간 초과!\" 글자 색 (짙은 청색)")]
        [SerializeField] private Color timeUpTextColor = new Color32(0x16, 0x4B, 0x8C, 0xFF);
        [Tooltip("시작·끝 문구 글자 가장자리 색 (시안의 연한 크림)")]
        [SerializeField] private Color finishedOutlineColor = new Color32(0xFC, 0xE8, 0xC8, 0xFF);
        [Tooltip("가장자리 두께 (Canvas 기준 픽셀)")]
        [SerializeField, Min(0f)] private float finishedOutlineWidth = 3f;
        [Tooltip("끝 문구(\"경기 종료!\"·\"봉인 성공!\"·\"시간 초과!\") 글자 크기. 기준 화면 너비(390)에서 한 줄로 들어가는 크기")]
        [SerializeField, Min(12)] private int finishedFontSize = 64;
        [Tooltip("\"봉인 시작!\" 글자 크기")]
        [SerializeField, Min(12)] private int sealStartedFontSize = 64;
        [Tooltip("끝 문구 뒤를 덮는 막의 색. 카운트다운과 같은 옅은 그늘이고, 다음 화면(막타 연출·결과·실패)까지 전투 화면을 누르지 못하게 막는다")]
        [SerializeField] private Color finishedBackground = new Color(0f, 0f, 0f, .25f);

        /// <summary>땅따먹기가 끝났을 때</summary>
        public const string FinishedText = "경기 종료!";
        /// <summary>스토리 스테이지(1~3)가 시작할 때, 3·2·1 뒤</summary>
        public const string SealStartedText = "봉인 시작!";
        /// <summary>스토리 스테이지(1~3)를 시간 안에 다 칠했을 때, 막타 연출 전</summary>
        public const string SealSucceededText = "봉인 성공!";
        /// <summary>스토리 스테이지(1~3)를 다 칠하지 못하고 시간이 끝났을 때, 실패 화면 전</summary>
        public const string TimeUpText = "시간 초과!";

        private enum DisplayMode { Hidden, Preparing, Countdown, Started, Finished, SealStarted, SealSucceeded, TimeUp }
        private DisplayMode mode;
        private int lastReady = -1, lastTotal = -1, lastNumber = -1;
        private UnityEngine.UI.Outline finishedOutline;
        public bool Visible => visibility != null && visibility.alpha > 0f;
        public bool BlocksInput => Visible && visibility.blocksRaycasts;
        public string DisplayText => caption == null ? string.Empty : caption.text;
        /// <summary>끝 문구("경기 종료!"·"봉인 성공!"·"시간 초과!")가 떠 있는지 (#717)</summary>
        public bool ShowsFinished => (mode == DisplayMode.Finished || mode == DisplayMode.SealSucceeded
            || mode == DisplayMode.TimeUp) && Visible;
        /// <summary>지금 글자 색 (검사·확인용)</summary>
        public Color CaptionColor => caption == null ? Color.clear : caption.color;
        /// <summary>지금 글자 가장자리 색. 가장자리가 꺼져 있으면 투명 (검사·확인용)</summary>
        public Color CaptionOutlineColor => finishedOutline != null && finishedOutline.enabled ? finishedOutline.effectColor : Color.clear;

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

        /// <summary>A preparing cover with its own words instead of the ready count (the tutorial, #477).</summary>
        public void ShowPreparing(string message)
        {
            if (mode == DisplayMode.Preparing && lastReady < 0 && caption != null && caption.text == message) return;
            lastReady = lastTotal = -1;
            Apply(DisplayMode.Preparing, message ?? string.Empty, preparingFontSize, preparingBackground, true);
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

        /// <summary>
        /// 스토리 스테이지가 시작할 때 "시작!" 대신 "봉인 시작!" (#717): 끝 문구와 같은 진홍 글자·크림색 가장자리. "시작!"처럼
        /// 화면을 막지 않는다.
        /// </summary>
        public void ShowSealStarted()
        {
            if (mode == DisplayMode.SealStarted) return;
            ApplyStyled(DisplayMode.SealStarted, SealStartedText, sealStartedFontSize, Color.clear, false, finishedTextColor);
        }

        /// <summary>
        /// 땅따먹기가 끝난 순간의 "경기 종료!" (#717): 진홍 글자에 크림색 가장자리, 옅은 그늘. 결과 화면이 열릴 때까지 화면을 덮어
        /// 전투 화면의 버튼이 눌리지 않는다.
        /// </summary>
        public void ShowFinished()
        {
            if (mode == DisplayMode.Finished) return;
            ApplyStyled(DisplayMode.Finished, FinishedText, finishedFontSize, finishedBackground, true, finishedTextColor);
        }

        /// <summary>
        /// 스토리 스테이지를 시간 안에 다 칠한 순간의 "봉인 성공!" (#717, 진홍). 막타 연출이 시작될 때까지 "경기 종료!"처럼 화면을 덮는다.
        /// </summary>
        public void ShowSealSucceeded()
        {
            if (mode == DisplayMode.SealSucceeded) return;
            ApplyStyled(DisplayMode.SealSucceeded, SealSucceededText, finishedFontSize, finishedBackground, true, finishedTextColor);
        }

        /// <summary>
        /// 스토리 스테이지를 다 칠하지 못하고 시간이 끝난 순간의 "시간 초과!" (#717, 짙은 청색). 막타 연출 없이 실패 화면이 뜰 때까지
        /// 화면을 덮는다.
        /// </summary>
        public void ShowTimeUp()
        {
            if (mode == DisplayMode.TimeUp) return;
            ApplyStyled(DisplayMode.TimeUp, TimeUpText, finishedFontSize, finishedBackground, true, timeUpTextColor);
        }

        private void ApplyStyled(DisplayMode value, string text, int fontSize, Color background, bool block, Color color)
        {
            Apply(value, text, fontSize, background, block);
            caption.color = color;
            if (finishedOutline == null) finishedOutline = caption.GetComponent<UnityEngine.UI.Outline>();
            if (finishedOutline == null) finishedOutline = caption.gameObject.AddComponent<UnityEngine.UI.Outline>();
            finishedOutline.effectColor = finishedOutlineColor;
            finishedOutline.effectDistance = new Vector2(finishedOutlineWidth, -finishedOutlineWidth);
            finishedOutline.useGraphicAlpha = true;
            finishedOutline.enabled = finishedOutlineWidth > 0f;
        }

        public void Hide()
        {
            mode = DisplayMode.Hidden;
            if (visibility == null) return;
            visibility.alpha = 0f;
            visibility.blocksRaycasts = visibility.interactable = false;
            if (background != null) background.raycastTarget = false;
            if (caption != null) caption.text = string.Empty;
            if (finishedOutline != null) finishedOutline.enabled = false;
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
            // Only "경기 종료!" has an edge; every other caption keeps the plain saved look.
            if (finishedOutline != null) finishedOutline.enabled = false;
        }
    }
}
