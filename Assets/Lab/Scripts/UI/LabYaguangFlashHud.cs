using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 야광귀 섬광 (#268)의 화면. 섬광을 맞을 사람 자신의 화면에만 그린다(DEV 대상 ALL이면 모두, ONE이면 한 명).
    /// - 예고: 모든 기기의 박 옆 말풍선에 야광귀의 두 눈이 뜨고(`LabGourdAlert.ShowEyes`), 예고 후반에 눈이 갑자기
    ///   노랗게 빛난다. 맞을 사람 화면은 눈이 빛나는 만큼 가장자리부터 노란 빛이 차오르고, 화면 가운데에
    ///   남은 초가 3 → 2 → 1 숫자로 크게 톡톡 튀어나온다 (#301).
    /// - 섬광: 화면 전체가 하얗게 번쩍했다가 빠르게 빠지고, 진동이 한 번 온다. 맞은 사람 화면의 박 옆 말풍선에
    ///   야광귀가 "안보이지롱~" 하고 놀린다 (#301).
    /// - 해제 (#301): 눈부신 사람 화면 가운데에 좌우로 흔들리는 폰 그림 + 게이지가 뜬다(문구는 Inspector에서 넣을 수 있다). 폰을 세게 흔들
    ///   때마다 하얀 막이 조금씩 걷히고, DEV 횟수(기본 6번)를 채우면 Host에 알려 그 사람 눈부심만 풀린다. 풀리면
    ///   말풍선에 "으악 어지러워~"가 뜬다. Editor·DEV 빌드에서는 K 키 한 번이 한 번 흔들기다.
    /// - 눈부심·회복: 판의 음양·결합 구슬과 아이템을 하얗게 덮고(북 제외), 회복 동안 색을 천천히 돌려준다.
    /// 시각은 Host 스냅샷(flashWarningAt/flashAt/flashLastAt)을, 길이는 DEV 값을 쓴다. 화면 표시만 하며 터치를 막지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabYaguangFlashHud : MonoBehaviour
    {
        /// <summary>번쩍이는 하얀 화면이 빠지는 데 걸리는 시간 (초)</summary>
        public const float BurstSeconds = .3f;
        private static readonly Color EdgeColor = new Color(1f, .9f, .45f, 1f);
        private static readonly Color BurstColor = new Color(1f, .99f, .95f, 1f);

        [Tooltip("예고 빛이 가장 밝을 때의 불투명도")]
        [SerializeField, Range(0f, 1f)] private float edgeMaxAlpha = .75f;
        [Tooltip("눈부신 동안 화면 전체에 남는 옅은 하얀 막의 불투명도")]
        [SerializeField, Range(0f, .5f)] private float veilAlpha = .12f;
        [Header("예고 카운트다운 (#301)")]
        [Tooltip("섬광 예고 동안 맞을 사람 화면에 남은 초(3 → 2 → 1)를 보여줄지")]
        [SerializeField] private bool showCountdown = true;
        [Tooltip("카운트다운 숫자 위치 (0~1, 왼쪽 아래가 0)")]
        [SerializeField] private Vector2 countdownAnchor = new Vector2(.5f, .58f);
        [Tooltip("카운트다운 숫자 크기 (Canvas 기준 픽셀)")]
        [SerializeField, Range(24, 240)] private int countdownSize = 120;
        [Tooltip("카운트다운 숫자 색")]
        [SerializeField] private Color countdownColor = new Color(1f, .86f, .25f, 1f);
        [Tooltip("숫자가 바뀔 때 톡 튀어나오는 시간 (초)")]
        [SerializeField, Range(.05f, .8f)] private float countdownPopSeconds = .25f;
        private static readonly Color CountdownInk = new Color32(43, 36, 32, 255);
        [Header("섬광 말풍선 (#301)")]
        [Tooltip("섬광을 맞은 직후 박 옆 말풍선에 야광귀가 하는 말 (맞은 사람 화면만). 비우면 안 띄운다.")]
        [SerializeField] private string tauntText = "안보이지롱~";
        [Tooltip("놀리는 말풍선이 떠 있는 시간 (초)")]
        [SerializeField, Range(.3f, 6f)] private float tauntSeconds = 2f;
        [Tooltip("폰을 흔들어 눈부심을 풀었을 때 말풍선 (푼 사람 화면만). 비우면 안 띄운다.")]
        [SerializeField] private string caughtText = "으악 어지러워~";
        [Tooltip("풀린 말풍선이 떠 있는 시간 (초)")]
        [SerializeField, Range(.3f, 6f)] private float caughtSeconds = 1.5f;
        [Header("흔들어 풀기 (#301)")]
        [Tooltip("흔들림이 중력의 몇 배를 넘으면 한 번 흔든 것으로 칠지. 작을수록 살살 흔들어도 센다.")]
        [SerializeField, Range(.3f, 3f)] private float shakeThreshold = 1.6f;
        [Tooltip("한 번 센 뒤 다시 세기까지 기다리는 시간 (초)")]
        [SerializeField, Range(.05f, .5f)] private float shakeMinGap = .25f;
        [Tooltip("화면 전체의 옅은 하얀 막이 돌아온 구슬 비율만큼 옅어지는 빠르기 (초당 비율). 한 번에 툭 바뀌지 않고 부드럽게 따라간다.")]
        [SerializeField, Range(.5f, 10f)] private float shakeClearSpeed = 2.5f;
        [Tooltip("흔들어 색을 되찾은 구슬이 톡 튀는 크기 (#330, 처음 크기 배). 1이면 튀지 않는다.")]
        [SerializeField, Range(1f, 2f)] private float revealPopScale = 1.3f;
        [Tooltip("색을 되찾은 구슬이 제 크기로 돌아오는 시간 (초)")]
        [SerializeField, Range(.05f, .6f)] private float revealPopSeconds = .18f;
        [Tooltip("흔들라는 안내 위치 (0~1, 왼쪽 아래가 0)")]
        [SerializeField] private Vector2 shakeCueAnchor = new Vector2(.5f, .42f);
        [Tooltip("흔들라는 안내의 폰 그림 높이 (Canvas 기준 픽셀)")]
        [SerializeField, Range(30f, 300f)] private float shakeCueSize = 110f;
        [Tooltip("흔들라는 안내 문구. 비우면(기본) 글자 없이 폰 그림과 게이지만 보인다.")]
        [SerializeField] private string shakeText = "";
        [Tooltip("폰 그림이 좌우로 흔들리는 빠르기 (초당 횟수)")]
        [SerializeField, Range(1f, 12f)] private float shakeCueWiggleHz = 5f;
        private static readonly Color ShakeGaugeColor = new Color(1f, .78f, .16f, 1f);
        private static Sprite phoneSprite;

        private static Sprite edgeSprite;
        private LabNetwork network;
        private LabOrbBoard boundBoard;
        private Canvas canvas;
        private RectTransform layer;
        private Image edge;
        private Image burst;
        private Text countdown;
        private int shownCount;
        private uint caughtShownFor;
        private readonly LabShakeCounter shakeCounter = new LabShakeCounter();
        private uint shakeFlash;
        private string shakeSession;
        private uint shakeRound;
        private float shakeSentAt = float.NegativeInfinity;
        public const float ShakeRetrySeconds = .5f;
        private float shownShake;
        private int revealedShakes;
        private RectTransform shakeCue, shakeGaugeFill, shakePhone;
        private Text shakeLabel;
        /// <summary>이번 섬광에 이 화면에서 센 흔들기 수 (#301)</summary>
        public int ShakeCount => shakeCounter.Count;
        /// <summary>흔들라는 안내가 보이는지 (#301)</summary>
        public bool ShakeCueVisible => shakeCue != null && shakeCue.gameObject.activeInHierarchy;
        /// <summary>
        /// 흔든 만큼 하얀 막이 걷힌 정도 (#301): 흔든 수 / 필요한 수 (0~1). 이 값만큼 눈부심을 덜어 낸다.
        /// </summary>
        public static float ShakeProgress(int shakes, int needed) => needed <= 0 ? 0f : Mathf.Clamp01((float)shakes / needed);
        /// <summary>이 화면이 띄운 섬광 말풍선 수 (테스트용): 놀림, 들킴</summary>
        public int TauntCount { get; private set; }
        public int CaughtCount { get; private set; }
        private float countChangedAt;
        private string seenSession;
        private uint seenRound;
        private uint seenFlashCount;
        private double seenServerTime = -1d;
        private string clockSession;
        private uint clockRound;
        private float seenAt;
        private double eyesShownFor = -1d;

        /// <summary>예고 중 눈이 빛나기 시작하는 지점과 다 빛나는 지점 (예고 길이 비율). 그 전에는 보통 눈이다.</summary>
        public const float EyesLightFrom = .3f, EyesLitAt = .45f;

        /// <summary>
        /// 예고 진행(0~1)에 따른 눈 빛 (0~1). 앞 30%는 보통 눈, 그 뒤 15% 동안 확 노랗게 빛나고, 남은 55%(기본 예고 3초면
        /// 약 1.6초)는 빛나는 노란 눈을 두근거리며 보여 준 다음 번쩍인다 (#271 피드백: 노란 눈을 더 오래 보이게).
        /// </summary>
        public static float EyeGlowAt(float progress)
        {
            float u = Mathf.Clamp01((progress - EyesLightFrom) / (EyesLitAt - EyesLightFrom));
            return u * u * (3f - 2f * u);
        }

        /// <summary>예고 빛의 지금 불투명도 (0이면 안 보임)</summary>
        public float EdgeAlpha => edge != null && edge.gameObject.activeInHierarchy ? edge.color.a : 0f;
        /// <summary>하얀 화면의 지금 불투명도 (번쩍임 + 눈부심 막)</summary>
        public float BurstAlpha => burst != null && burst.gameObject.activeInHierarchy ? burst.color.a : 0f;
        /// <summary>지금 화면에 보이는 카운트다운 숫자 (안 보이면 0)</summary>
        public int CountdownShown => countdown != null && countdown.gameObject.activeInHierarchy ? shownCount : 0;

        /// <summary>
        /// 이 사람 화면의 섬광 예고 카운트다운 숫자 (#301): 이 사람이 다음 섬광 대상이고 예고 중이면 번쩍일 때까지
        /// 남은 초를 올림한 값(3 → 2 → 1), 아니면 0. 전투 중이 아니면 0.
        /// </summary>
        public static int CountdownAt(LabSnapshot snapshot, double now, ulong player)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || !snapshot.FlashWarningOn(now)
                || !snapshot.FlashTargets(player)) return 0;
            return Mathf.Max(1, Mathf.CeilToInt((float)(snapshot.flashAt - now) - 1e-4f));
        }

        /// <summary>이 화면이 진동을 낸 섬광 수 (테스트용)</summary>
        public int HapticCount { get; private set; }

        /// <summary>스냅샷을 읽을 네트워크와, 빛을 그릴 Canvas를 연결한다.</summary>
        public void Bind(LabNetwork target, Canvas uiCanvas)
        {
            Hide();
            network = target;
            // A persistent session can already point at the next stage when this HUD is torn down.
            // Only the board captured by this binding belongs to this screen.
            boundBoard = target != null ? target.board : null;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
        }

        private void LateUpdate()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            LabConfig config = network != null ? network.config : null;
            if (snapshot == null || config == null || snapshot.phase != LabPhase.Playing
                || network.GameStartHeld || !ReferenceEquals(network.board, boundBoard))
            {
                Hide();
                return;
            }
            double now = ServerNow(snapshot);
            ulong me = network.LocalPlayerId;
            float glare = LabYaguangFlash.GlareAmount(snapshot, now, me, config.YaguangFlashGlareSeconds,
                config.YaguangFlashRecoverySeconds);
            // 흔들어 풀기 (#301): 흔든 만큼 이 화면의 하얀 막을 덜어 낸다. 다 흔들면 Host가 풀기 전에도 바로 맑다.
            bool shakeRelease = config.YaguangFlashShakeRelease;
            bool myGlare = shakeRelease && LabYaguangFlash.Glared(snapshot, now, me, config.YaguangFlashGlareSeconds);
            float shakeProgress = DriveShake(snapshot, myGlare, config.YaguangFlashShakeCount);
            // 흔들 때마다 구슬 하나씩 색이 돌아온다 (#330, 판이 정한다). 다 흔들었으면 Host가 풀기 전에도 바로 맑다.
            // 화면 전체의 옅은 막은 돌아온 비율만큼 부드럽게 옅어진다.
            shownShake = Mathf.MoveTowards(shownShake, shakeProgress, shakeClearSpeed * Time.unscaledDeltaTime);
            if (shakeRelease && shakeProgress >= 1f) glare = 0f;
            if (boundBoard != null) boundBoard.SetFlashGlare(glare);
            float veilGlare = shakeRelease ? glare * (1f - shownShake) : glare;
            CheckNewFlash(snapshot, now, me, config);
            CheckCaught(snapshot, now, me);

            float progress = DriveEyes(snapshot, now);
            float warning = LabYaguangFlash.WarningAmount(snapshot, now, me) > 0f ? EyeGlowAt(progress) : 0f;
            float burstAlpha = 0f;
            if (snapshot.LastFlashHit(me) && snapshot.flashLastAt >= 0d)
            {
                double since = now - snapshot.flashLastAt;
                if (since >= 0d && since < BurstSeconds)
                {
                    float u = (float)(since / BurstSeconds);
                    burstAlpha = (1f - u) * (1f - u);
                }
            }
            burstAlpha = Mathf.Max(burstAlpha, veilAlpha * veilGlare);
            int count = showCountdown && !snapshot.paused ? CountdownAt(snapshot, now, me) : 0;
            bool cue = myGlare && shakeProgress < 1f && !snapshot.paused;
            if (warning <= 0f && burstAlpha <= 0f && count <= 0 && !cue)
            {
                if (layer != null) layer.gameObject.SetActive(false);
                shownCount = 0;
                return;
            }
            if (!EnsureLayer()) return;
            layer.gameObject.SetActive(true);
            layer.SetAsLastSibling();
            // 말풍선의 눈이 빛나는 만큼 가장자리 빛도 차오른다.
            edge.color = new Color(EdgeColor.r, EdgeColor.g, EdgeColor.b, edgeMaxAlpha * warning);
            edge.gameObject.SetActive(warning > 0f);
            burst.color = new Color(BurstColor.r, BurstColor.g, BurstColor.b, burstAlpha);
            burst.gameObject.SetActive(burstAlpha > 0f);
            DriveCountdown(count);
            DriveShakeCue(cue, shakeProgress);
        }

        /// <summary>
        /// 이번 섬광에서 흔든 수를 세고, 다 흔들면 Host에 한 번 알린다. 흔든 만큼의 비율(0~1)을 돌려준다.
        /// 새 섬광이면 처음부터 다시 센다. 눈부시지 않으면(이미 풀렸거나 시간이 지났으면) 세지 않는다.
        /// #330: 한 번 흔들 때마다 판의 하얀 구슬 하나가 무작위로 색을 되찾는다. 판에 하얀 구슬이 남지 않으면(적어도
        /// 한 번 흔든 뒤) 다 흔든 것이고, 아무리 구슬이 많아도 maxShakes번이면 나머지가 한꺼번에 돌아온다.
        /// 판이 없으면(테스트 등) maxShakes번을 채워야 한다.
        /// </summary>
        private float DriveShake(LabSnapshot snapshot, bool myGlare, int maxShakes)
        {
            if (snapshot.sessionId != shakeSession || snapshot.round != shakeRound || snapshot.flashCount != shakeFlash)
            {
                shakeSession = snapshot.sessionId;
                shakeRound = snapshot.round;
                shakeFlash = snapshot.flashCount;
                shakeCounter.Reset();
                shakeSentAt = float.NegativeInfinity;
                shownShake = 0f;
                revealedShakes = 0;
                if (boundBoard != null) boundBoard.ResetFlashReveal();
            }
            shakeCounter.Threshold = shakeThreshold;
            shakeCounter.MinGap = shakeMinGap;
            if (myGlare && !snapshot.paused && network != null && !network.GameStartHeld && network.IsConnected)
            {
                if (!ShakesDone(maxShakes) && ReadShake()) HapticTick();
                RevealShaken(maxShakes);
                // A report can arrive while the Host is paused. Retain this generation's shakes and
                // retry at a bounded rate until the authoritative snapshot confirms release.
                if (ShakesDone(maxShakes) && Time.unscaledTime - shakeSentAt >= ShakeRetrySeconds
                    && network.RequestFlashShakeRelease())
                    shakeSentAt = Time.unscaledTime;
            }
            else RevealShaken(maxShakes);
            if (ShakesDone(maxShakes)) return 1f;
            if (boundBoard == null) return ShakeProgress(shakeCounter.Count, maxShakes);
            int count = shakeCounter.Count;
            return ShakeProgress(count, count + Mathf.Min(boundBoard.FlashWhiteCount, maxShakes - count));
        }

        /// <summary>다 흔들었는지 (#330): 최대 횟수를 채웠거나, 한 번 이상 흔들었고 판에 하얀 구슬이 남지 않았다.</summary>
        private bool ShakesDone(int maxShakes)
        {
            int count = shakeCounter.Count;
            if (count >= Mathf.Max(1, maxShakes)) return true;
            return boundBoard != null && count >= 1 && boundBoard.FlashWhiteCount == 0;
        }

        /// <summary>새로 센 흔들기마다 판의 하얀 구슬 하나씩 색을 돌려준다. 다 흔들었으면 남은 구슬도 모두 돌려준다.</summary>
        private void RevealShaken(int maxShakes)
        {
            if (boundBoard == null) { revealedShakes = shakeCounter.Count; return; }
            while (revealedShakes < shakeCounter.Count)
            {
                revealedShakes++;
                boundBoard.RevealFlashOrb(revealPopScale, revealPopSeconds);
            }
            if (shakeCounter.Count >= Mathf.Max(1, maxShakes)) boundBoard.RevealAllFlashOrbs();
        }

        /// <summary>이번 프레임에 한 번 흔들었는지. 가속도계(폰)가 있으면 그것으로, Editor·DEV 빌드에서는 K 키로도.</summary>
        private bool ReadShake()
        {
            bool shaken = false;
            Accelerometer sensor = Accelerometer.current;
            if (sensor != null)
            {
                if (!sensor.enabled) InputSystem.EnableDevice(sensor);
                shaken = shakeCounter.Feed(sensor.acceleration.ReadValue(), Time.unscaledTime);
            }
            if (!shaken && (Application.isEditor || Debug.isDebugBuild) && Keyboard.current != null
                && Keyboard.current.kKey.wasPressedThisFrame)
            {
                shakeCounter.AddOne();
                shaken = true;
            }
            return shaken;
        }

        /// <summary>한 번 흔들 때마다 아주 짧게 진동한다 (흔든 게 세졌다는 손맛).</summary>
        private void HapticTick()
        {
            LabHaptics haptics = GetComponent<LabHaptics>();
            // 구슬 생성과 같은 가벼운 진동 (섬광의 센 진동과 구분).
            if (haptics != null && network != null && network.config != null) haptics.PlayForGenerate(network.config);
        }

        /// <summary>흔들라는 안내: 좌우로 흔들리는 폰 그림 + 문구 + 흔든 만큼 차오르는 게이지.</summary>
        private void DriveShakeCue(bool show, float progress)
        {
            if (shakeCue == null) return;
            shakeCue.gameObject.SetActive(show);
            if (!show) return;
            shakeCue.anchorMin = shakeCue.anchorMax = shakeCueAnchor;
            shakeCue.SetAsLastSibling();
            float size = shakeCueSize;
            shakePhone.sizeDelta = new Vector2(size * .58f, size);
            shakePhone.anchoredPosition = new Vector2(0f, size * .55f);
            float wiggle = Mathf.Sin(Time.unscaledTime * shakeCueWiggleHz * Mathf.PI * 2f);
            shakePhone.localRotation = Quaternion.Euler(0f, 0f, 18f * wiggle);
            shakePhone.anchoredPosition += new Vector2(size * .12f * wiggle, 0f);
            bool hasText = !string.IsNullOrEmpty(shakeText);
            shakeLabel.gameObject.SetActive(hasText);
            shakeLabel.text = shakeText;
            shakeLabel.fontSize = Mathf.RoundToInt(size * .4f);
            shakeLabel.rectTransform.sizeDelta = new Vector2(size * 3f, size * .55f);
            shakeLabel.rectTransform.anchoredPosition = new Vector2(0f, -size * .12f);
            var track = (RectTransform)shakeGaugeFill.parent;
            track.sizeDelta = new Vector2(size * 1.8f, size * .14f);
            // 문구가 없으면 게이지를 폰 그림 바로 밑으로 올린다.
            track.anchoredPosition = new Vector2(0f, hasText ? -size * .5f : -size * .08f);
            shakeGaugeFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        }

        /// <summary>카운트다운 숫자를 맞추고, 숫자가 바뀔 때마다 크게 나왔다가 제자리로 톡 줄어든다.</summary>
        private void DriveCountdown(int count)
        {
            if (countdown == null) return;
            if (count <= 0)
            {
                countdown.gameObject.SetActive(false);
                shownCount = 0;
                return;
            }
            if (count != shownCount)
            {
                shownCount = count;
                countChangedAt = Time.unscaledTime;
                countdown.text = count.ToString();
            }
            var rect = countdown.rectTransform;
            rect.anchorMin = rect.anchorMax = countdownAnchor;
            rect.sizeDelta = new Vector2(countdownSize * 1.6f, countdownSize * 1.4f);
            countdown.fontSize = countdownSize;
            float u = countdownPopSeconds > 0f ? Mathf.Clamp01((Time.unscaledTime - countChangedAt) / countdownPopSeconds) : 1f;
            // 1.45배로 튀어나와 1배로 내려앉는다.
            float scale = Mathf.Lerp(1.45f, 1f, 1f - (1f - u) * (1f - u));
            rect.localScale = new Vector3(scale, scale, 1f);
            countdown.color = new Color(countdownColor.r, countdownColor.g, countdownColor.b, countdownColor.a * Mathf.Lerp(.4f, 1f, u));
            countdown.gameObject.SetActive(true);
            countdown.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 예고가 시작되면 박 옆 말풍선에 야광귀 눈을 한 번 띄우고, 매 프레임 눈 빛을 맞춘다. 모든 기기에서 한다.
        /// 예고 진행(0~1)을 돌려준다. 예고가 아니면 -1.
        /// </summary>
        private float DriveEyes(LabSnapshot snapshot, double now)
        {
            LabGourdAlert eyes = GetComponent<LabGourdAlert>();
            if (!snapshot.FlashWarningOn(now))
            {
                // 예고가 끝났거나(번쩍임), 콤보 때문에 취소됐으면 눈 말풍선을 바로 거둔다.
                if (eyes != null && eyes.ShowingEyes) eyes.Hide();
                return -1f;
            }
            double length = snapshot.flashAt - snapshot.flashWarningAt;
            float progress = length > 0d ? Mathf.Clamp01((float)((now - snapshot.flashWarningAt) / length)) : 1f;
            LabGourdAlert alert = GetComponent<LabGourdAlert>();
            if (snapshot.paused || alert == null || network.target == null) return progress;
            if (eyesShownFor != snapshot.flashAt || !alert.ShowingEyes)
            {
                // Pause and other alerts can hide/replace this bubble without changing flashAt.
                // Restore the remaining warning when this screen returns to the battle.
                alert.ShowEyes(network.target, (float)(snapshot.flashAt - now));
                eyesShownFor = snapshot.flashAt;
            }
            if (alert.ShowingEyes) alert.SetEyeGlow(EyeGlowAt(progress));
            return progress;
        }

        /// <summary>
        /// 새 섬광이 이 사람에게 터졌으면 진동을 한 번 낸다. 새 라운드·접속 직후 이미 지난 섬광은 울리지 않는다.
        /// </summary>
        private void CheckNewFlash(LabSnapshot snapshot, double now, ulong me, LabConfig config)
        {
            if (snapshot.sessionId != seenSession || snapshot.round != seenRound || snapshot.flashCount < seenFlashCount)
            {
                seenSession = snapshot.sessionId;
                seenRound = snapshot.round;
                seenFlashCount = snapshot.flashCount;
                caughtShownFor = snapshot.flashCount;
                return;
            }
            if (snapshot.flashCount == seenFlashCount) return;
            seenFlashCount = snapshot.flashCount;
            if (!snapshot.LastFlashHit(me) || now - snapshot.flashLastAt > 1d) return;
            HapticCount++;
            LabHaptics haptics = GetComponent<LabHaptics>();
            if (haptics != null) haptics.PlayForFlash(config);
            if (Say(tauntText, tauntSeconds)) TauntCount++;
        }

        /// <summary>내가 요괴 얼굴을 맞혀 이번 섬광의 눈부심을 풀었으면(#301) "앗 들켰다!"를 한 번 띄운다.</summary>
        private void CheckCaught(LabSnapshot snapshot, double now, ulong me)
        {
            double released = snapshot.FlashReleasedAt(me);
            if (released < 0d || caughtShownFor == snapshot.flashCount) return;
            caughtShownFor = snapshot.flashCount;
            // 접속 직후 이미 오래전에 푼 기록에는 띄우지 않는다.
            if (now - released > 1d) return;
            if (Say(caughtText, caughtSeconds)) CaughtCount++;
        }

        /// <summary>박 옆 말풍선에 짧은 말을 띄운다. 말풍선이나 박이 없으면 false.</summary>
        private bool Say(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) return false;
            LabGourdAlert alert = GetComponent<LabGourdAlert>();
            if (alert == null || network == null || network.target == null) return false;
            alert.ShowWord(network.target, text, seconds);
            return true;
        }

        private void Hide()
        {
            if (boundBoard != null) boundBoard.SetFlashGlare(0f);
            if (layer != null) layer.gameObject.SetActive(false);
            LabGourdAlert alert = GetComponent<LabGourdAlert>();
            if (alert != null && alert.ShowingEyes) alert.Hide();
            eyesShownFor = -1d;
        }

        private double ServerNow(LabSnapshot snapshot)
        {
            float realtime = Time.realtimeSinceStartup;
            if (snapshot.paused || snapshot.sessionId != clockSession || snapshot.round != clockRound
                || snapshot.serverTime != seenServerTime)
            {
                clockSession = snapshot.sessionId;
                clockRound = snapshot.round;
                seenServerTime = snapshot.serverTime;
                seenAt = realtime;
            }
            return snapshot.paused ? snapshot.serverTime
                : snapshot.serverTime + System.Math.Min(1d, realtime - seenAt);
        }

        private bool EnsureLayer()
        {
            if (layer != null) return true;
            if (canvas == null)
            {
                Canvas found = GetComponentInParent<Canvas>();
                canvas = found != null ? found.rootCanvas : null;
            }
            RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            if (canvasRect == null) return false;
            layer = FullScreen("Yaguang Flash", canvasRect);
            edge = FullScreen("Yaguang Flash Edge", layer).gameObject.AddComponent<Image>();
            edge.sprite = EdgeSprite();
            edge.raycastTarget = false;
            burst = FullScreen("Yaguang Flash Burst", layer).gameObject.AddComponent<Image>();
            burst.raycastTarget = false;
            countdown = CreateCountdown(layer);
            CreateShakeCue(layer);
            return true;
        }

        private void CreateShakeCue(RectTransform parent)
        {
            shakeCue = new GameObject("Yaguang Shake Cue", typeof(RectTransform)).GetComponent<RectTransform>();
            shakeCue.gameObject.layer = parent.gameObject.layer;
            shakeCue.SetParent(parent, false);
            shakeCue.sizeDelta = Vector2.zero;

            shakePhone = new GameObject("Phone", typeof(RectTransform)).GetComponent<RectTransform>();
            shakePhone.SetParent(shakeCue, false);
            Image phone = shakePhone.gameObject.AddComponent<Image>();
            phone.sprite = PhoneSprite();
            phone.raycastTarget = false;

            Font font = null;
            Text[] existing = canvas != null ? canvas.GetComponentsInChildren<Text>(true) : new Text[0];
            for (int i = 0; i < existing.Length && font == null; i++) font = existing[i].font;
            shakeLabel = new GameObject("Shake Label", typeof(RectTransform)).AddComponent<Text>();
            shakeLabel.rectTransform.SetParent(shakeCue, false);
            shakeLabel.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            shakeLabel.fontStyle = FontStyle.Bold;
            shakeLabel.alignment = TextAnchor.MiddleCenter;
            shakeLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            shakeLabel.verticalOverflow = VerticalWrapMode.Overflow;
            shakeLabel.color = countdownColor;
            shakeLabel.raycastTarget = false;
            Outline outline = shakeLabel.gameObject.AddComponent<Outline>();
            outline.effectColor = CountdownInk;
            outline.effectDistance = new Vector2(3f, -3f);

            var track = new GameObject("Shake Gauge", typeof(RectTransform)).GetComponent<RectTransform>();
            track.SetParent(shakeCue, false);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(CountdownInk.r, CountdownInk.g, CountdownInk.b, .55f);
            trackImage.raycastTarget = false;
            shakeGaugeFill = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
            shakeGaugeFill.SetParent(track, false);
            shakeGaugeFill.anchorMin = Vector2.zero;
            shakeGaugeFill.anchorMax = new Vector2(0f, 1f);
            shakeGaugeFill.offsetMin = shakeGaugeFill.offsetMax = Vector2.zero;
            Image fill = shakeGaugeFill.gameObject.AddComponent<Image>();
            fill.color = ShakeGaugeColor;
            fill.raycastTarget = false;
            shakeCue.gameObject.SetActive(false);
        }

        /// <summary>폰 그림: 먹색 테두리의 둥근 직사각형, 흰 화면, 아래 버튼 점.</summary>
        private static Sprite PhoneSprite()
        {
            if (phoneSprite != null) return phoneSprite;
            const int w = 58, h = 100;
            var pixels = new Color32[w * h];
            Color32 ink = new Color32(43, 36, 32, 255), screen = new Color32(255, 252, 240, 255), clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float r = 12f;
                    float dx = Mathf.Max(0f, Mathf.Max(r - x, x - (w - 1 - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - y, y - (h - 1 - r)));
                    float outside = Mathf.Sqrt(dx * dx + dy * dy) - r;
                    Color32 c = outside > 0f ? clear : ink;
                    bool inScreen = x >= 7 && x < w - 7 && y >= 16 && y < h - 12;
                    if (outside <= 0f && inScreen) c = screen;
                    float bx = x - w * .5f, by = y - 8f;
                    if (bx * bx + by * by < 9f) c = screen;
                    pixels[y * w + x] = c;
                }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Yaguang shake phone", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            phoneSprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(.5f, .5f), h);
            phoneSprite.hideFlags = HideFlags.DontSave;
            return phoneSprite;
        }

        private Text CreateCountdown(RectTransform parent)
        {
            var rect = new GameObject("Yaguang Flash Countdown", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(.5f, .5f);
            var text = rect.gameObject.AddComponent<Text>();
            Font font = null;
            Text[] existing = canvas != null ? canvas.GetComponentsInChildren<Text>(true) : new Text[0];
            for (int i = 0; i < existing.Length && font == null; i++) font = existing[i].font;
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = CountdownInk;
            outline.effectDistance = new Vector2(4f, -4f);
            rect.gameObject.SetActive(false);
            return text;
        }

        private static RectTransform FullScreen(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        /// <summary>가운데는 투명하고 가장자리로 갈수록 진해지는 빛 (흰색, 색은 Image 색으로 입힌다).</summary>
        private static Sprite EdgeSprite()
        {
            if (edgeSprite != null) return edgeSprite;
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + .5f) / size * 2f - 1f), dy = Mathf.Abs((y + .5f) / size * 2f - 1f);
                    float d = Mathf.Max(dx, dy) * .55f + Mathf.Sqrt(dx * dx + dy * dy) * .45f;
                    float alpha = Mathf.Clamp01((d - .45f) / .55f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * alpha * 255f));
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Yaguang flash edge",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            edgeSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            edgeSprite.name = texture.name;
            edgeSprite.hideFlags = HideFlags.DontSave;
            return edgeSprite;
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            Hide();
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
