using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 야광귀 피버 북 신호 (#414). 피버 동안 "북을 마구 눌러야 한다"는 걸 글 없이 알려 준다. 이 기기 화면에서만 보인다.
    /// - 두근두근: 피버 내내 내 판의 북 그림이 심장 박동처럼 "두-근" 두 번씩 커졌다 돌아온다 (LabOrbBoard.SetDrumPulse).
    /// - 연타 숫자: 피버 중 북을 Inspector 간격(기본 0.8초) 안에 연달아 누르면 그 북 옆에 x2, x3…가 오색으로 톡톡 튀어나오고,
    ///   숫자가 클수록 조금씩 커진다. 간격보다 오래 멈추면 사라지고 다시 1부터 센다.
    /// 화면 표시만 하며 규칙(생성, 스태미나)과 터치는 건드리지 않는다. 박자/크기/간격은 LabConfig Inspector(#414).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabYaguangFeverDrumCue : MonoBehaviour
    {
        private static readonly LabOrbElement[] Colors =
            { LabOrbElement.Fire, LabOrbElement.Earth, LabOrbElement.Metal, LabOrbElement.Water, LabOrbElement.Wood };

        private LabNetwork network;
        private Canvas canvas;
        private LabOrbBoard board;
        private RectTransform tapRect;
        private Text tapText;
        private int tapCount;
        private float lastTapAt = -999f;
        private float tapPopElapsed = -1f;
        private string tapDrum;
        private bool fever;

        /// <summary>지금 연타 수 (테스트·확인용). 연타가 끊겼으면 0.</summary>
        public int TapCount => Time.unscaledTime - lastTapAt <= TapWindow ? tapCount : 0;
        /// <summary>연타 숫자가 보이는지 (테스트·확인용)</summary>
        public bool TapCountVisible => tapRect != null && tapRect.gameObject.activeSelf;

        private float TapWindow => network != null && network.config != null ? network.config.YaguangFeverTapComboSeconds : .8f;

        public void Bind(LabNetwork target, Canvas targetCanvas)
        {
            network = target;
            canvas = targetCanvas != null ? targetCanvas.rootCanvas : null;
        }

        /// <summary>
        /// 심장 박동 모양 0~1: 한 박(1/rate초)마다 "두"(1)와 조금 뒤 작은 "근"(0.6)이 한 번씩 솟았다 가라앉는다.
        /// </summary>
        public static float Heartbeat(float time, float rate)
        {
            if (!(rate > 0f)) return 0f;
            float phase = Mathf.Repeat(time * rate, 1f);
            return Mathf.Max(Bump(phase, 0f, .14f), .6f * Bump(phase, .22f, .14f));
        }

        private static float Bump(float phase, float start, float width)
        {
            float u = (phase - start) / width;
            return u < 0f || u > 1f ? 0f : Mathf.Sin(u * Mathf.PI);
        }

        /// <summary>앞 연타 수 count, 마지막으로 누른 시각 lastAt에서 now에 또 누르면 몇 연타인지 (간격 window 안이면 이어서, 아니면 1).</summary>
        public static int NextTapCount(int count, float lastAt, float now, float window) =>
            now - lastAt <= window && count > 0 ? count + 1 : 1;

        private void Update()
        {
            LabOrbBoard current = network != null ? network.board : null;
            if (current != board)
            {
                if (board != null) { board.DrumTapped -= OnDrumTapped; board.SetDrumPulse(1f); }
                board = current;
                if (board != null) board.DrumTapped += OnDrumTapped;
            }
            fever = network != null && network.StageFeverOn; // #441: 세 스테이지 피버 공통
            if (board == null) return;
            if (!fever)
            {
                board.SetDrumPulse(1f);
                HideTap();
                return;
            }
            LabConfig config = network.config;
            float amount = config != null ? config.YaguangFeverDrumPulseScale : .12f;
            float rate = config != null ? config.YaguangFeverDrumPulseRate : 1.6f;
            board.SetDrumPulse(1f + amount * Heartbeat(Time.unscaledTime, rate));
        }

        private void LateUpdate() => DrawTap();

        private void OnDrumTapped(string orbId)
        {
            if (!fever) return;
            float now = Time.unscaledTime;
            tapCount = NextTapCount(tapCount, lastTapAt, now, TapWindow);
            lastTapAt = now;
            tapDrum = orbId;
            tapPopElapsed = 0f;
        }

        private void DrawTap()
        {
            if (!fever || tapCount < 2 || Time.unscaledTime - lastTapAt > TapWindow)
            {
                if (Time.unscaledTime - lastTapAt > TapWindow) tapCount = 0;
                HideTap();
                return;
            }
            if (!EnsureText() || board == null || !board.TryGetScreenPoint(tapDrum, out Vector2 screen))
            {
                HideTap();
                return;
            }
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Camera eye = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, eye, out Vector2 local))
            {
                HideTap();
                return;
            }
            tapRect.gameObject.SetActive(true);
            tapRect.SetAsLastSibling();
            float size = Mathf.Min(1.8f, 1f + .08f * (tapCount - 2));
            float pop = 1f;
            if (tapPopElapsed >= 0f)
            {
                tapPopElapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(tapPopElapsed / .18f);
                pop = Mathf.Lerp(1.6f, 1f, 1f - (1f - u) * (1f - u));
                if (tapPopElapsed >= .18f) tapPopElapsed = -1f;
            }
            float left = 1f - (Time.unscaledTime - lastTapAt) / Mathf.Max(.01f, TapWindow);
            tapRect.anchoredPosition = local + new Vector2(70f, 60f);
            tapRect.localScale = Vector3.one * size * pop;
            tapRect.localRotation = Quaternion.Euler(0f, 0f, -8f + 4f * Mathf.Sin(Time.unscaledTime * 12f));
            tapText.text = "x" + tapCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Color color = LabPaintPalette.SrgbColorFor(Colors[tapCount % Colors.Length]);
            color.a = Mathf.Clamp01(left * 3f);
            tapText.color = color;
        }

        private void HideTap()
        {
            if (tapRect != null) tapRect.gameObject.SetActive(false);
        }

        private bool EnsureText()
        {
            if (tapText != null) return true;
            if (canvas == null) return false;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect == null) return false;
            tapRect = new GameObject("Yaguang Fever Tap Count", typeof(RectTransform)).GetComponent<RectTransform>();
            tapRect.gameObject.layer = canvasRect.gameObject.layer;
            tapRect.SetParent(canvasRect, false);
            tapRect.anchorMin = tapRect.anchorMax = new Vector2(.5f, .5f);
            tapRect.sizeDelta = new Vector2(220f, 110f);
            tapText = tapRect.gameObject.AddComponent<Text>();
            tapText.font = LabYaguangFeverHud.ShillaFont(canvas);
            tapText.fontSize = 72;
            tapText.fontStyle = FontStyle.Bold;
            tapText.alignment = TextAnchor.MiddleCenter;
            tapText.horizontalOverflow = HorizontalWrapMode.Overflow;
            tapText.verticalOverflow = VerticalWrapMode.Overflow;
            tapText.raycastTarget = false;
            Outline outline = tapRect.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(4f, -4f);
            Shadow shadow = tapRect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.17f, .14f, .12f, .8f);
            shadow.effectDistance = new Vector2(3f, -5f);
            tapRect.gameObject.SetActive(false);
            return true;
        }

        private void OnDisable()
        {
            if (board != null) board.SetDrumPulse(1f);
            HideTap();
        }

        private void OnDestroy()
        {
            if (board != null) board.DrumTapped -= OnDrumTapped;
            if (tapRect != null) Destroy(tapRect.gameObject);
        }
    }
}
