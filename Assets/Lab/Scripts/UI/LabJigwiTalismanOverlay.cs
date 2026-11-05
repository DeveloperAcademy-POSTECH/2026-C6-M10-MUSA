using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace C6Lab
{
    /// <summary>
    /// #479 지귀 부적: A가 시작되면 아직 부적을 안 쓴 사람의 전투 영역(박 쪽)에 세로 한지 부적을 띄우고, 쓰는 동안 조합판 조작(구슬 끌기·북·던지기)을 막는다.
    /// 부적의 점선 "물"을 손가락으로 따라 쓰면(획 순서/손 떼기 자유, 점선 밖은 무시) 지나간 점이 붉게 칠해지고,
    /// 정해 둔 비율이 칠해지면 이 기기가 Host에 한 번 알린다. 불 끄기와 보상은 Host가 정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabJigwiTalismanOverlay : MonoBehaviour
    {
        [Tooltip("조합판 영역 안쪽 여백 (판 크기 대비). 부적 종이가 판 가장자리에서 이만큼 떨어진다.")]
        [SerializeField, Range(0f, .2f)] private float margin = .04f;
        [Tooltip("한지 무늬 위에 곱하는 색. 흰색이면 한지 그대로.")]
        [SerializeField] private Color paperColor = new Color(1f, 1f, 1f, .98f);
        [Tooltip("부적 종이의 가로/세로 비율. 작을수록 좁고 길다.")]
        [SerializeField, Range(.35f, 1f)] private float paperAspect = .55f;
        [Tooltip("전투 영역 위쪽에서 비워 둘 높이 (전투 영역 대비). 시간·체력 HUD를 가리지 않게.")]
        [SerializeField, Range(0f, .4f)] private float topInset = .14f;
        [SerializeField] private Color inkColor = new Color(.72f, .11f, .07f, 1f);
        [SerializeField] private Color guideColor = new Color(.45f, .3f, .12f, .35f);
        [Tooltip("#479: 손가락이 지나가 칠해진 점선 색 (물빛 파랑).")]
        [SerializeField] private Color writtenColor = new Color(.1f, .42f, .95f, 1f);
        [Tooltip("#509: 다 쓴 글씨가 빛나는 시간(초). 그다음 부적이 천천히 사라진다.")]
        [SerializeField, Range(.1f, 2f)] private float glowSeconds = .6f;
        [Tooltip("#509: 빛난 뒤 부적이 사라지는 시간(초).")]
        [SerializeField, Range(.2f, 3f)] private float fadeSeconds = 1.2f;
        [Tooltip("#509: 다 쓴 글씨가 빛날 때의 색 (금빛).")]
        [SerializeField] private Color glowColor = new Color(1f, .86f, .32f, 1f);
        /// <summary>요청이 사라졌을 때 다시 보내기까지 (초)</summary>
        private const float ResendSeconds = 1f;
        private CanvasGroup paperGroup;
        private float finishAge = -1f;
        private Vector3 finishFrom;

        private LabNetwork source;
        private RectTransform root, paper, frame, glyphArea;
        private UnityEngine.UI.RawImage paperImage;
        private UnityEngine.UI.Image frameImage;
        private static Texture2D hanji;
        private UnityEngine.UI.Image[] dots = Array.Empty<UnityEngine.UI.Image>();
        private UnityEngine.UI.Text hint;
        private Font font;
        private bool blocking, hasLast;
        private Vector2 last;
        private string writtenRound;
        private float sentAt;
        private readonly LabJigwiTalismanStroke stroke = new LabJigwiTalismanStroke(LabJigwiTalismanStroke.Water);
        private readonly List<int> fresh = new List<int>();

        /// <summary>부적이 보이는지</summary>
        public bool Visible { get; private set; }
        /// <summary>지금 덮고 있는 A 회차 id (보이지 않으면 null)</summary>
        public string RoundId { get; private set; }
        /// <summary>판 조작을 막아야 하는지 (보일 때만)</summary>
        public bool WantsBoardBlocked => Visible;
        public RectTransform Paper => paper;
        /// <summary>점선 중 칠해진 비율 (0~1)</summary>
        public float Coverage => stroke.Coverage;
        /// <summary>이 화면에서 부적을 다 쓴 횟수 (요청을 보낸 횟수, 다시 보낸 것은 빼고)</summary>
        public int CompletedCount { get; private set; }
        public LabJigwiTalismanStroke Stroke => stroke;
        /// <summary>#509: 다 쓴 부적이 빛나며 사라지는 중인지. 이때 조합판은 이미 풀려 있다.</summary>
        public bool Finishing => finishAge >= 0f;
        public float FinishSeconds => glowSeconds + fadeSeconds;
        /// <summary>#509: 사라지는 동안의 부적 불투명도 (1 → 0).</summary>
        public float PaperAlpha => paperGroup != null ? paperGroup.alpha : 1f;

        public void Bind(LabNetwork network, Font labelFont)
        {
            source = network;
            if (labelFont != null) font = labelFont;
            EnsureWidgets();
            if (labelFont != null && hint != null) hint.font = labelFont;
        }

        /// <summary>아직 부적을 안 쓴 내 자리가 있는 진행 중인 A. 없으면 null.</summary>
        public static LabJigwiABRound PendingTalisman(LabSnapshot snapshot, ulong localPlayer, double now)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.jigwiAB?.enabled != true) return null;
            LabJigwiABRound round = snapshot.jigwiAB.Active(now);
            if (round == null || round.kind != LabJigwiABKind.PersonalBurn || !round.Active(now)) return null;
            foreach (LabJigwiABPlayerFire seat in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                if (seat != null && seat.playerId == localPlayer) return seat.talismanAt < 0d ? round : null;
            return null;
        }

        private void LateUpdate()
        {
            if (source == null) { Present(null, ulong.MaxValue, 0d); return; }
            LabSnapshot snapshot = source.Snapshot;
            Present(snapshot, source.LocalPlayerId, source.JigwiABPresentationTime);
            if (Finishing) AdvanceFinish(Time.unscaledDeltaTime);
            if (Visible)
            {
                FitToBattle(source.board != null ? source.board.ViewCamera : null);
                bool held = snapshot == null || snapshot.paused || source.GameStartHeld || source.BattlePresentationHeld;
                if (held) hasLast = false; else ReadPointer();
            }
            ResendIfLost(snapshot);
        }

        /// <summary>테스트와 LateUpdate가 같이 쓰는 표시 진입점.</summary>
        public void Present(LabSnapshot snapshot, ulong localPlayer, double now)
        {
            EnsureWidgets();
            LabJigwiABRound round = PendingTalisman(snapshot, localPlayer, now);
            // A new talisman cuts a still-fading old one short.
            if (round != null && round.id != writtenRound && Finishing) EndFinish();
            if (round?.id != RoundId && round?.id != writtenRound && !Finishing) ResetWriting();
            // A talisman this screen already finished stays hidden while the Host confirms it.
            Visible = round != null && round.id != writtenRound;
            RoundId = Visible ? round.id : null;
            if (paper != null) paper.gameObject.SetActive(Visible || Finishing);
            if (!Visible) hasLast = false;
            SetBoardBlocked(Visible);
        }

        /// <summary>
        /// 부적 위 한 획 조각을 칠한다. from/to는 "물" 글자 칸 기준 0~1 좌표다. 다 쓰면 Host에 알리고 부적을 내린다.
        /// LateUpdate의 손가락/마우스 입력과 테스트가 같이 쓴다.
        /// </summary>
        public void PaintNormalized(Vector2 from, Vector2 to)
        {
            if (!Visible) return;
            fresh.Clear();
            stroke.Paint(from, to, Tolerance, fresh);
            for (int i = 0; i < fresh.Count; i++)
                if (fresh[i] < dots.Length && dots[fresh[i]] != null) dots[fresh[i]].color = writtenColor;
            if (stroke.Coverage + 1e-5f < Required) return;
            writtenRound = RoundId;
            CompletedCount++;
            Send();
            Visible = false; RoundId = null; hasLast = false;
            SetBoardBlocked(false);
            // #509: the written "물" glows, then the talisman slowly fades away (the board is already free).
            BeginFinish();
        }

        private void BeginFinish()
        {
            if (paper == null) return;
            finishAge = 0f; finishFrom = paper.localPosition;
            paper.gameObject.SetActive(true);
            if (hint != null) hint.gameObject.SetActive(false);
            DrawFinish();
        }

        /// <summary>#509: LateUpdate와 테스트가 같이 쓰는 빛남/사라짐 진행.</summary>
        public void AdvanceFinish(float seconds)
        {
            if (!Finishing || float.IsNaN(seconds) || seconds < 0f) return;
            finishAge += seconds;
            if (finishAge >= FinishSeconds) { EndFinish(); return; }
            DrawFinish();
        }

        private void DrawFinish()
        {
            float glow = Mathf.Clamp01(finishAge / Mathf.Max(.01f, glowSeconds));
            float fade = Mathf.Clamp01((finishAge - glowSeconds) / Mathf.Max(.01f, fadeSeconds));
            float eased = fade * fade * (3f - 2f * fade);
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null || !stroke.IsCovered(i)) continue;
                dots[i].color = Color.Lerp(writtenColor, glowColor, glow);
                float pulse = 1f + .55f * glow + .15f * Mathf.Sin(finishAge * 14f) * (1f - fade);
                dots[i].rectTransform.localScale = Vector3.one * pulse;
            }
            if (paperImage != null) paperImage.color = Color.Lerp(paperColor, new Color(1f, .97f, .85f, paperColor.a), glow);
            if (paperGroup != null) paperGroup.alpha = 1f - eased;
            paper.localScale = Vector3.one * (1f + .05f * eased);
            paper.localPosition = finishFrom + Vector3.up * (paper.rect.height * .06f * eased);
        }

        private void EndFinish()
        {
            finishAge = -1f;
            if (paper == null) return;
            if (paperGroup != null) paperGroup.alpha = 1f;
            paper.localScale = Vector3.one; paper.localPosition = finishFrom;
            if (paperImage != null) paperImage.color = paperColor;
            if (hint != null) hint.gameObject.SetActive(true);
            for (int i = 0; i < dots.Length; i++) if (dots[i] != null) dots[i].rectTransform.localScale = Vector3.one;
            paper.gameObject.SetActive(Visible);
        }

        private float Tolerance => source != null && source.config != null ? source.config.JigwiTalismanTolerance : .07f;
        private float Required => source != null && source.config != null ? source.config.JigwiTalismanCoverage : .9f;

        private void Send()
        {
            sentAt = Time.unscaledTime;
            if (source != null && writtenRound != null) source.RequestJigwiTalismanDone(writtenRound);
        }

        private void ResendIfLost(LabSnapshot snapshot)
        {
            if (writtenRound == null || source == null || Time.unscaledTime - sentAt < ResendSeconds) return;
            LabJigwiABRound round = PendingTalisman(snapshot, source.LocalPlayerId, source.JigwiABPresentationTime);
            if (round != null && round.id == writtenRound) Send();
        }

        private void ReadPointer()
        {
            Vector2 screen = default; bool pressed = false, began = false;
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && touch.press.isPressed)
            { screen = touch.position.ReadValue(); pressed = true; began = touch.press.wasPressedThisFrame; }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            { screen = Mouse.current.position.ReadValue(); pressed = true; began = Mouse.current.leftButton.wasPressedThisFrame; }
            if (!pressed || !TryNormalize(screen, out Vector2 point)) { hasLast = false; return; }
            if (began || !hasLast) last = point;
            Vector2 from = last;
            last = point; hasLast = true;
            PaintNormalized(from, point);
        }

        private bool TryNormalize(Vector2 screen, out Vector2 point)
        {
            point = default;
            if (glyphArea == null) return false;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(glyphArea, screen, eye, out Vector2 local)) return false;
            Rect rect = glyphArea.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;
            point = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
            return true;
        }

        private void ResetWriting()
        {
            stroke.Clear(); hasLast = false; writtenRound = null;
            for (int i = 0; i < dots.Length; i++) if (dots[i] != null) dots[i].color = guideColor;
        }

        private void SetBoardBlocked(bool value)
        {
            if (blocking == value) return;
            blocking = value;
            if (source != null && source.board != null) source.board.SetUiInputBlocked(value);
        }

        /// <summary>
        /// 조합판 위쪽 전투 영역(박이 보이는 칸)에 부적 종이를 놓는다. 아래 조합판은 가리지 않아 구슬이 타는 게 보인다.
        /// 위쪽 HUD(시간·체력)는 topInset만큼 비운다.
        /// </summary>
        private void FitToBattle(Camera boardCamera)
        {
            if (paper == null || root == null) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            float boardTop = boardCamera != null ? boardCamera.pixelRect.yMax : Screen.height * .46f;
            var pixels = new Rect(0f, boardTop, Screen.width, Mathf.Max(1f, Screen.height - boardTop));
            Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float inset = Mathf.Min(pixels.width, pixels.height) * margin;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                    new Vector2(pixels.xMin + inset, pixels.yMin + inset), eye, out Vector2 low)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                    new Vector2(pixels.xMax - inset, pixels.yMax - pixels.height * topInset), eye, out Vector2 high)) return;
            // Local points are relative to the root's pivot, which is what localPosition uses.
            paper.anchorMin = paper.anchorMax = paper.pivot = new Vector2(.5f, .5f);
            paper.localPosition = new Vector3((low.x + high.x) * .5f, (low.y + high.y) * .5f, 0f);
            // A tall, narrow talisman inside the board area.
            float height = Mathf.Abs(high.y - low.y), width = Mathf.Min(Mathf.Abs(high.x - low.x), height * paperAspect);
            paper.sizeDelta = new Vector2(width, height);
            LayoutGlyph();
        }

        /// <summary>"물" 칸은 늘 정사각형으로, 안내 문구 아래 가운데에 둔다.</summary>
        private void LayoutGlyph()
        {
            if (glyphArea == null || paper == null) return;
            Vector2 size = paper.rect.size;
            float side = Mathf.Max(1f, Mathf.Min(size.x * .84f, size.y * .62f));
            glyphArea.anchorMin = glyphArea.anchorMax = glyphArea.pivot = new Vector2(.5f, .5f);
            glyphArea.anchoredPosition = new Vector2(0f, -size.y * .04f);
            glyphArea.sizeDelta = new Vector2(side, side);
            float dot = Mathf.Max(4f, side * .045f);
            for (int i = 0; i < dots.Length; i++)
                if (dots[i] != null) dots[i].rectTransform.sizeDelta = new Vector2(dot, dot);
        }

        private void EnsureWidgets()
        {
            if (paper != null) return;
            root = transform as RectTransform;
            if (root == null) return;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            paper = NewRect("Talisman Paper", root);
            paper.sizeDelta = new Vector2(200f, 360f);
            paperGroup = paper.gameObject.AddComponent<CanvasGroup>();
            paperGroup.blocksRaycasts = false; paperGroup.interactable = false;
            paperImage = paper.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            paperImage.texture = HanjiTexture(); paperImage.color = paperColor; paperImage.raycastTarget = false;
            frame = NewRect("Talisman Frame", paper);
            Stretch(frame, .035f);
            frameImage = frame.gameObject.AddComponent<UnityEngine.UI.Image>();
            frameImage.color = new Color(0f, 0f, 0f, 0f); frameImage.raycastTarget = false;
            var outline = frame.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = inkColor; outline.effectDistance = new Vector2(3f, -3f);
            hint = NewText("Talisman Hint", paper, "손가락으로 '물'을 적어\n부적을 완성하세요.", 22, inkColor);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(.08f, .83f); hintRect.anchorMax = new Vector2(.92f, .96f);
            hintRect.offsetMin = hintRect.offsetMax = Vector2.zero;
            hint.resizeTextForBestFit = true; hint.resizeTextMinSize = 10; hint.resizeTextMaxSize = 28;
            glyphArea = NewRect("Talisman Glyph (물)", paper);
            dots = new UnityEngine.UI.Image[stroke.Samples.Length];
            for (int i = 0; i < dots.Length; i++)
            {
                var dot = NewRect("Dot " + i, glyphArea).gameObject.AddComponent<UnityEngine.UI.Image>();
                dot.raycastTarget = false; dot.color = guideColor;
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = stroke.Samples[i];
                dot.rectTransform.anchoredPosition = Vector2.zero;
                dots[i] = dot;
            }
            LayoutGlyph();
            paper.gameObject.SetActive(false);
        }

        /// <summary>
        /// 한지 질감 (런타임에 한 번 만든다, 에셋 없음): 노란 괴황지 바탕에 얼룩, 가는 섬유, 가장자리 바램.
        /// </summary>
        public static Texture2D HanjiTexture()
        {
            if (hanji != null) return hanji;
            const int width = 256, height = 512;
            var light = new Color(.98f, .88f, .52f); var dark = new Color(.86f, .7f, .34f);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float mottle = Mathf.PerlinNoise(x * .03f + 7f, y * .03f + 3f) * .65f
                        + Mathf.PerlinNoise(x * .13f + 31f, y * .13f + 17f) * .35f;
                    float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y)) / 26f);
                    Color c = Color.Lerp(dark, light, mottle);
                    pixels[y * width + x] = c * Mathf.Lerp(.8f, 1f, edge);
                }
            var random = new System.Random(479);
            for (int i = 0; i < 900; i++)
            {
                float px = (float)random.NextDouble() * width, py = (float)random.NextDouble() * height;
                float angle = (float)(random.NextDouble() * Math.PI * 2d), length = 5f + (float)random.NextDouble() * 20f;
                Color fiber = random.NextDouble() < .6d ? new Color(1f, .96f, .78f) : new Color(.78f, .6f, .28f);
                float strength = .18f + (float)random.NextDouble() * .25f;
                for (float t = 0f; t < length; t += .7f)
                {
                    int fx = Mathf.RoundToInt(px + Mathf.Cos(angle) * t), fy = Mathf.RoundToInt(py + Mathf.Sin(angle) * t);
                    if (fx < 0 || fy < 0 || fx >= width || fy >= height) break;
                    int index = fy * width + fx;
                    pixels[index] = Color.Lerp(pixels[index], fiber, strength);
                }
            }
            hanji = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "Talisman Hanji (#479 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < pixels.Length; i++) pixels[i].a = 1f;
            hanji.SetPixels(pixels); hanji.Apply(false, true);
            return hanji;
        }

        private UnityEngine.UI.Text NewText(string name, RectTransform parent, string value, int size, Color color)
        {
            var text = NewRect(name, parent).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font; text.text = value; text.color = color; text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleCenter; if (size > 0) text.fontSize = size;
            return text;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = new Vector2(inset, inset); rect.anchorMax = new Vector2(1f - inset, 1f - inset);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void OnDisable() { SetBoardBlocked(false); Visible = false; RoundId = null; hasLast = false; EndFinish(); if (paper != null) paper.gameObject.SetActive(false); }
        private void OnDestroy() => SetBoardBlocked(false);
    }

    /// <summary>
    /// #479 부적 "물"의 점선. 신라문화체 굵은 "물"을 그린 뒤 획의 가운데 선만 남겨 일정 간격(글자 칸의 2.8%)으로 고른 점이다
    /// (글자 칸 0~1, 위가 1). 지나간 선분에서 여유 거리 안에 든 점을 칠하며, 획 순서와 손 떼기는 따지지 않는다.
    /// </summary>
    public sealed class LabJigwiTalismanStroke
    {
        /// <summary>신라문화체(ShillaCultureBold) "물"의 가운데 선 점들</summary>
        public static readonly Vector2[] Water =
        {
            new Vector2(0.514f, 0.122f), new Vector2(0.549f, 0.124f), new Vector2(0.467f, 0.145f), new Vector2(0.575f, 0.138f), new Vector2(0.603f, 0.148f), new Vector2(0.446f, 0.164f),
            new Vector2(0.630f, 0.156f), new Vector2(0.657f, 0.166f), new Vector2(0.685f, 0.177f), new Vector2(0.425f, 0.195f), new Vector2(0.711f, 0.188f), new Vector2(0.738f, 0.195f),
            new Vector2(0.415f, 0.224f), new Vector2(0.407f, 0.254f), new Vector2(0.486f, 0.264f), new Vector2(0.370f, 0.271f), new Vector2(0.433f, 0.266f), new Vector2(0.515f, 0.269f),
            new Vector2(0.544f, 0.274f), new Vector2(0.574f, 0.280f), new Vector2(0.601f, 0.288f), new Vector2(0.630f, 0.290f), new Vector2(0.616f, 0.314f), new Vector2(0.627f, 0.340f),
            new Vector2(0.640f, 0.369f), new Vector2(0.397f, 0.379f), new Vector2(0.426f, 0.382f), new Vector2(0.456f, 0.385f), new Vector2(0.485f, 0.389f), new Vector2(0.651f, 0.400f),
            new Vector2(0.680f, 0.401f), new Vector2(0.509f, 0.408f), new Vector2(0.538f, 0.410f), new Vector2(0.567f, 0.410f), new Vector2(0.596f, 0.411f), new Vector2(0.625f, 0.413f),
            new Vector2(0.522f, 0.434f), new Vector2(0.494f, 0.453f), new Vector2(0.522f, 0.464f), new Vector2(0.551f, 0.460f), new Vector2(0.215f, 0.484f), new Vector2(0.244f, 0.487f),
            new Vector2(0.273f, 0.492f), new Vector2(0.522f, 0.494f), new Vector2(0.302f, 0.497f), new Vector2(0.330f, 0.503f), new Vector2(0.357f, 0.511f), new Vector2(0.386f, 0.516f),
            new Vector2(0.415f, 0.521f), new Vector2(0.444f, 0.524f), new Vector2(0.498f, 0.524f), new Vector2(0.535f, 0.519f), new Vector2(0.554f, 0.542f), new Vector2(0.648f, 0.540f),
            new Vector2(0.580f, 0.555f), new Vector2(0.607f, 0.563f), new Vector2(0.832f, 0.565f), new Vector2(0.637f, 0.568f), new Vector2(0.666f, 0.569f), new Vector2(0.695f, 0.574f),
            new Vector2(0.788f, 0.574f), new Vector2(0.722f, 0.584f), new Vector2(0.751f, 0.581f), new Vector2(0.527f, 0.644f), new Vector2(0.428f, 0.665f), new Vector2(0.486f, 0.665f),
            new Vector2(0.457f, 0.668f), new Vector2(0.528f, 0.673f), new Vector2(0.404f, 0.684f), new Vector2(0.556f, 0.681f), new Vector2(0.585f, 0.684f), new Vector2(0.536f, 0.704f),
            new Vector2(0.389f, 0.715f), new Vector2(0.544f, 0.734f), new Vector2(0.376f, 0.744f), new Vector2(0.556f, 0.760f), new Vector2(0.372f, 0.775f), new Vector2(0.570f, 0.784f),
            new Vector2(0.360f, 0.804f), new Vector2(0.580f, 0.813f), new Vector2(0.351f, 0.834f), new Vector2(0.475f, 0.839f), new Vector2(0.586f, 0.844f), new Vector2(0.502f, 0.849f),
            new Vector2(0.338f, 0.863f), new Vector2(0.530f, 0.859f), new Vector2(0.557f, 0.865f), new Vector2(0.601f, 0.868f)
        };

        public readonly Vector2[] Samples;
        private readonly bool[] covered;
        public int CoveredCount { get; private set; }
        public float Coverage => Samples.Length == 0 ? 0f : CoveredCount / (float)Samples.Length;

        public LabJigwiTalismanStroke(Vector2[] samples)
        {
            Samples = (Vector2[])samples.Clone();
            covered = new bool[Samples.Length];
        }

        public bool IsCovered(int index) => covered[index];

        public void Clear() { Array.Clear(covered, 0, covered.Length); CoveredCount = 0; }

        /// <summary>선분 from→to에서 tolerance 안에 든 점을 칠하고, 새로 칠한 점 번호를 fresh에 담는다.</summary>
        public void Paint(Vector2 from, Vector2 to, float tolerance, List<int> fresh = null)
        {
            if (!Finite(from) || !Finite(to) || !(tolerance > 0f)) return;
            float limit = tolerance * tolerance;
            for (int i = 0; i < Samples.Length; i++)
            {
                if (covered[i] || DistanceSquared(Samples[i], from, to) > limit) continue;
                covered[i] = true; CoveredCount++;
                fresh?.Add(i);
            }
        }

        private static bool Finite(Vector2 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);

        private static float DistanceSquared(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float length = ab.sqrMagnitude;
            float t = length <= 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / length);
            return (a + ab * t - p).sqrMagnitude;
        }
    }
}
