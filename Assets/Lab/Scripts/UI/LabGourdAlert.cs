using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 박이 누군가에게 폭탄을 떨어뜨릴 때(#177) 전투 화면의 박 옆에 뜨는 "!" 말풍선. 모든 기기에서 보인다.
    /// 흰 말풍선에 굵은 테두리, 박을 가리키는 꼬리, 빨간 "!"가 그려진 그림체이며, 톡 튀어나와 흔들리다
    /// 들어간다. 박이 흔들리고 돌아도 화면에서 박 위를 따라간다. 화면 표시만 하며 터치를 막지 않는다.
    /// 일시정지 중이거나 전투가 끝나면 사라진다.
    /// 박이 내 한쪽 넘기기를 막을 때(#215)는 같은 말풍선에 "← 🚫" 또는 "→ 🚫"(먹색 화살표와 빨간 금지
    /// 표시를 직접 그림)가 떠, 막힌 사람 화면에만 보인다.
    /// 요괴가 통합 추첨으로 매혹을 걸 때(#251)는 같은 말풍선에 하트(직접 그림)가 떠, 매혹된 사람 화면에만 보인다.
    /// 야광귀 섬광 예고(#268)에는 같은 말풍선에 야광귀의 튀어나온 두 눈(직접 그림)이 떠 모든 기기에서 보이고,
    /// 섬광 직전에 눈이 갑자기 노랗게 빛난다(`SetEyeGlow`). 번쩍이는 순간 말풍선은 들어간다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGourdAlert : MonoBehaviour
    {
        public const string Text = "!";
        private const int TextureSize = 150;
        // 말풍선 그림 안의 위치 (픽셀, 아래가 0): 몸통 타원과 박을 가리키는 꼬리 끝.
        private static readonly Vector2 BodyCenter = new Vector2(88f, 94f);
        private static readonly Vector2 BodyRadii = new Vector2(64f, 56f);
        private static readonly Vector2 TailTip = new Vector2(16f, 8f);
        private static readonly Vector2 TailBaseA = new Vector2(44f, 70f);
        private static readonly Vector2 TailBaseB = new Vector2(70f, 46f);
        private const float OutlinePixels = 7f;
        private static readonly Color32 FillColor = new Color32(255, 255, 255, 255);
        private static readonly Color32 InkColor = new Color32(43, 36, 32, 255);
        private static readonly Color MarkColor = new Color(.91f, .16f, .1f);
        private const float PopSeconds = .18f;
        private const float LeaveSeconds = .15f;
        private const float WiggleDegrees = 9f;
        private const float WiggleHz = 3f;
        /// <summary>넘기기 막기 말풍선이 떠 있는 시간 (초, #215). 막힌 가장자리와 남은 초는 막기 내내 판에 남는다.</summary>
        public const float BlockShowSeconds = 2f;
        // 막기 표시 그림 (픽셀, 아래가 0): 왼쪽 절반에 막힌 쪽을 가리키는 화살표, 오른쪽에 금지 표시.
        private const int MarkWidth = 128, MarkHeight = 64;
        private static readonly Color32 SignColor = new Color32(232, 41, 26, 255);
        private static Sprite bubbleSprite;
        private static Sprite blockLeftSprite;
        private static Sprite blockRightSprite;
        private static Sprite heartSprite;
        private static Sprite eyesSprite;
        private static Sprite eyesLitSprite;
        private static Sprite eyesHaloSprite;
        // 야광귀 눈 그림 (#268): 가로로 긴 그림에 튀어나온 두 눈. 픽셀 크기와 두 눈의 중심·반지름.
        private const int EyesWidth = 128, EyesHeight = 64;
        private static readonly Vector2 LeftEye = new Vector2(38f, 32f), RightEye = new Vector2(90f, 32f);
        private const float EyeRadius = 23f;
        private static readonly Color32 EyeLitColor = new Color32(255, 214, 64, 255);
        // 매혹 하트 그림 (#251): 정사각형 픽셀 크기와, 먹색 테두리 안쪽 하트의 크기 비율.
        private const int HeartSize = 96;
        private const float HeartInner = .8f;

        [Tooltip("말풍선 크기 (Canvas 기준 픽셀)")]
        [SerializeField, Min(10f)] private float bubbleSize = 110f;
        [Tooltip("말풍선이 떠 있는 시간 (초)")]
        [SerializeField, Min(.3f)] private float showSeconds = 1.4f;

        private LabNetwork network;
        private Canvas canvas;
        private RectTransform canvasRect;
        private Camera battleCamera;
        private LabTarget target;
        private RectTransform layer;
        private RectTransform bubble;
        private UnityEngine.UI.Text mark;
        private Image blockMark;
        private Image heartMark;
        private bool showingHeart;
        private RectTransform eyesRect;
        private Image eyesHalo;
        private Image eyesLit;
        private bool showingEyes;
        private float eyeGlow;
        private float elapsed = -1f;
        private float duration;
        private bool showingBlock;
        private bool blockRight;

        /// <summary>박 폭탄 알림이 진행 중인지 (박이 화면 밖이라 안 보이는 순간도 포함).</summary>
        public bool Active => elapsed >= 0f;
        /// <summary>말풍선이 지금 화면에 그려지고 있는지.</summary>
        public bool Visible => bubble != null && bubble.gameObject.activeSelf;
        public RectTransform Bubble => bubble;
        /// <summary>말풍선의 내용: 폭탄이면 "!", 넘기기 막기면 "← 🚫" 또는 "→ 🚫", 매혹이면 "♥".</summary>
        public string Label => showingBlock ? (blockRight ? "→ 🚫" : "← 🚫") : showingHeart ? "♥"
            : showingEyes ? "👀" : mark != null ? mark.text : string.Empty;
        /// <summary>지금 야광귀 눈 말풍선을 보여 주는 중인지 (#268).</summary>
        public bool ShowingEyes => Active && showingEyes;
        /// <summary>야광귀 눈이 빛나는 정도 (0 보통 눈 ~ 1 완전히 빛남, #268).</summary>
        public float EyeGlow => eyeGlow;
        /// <summary>지금 매혹 하트 말풍선을 보여 주는 중인지 (#251).</summary>
        public bool ShowingHeart => Active && showingHeart;
        public float ShowSeconds => showSeconds;
        /// <summary>지금 넘기기 막기 말풍선을 보여 주는 중인지 (#215).</summary>
        public bool ShowingBlock => Active && showingBlock;
        /// <summary>막기 말풍선이 오른쪽 막힘을 가리키는지.</summary>
        public bool BlockRight => blockRight;

        /// <summary>박 폭탄 신호를 받을 네트워크와, 말풍선을 그릴 Canvas·전투 카메라를 연결한다.</summary>
        public void Bind(LabNetwork target, Canvas uiCanvas, Camera camera)
        {
            if (network != null)
            {
                network.GourdBombDropped -= OnGourdBombDropped;
                network.SwipeBlockStarted -= OnSwipeBlockStarted;
                network.SabotageCast -= OnSabotageCast;
            }
            network = target;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
            battleCamera = camera;
            if (network != null)
            {
                network.GourdBombDropped += OnGourdBombDropped;
                network.SwipeBlockStarted += OnSwipeBlockStarted;
                network.SabotageCast += OnSabotageCast;
            }
        }

        // 통합 추첨의 여우불은 모든 기기에서 박 옆에 "!"를, 매혹은 걸린 사람 화면에만 하트를 띄운다 (#251).
        // 폭탄과 넘기기 막기는 위에서 따로 띄운다.
        private void OnSabotageCast(LabSabotageKind kind, ulong targetPlayer)
        {
            if (network == null) return;
            if (kind == LabSabotageKind.FoxFire) Show(network.target);
            else if (kind == LabSabotageKind.Charm && targetPlayer == network.LocalPlayerId) ShowHeart(network.target);
        }

        private void OnGourdBombDropped(ulong targetPlayer, string bombId, Vector2 landing)
        {
            if (network != null) Show(network.target);
        }

        // 네트워크는 막힌 사람 자신에게만 알린다.
        private void OnSwipeBlockStarted(bool right, float seconds)
        {
            if (network != null) ShowBlock(network.target, right);
        }

        /// <summary>박 옆에 "!" 말풍선을 띄운다. 이미 떠 있으면 처음부터 다시 튀어나온다.</summary>
        public void Show(LabTarget gourd)
        {
            if (gourd == null || !EnsureBubble()) return;
            showingBlock = showingHeart = false;
            ShowOnly(mark.gameObject);
            Begin(gourd, showSeconds);
        }

        /// <summary>요괴가 이 사람에게 매혹을 걸었다고(#251) 박 옆에 하트 말풍선을 띄운다. 매혹된 사람 화면에서만 부른다.</summary>
        public void ShowHeart(LabTarget gourd)
        {
            if (gourd == null || !EnsureBubble()) return;
            showingBlock = false;
            showingHeart = true;
            ShowOnly(heartMark.gameObject);
            Begin(gourd, showSeconds);
        }

        /// <summary>
        /// 박이 이 사람의 한쪽 넘기기를 막았다고(#215) "← 🚫" 또는 "→ 🚫" 말풍선을 띄운다. 막힌 사람 화면에서만 부른다.
        /// </summary>
        public void ShowBlock(LabTarget gourd, bool right)
        {
            if (gourd == null || !EnsureBubble()) return;
            showingBlock = true;
            showingHeart = false;
            blockRight = right;
            blockMark.sprite = right ? BlockRightSprite() : BlockLeftSprite();
            ShowOnly(blockMark.gameObject);
            Begin(gourd, BlockShowSeconds);
        }

        /// <summary>
        /// 야광귀 섬광 예고 (#268): 박 옆 말풍선에 야광귀의 두 눈을 seconds 동안 띄운다(번쩍이는 순간까지). 모든 기기에서 부른다.
        /// 눈은 처음엔 보통 눈이고, <see cref="SetEyeGlow"/>로 섬광 직전에 노랗게 빛나게 한다.
        /// </summary>
        public void ShowEyes(LabTarget gourd, float seconds)
        {
            if (gourd == null || !EnsureBubble()) return;
            showingBlock = showingHeart = false;
            showingEyes = true;
            ShowOnly(eyesRect.gameObject);
            SetEyeGlow(0f);
            Begin(gourd, Mathf.Max(.2f, seconds));
        }

        /// <summary>야광귀 눈이 빛나는 정도 (0~1). 노란 눈동자가 겹쳐 나오고 둘레 빛이 커진다.</summary>
        public void SetEyeGlow(float glow)
        {
            eyeGlow = float.IsNaN(glow) ? 0f : Mathf.Clamp01(glow);
            if (eyesLit == null) return;
            eyesLit.color = new Color(1f, 1f, 1f, eyeGlow);
            eyesHalo.color = new Color(1f, 1f, 1f, .85f * eyeGlow);
            eyesHalo.rectTransform.localScale = Vector3.one * (1f + .3f * eyeGlow);
        }

        // 말풍선 안 표시는 한 번에 하나만 보인다.
        private void ShowOnly(GameObject shown)
        {
            if (shown != eyesRect.gameObject) showingEyes = false;
            mark.gameObject.SetActive(shown == mark.gameObject);
            blockMark.gameObject.SetActive(shown == blockMark.gameObject);
            heartMark.gameObject.SetActive(shown == heartMark.gameObject);
            eyesRect.gameObject.SetActive(shown == eyesRect.gameObject);
        }

        private void Begin(LabTarget gourd, float seconds)
        {
            target = gourd;
            duration = seconds;
            elapsed = 0f;
            Place();
            Animate();
        }

        public void Hide()
        {
            elapsed = -1f;
            showingEyes = false;
            target = null;
            if (bubble != null) bubble.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!Active) return;
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool battle = network == null
                || (snapshot != null && snapshot.phase == LabPhase.Playing && !snapshot.paused);
            elapsed += Time.deltaTime;
            if (!battle || target == null || elapsed >= duration)
            {
                Hide();
                return;
            }
            Place();
            Animate();
        }

        /// <summary>꼬리 끝을 화면에서 박의 오른쪽 위 어깨에 맞춘다.</summary>
        private void Place()
        {
            Collider hitbox = target != null ? target.Hitbox : null;
            if (battleCamera == null) battleCamera = Camera.main;
            if (hitbox == null || battleCamera == null)
            {
                bubble.gameObject.SetActive(false);
                return;
            }
            Bounds gourd = hitbox.bounds;
            Vector3 shoulder = gourd.center + Vector3.up * (gourd.extents.y * .8f)
                + battleCamera.transform.right * (Mathf.Max(gourd.extents.x, gourd.extents.z) * .55f);
            Vector3 screen = battleCamera.WorldToScreenPoint(shoulder);
            bool visible = screen.z > 0f && battleCamera.pixelRect.Contains(new Vector2(screen.x, screen.y));
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            visible &= RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                new Vector2(screen.x, screen.y), uiCamera, out Vector2 canvasPoint);
            bubble.gameObject.SetActive(visible);
            if (visible) bubble.anchoredPosition = canvasPoint;
        }

        /// <summary>톡 튀어나와(살짝 크게 갔다 돌아옴) 흔들리다가, 끝에 작아지며 들어간다.</summary>
        private void Animate()
        {
            float t = Mathf.Max(0f, elapsed);
            float pop = t < PopSeconds ? EaseOut(t / PopSeconds) * 1.15f
                : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - PopSeconds) / .12f));
            float leave = Mathf.Clamp01((t - (duration - LeaveSeconds)) / LeaveSeconds);
            bubble.localScale = Vector3.one * (pop * (1f - leave));
            float wiggle = WiggleDegrees * Mathf.Sin(t * WiggleHz * Mathf.PI * 2f) * (1f - t / duration);
            bubble.localRotation = Quaternion.Euler(0f, 0f, wiggle);
        }

        private bool EnsureBubble()
        {
            if (bubble != null) return true;
            if (canvas == null)
            {
                Canvas found = GetComponentInParent<Canvas>();
                canvas = found != null ? found.rootCanvas : null;
            }
            if (canvas == null) return false;
            canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect == null) return false;

            layer = NewRect("Gourd Alert", canvasRect);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            layer.SetAsLastSibling();

            bubble = NewRect("Gourd Alert Bubble", layer);
            bubble.anchorMin = bubble.anchorMax = new Vector2(.5f, .5f);
            bubble.sizeDelta = new Vector2(bubbleSize, bubbleSize);
            // 꼬리 끝이 기준점이라, 위치를 박에 맞추면 말풍선은 그 오른쪽 위로 펼쳐진다.
            bubble.pivot = TailTip / TextureSize;
            var image = bubble.gameObject.AddComponent<Image>();
            image.sprite = BubbleSprite();
            image.raycastTarget = false;

            RectTransform markRect = NewRect("Gourd Alert Mark", bubble);
            Vector2 center = BodyCenter / TextureSize;
            markRect.anchorMin = markRect.anchorMax = center;
            markRect.sizeDelta = new Vector2(bubbleSize, bubbleSize);
            mark = markRect.gameObject.AddComponent<UnityEngine.UI.Text>();
            UnityEngine.UI.Text[] existing = canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            for (int i = 0; i < existing.Length && mark.font == null; i++)
                if (existing[i] != mark) mark.font = existing[i].font;
            if (mark.font == null) mark.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mark.text = Text;
            mark.fontSize = Mathf.RoundToInt(bubbleSize * .5f);
            mark.fontStyle = FontStyle.Bold;
            mark.alignment = TextAnchor.MiddleCenter;
            mark.horizontalOverflow = HorizontalWrapMode.Overflow;
            mark.verticalOverflow = VerticalWrapMode.Overflow;
            mark.color = MarkColor;
            mark.raycastTarget = false;
            var outline = markRect.gameObject.AddComponent<Outline>();
            outline.effectColor = InkColor;
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            RectTransform blockRect = NewRect("Gourd Alert Block Mark", bubble);
            blockRect.anchorMin = blockRect.anchorMax = center;
            blockRect.sizeDelta = new Vector2(bubbleSize * .68f, bubbleSize * .34f);
            blockMark = blockRect.gameObject.AddComponent<Image>();
            blockMark.sprite = BlockLeftSprite();
            blockMark.preserveAspect = true;
            blockMark.raycastTarget = false;
            blockRect.gameObject.SetActive(false);

            RectTransform heartRect = NewRect("Gourd Alert Heart", bubble);
            heartRect.anchorMin = heartRect.anchorMax = center;
            heartRect.sizeDelta = new Vector2(bubbleSize * .52f, bubbleSize * .52f);
            heartMark = heartRect.gameObject.AddComponent<Image>();
            heartMark.sprite = HeartSprite();
            heartMark.preserveAspect = true;
            heartMark.raycastTarget = false;
            heartRect.gameObject.SetActive(false);

            // 야광귀 눈 (#268): 둘레 빛 → 보통 눈 → 빛나는 눈 순서로 겹친다.
            eyesRect = NewRect("Gourd Alert Eyes", bubble);
            eyesRect.anchorMin = eyesRect.anchorMax = center;
            eyesRect.sizeDelta = new Vector2(bubbleSize * .74f, bubbleSize * .37f);
            eyesHalo = EyesLayer("Gourd Alert Eyes Halo", EyesHaloSprite());
            EyesLayer("Gourd Alert Eyes Plain", EyesSprite(false));
            eyesLit = EyesLayer("Gourd Alert Eyes Lit", EyesSprite(true));
            eyesRect.gameObject.SetActive(false);
            bubble.gameObject.SetActive(false);
            return true;
        }

        private Image EyesLayer(string name, Sprite sprite)
        {
            RectTransform rect = NewRect(name, eyesRect);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// 야광귀의 튀어나온 두 눈 (#268). 보통 눈은 먹색 테두리·흰자·검은 눈동자와 작은 하이라이트, 빛나는 눈은
        /// 흰자가 노랗게, 눈동자가 작고 밝은 심지로 바뀐다. 글꼴 이모지 대신 직접 그린다.
        /// </summary>
        private static Sprite EyesSprite(bool lit)
        {
            if (lit && eyesLitSprite != null) return eyesLitSprite;
            if (!lit && eyesSprite != null) return eyesSprite;
            const int samples = 3;
            var pixels = new Color32[EyesWidth * EyesHeight];
            for (int y = 0; y < EyesHeight; y++)
                for (int x = 0; x < EyesWidth; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                        {
                            var p = new Vector2(x + (sx + .5f) / samples, y + (sy + .5f) / samples);
                            sum += EyeColor(p, lit);
                        }
                    Color average = sum / (samples * samples);
                    // Premultiplied sum: divide colour back by alpha so edges keep the ink colour.
                    if (average.a > 0f) average = new Color(average.r / average.a, average.g / average.a, average.b / average.a, average.a);
                    pixels[y * EyesWidth + x] = average;
                }
            Sprite sprite = SpriteFrom(pixels, EyesWidth, EyesHeight, lit ? "Lab yaguang eyes lit" : "Lab yaguang eyes");
            if (lit) eyesLitSprite = sprite; else eyesSprite = sprite;
            return sprite;
        }

        // One sample of the eyes, premultiplied by its coverage.
        private static Color EyeColor(Vector2 p, bool lit)
        {
            foreach (Vector2 eye in new[] { LeftEye, RightEye })
            {
                float d = Vector2.Distance(p, eye);
                if (d > EyeRadius) continue;
                Color ink = InkColor;
                if (d > EyeRadius - 4f) return ink;
                Color color = lit ? (Color)EyeLitColor : Color.white;
                // 눈동자: 보통은 크고 검게, 빛나면 작고 거의 흰 심지로.
                Vector2 pupil = eye + new Vector2(2f, -1f);
                float pd = Vector2.Distance(p, pupil);
                if (lit)
                {
                    if (pd < 7f) color = Color.Lerp(Color.white, color, pd / 7f);
                }
                else if (pd < 10f) color = ink;
                if (!lit && Vector2.Distance(p, pupil + new Vector2(3f, 4f)) < 3f) color = Color.white;
                return color;
            }
            return Color.clear;
        }

        /// <summary>두 눈 둘레의 부드러운 노란 빛 (#268). 눈이 빛날 때만 보인다.</summary>
        private static Sprite EyesHaloSprite()
        {
            if (eyesHaloSprite != null) return eyesHaloSprite;
            var pixels = new Color32[EyesWidth * EyesHeight];
            for (int y = 0; y < EyesHeight; y++)
                for (int x = 0; x < EyesWidth; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    float d = Mathf.Min(Vector2.Distance(p, LeftEye), Vector2.Distance(p, RightEye));
                    float alpha = Mathf.Clamp01(1f - (d - EyeRadius * .6f) / (EyeRadius * .8f));
                    Color32 color = EyeLitColor;
                    color.a = (byte)Mathf.RoundToInt(alpha * alpha * 255f);
                    pixels[y * EyesWidth + x] = color;
                }
            return eyesHaloSprite = SpriteFrom(pixels, EyesWidth, EyesHeight, "Lab yaguang eyes halo");
        }

        private static Sprite SpriteFrom(Color32[] pixels, int width, int height, string name)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(.5f, .5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>
        /// 매혹 표시 (#251): 먹색 테두리를 두른 하트색 하트. 글꼴에 없을 수 있는 ♥ 대신 직접 그린다.
        /// 테두리는 같은 하트를 조금 작게 그린 안쪽과의 차이다.
        /// </summary>
        private static Sprite HeartSprite()
        {
            if (heartSprite != null) return heartSprite;
            const int samples = 3;
            Color32 fill = LabCharmEdgeFlash.HeartColor;
            var pixels = new Color32[HeartSize * HeartSize];
            for (int y = 0; y < HeartSize; y++)
                for (int x = 0; x < HeartSize; x++)
                {
                    int outer = 0, inner = 0;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = ((x + (sx + .5f) / samples) / HeartSize * 2f - 1f) * 1.25f;
                            float v = ((y + (sy + .5f) / samples) / HeartSize * 2f - 1f) * 1.25f + .12f;
                            if (InHeart(u, v)) outer++;
                            if (InHeart(u / HeartInner, v / HeartInner)) inner++;
                        }
                    float alpha = outer / (float)(samples * samples);
                    Color32 color = Color32.Lerp(InkColor, fill, outer > 0 ? inner / (float)outer : 0f);
                    color.a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * HeartSize + x] = color;
                }
            var texture = new Texture2D(HeartSize, HeartSize, TextureFormat.RGBA32, false)
            {
                name = "Lab gourd charm heart",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            heartSprite = Sprite.Create(texture, new Rect(0f, 0f, HeartSize, HeartSize), new Vector2(.5f, .5f), 100f);
            heartSprite.name = texture.name;
            heartSprite.hideFlags = HideFlags.DontSave;
            return heartSprite;
        }

        /// <summary>The classic heart curve, (u² + v² − 1)³ − u²v³ ≤ 0, lobes up.</summary>
        private static bool InHeart(float u, float v)
        {
            float a = u * u + v * v - 1f;
            return a * a * a - u * u * v * v * v <= 0f;
        }

        private static Sprite BlockLeftSprite() =>
            blockLeftSprite != null ? blockLeftSprite : blockLeftSprite = BlockSprite(false);

        private static Sprite BlockRightSprite() =>
            blockRightSprite != null ? blockRightSprite : blockRightSprite = BlockSprite(true);

        /// <summary>
        /// 막기 표시 (#215): 왼쪽 절반에 막힌 쪽을 가리키는 굵은 먹색 화살표, 오른쪽에 얇은 먹색 테두리를
        /// 두른 빨간 금지 표시(고리와 왼쪽 위에서 오른쪽 아래로 긋는 사선). 글꼴에 없을 수 있는 이모지 대신 직접 그린다.
        /// </summary>
        private static Sprite BlockSprite(bool right)
        {
            var pixels = new Color32[MarkWidth * MarkHeight];
            var center = new Vector2(96f, 32f);
            float slash = 24f / Mathf.Sqrt(2f);
            var slashA = new Vector2(center.x - slash, center.y + slash);
            var slashB = new Vector2(center.x + slash, center.y - slash);
            for (int y = 0; y < MarkHeight; y++)
                for (int x = 0; x < MarkWidth; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    // The arrow is drawn pointing left in its half, mirrored there for the right.
                    var a = new Vector2(right ? 64f - p.x : p.x, p.y);
                    float arrow = Mathf.Min(BoxDistance(a, 24f, 26f, 58f, 38f),
                        TriangleDistance(a, new Vector2(6f, 32f), new Vector2(30f, 52f), new Vector2(30f, 12f)));
                    float sign = Mathf.Min(Mathf.Abs(Vector2.Distance(p, center) - 24f) - 5f,
                        SegmentDistance(p, slashA, slashB) - 5f);
                    Color32 color = new Color32(0, 0, 0, 0);
                    if (sign < 2.5f)
                    {
                        color = Color32.Lerp(SignColor, InkColor, Mathf.Clamp01(sign + 1f));
                        color.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(2.5f - sign) * 255f);
                    }
                    else if (arrow < .5f)
                    {
                        color = InkColor;
                        color.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(.5f - arrow) * 255f);
                    }
                    pixels[y * MarkWidth + x] = color;
                }
            var texture = new Texture2D(MarkWidth, MarkHeight, TextureFormat.RGBA32, false)
            {
                name = right ? "Lab gourd block right" : "Lab gourd block left",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, MarkWidth, MarkHeight), new Vector2(.5f, .5f), 100f);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>점에서 직사각형 테두리까지의 거리. 안쪽이면 음수.</summary>
        private static float BoxDistance(Vector2 p, float xMin, float yMin, float xMax, float yMax)
        {
            float dx = Mathf.Max(xMin - p.x, 0f, p.x - xMax), dy = Mathf.Max(yMin - p.y, 0f, p.y - yMax);
            float outside = Mathf.Sqrt(dx * dx + dy * dy);
            return outside > 0f ? outside : -Mathf.Min(Mathf.Min(p.x - xMin, xMax - p.x), Mathf.Min(p.y - yMin, yMax - p.y));
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        /// <summary>
        /// 손그림 느낌의 말풍선: 흰 타원 몸통과 왼쪽 아래로 뻗은 꼬리를 하나로 합친 모양에 굵은
        /// 테두리를 두른다. 두 모양을 거리값으로 합쳐 이음매 없이 테두리가 이어진다.
        /// </summary>
        private static Sprite BubbleSprite()
        {
            if (bubbleSprite != null) return bubbleSprite;
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    Vector2 q = (p - BodyCenter) / BodyRadii;
                    float body = (q.magnitude - 1f) * Mathf.Min(BodyRadii.x, BodyRadii.y);
                    float tail = TriangleDistance(p, TailBaseA, TailTip, TailBaseB);
                    float distance = Mathf.Min(body, tail); // negative inside the bubble
                    float alpha = Mathf.Clamp01(.5f - distance);
                    float ink = Mathf.Clamp01(distance + OutlinePixels + .5f);
                    Color32 color = Color32.Lerp(FillColor, InkColor, ink);
                    color.a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * TextureSize + x] = color;
                }
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "Lab gourd alert bubble",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            bubbleSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(.5f, .5f), 100f);
            bubbleSprite.name = texture.name;
            bubbleSprite.hideFlags = HideFlags.DontSave;
            return bubbleSprite;
        }

        /// <summary>점에서 삼각형 테두리까지의 거리. 안쪽이면 음수.</summary>
        private static float TriangleDistance(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float distance = Mathf.Min(SegmentDistance(p, a, b), Mathf.Min(SegmentDistance(p, b, c), SegmentDistance(p, c, a)));
            float ab = Cross(b - a, p - a), bc = Cross(c - b, p - b), ca = Cross(a - c, p - c);
            bool inside = (ab >= 0f && bc >= 0f && ca >= 0f) || (ab <= 0f && bc <= 0f && ca <= 0f);
            return inside ? -distance : distance;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            if (network != null)
            {
                network.GourdBombDropped -= OnGourdBombDropped;
                network.SwipeBlockStarted -= OnSwipeBlockStarted;
                network.SabotageCast -= OnSabotageCast;
            }
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
