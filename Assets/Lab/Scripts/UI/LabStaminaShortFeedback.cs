using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 스태미나 부족 표시 (#418). 북을 눌렀는데 스태미나가 모자라 Host가 생성을 거절하면, 누른 사람 화면의 스태미나 게이지가
    /// 지지직 흔들리고 지금 충전 중인 칸(차 있는 부분)이 순간 빨갛게 빛난다. 다시 누르면 처음부터 다시 낸다.
    /// #718부터 조합 영역 전체(판 그림·구슬·북, LabOrbHudComposition)도 같은 시간 동안 함께 흔들린다. 그림만 움직이고 판의 물리·
    /// 터치 위치는 그대로다. 흔들리는 거리는 프로필의 "Short Board Shake Distance".
    /// 게이지 원본 그림·채움 값은 바꾸지 않고, 흔들림이 끝나면 게이지 자리를 그대로 되돌린다. 시간·세기·색은
    /// Resources/Stamina/StaminaFeedbackProfile(#412 소비 표시와 같은 프로필)의 "스태미나 부족" 칸에서 조절한다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(610)] // #412 표시(600)보다 뒤에 그 위로 올린다.
    public sealed class LabStaminaShortFeedback : MonoBehaviour
    {
        [SerializeField] private LabStaminaFeedbackProfile profile;
        private LabStaminaFeedbackProfile fallback;
        private LabNetwork network, subscribed;
        private UnityEngine.UI.Image source, flash;
        private RectTransform bar;
        private LabOrbHudComposition composition;
        private Vector2 barPosition;
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
        /// <summary>조합 영역 전체가 밀려난 거리 (Canvas 단위, #718)</summary>
        public Vector2 BoardShakeOffset { get; private set; }
        public float FlashAlpha { get; private set; }
        public UnityEngine.UI.Image FlashImage => flash;

        public void SetProfile(LabStaminaFeedbackProfile value) => profile = value;

        public void Bind(LabNetwork target, UnityEngine.UI.Image staminaFill)
        {
            Unsubscribe(); Stop();
            network = target;
            if (source != staminaFill) { DestroyFlash(); source = staminaFill; composition = null; }
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
            if (age < 0f) barPosition = bar.anchoredPosition;
            age = 0f;
            ShortCount++;
            EnsureFlash();
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
            if (age >= Mathf.Max(shakeSeconds, flashSeconds)) { Stop(); return; }
            float distance = settings.ShortShakeRatio * Mathf.Abs(source.rectTransform.rect.height);
            ShakeOffset = ShakeAt(age, shakeSeconds, distance, settings.ShortShakeFrequency);
            bar.anchoredPosition = barPosition + ShakeOffset;
            BoardShakeOffset = ShakeAt(age, shakeSeconds, settings.ShortBoardShakeDistance, settings.ShortShakeFrequency);
            Composition()?.SetBoardShake(BoardShakeOffset);
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

        /// <summary>빨간 빛의 세기 (0~1): 금방 차올랐다가 부드럽게 빠진다.</summary>
        public static float FlashAmount(float age, float seconds)
        {
            if (seconds <= 0f || age < 0f || age >= seconds) return 0f;
            float u = age / seconds;
            return u < .15f ? u / .15f : 1f - Mathf.SmoothStep(0f, 1f, (u - .15f) / .85f);
        }

        /// <summary>The board picture this gauge belongs to: its HUD's composition, or the battle's only one.</summary>
        private LabOrbHudComposition Composition()
        {
            if (composition != null) return composition;
            LabHud hud = source != null ? source.GetComponentInParent<LabHud>(true) : null;
            composition = hud != null ? hud.GetComponent<LabOrbHudComposition>() : null;
            if (composition == null) composition = FindFirstObjectByType<LabOrbHudComposition>();
            return composition;
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
            if (age >= 0f && bar != null) bar.anchoredPosition = barPosition;
            age = -1f;
            ShakeOffset = Vector2.zero;
            if (BoardShakeOffset != Vector2.zero) Composition()?.SetBoardShake(Vector2.zero);
            BoardShakeOffset = Vector2.zero;
            FlashAlpha = 0f;
            if (flash != null && flash.gameObject.activeSelf) flash.gameObject.SetActive(false);
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
            Unsubscribe(); Stop(); DestroyFlash();
            if (fallback != null)
            {
                if (Application.isPlaying) Destroy(fallback); else DestroyImmediate(fallback);
            }
        }
    }
}
