using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace C6Lab
{
    /// <summary>
    /// #479 지귀 부적: A가 시작되면 아직 부적을 안 쓴 사람의 조합판 위에 노란 부적을 덮고, 쓰는 동안 판 조작(구슬 끌기·북·던지기)을 막는다.
    /// 부적의 점선 "물"을 손가락으로 따라 쓰면(획 순서/손 떼기 자유, 점선 밖은 무시) 지나간 점이 붉게 칠해지고,
    /// 정해 둔 비율이 칠해지면 이 기기가 Host에 한 번 알린다. 불 끄기와 보상은 Host가 정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabJigwiTalismanOverlay : MonoBehaviour
    {
        [Tooltip("조합판 영역 안쪽 여백 (판 크기 대비). 부적 종이가 판 가장자리에서 이만큼 떨어진다.")]
        [SerializeField, Range(0f, .2f)] private float margin = .04f;
        [SerializeField] private Color paperColor = new Color(.96f, .82f, .38f, .97f);
        [SerializeField] private Color inkColor = new Color(.72f, .11f, .07f, 1f);
        [SerializeField] private Color guideColor = new Color(.45f, .3f, .12f, .35f);
        [Tooltip("#479: 손가락이 지나가 칠해진 점선 색 (물빛 파랑).")]
        [SerializeField] private Color writtenColor = new Color(.1f, .42f, .95f, 1f);
        /// <summary>요청이 사라졌을 때 다시 보내기까지 (초)</summary>
        private const float ResendSeconds = 1f;

        private LabNetwork source;
        private RectTransform root, paper, frame, glyphArea;
        private UnityEngine.UI.Image paperImage, frameImage;
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
            if (Visible)
            {
                FitToBoard(source.board != null ? source.board.ViewCamera : null);
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
            if (round?.id != RoundId && round?.id != writtenRound) ResetWriting();
            // A talisman this screen already finished stays hidden while the Host confirms it.
            Visible = round != null && round.id != writtenRound;
            RoundId = Visible ? round.id : null;
            if (paper != null) paper.gameObject.SetActive(Visible);
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
            if (paper != null) paper.gameObject.SetActive(false);
            SetBoardBlocked(false);
        }

        private float Tolerance => source != null && source.config != null ? source.config.JigwiTalismanTolerance : .07f;
        private float Required => source != null && source.config != null ? source.config.JigwiTalismanCoverage : .95f;

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

        /// <summary>판을 비추는 카메라의 화면 칸에 맞춰 부적 종이를 놓는다.</summary>
        private void FitToBoard(Camera boardCamera)
        {
            if (paper == null || root == null) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            Rect pixels = boardCamera != null ? boardCamera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height * .46f);
            Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float inset = Mathf.Min(pixels.width, pixels.height) * margin;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                    new Vector2(pixels.xMin + inset, pixels.yMin + inset), eye, out Vector2 low)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                    new Vector2(pixels.xMax - inset, pixels.yMax - inset), eye, out Vector2 high)) return;
            // Local points are relative to the root's pivot, which is what localPosition uses.
            paper.anchorMin = paper.anchorMax = paper.pivot = new Vector2(.5f, .5f);
            paper.localPosition = new Vector3((low.x + high.x) * .5f, (low.y + high.y) * .5f, 0f);
            paper.sizeDelta = new Vector2(Mathf.Abs(high.x - low.x), Mathf.Abs(high.y - low.y));
            LayoutGlyph();
        }

        /// <summary>"물" 칸은 늘 정사각형으로, 안내 문구 아래 가운데에 둔다.</summary>
        private void LayoutGlyph()
        {
            if (glyphArea == null || paper == null) return;
            Vector2 size = paper.rect.size;
            float side = Mathf.Max(1f, Mathf.Min(size.x * .86f, size.y * .74f));
            glyphArea.anchorMin = glyphArea.anchorMax = glyphArea.pivot = new Vector2(.5f, .5f);
            glyphArea.anchoredPosition = new Vector2(0f, -size.y * .06f);
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
            paper.sizeDelta = new Vector2(360f, 360f);
            paperImage = paper.gameObject.AddComponent<UnityEngine.UI.Image>();
            paperImage.color = paperColor; paperImage.raycastTarget = false;
            frame = NewRect("Talisman Frame", paper);
            Stretch(frame, .035f);
            frameImage = frame.gameObject.AddComponent<UnityEngine.UI.Image>();
            frameImage.color = new Color(0f, 0f, 0f, 0f); frameImage.raycastTarget = false;
            var outline = frame.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = inkColor; outline.effectDistance = new Vector2(3f, -3f);
            hint = NewText("Talisman Hint", paper, "손가락으로 '물'을 따라 쓰세요", 22, inkColor);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(.05f, .86f); hintRect.anchorMax = new Vector2(.95f, .98f);
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

        private void OnDisable() { SetBoardBlocked(false); Visible = false; RoundId = null; hasLast = false; if (paper != null) paper.gameObject.SetActive(false); }
        private void OnDestroy() => SetBoardBlocked(false);
    }

    /// <summary>
    /// #479 부적 "물"의 점선. 글자 칸(0~1, 위가 1) 안의 꺾은선을 일정 간격 점으로 나누고,
    /// 지나간 선분에서 여유 거리 안에 든 점을 칠한다. 획 순서와 손 떼기는 따지지 않는다.
    /// </summary>
    public sealed class LabJigwiTalismanStroke
    {
        /// <summary>한글 "물": ㅁ(위) / ㅜ(가운데) / ㄹ(아래)</summary>
        public static readonly Vector2[][] Water =
        {
            new[] { new Vector2(.3f, .9f), new Vector2(.3f, .68f) },
            new[] { new Vector2(.3f, .9f), new Vector2(.7f, .9f), new Vector2(.7f, .68f) },
            new[] { new Vector2(.3f, .68f), new Vector2(.7f, .68f) },
            new[] { new Vector2(.12f, .58f), new Vector2(.88f, .58f) },
            new[] { new Vector2(.5f, .58f), new Vector2(.5f, .44f) },
            new[] { new Vector2(.28f, .36f), new Vector2(.72f, .36f), new Vector2(.72f, .24f),
                new Vector2(.28f, .24f), new Vector2(.28f, .1f), new Vector2(.74f, .1f) }
        };

        public readonly Vector2[] Samples;
        private readonly bool[] covered;
        public int CoveredCount { get; private set; }
        public float Coverage => Samples.Length == 0 ? 0f : CoveredCount / (float)Samples.Length;

        public LabJigwiTalismanStroke(Vector2[][] lines, float spacing = .03f)
        {
            var points = new List<Vector2>();
            foreach (Vector2[] line in lines)
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(line[i], line[i + 1]) / spacing));
                    for (int s = i == 0 ? 0 : 1; s <= steps; s++) points.Add(Vector2.Lerp(line[i], line[i + 1], s / (float)steps));
                }
            Samples = points.ToArray();
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
