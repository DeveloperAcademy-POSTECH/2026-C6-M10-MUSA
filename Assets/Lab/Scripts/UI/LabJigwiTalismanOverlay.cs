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
        [Tooltip("#533: 아직 안 쓴 글자 획 색. 둥근 점이 겹치므로 불투명으로 둔다(반투명이면 겹친 곳이 얼룩진다).")]
        [SerializeField] private Color guideColor = new Color(.79f, .67f, .4f, 1f);
        [Tooltip("#479/#533: 손가락이 지나가 칠해진 획 색. #533부터 수채화 파랑(반투명 점이 겹쳐 물감처럼 고인다). 알파가 낮을수록 묽다.")]
        [SerializeField] private Color writtenColor = new Color(.16f, .47f, .93f, .5f);
        [Tooltip("#533: 수채화 점마다 색이 조금씩 흔들리는 정도 (0이면 모두 같은 색).")]
        [SerializeField, Range(0f, .3f)] private float watercolorJitter = .12f;
        [Tooltip("#533: 한 획을 이만큼 쓰면 깜빡이는 안내 점이 다음 획으로 넘어간다 (0~1). 칠하기 자체는 순서와 상관없다.")]
        [SerializeField, Range(.3f, 1f)] private float strokeAdvance = .8f;
        [Tooltip("#533: 글자에서 이만큼(글자 칸 대비) 떨어진 곳을 문지르면 '글자 밖'으로 센다.")]
        [SerializeField, Range(.05f, .3f)] private float offDistance = .11f;
        [Tooltip("#533: 글자 밖을 문지른 길이(글자 칸 폭 = 1)가 이만큼 쌓이면 '번졌다'며 처음부터 다시 쓴다. 아무렇게나 문지르기 막기. 클수록 너그럽다.")]
        [SerializeField, Range(.5f, 6f)] private float smudgeBudget = 2f;
        [Tooltip("#533: 번졌을 때 안내 문구를 바꿔 보여 주는 시간(초).")]
        [SerializeField, Range(.3f, 3f)] private float smudgeHintSeconds = 1.4f;
        [Tooltip("#533: 다음에 쓸 곳을 알려 주는 깜빡이는 점 색.")]
        [SerializeField] private Color nextMarkerColor = new Color(.16f, .47f, .93f, .9f);
        [Tooltip("#533: 흐린 글자의 경계선 색 (칠하기 전부터 글자 모양이 보이게).")]
        [SerializeField] private Color ghostOutlineColor = new Color(.62f, .45f, .22f, 1f);
        [Tooltip("#533: 흐린 글자 경계선 두께 (화면 픽셀).")]
        [SerializeField, Range(0f, 6f)] private float ghostOutlineWidth = 2.5f;
        [Tooltip("#533: 아직 안 쓴 \"물\" 글자(실제 글꼴)를 흐리게 깔아 두는 색.")]
        [SerializeField] private Color ghostColor = new Color(.88f, .76f, .48f, 1f);
        [Tooltip("#533: 먹 붓 굵기 (글자 칸 대비). 지나간 곳의 글자 획을 이만큼 채운다(글자 모양 밖으로는 안 번진다). 판정 거리와는 따로다.")]
        [SerializeField, Range(.06f, .3f)] private float brushWidth = .16f;
        [Tooltip("#533: 다 쓰면 금빛이 획 순서대로 글자를 훑고 지나가는 시간(초). 빈 곳도 이때 채워진다.")]
        [SerializeField, Range(.1f, 1.5f)] private float sweepSeconds = .5f;
        [Tooltip("#533: 훑기가 끝난 순간 부적이 \"쾅\" 하고 커지는 정도 (종이 크기 대비).")]
        [SerializeField, Range(0f, .25f)] private float burstPunch = .09f;
        [Tooltip("#533: 훑기가 끝난 순간 부적이 떨리는 세기 (종이 높이 대비).")]
        [SerializeField, Range(0f, .05f)] private float burstShake = .012f;
        [Tooltip("#533: 글자에서 퍼져 나가는 빛 고리가 퍼지는 시간(초).")]
        [SerializeField, Range(.1f, 1.5f)] private float ringSeconds = .55f;
        [Header("#533 소리 (Resources/Audio 아래 폴더 이름, 폴더 안 파일 중 무작위. 비우면 소리 없음)")]
        [Tooltip("금빛이 획을 훑기 시작할 때. 예: FiveColorHit(가야금 띠리링), CharmRelease, YaguangFlashGlow")]
        [SerializeField] private string sweepSoundFolder = "FiveColorHit";
        [SerializeField, Range(0f, 1f)] private float sweepSoundVolume = .7f;
        [Tooltip("\"쾅\" 순간. 예: YaguangShakeBellFinal(방울), YaguangFlashBurst, ComboClear")]
        [SerializeField] private string burstSoundFolder = "YaguangShakeBellFinal";
        [SerializeField, Range(0f, 1f)] private float burstSoundVolume = 1f;
        [Tooltip("번졌을 때. 예: Miss, ComboFail")]
        [SerializeField] private string smudgeSoundFolder = "Miss";
        [SerializeField, Range(0f, 1f)] private float smudgeSoundVolume = .8f;
        [Header("#533 진동 (아이폰 빌드에서만, 설정의 진동 끄기를 따름)")]
        [Tooltip("글자를 칠할 때 사각사각 짧은 진동 세기. 0이면 끔.")]
        [SerializeField, Range(0f, 1f)] private float writeHapticIntensity = .35f;
        [Tooltip("칠하는 진동 사이 최소 간격(초). 너무 자주 울리지 않게.")]
        [SerializeField, Range(.02f, .3f)] private float writeHapticGapSeconds = .07f;
        [Tooltip("\"쾅\" 진동: 센 탭 한 번 뒤 이만큼 이어지는 진동(초). 0이면 탭만.")]
        [SerializeField, Range(0f, 1.5f)] private float burstHapticSeconds = .35f;
        [Tooltip("번졌을 때 진동 세기. 0이면 끔.")]
        [SerializeField, Range(0f, 1f)] private float smudgeHapticIntensity = .9f;
        [Tooltip("#509: 다 쓴 글씨가 빛나는 시간(초). #533부터 훑기 뒤에 빛난 채 머무는 시간. 그다음 부적이 천천히 사라진다.")]
        [SerializeField, Range(.1f, 2f)] private float glowSeconds = .6f;
        [Tooltip("#509: 빛난 뒤 부적이 사라지는 시간(초).")]
        [SerializeField, Range(.2f, 3f)] private float fadeSeconds = 1.2f;
        [Tooltip("#509: 다 쓴 글씨가 빛날 때의 색 (금빛).")]
        [SerializeField] private Color glowColor = new Color(1f, .86f, .32f, 1f);
        [Tooltip("#533: \"물\" 글자의 가로 폭이 부적 종이 폭에서 차지하는 비율. 클수록 글자가 크다(세로는 종이 높이의 72%까지).")]
        [SerializeField, Range(.4f, .95f)] private float glyphWidthShare = .82f;
        [Tooltip("#533: 글자 획 굵기 (글자 칸 대비). 점끼리 겹쳐 이어진 굵은 획으로 보인다. 판정 거리와는 따로다.")]
        [SerializeField, Range(.03f, .15f)] private float strokeWidth = .085f;
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
        private static Sprite roundDot;
        /// <summary>#533: 신라문화체 굵은 "물"의 글자 모양 범위 (판정 점과 같은 글자 칸 0~1 좌표, 위가 1). 점선에 맞춰 잰 값.</summary>
        private static readonly Rect GlyphInkRect = Rect.MinMaxRect(.149f, .081f, .848f, .92f);
        /// <summary>#533: 흐린 "물" 글자(보통 Text). 먹 붓 점을 글자 모양 안에만 보이게 가리는 Mask 그림이기도 하다.</summary>
        private UnityEngine.UI.Text ghost, ghostEdge;
        private UnityEngine.UI.Outline ghostOutline;
        private static Sprite watercolorDot;
        private Color[] inkColors = Array.Empty<Color>();
        private const int GhostFontSize = 200;
        private readonly TextGenerator ghostGenerator = new TextGenerator();
        /// <summary>#533: 실제 글꼴 글자 위에 먹으로 쓰는 모양인지 (신라문화체가 없으면 예전 점선 모양).</summary>
        public bool InkMode => ghost != null;
        private UnityEngine.UI.Image[] dots = Array.Empty<UnityEngine.UI.Image>();
        private UnityEngine.UI.Text hint;
        private Font font;
        private bool blocking, hasLast;
        private Vector2 last;
        private string writtenRound;
        private float sentAt;
        private readonly LabJigwiTalismanStroke stroke = new LabJigwiTalismanStroke(LabJigwiTalismanStroke.Water, LabJigwiTalismanStroke.WaterStrokes);
        private UnityEngine.UI.Image nextMarker, burstRing;
        private static Sprite ringSprite;
        /// <summary>#533: 점마다 획 순서상 위치 (0~1). 완성 때 금빛이 이 순서로 훑는다.</summary>
        private float[] strokeRank = Array.Empty<float>();
        private AudioSource sfx;
        private float lastWriteHaptic = -1f;
        private bool burstPlayed;
        /// <summary>#533: 테스트용 — 마지막으로 낸 효과 이름(Sweep/Burst/Smudge)과 횟수.</summary>
        public string LastCue { get; private set; }
        public int CueCount { get; private set; }
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
        /// <summary>#533: 글자 밖을 너무 문질러 처음부터 다시 쓰게 된 횟수.</summary>
        public int SmudgeCount { get; private set; }
        private const string HintText = "손가락으로 '물'을 적어\n부적을 완성하세요.";
        private const string SmudgeText = "번졌어요!\n글자를 따라 다시 써 주세요.";
        private float smudgeHintUntil = -1f;
        public LabJigwiTalismanStroke Stroke => stroke;
        /// <summary>#509: 다 쓴 부적이 빛나며 사라지는 중인지. 이때 조합판은 이미 풀려 있다.</summary>
        public bool Finishing => finishAge >= 0f;
        public float FinishSeconds => sweepSeconds + glowSeconds + fadeSeconds;
        /// <summary>#509: 사라지는 동안의 부적 불투명도 (1 → 0).</summary>
        public float PaperAlpha => paperGroup != null ? paperGroup.alpha : 1f;

        public void Bind(LabNetwork network, Font labelFont)
        {
            source = network;
            if (labelFont != null) font = labelFont;
            EnsureWidgets();
            if (labelFont != null && hint != null) hint.font = labelFont;
            if (ghost != null && IsShilla(labelFont) && ghost.font != labelFont)
            {
                ghost.font = labelFont;
                if (ghostEdge != null) ghostEdge.font = labelFont;
                LayoutGlyph();
            }
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
                UpdateNextMarker(Time.unscaledTime);
                if (smudgeHintUntil >= 0f && Time.unscaledTime >= smudgeHintUntil) RestoreHint();
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
            // #533: rubbing far from the letter (coin-scratch scribbling) smudges the talisman; it starts over.
            stroke.AddOffGuide(from, to, offDistance);
            if (stroke.OffLength > smudgeBudget) { Smudge(); return; }
            stroke.StrokeAdvance = strokeAdvance;
            stroke.Paint(from, to, Tolerance, fresh);
            for (int i = 0; i < fresh.Count; i++)
                if (fresh[i] < dots.Length && dots[fresh[i]] != null) ShowWritten(dots[fresh[i]], fresh[i]);
            if (fresh.Count > 0 && Time.unscaledTime - lastWriteHaptic >= writeHapticGapSeconds)
            {
                lastWriteHaptic = Time.unscaledTime;
                LabHaptics.PlayDirect(Config, LabHapticStyle.Light, writeHapticIntensity);
            }
            if (stroke.Coverage + 1e-5f < Required) return;
            writtenRound = RoundId;
            CompletedCount++;
            Send();
            Visible = false; RoundId = null; hasLast = false;
            SetBoardBlocked(false);
            // #509: the written "물" glows, then the talisman slowly fades away (the board is already free).
            BeginFinish();
        }

        private void ShowWritten(UnityEngine.UI.Image dot, int index)
        {
            dot.color = InkColor(index);
            if (InkMode) dot.gameObject.SetActive(true);
        }

        /// <summary>#533: 지금 써야 할 획의 다음 빈 점에서 깜빡이는 표시. 획 순서를 알려 줄 뿐, 다른 곳을 칠해도 된다.</summary>
        private void UpdateNextMarker(float time)
        {
            if (nextMarker == null) return;
            int next = stroke.NextPoint;
            bool show = Visible && next >= 0;
            if (nextMarker.gameObject.activeSelf != show) nextMarker.gameObject.SetActive(show);
            if (!show) return;
            RectTransform m = nextMarker.rectTransform;
            m.anchorMin = m.anchorMax = stroke.Samples[next];
            m.anchoredPosition = Vector2.zero;
            float pulse = .5f + .5f * Mathf.Sin(time * 6f);
            m.localScale = Vector3.one * (.75f + .35f * pulse);
            Color c = nextMarkerColor; c.a *= .45f + .55f * pulse; nextMarker.color = c;
        }

        private void BuildStrokeRank()
        {
            strokeRank = new float[dots.Length];
            int total = Mathf.Max(1, stroke.Samples.Length - 1), n = 0;
            for (int s = 0; s < stroke.StrokeCount; s++)
                foreach (int index in stroke.StrokePoints(s))
                    if (index < strokeRank.Length) strokeRank[index] = n++ / (float)total;
        }

        /// <summary>#533: 빛 고리 무늬 (런타임에 한 번 만든다). 가운데가 빈 부드러운 고리.</summary>
        private static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Talisman Burst Ring (#533 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            float half = size * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(half, half)) / half;
                    float band = Mathf.Clamp01(1f - Mathf.Abs(r - .82f) / .14f);
                    float glow = Mathf.Clamp01(1f - Mathf.Abs(r - .8f) / .4f) * .35f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(band * band + glow) * (r < 1f ? 1f : 0f));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            ringSprite.hideFlags = HideFlags.DontSave;
            return ringSprite;
        }

        private Color InkColor(int index) => InkMode && index < inkColors.Length ? inkColors[index] : writtenColor;

        /// <summary>#533: 점마다 정해 둔(매번 같은) 수채화 색 흔들림. 물감이 고르게 안 먹은 느낌.</summary>
        private void BuildInkColors()
        {
            inkColors = new Color[dots.Length];
            var random = new System.Random(533);
            Color.RGBToHSV(writtenColor, out float h, out float sat, out float v);
            for (int i = 0; i < inkColors.Length; i++)
            {
                float a = (float)random.NextDouble() * 2f - 1f, b = (float)random.NextDouble() * 2f - 1f, c = (float)random.NextDouble() * 2f - 1f;
                Color tint = Color.HSVToRGB(Mathf.Repeat(h + a * watercolorJitter * .12f, 1f),
                    Mathf.Clamp01(sat + b * watercolorJitter * .5f), Mathf.Clamp01(v + c * watercolorJitter * .4f));
                tint.a = Mathf.Clamp01(writtenColor.a * (1f + c * watercolorJitter * 1.5f));
                inkColors[i] = tint;
            }
        }

        private void BeginFinish()
        {
            if (paper == null) return;
            finishAge = 0f; finishFrom = paper.localPosition;
            paper.gameObject.SetActive(true);
            if (hint != null) hint.gameObject.SetActive(false);
            if (nextMarker != null) nextMarker.gameObject.SetActive(false);
            burstPlayed = false;
            Cue("Sweep", sweepSoundFolder, sweepSoundVolume);
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

        /// <summary>
        /// #533 완성 연출: ① 금빛이 획 순서대로 글자를 훑으며 빈 곳까지 채움 → ② "쾅": 부적이 커졌다 돌아오며 떨리고,
        /// 글자에서 빛 고리가 퍼지고, 흐린 글자·경계선도 금빛으로 → ③ 빛난 채 잠깐 머문 뒤 위로 떠오르며 사라짐.
        /// </summary>
        private void DrawFinish()
        {
            float sweep = Mathf.Max(.01f, sweepSeconds);
            float sweepT = Mathf.Clamp01(finishAge / sweep);
            float after = finishAge - sweep;
            if (after >= 0f && !burstPlayed)
            {
                burstPlayed = true;
                Cue("Burst", burstSoundFolder, burstSoundVolume);
                LabHaptics.PlayDirect(Config, LabHapticStyle.Heavy, 1f, burstHapticSeconds, .7f, .3f);
            }
            float burst = after < 0f ? 0f : Mathf.Clamp01(after / .35f);
            float bump = after < 0f ? 0f : Mathf.Sin(Mathf.PI * burst);
            float fade = Mathf.Clamp01((finishAge - sweep - glowSeconds) / Mathf.Max(.01f, fadeSeconds));
            float eased = fade * fade * (3f - 2f * fade);
            Color gold = new Color(glowColor.r, glowColor.g, glowColor.b, 1f);
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                bool written = stroke.IsCovered(i);
                if (!InkMode && !written) continue;
                float rank = i < strokeRank.Length ? strokeRank[i] : 0f;
                float lit = Mathf.Clamp01((sweepT * 1.12f - rank) / .12f);
                if (lit <= 0f)
                {
                    // Not reached yet: written ink stays as written, gaps stay empty.
                    if (written) { dots[i].color = InkColor(i); dots[i].rectTransform.localScale = Vector3.one; }
                    continue;
                }
                if (InkMode && !dots[i].gameObject.activeSelf) dots[i].gameObject.SetActive(true);
                dots[i].color = Color.Lerp(InkColor(i), gold, lit);
                float pop = 1f + .35f * Mathf.Sin(Mathf.PI * lit) * (1f - lit * .5f) + .12f * bump;
                dots[i].rectTransform.localScale = Vector3.one * pop;
            }
            if (ghost != null) ghost.color = Color.Lerp(ghostColor, new Color(1f, .94f, .62f, ghostColor.a), burst);
            if (ghostEdge != null)
            {
                Color edge = Color.Lerp(ghostOutlineColor, gold, burst);
                ghostEdge.color = edge;
                if (ghostOutline != null) ghostOutline.effectColor = edge;
            }
            DrawRing(after, gold);
            if (paperImage != null) paperImage.color = Color.Lerp(paperColor, new Color(1f, .97f, .85f, paperColor.a), Mathf.Max(burst * .8f, sweepT * .3f));
            if (paperGroup != null) paperGroup.alpha = 1f - eased;
            paper.localScale = Vector3.one * (1f + burstPunch * bump + .05f * eased);
            float shake = after < 0f ? 0f : burstShake * paper.rect.height * (1f - burst);
            var jitter = new Vector3(Mathf.Sin(finishAge * 93f) * shake, Mathf.Cos(finishAge * 71f) * shake, 0f);
            paper.localPosition = finishFrom + jitter + Vector3.up * (paper.rect.height * .06f * eased);
        }

        private void DrawRing(float after, Color gold)
        {
            if (burstRing == null) return;
            float t = after < 0f ? -1f : after / Mathf.Max(.01f, ringSeconds);
            bool show = t >= 0f && t < 1f;
            if (burstRing.gameObject.activeSelf != show) burstRing.gameObject.SetActive(show);
            if (!show) return;
            float grow = 1f - (1f - t) * (1f - t);
            burstRing.rectTransform.localScale = Vector3.one * (.35f + 1.9f * grow);
            Color c = gold; c.a = (1f - t) * .9f; burstRing.color = c;
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
            if (ghost != null) ghost.color = ghostColor;
            if (ghostEdge != null) ghostEdge.color = ghostOutlineColor;
            if (ghostOutline != null) ghostOutline.effectColor = ghostOutlineColor;
            if (burstRing != null) burstRing.gameObject.SetActive(false);
            paper.gameObject.SetActive(Visible);
        }

        private LabConfig Config => source != null ? source.config : null;

        /// <summary>#533: Resources/Audio/폴더의 소리 하나를 이 기기에서만 낸다(파일이 없으면 조용히 넘어감).</summary>
        private void Cue(string name, string folder, float volume)
        {
            LastCue = name; CueCount++;
            if (string.IsNullOrEmpty(folder) || volume <= 0f) return;
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/" + folder);
            if (clips == null || clips.Length == 0) return;
            if (sfx == null)
            {
                sfx = gameObject.AddComponent<AudioSource>();
                sfx.playOnAwake = false; sfx.spatialBlend = 0f;
            }
            sfx.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)], volume * LabAudioSettings.SfxVolume);
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

        private void Smudge()
        {
            SmudgeCount++;
            ResetWriting();
            Cue("Smudge", smudgeSoundFolder, smudgeSoundVolume);
            LabHaptics.PlayDirect(Config, LabHapticStyle.Rigid, smudgeHapticIntensity);
            if (hint == null) return;
            hint.text = SmudgeText;
            smudgeHintUntil = Time.unscaledTime + smudgeHintSeconds;
        }

        private void RestoreHint()
        {
            smudgeHintUntil = -1f;
            if (hint != null) hint.text = HintText;
        }

        private void ResetWriting()
        {
            stroke.Clear(); hasLast = false; writtenRound = null;
            RestoreHint();
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                if (InkMode) { dots[i].gameObject.SetActive(false); dots[i].color = InkColor(i); }
                else dots[i].color = guideColor;
            }
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

        /// <summary>
        /// "물" 칸은 늘 정사각형이다. #533: 칸 크기를 칸 전체가 아니라 실제 글자 획이 놓인 범위에 맞춰,
        /// 글자가 종이 폭의 glyphWidthShare만큼 차지하게 키우고 글자 가운데를 안내 문구 아래에 둔다.
        /// 판정 좌표(칸 기준 0~1)는 그대로라 점선과 판정은 함께 커진다.
        /// </summary>
        private void LayoutGlyph()
        {
            if (glyphArea == null || paper == null) return;
            Vector2 size = paper.rect.size;
            // #533: with the real glyph the letter's own outline sets the size; the old dotted look pads by its stroke width.
            Rect bounds = InkMode ? GlyphInkRect : SampleBounds(stroke.Samples);
            float pad = InkMode ? 0f : strokeWidth;
            float spanX = Mathf.Max(.05f, bounds.width + pad), spanY = Mathf.Max(.05f, bounds.height + pad);
            float side = Mathf.Max(1f, Mathf.Min(size.x * glyphWidthShare / spanX, size.y * .72f / spanY));
            glyphArea.anchorMin = glyphArea.anchorMax = glyphArea.pivot = new Vector2(.5f, .5f);
            // Put the letter's own centre (not the box centre) a little below the paper centre, under the hint.
            Vector2 centre = bounds.center;
            glyphArea.anchoredPosition = new Vector2((.5f - centre.x) * side, -size.y * .06f + (.5f - centre.y) * side);
            glyphArea.sizeDelta = new Vector2(side, side);
            float dot = Mathf.Max(4f, side * (InkMode ? brushWidth : strokeWidth));
            if (nextMarker != null) nextMarker.rectTransform.sizeDelta = Vector2.one * Mathf.Max(6f, side * .07f);
            if (burstRing != null) { burstRing.rectTransform.sizeDelta = Vector2.one * side * .8f; burstRing.rectTransform.anchoredPosition = Vector2.zero; }
            if (InkMode && FitGhost(side, dot)) return;
            for (int i = 0; i < dots.Length; i++)
                if (dots[i] != null) dots[i].rectTransform.sizeDelta = new Vector2(dot, dot);
        }

        /// <summary>
        /// #533: 흐린 "물" Text의 실제 글자 사각형을 재서, 그 사각형이 GlyphInkRect(판정 점과 같은 칸 좌표)에 꼭 맞게
        /// Text를 늘이고 옮긴다. 먹 붓 점은 Text의 자식이라 같은 비율을 되돌려 놓는다.
        /// </summary>
        private bool FitGhost(float side, float dot)
        {
            RectTransform g = ghost.rectTransform;
            g.anchorMin = g.anchorMax = g.pivot = new Vector2(.5f, .5f);
            g.sizeDelta = new Vector2(GhostFontSize * 2f, GhostFontSize * 2f);
            TextGenerationSettings settings = ghost.GetGenerationSettings(g.rect.size);
            if (!ghostGenerator.Populate(ghost.text, settings) || ghostGenerator.vertexCount < 4) return false;
            IList<UIVertex> verts = ghostGenerator.verts;
            float unit = 1f / Mathf.Max(.0001f, ghost.pixelsPerUnit);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++) { Vector2 v = verts[i].position * unit; min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
            Vector2 quad = max - min;
            if (quad.x <= .001f || quad.y <= .001f) return false;
            var target = Rect.MinMaxRect((GlyphInkRect.xMin - .5f) * side, (GlyphInkRect.yMin - .5f) * side,
                (GlyphInkRect.xMax - .5f) * side, (GlyphInkRect.yMax - .5f) * side);
            var scale = new Vector2(target.width / quad.x, target.height / quad.y);
            Vector2 offset = target.center - Vector2.Scale((min + max) * .5f, scale);
            g.localScale = new Vector3(scale.x, scale.y, 1f);
            g.anchoredPosition = offset;
            if (ghostEdge != null)
            {
                RectTransform e = ghostEdge.rectTransform;
                e.anchorMin = e.anchorMax = e.pivot = g.pivot; e.sizeDelta = g.sizeDelta;
                e.localScale = g.localScale; e.anchoredPosition = g.anchoredPosition;
                // The outline offset is in the text's own units, so undo the scale to keep it a steady screen width.
                if (ghostOutline != null) ghostOutline.effectDistance = new Vector2(ghostOutlineWidth / scale.x, -ghostOutlineWidth / scale.y);
            }
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                Vector2 at = (stroke.Samples[i] - new Vector2(.5f, .5f)) * side;
                RectTransform d = dots[i].rectTransform;
                d.anchorMin = d.anchorMax = new Vector2(.5f, .5f);
                d.anchoredPosition = new Vector2((at.x - offset.x) / scale.x, (at.y - offset.y) / scale.y);
                d.sizeDelta = new Vector2(dot / scale.x, dot / scale.y);
            }
            return true;
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
            hint = NewText("Talisman Hint", paper, HintText, 22, inkColor);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(.08f, .83f); hintRect.anchorMax = new Vector2(.92f, .96f);
            hintRect.offsetMin = hintRect.offsetMax = Vector2.zero;
            hint.resizeTextForBestFit = true; hint.resizeTextMinSize = 10; hint.resizeTextMaxSize = 28;
            glyphArea = NewRect("Talisman Glyph (물)", paper);
            // #533: the real Shilla Culture "물" lies faintly on the paper and masks the ink, so written ink only shows
            // inside the letter's shape (a brush, not sausage dots). Without the font it falls back to the dotted guide.
            RectTransform dotParent = glyphArea;
            if (IsShilla(font))
            {
                // #533: a copy underneath carries the outline, so the letter's edge shows before any writing
                // and the watercolour (masked by the ghost on top) never paints over it.
                ghostEdge = NewText("Talisman Ghost Edge (물)", glyphArea, "물", GhostFontSize, ghostOutlineColor);
                ghostEdge.horizontalOverflow = HorizontalWrapMode.Overflow; ghostEdge.verticalOverflow = VerticalWrapMode.Overflow;
                ghostEdge.alignment = TextAnchor.MiddleCenter;
                ghostOutline = ghostEdge.gameObject.AddComponent<UnityEngine.UI.Outline>();
                ghostOutline.effectColor = ghostOutlineColor; ghostOutline.useGraphicAlpha = true;
                ghost = NewText("Talisman Ghost Glyph (물)", glyphArea, "물", GhostFontSize, ghostColor);
                ghost.horizontalOverflow = HorizontalWrapMode.Overflow; ghost.verticalOverflow = VerticalWrapMode.Overflow;
                ghost.alignment = TextAnchor.MiddleCenter;
                ghost.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
                dotParent = ghost.rectTransform;
            }
            dots = new UnityEngine.UI.Image[stroke.Samples.Length];
            for (int i = 0; i < dots.Length; i++)
            {
                var dot = NewRect("Dot " + i, dotParent).gameObject.AddComponent<UnityEngine.UI.Image>();
                dot.raycastTarget = false; dot.sprite = InkMode ? WatercolorDot() : RoundDot();
                // In ink mode FitGhost places the dots inside the scaled glyph.
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = stroke.Samples[i];
                dot.rectTransform.anchoredPosition = Vector2.zero;
                dot.color = InkMode ? writtenColor : guideColor;
                if (InkMode) dot.gameObject.SetActive(false);
                dots[i] = dot;
            }
            BuildInkColors();
            // #533: drawn last so it sits above the ghost letter and the ink.
            nextMarker = NewRect("Talisman Next Stroke", glyphArea).gameObject.AddComponent<UnityEngine.UI.Image>();
            nextMarker.raycastTarget = false; nextMarker.sprite = RoundDot(); nextMarker.color = nextMarkerColor;
            nextMarker.gameObject.SetActive(false);
            // #533: the light ring that bursts out of the finished letter.
            burstRing = NewRect("Talisman Burst Ring", glyphArea).gameObject.AddComponent<UnityEngine.UI.Image>();
            burstRing.raycastTarget = false; burstRing.sprite = RingSprite();
            Vector2 heart = InkMode ? GlyphInkRect.center : SampleBounds(stroke.Samples).center;
            burstRing.rectTransform.anchorMin = burstRing.rectTransform.anchorMax = heart;
            burstRing.gameObject.SetActive(false);
            BuildStrokeRank();
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

        private static bool IsShilla(Font candidate) =>
            candidate != null && candidate.name.IndexOf("Shilla", StringComparison.OrdinalIgnoreCase) >= 0;

        private static Rect SampleBounds(Vector2[] samples)
        {
            if (samples == null || samples.Length == 0) return new Rect(0f, 0f, 1f, 1f);
            Vector2 min = samples[0], max = samples[0];
            for (int i = 1; i < samples.Length; i++) { min = Vector2.Min(min, samples[i]); max = Vector2.Max(max, samples[i]); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>#533: 획 점용 둥근 무늬 (런타임에 한 번 만든다). 둥근 점이 겹쳐 매끈한 굵은 획이 된다.</summary>
        /// <summary>
        /// #533: 수채화 점. 가운데는 묽고 가장자리가 조금 진하며(물감이 마르며 테두리에 고이는 느낌), 테두리가 살짝 울퉁불퉁하다.
        /// </summary>
        private static Sprite WatercolorDot()
        {
            if (watercolorDot != null) return watercolorDot;
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Talisman Watercolor Dot (#533 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            float half = size * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x + .5f - half, y + .5f - half) / half;
                    float angle = Mathf.Atan2(d.y, d.x);
                    float edge = .86f + .07f * Mathf.Sin(angle * 5f + 1.3f) + .05f * Mathf.Sin(angle * 11f + .4f);
                    float r = d.magnitude / edge;
                    float body = .55f + .45f * Mathf.SmoothStep(.55f, .95f, r);
                    float cut = Mathf.Clamp01((1f - r) * half * .6f);
                    float grain = .85f + .15f * Mathf.PerlinNoise(x * .18f + 11f, y * .18f + 5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(body * cut * grain));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            watercolorDot = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            watercolorDot.hideFlags = HideFlags.DontSave;
            return watercolorDot;
        }

        private static Sprite RoundDot()
        {
            if (roundDot != null) return roundDot;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Talisman Round Dot (#533 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            float r = size * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            roundDot = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            roundDot.hideFlags = HideFlags.DontSave;
            return roundDot;
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

        /// <summary>
        /// #533 "물"의 획 순서. 각 획은 Water 점 번호를 쓰는 방향대로 나열한다.
        /// ㅁ(왼쪽 세로 → 위·오른쪽 → 아래) → ㅜ(가로 → 세로) → ㄹ(위·오른쪽 → 가운데 → 왼쪽·아래).
        /// </summary>
        public static readonly int[][] WaterStrokes =
        {
            new[] { 84, 80, 78, 76, 74, 72, 68 },
            new[] { 81, 83, 85, 86, 87, 82, 79, 77, 75, 73, 71 },
            new[] { 64, 66, 65, 63, 67, 69, 70 },
            new[] { 40, 41, 42, 44, 45, 46, 47, 48, 49, 50, 52, 54, 55, 53, 57, 58, 59, 61, 62, 60, 56 },
            new[] { 51, 43, 38, 39, 37, 36 },
            new[] { 25, 26, 27, 28, 31, 32, 33, 34, 35, 29, 30, 24, 23, 22 },
            new[] { 16, 14, 17, 18, 19, 20, 21 },
            new[] { 15, 13, 12, 9, 5, 2, 0, 1, 3, 4, 6, 7, 8, 10, 11 }
        };

        public readonly Vector2[] Samples;
        private readonly bool[] covered;
        private readonly int[][] strokes;
        private readonly int[] strokeOf, strokeCovered;
        public int CoveredCount { get; private set; }
        public float Coverage => Samples.Length == 0 ? 0f : CoveredCount / (float)Samples.Length;
        /// <summary>#533: 안내할 획 번호 (아직 StrokeAdvance만큼 쓰지 않은 첫 획). EnforceOrder면 이보다 뒤 획은 칠해지지 않는다.</summary>
        public int CurrentStroke { get; private set; }
        public int StrokeCount => strokes.Length;
        /// <summary>#533: 한 획을 이 비율만큼 쓰면 다음 획이 열린다.</summary>
        public float StrokeAdvance { get; set; } = .8f;
        /// <summary>#533: 켜면 안내 획보다 뒤 획은 칠해지지 않는다. 직접 해 보니 너무 엄격해 기본은 끔(순서는 안내만).</summary>
        public bool EnforceOrder { get; set; }

        /// <summary>strokes가 없으면 획 순서 없이 아무 점이나 칠한다.</summary>
        public LabJigwiTalismanStroke(Vector2[] samples, int[][] strokeOrder = null)
        {
            Samples = (Vector2[])samples.Clone();
            covered = new bool[Samples.Length];
            strokeOf = new int[Samples.Length];
            var listed = new bool[Samples.Length];
            var built = new List<int[]>();
            if (strokeOrder != null)
                foreach (int[] order in strokeOrder)
                {
                    var keep = new List<int>();
                    foreach (int index in order ?? Array.Empty<int>())
                        if (index >= 0 && index < Samples.Length && !listed[index]) { listed[index] = true; keep.Add(index); }
                    if (keep.Count > 0) built.Add(keep.ToArray());
                }
            // Points no stroke lists join the last stroke, so a guide is always finishable.
            var rest = new List<int>();
            for (int i = 0; i < Samples.Length; i++) if (!listed[i]) rest.Add(i);
            if (rest.Count > 0 || built.Count == 0) built.Add(rest.ToArray());
            strokes = built.ToArray();
            for (int s = 0; s < strokes.Length; s++) foreach (int index in strokes[s]) strokeOf[index] = s;
            strokeCovered = new int[strokes.Length];
        }

        public bool IsCovered(int index) => covered[index];
        public int StrokeOf(int index) => strokeOf[index];
        public int[] StrokePoints(int stroke) => (int[])strokes[stroke].Clone();

        /// <summary>#533: 지금 획에서 쓰는 방향으로 다음에 칠할 빈 점 번호. 다 썼으면 -1.</summary>
        public int NextPoint
        {
            get
            {
                for (int s = CurrentStroke; s < strokes.Length; s++)
                    foreach (int index in strokes[s]) if (!covered[index]) return index;
                return -1;
            }
        }

        /// <summary>#533: 글자에서 떨어진 곳을 문지른 길이의 합 (글자 칸 폭 = 1).</summary>
        public float OffLength { get; private set; }

        /// <summary>
        /// #533: 선분 from→to 중 어느 점에서도 offDistance보다 먼 부분의 길이를 OffLength에 더한다(잘게 나눠 잰다). 더한 길이를 돌려준다.
        /// </summary>
        public float AddOffGuide(Vector2 from, Vector2 to, float offDistance)
        {
            if (!Finite(from) || !Finite(to) || !(offDistance > 0f) || Samples.Length == 0) return 0f;
            float length = Vector2.Distance(from, to);
            if (length <= 1e-6f) return 0f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(length / .02f), 1, 200);
            float piece = length / steps, limit = offDistance * offDistance, added = 0f;
            for (int k = 0; k < steps; k++)
            {
                Vector2 at = Vector2.Lerp(from, to, (k + .5f) / steps);
                bool near = false;
                for (int i = 0; i < Samples.Length && !near; i++) near = (Samples[i] - at).sqrMagnitude <= limit;
                if (!near) added += piece;
            }
            OffLength += added;
            return added;
        }

        public void Clear()
        {
            Array.Clear(covered, 0, covered.Length); CoveredCount = 0; OffLength = 0f;
            Array.Clear(strokeCovered, 0, strokeCovered.Length); CurrentStroke = 0;
        }

        /// <summary>선분 from→to에서 tolerance 안에 든 점을 칠하고, 새로 칠한 점 번호를 fresh에 담는다. #533: EnforceOrder일 때만 아직 열리지 않은 획을 건너뛴다.</summary>
        public void Paint(Vector2 from, Vector2 to, float tolerance, List<int> fresh = null)
        {
            if (!Finite(from) || !Finite(to) || !(tolerance > 0f)) return;
            float limit = tolerance * tolerance;
            for (int i = 0; i < Samples.Length; i++)
            {
                if (covered[i] || (EnforceOrder && strokeOf[i] > CurrentStroke) || DistanceSquared(Samples[i], from, to) > limit) continue;
                covered[i] = true; CoveredCount++; strokeCovered[strokeOf[i]]++;
                fresh?.Add(i);
                Advance();
            }
        }

        private void Advance()
        {
            float need = Mathf.Clamp01(StrokeAdvance);
            while (CurrentStroke < strokes.Length - 1
                   && strokeCovered[CurrentStroke] >= Mathf.CeilToInt(strokes[CurrentStroke].Length * need - 1e-4f)) CurrentStroke++;
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
