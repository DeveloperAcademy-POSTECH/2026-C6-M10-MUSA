using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 스태미나 부족 표시 (#418). 북을 눌렀는데 스태미나가 모자라 Host가 생성을 거절하면, 누른 사람 화면의 스태미나 게이지가
    /// 지지직 흔들리고 지금 충전 중인 칸(차 있는 부분)이 순간 빨갛게 빛난다. 다시 누르면 처음부터 다시 낸다.
    /// #718부터 게이지는 흔들리는 동안 살짝 커졌다가 원래 크기로 돌아오고, 화면 가운데("시작!" 자리)에 "기운이 부족하다"와 빨간 느낌표
    /// (Resources/Stamina/StaminaShortWarning)가 한 덩어리로 톡 튀어나와 게이지와 함께 흔들리다 사라지며, 화면 가장자리가 매혹의
    /// 분홍빛처럼 빨갛게 잠깐 번쩍한다. 모두 누른 사람 화면에만 보이고 터치를 막지 않는다.
    /// 게이지 원본 그림·채움 값은 바꾸지 않고, 흔들림이 끝나면 게이지 자리를 그대로 되돌린다. 시간·세기·색은
    /// Resources/Stamina/StaminaFeedbackProfile(#412 소비 표시와 같은 프로필)의 "스태미나 부족" 칸에서 조절한다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(610)] // #412 표시(600)보다 뒤에 그 위로 올린다.
    public sealed class LabStaminaShortFeedback : MonoBehaviour
    {
        public const string WarningText = "기운이 부족하다";
        public const string WarningIconPath = "Stamina/StaminaShortWarning";
        private const float WarningFadeSeconds = .2f;

        [SerializeField] private LabStaminaFeedbackProfile profile;
        private LabStaminaFeedbackProfile fallback;
        private LabNetwork network, subscribed;
        private UnityEngine.UI.Image source, flash;
        private RectTransform bar;
        private Vector2 barPosition;
        private Vector3 barScale = Vector3.one;
        // #718: the warning words with their red mark in the middle of the screen, and the red screen edges.
        private RectTransform warningLayer, warning;
        private CanvasGroup warningGroup;
        private UnityEngine.UI.Text warningText;
        private UnityEngine.UI.Image warningIcon;
        private UnityEngine.UI.RawImage edges;
        private float age = -1f;
        private string session;
        private uint round;
        private bool hasIdentity;

        public LabStaminaFeedbackProfile Profile
        {
            get
            {
                if (profile != null) return profile;
                if (fallback != null) return fallback;
                profile = Resources.Load<LabStaminaFeedbackProfile>(LabStaminaFeedbackProfile.ResourcesPath);
                if (profile != null) return profile;
                fallback = ScriptableObject.CreateInstance<LabStaminaFeedbackProfile>();
                fallback.hideFlags = HideFlags.HideAndDontSave;
                return fallback;
            }
        }
        /// <summary>부족 표시를 낸 횟수 (테스트·확인용)</summary>
        public int ShortCount { get; private set; }
        public bool Playing => age >= 0f;
        public Vector2 ShakeOffset { get; private set; }
        /// <summary>게이지가 지금 커진 배율 (1 = 그대로, #718)</summary>
        public float GaugeScale { get; private set; } = 1f;
        /// <summary>가운데 경고("기운이 부족하다" + 느낌표)가 보이는지 (#718)</summary>
        public bool WarningVisible => warning != null && warning.gameObject.activeInHierarchy && warningGroup.alpha > 0f;
        public string WarningLabel => warningText != null ? warningText.text : string.Empty;
        public Sprite WarningIcon => warningIcon != null ? warningIcon.sprite : null;
        /// <summary>경고가 흔들려 밀려난 거리 (#718)</summary>
        public Vector2 WarningShake { get; private set; }
        public float WarningScale { get; private set; }
        /// <summary>화면 가장자리 빨간빛의 지금 진하기 (#718)</summary>
        public float EdgeAlpha { get; private set; }
        public float FlashAlpha { get; private set; }
        public UnityEngine.UI.Image FlashImage => flash;

        public void SetProfile(LabStaminaFeedbackProfile value) => profile = value;

        public void Bind(LabNetwork target, UnityEngine.UI.Image staminaFill)
        {
            Unsubscribe(); Stop();
            network = target;
            if (source != staminaFill) { DestroyFlash(); DestroyWarning(); source = staminaFill; }
            bar = source != null ? source.transform.parent as RectTransform : null;
            hasIdentity = false;
            Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            if (!isActiveAndEnabled || network == null || subscribed == network) return;
            Unsubscribe(); subscribed = network; subscribed.LocalStaminaShort += Play;
        }
        private void Unsubscribe()
        {
            if (subscribed != null) subscribed.LocalStaminaShort -= Play;
            subscribed = null;
        }

        /// <summary>부족 표시를 처음부터 낸다. 이미 흔들리는 중이면 게이지의 원래 자리를 그대로 두고 다시 시작한다.</summary>
        public void Play()
        {
            if (!Profile.ShortEnabled || source == null || bar == null) return;
            if (network != null)
            {
                LabSnapshot snapshot = network.Snapshot;
                if (snapshot == null || snapshot.phase != LabPhase.Playing) return;
                ObserveIdentity(snapshot);
            }
            if (age < 0f) { barPosition = bar.anchoredPosition; barScale = bar.localScale; }
            age = 0f;
            ShortCount++;
            EnsureFlash();
            EnsureWarning();
            LayOutWarning();
            if (warningLayer != null) warningLayer.SetAsLastSibling(); // over the board and the HUD, once per tap
        }

        private void LateUpdate()
        {
            if (network != null)
            {
                LabSnapshot snapshot = network.Snapshot;
                if (snapshot == null || snapshot.phase != LabPhase.Playing) { Stop(); return; }
                ObserveIdentity(snapshot);
            }
            Advance(Time.unscaledDeltaTime);
        }

        /// <summary>표시를 deltaTime초 진행한다. 시간이 다 되면 게이지를 원래 자리로 되돌리고 빨간 빛을 끈다.</summary>
        public void Advance(float deltaTime)
        {
            if (age < 0f) return;
            if (source == null || bar == null) { Stop(); return; }
            age += float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ? 0f : Mathf.Max(0f, deltaTime);
            LabStaminaFeedbackProfile settings = Profile;
            float shakeSeconds = settings.ShortShakeSeconds, flashSeconds = settings.ShortFlashSeconds;
            float warningSeconds = settings.ShortWarningEnabled ? settings.ShortWarningSeconds : 0f;
            float edgeSeconds = settings.ShortEdgeSeconds;
            if (age >= Mathf.Max(Mathf.Max(shakeSeconds, flashSeconds), Mathf.Max(warningSeconds, edgeSeconds))) { Stop(); return; }
            float distance = settings.ShortShakeRatio * Mathf.Abs(source.rectTransform.rect.height);
            ShakeOffset = ShakeAt(age, shakeSeconds, distance, settings.ShortShakeFrequency);
            bar.anchoredPosition = barPosition + ShakeOffset;
            // #718: the gauge swells a little and settles back while it shakes.
            GaugeScale = 1f + (settings.ShortGaugePopScale - 1f) * PopAmount(age, shakeSeconds);
            bar.localScale = barScale * GaugeScale;
            ShowWarning(settings, warningSeconds, shakeSeconds);
            EdgeAlpha = settings.ShortEdgeColor.a * FlashAmount(age, edgeSeconds);
            ShowEdges(settings.ShortEdgeColor);
            FlashAlpha = settings.ShortFlashColor.a * FlashAmount(age, flashSeconds);
            ShowFlash(settings.ShortFlashColor);
        }

        /// <summary>
        /// age초에 게이지가 밀려난 거리. 1초에 frequency번 다른 자리로 튀어(지지직) 끝날수록 작아진다. 같은 시각이면 늘 같은 값이다.
        /// </summary>
        public static Vector2 ShakeAt(float age, float seconds, float distance, float frequency)
        {
            if (age < 0f || age >= seconds || !(distance > 0f)) return Vector2.zero;
            float decay = 1f - age / Mathf.Max(.001f, seconds);
            int step = Mathf.FloorToInt(age * Mathf.Max(1f, frequency));
            return new Vector2(Jitter(step), .4f * Jitter(step + 97)) * (distance * decay);
        }

        /// <summary>게이지가 커진 정도 (0~1): 흔들림의 앞 30%에 커지고 나머지 동안 원래 크기로 돌아온다 (#718).</summary>
        public static float PopAmount(float age, float seconds)
        {
            if (seconds <= 0f || age < 0f || age >= seconds) return 0f;
            float u = age / seconds;
            return u < .3f ? Mathf.SmoothStep(0f, 1f, u / .3f) : 1f - Mathf.SmoothStep(0f, 1f, (u - .3f) / .7f);
        }

        /// <summary>경고가 튀어나오는 크기: 작게 나와 살짝 커졌다가(1.15) 제 크기로 (#718).</summary>
        public static float WarningPop(float age)
        {
            if (age < 0f) return 0f;
            if (age < .12f) return Mathf.Lerp(.6f, 1.15f, Mathf.SmoothStep(0f, 1f, age / .12f));
            if (age < .24f) return Mathf.Lerp(1.15f, 1f, Mathf.SmoothStep(0f, 1f, (age - .12f) / .12f));
            return 1f;
        }

        /// <summary>빨간 빛의 세기 (0~1): 금방 차올랐다가 부드럽게 빠진다.</summary>
        public static float FlashAmount(float age, float seconds)
        {
            if (seconds <= 0f || age < 0f || age >= seconds) return 0f;
            float u = age / seconds;
            return u < .15f ? u / .15f : 1f - Mathf.SmoothStep(0f, 1f, (u - .15f) / .85f);
        }

        // 걸음마다 -1~1 사이의 정해진 값. 0은 피해 늘 눈에 띄게 움직인다.
        private static float Jitter(int step)
        {
            float value = Mathf.Repeat(Mathf.Sin(step * 12.9898f + 1.618f) * 43758.547f, 1f) * 2f - 1f;
            return value >= 0f ? Mathf.Lerp(.45f, 1f, value) : Mathf.Lerp(-.45f, -1f, -value);
        }

        private void EnsureFlash()
        {
            if (source == null || bar == null) return;
            if (flash != null && flash.transform.parent == bar) return;
            DestroyFlash();
            var go = new GameObject("Stamina Short Red Flash", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.layer = source.gameObject.layer;
            go.transform.SetParent(bar, false);
            flash = go.GetComponent<UnityEngine.UI.Image>();
            flash.raycastTarget = false;
            go.SetActive(false);
        }

        /// <summary>원본 게이지와 같은 칸 모양으로, 지금 차 있는 만큼(충전 중인 칸)만 빨갛게 덮는다.</summary>
        private void ShowFlash(Color color)
        {
            if (flash == null) return;
            bool visible = FlashAlpha > .001f;
            if (flash.gameObject.activeSelf != visible) flash.gameObject.SetActive(visible);
            if (!visible) return;
            RectTransform from = source.rectTransform, to = flash.rectTransform;
            to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax; to.pivot = from.pivot;
            to.sizeDelta = from.sizeDelta; to.anchoredPosition3D = from.anchoredPosition3D;
            to.localRotation = from.localRotation; to.localScale = from.localScale;
            flash.sprite = source.sprite; flash.preserveAspect = source.preserveAspect;
            flash.type = UnityEngine.UI.Image.Type.Filled;
            flash.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            flash.fillOrigin = source.fillOrigin;
            flash.fillAmount = source.fillAmount;
            color.a = FlashAlpha;
            flash.color = color;
            to.SetAsLastSibling();
        }

        private void Stop()
        {
            if (age >= 0f && bar != null) { bar.anchoredPosition = barPosition; bar.localScale = barScale; }
            age = -1f;
            ShakeOffset = WarningShake = Vector2.zero;
            GaugeScale = 1f;
            WarningScale = 0f;
            FlashAlpha = EdgeAlpha = 0f;
            if (flash != null && flash.gameObject.activeSelf) flash.gameObject.SetActive(false);
            if (warningLayer != null && warningLayer.gameObject.activeSelf) warningLayer.gameObject.SetActive(false);
        }

        // ---------- #718: "기운이 부족하다" + red mark, and the red screen edges ----------

        private void EnsureWarning()
        {
            if (warningLayer != null || source == null) return;
            Canvas canvas = source.canvas != null ? source.canvas.rootCanvas : null;
            RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            if (canvasRect == null) return;
            warningLayer = NewRect("Stamina Short Warning", canvasRect);
            warningLayer.anchorMin = Vector2.zero;
            warningLayer.anchorMax = Vector2.one;
            warningLayer.offsetMin = warningLayer.offsetMax = Vector2.zero;

            // The red edges under the words, like the charm's pink (LabCharmHeartRain), only red and only for a moment.
            edges = NewRect("Red Edges", warningLayer).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            edges.rectTransform.anchorMin = Vector2.zero;
            edges.rectTransform.anchorMax = Vector2.one;
            edges.rectTransform.offsetMin = edges.rectTransform.offsetMax = Vector2.zero;
            edges.texture = LabCharmHeartRain.Vignette();
            edges.raycastTarget = false;
            edges.color = Color.clear;

            warning = NewRect("Warning", warningLayer);
            warningGroup = warning.gameObject.AddComponent<CanvasGroup>();
            warningGroup.blocksRaycasts = warningGroup.interactable = false;
            warningText = NewRect("Words", warning).gameObject.AddComponent<UnityEngine.UI.Text>();
            Font font = LabYaguangFeverHud.ShillaFont(canvas);
            warningText.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            warningText.fontStyle = FontStyle.Bold;
            warningText.alignment = TextAnchor.MiddleCenter;
            warningText.horizontalOverflow = HorizontalWrapMode.Overflow;
            warningText.verticalOverflow = VerticalWrapMode.Overflow;
            warningText.raycastTarget = false;
            warningText.text = WarningText;
            warningIcon = NewRect("Red Mark", warning).gameObject.AddComponent<UnityEngine.UI.Image>();
            warningIcon.sprite = Resources.Load<Sprite>(WarningIconPath);
            warningIcon.preserveAspect = true;
            warningIcon.raycastTarget = false;
            if (warningIcon.sprite == null) warningIcon.enabled = false;
            warningLayer.gameObject.SetActive(false);
        }

        /// <summary>The words and the mark side by side as one group, centred on the profile's place.</summary>
        private void LayOutWarning()
        {
            if (warning == null) return;
            LabStaminaFeedbackProfile settings = Profile;
            float size = settings.ShortWarningFontSize;
            warning.anchorMin = warning.anchorMax = settings.ShortWarningAnchor;
            warning.pivot = new Vector2(.5f, .5f);
            warningText.fontSize = Mathf.RoundToInt(size);
            warningText.color = settings.ShortWarningTextColor; // read at each tap, so a changed profile shows at once
            float textWidth = warningText.preferredWidth;
            float iconHeight = size * 1.25f;
            Sprite mark = warningIcon.sprite;
            float iconWidth = mark != null && mark.rect.height > 0f ? iconHeight * mark.rect.width / mark.rect.height : 0f;
            float gap = mark != null ? size * .12f : 0f;
            float total = textWidth + gap + iconWidth;
            warning.sizeDelta = new Vector2(total, Mathf.Max(iconHeight, size * 1.4f));
            warningText.rectTransform.sizeDelta = new Vector2(textWidth, size * 1.4f);
            warningText.rectTransform.anchoredPosition = new Vector2(-total * .5f + textWidth * .5f, 0f);
            warningIcon.rectTransform.sizeDelta = new Vector2(iconWidth, iconHeight);
            warningIcon.rectTransform.anchoredPosition = new Vector2(total * .5f - iconWidth * .5f, size * .05f);
        }

        private void ShowWarning(LabStaminaFeedbackProfile settings, float warningSeconds, float shakeSeconds)
        {
            if (warningLayer == null) return;
            if (!warningLayer.gameObject.activeSelf) warningLayer.gameObject.SetActive(true);
            bool on = age < warningSeconds;
            if (warning.gameObject.activeSelf != on) warning.gameObject.SetActive(on);
            if (!on) { WarningScale = 0f; WarningShake = Vector2.zero; return; }
            // Shakes with the gauge (same steps), pops in small then a little big, and fades over its last moment.
            WarningShake = ShakeAt(age, shakeSeconds, settings.ShortWarningShakeDistance, settings.ShortShakeFrequency);
            WarningScale = WarningPop(age);
            warning.anchoredPosition = WarningShake;
            warning.localScale = Vector3.one * WarningScale;
            warningGroup.alpha = 1f - Mathf.InverseLerp(warningSeconds - WarningFadeSeconds, warningSeconds, age);
        }

        private void ShowEdges(Color color)
        {
            if (edges == null) return;
            color.a = EdgeAlpha;
            edges.color = color;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            return rect;
        }

        private void DestroyWarning()
        {
            if (warningLayer == null) return;
            if (Application.isPlaying) Destroy(warningLayer.gameObject); else DestroyImmediate(warningLayer.gameObject);
            warningLayer = warning = null;
            warningGroup = null; warningText = null; warningIcon = null; edges = null;
        }

        private void ObserveIdentity(LabSnapshot snapshot)
        {
            if (hasIdentity && (session != snapshot.sessionId || round != snapshot.round)) Stop();
            session = snapshot.sessionId; round = snapshot.round; hasIdentity = true;
        }

        private void DestroyFlash()
        {
            if (flash == null) return;
            if (Application.isPlaying) Destroy(flash.gameObject); else DestroyImmediate(flash.gameObject);
            flash = null;
        }

        private void OnDisable() { Unsubscribe(); Stop(); }
        private void OnDestroy()
        {
            Unsubscribe(); Stop(); DestroyFlash(); DestroyWarning();
            if (fallback != null)
            {
                if (Application.isPlaying) Destroy(fallback); else DestroyImmediate(fallback);
            }
        }
    }
}
