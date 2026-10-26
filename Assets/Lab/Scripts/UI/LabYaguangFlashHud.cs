using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 야광귀 섬광 (#268)의 화면. 섬광을 맞을 사람 자신의 화면에만 그린다(DEV 대상 ALL이면 모두, ONE이면 한 명).
    /// - 예고: 모든 기기의 박 옆 말풍선에 야광귀의 두 눈이 뜨고(`LabGourdAlert.ShowEyes`), 예고 후반에 눈이 갑자기
    ///   노랗게 빛난다. 맞을 사람 화면은 눈이 빛나는 만큼 가장자리부터 노란 빛이 차오르고, 화면 가운데에
    ///   남은 초가 3 → 2 → 1 숫자로 크게 톡톡 튀어나온다 (#301).
    /// - 섬광: 화면 전체가 하얗게 번쩍했다가 빠르게 빠지고, 진동이 한 번 온다.
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

        private static Sprite edgeSprite;
        private LabNetwork network;
        private LabOrbBoard boundBoard;
        private Canvas canvas;
        private RectTransform layer;
        private Image edge;
        private Image burst;
        private Text countdown;
        private int shownCount;
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
                || !ReferenceEquals(network.board, boundBoard))
            {
                Hide();
                return;
            }
            double now = ServerNow(snapshot);
            ulong me = network.LocalPlayerId;
            float glare = LabYaguangFlash.GlareAmount(snapshot, now, me, config.YaguangFlashGlareSeconds,
                config.YaguangFlashRecoverySeconds);
            if (boundBoard != null) boundBoard.SetFlashGlare(glare);
            CheckNewFlash(snapshot, now, me, config);

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
            burstAlpha = Mathf.Max(burstAlpha, veilAlpha * glare);
            int count = showCountdown && !snapshot.paused ? CountdownAt(snapshot, now, me) : 0;
            if (warning <= 0f && burstAlpha <= 0f && count <= 0)
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
                return;
            }
            if (snapshot.flashCount == seenFlashCount) return;
            seenFlashCount = snapshot.flashCount;
            if (!snapshot.LastFlashHit(me) || now - snapshot.flashLastAt > 1d) return;
            HapticCount++;
            LabHaptics haptics = GetComponent<LabHaptics>();
            if (haptics != null) haptics.PlayForFlash(config);
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
            return true;
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
