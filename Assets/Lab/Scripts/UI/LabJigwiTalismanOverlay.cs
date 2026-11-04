using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #479 지귀 부적: A가 시작되면 아직 부적을 안 쓴 사람의 조합판 위에 노란 부적을 덮고, 쓰는 동안 판 조작(구슬 끌기·북·던지기)을 막는다.
    /// 스냅샷만 보고 그리며, 불 끄기와 보상은 Host가 정한다. "물" 쓰기 판정은 (5/6)에서 더한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabJigwiTalismanOverlay : MonoBehaviour
    {
        [Tooltip("조합판 영역 안쪽 여백 (판 크기 대비). 부적 종이가 판 가장자리에서 이만큼 떨어진다.")]
        [SerializeField, Range(0f, .2f)] private float margin = .04f;
        [SerializeField] private Color paperColor = new Color(.96f, .82f, .38f, .97f);
        [SerializeField] private Color inkColor = new Color(.72f, .11f, .07f, 1f);

        private LabNetwork source;
        private RectTransform root, paper, frame;
        private UnityEngine.UI.Image paperImage, frameImage;
        private UnityEngine.UI.Text glyph, hint;
        private Font font;
        private bool blocking;

        /// <summary>부적이 보이는지</summary>
        public bool Visible { get; private set; }
        /// <summary>지금 덮고 있는 A 회차 id (보이지 않으면 null)</summary>
        public string RoundId { get; private set; }
        /// <summary>판 조작을 막아야 하는지 (보일 때만)</summary>
        public bool WantsBoardBlocked => Visible;
        public RectTransform Paper => paper;

        public void Bind(LabNetwork network, Font labelFont)
        {
            source = network;
            if (labelFont != null) font = labelFont;
            EnsureWidgets();
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
            Present(source.Snapshot, source.LocalPlayerId, source.JigwiABPresentationTime);
            if (Visible) FitToBoard(source.board != null ? source.board.ViewCamera : null);
        }

        /// <summary>테스트와 LateUpdate가 같이 쓰는 표시 진입점.</summary>
        public void Present(LabSnapshot snapshot, ulong localPlayer, double now)
        {
            EnsureWidgets();
            LabJigwiABRound round = PendingTalisman(snapshot, localPlayer, now);
            Visible = round != null;
            RoundId = round?.id;
            if (paper != null) paper.gameObject.SetActive(Visible);
            SetBoardBlocked(Visible);
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
        }

        private void EnsureWidgets()
        {
            if (paper != null) return;
            root = transform as RectTransform;
            if (root == null) return;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            paper = NewRect("Talisman Paper", root);
            paperImage = paper.gameObject.AddComponent<UnityEngine.UI.Image>();
            paperImage.color = paperColor; paperImage.raycastTarget = false;
            frame = NewRect("Talisman Frame", paper);
            Stretch(frame, .035f);
            frameImage = frame.gameObject.AddComponent<UnityEngine.UI.Image>();
            frameImage.color = new Color(0f, 0f, 0f, 0f); frameImage.raycastTarget = false;
            var outline = frame.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = inkColor; outline.effectDistance = new Vector2(3f, -3f);
            glyph = NewText("Talisman Glyph", paper, "물", 0, new Color(inkColor.r, inkColor.g, inkColor.b, .22f));
            Stretch(glyph.rectTransform, .1f);
            glyph.resizeTextForBestFit = true; glyph.resizeTextMinSize = 40; glyph.resizeTextMaxSize = 400;
            hint = NewText("Talisman Hint", paper, "부적에 '물'을 쓰세요", 22, inkColor);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(.05f, .86f); hintRect.anchorMax = new Vector2(.95f, .98f);
            hintRect.offsetMin = hintRect.offsetMax = Vector2.zero;
            hint.resizeTextForBestFit = true; hint.resizeTextMinSize = 10; hint.resizeTextMaxSize = 28;
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

        private void OnDisable() { SetBoardBlocked(false); Visible = false; RoundId = null; if (paper != null) paper.gameObject.SetActive(false); }
        private void OnDestroy() => SetBoardBlocked(false);
    }
}
