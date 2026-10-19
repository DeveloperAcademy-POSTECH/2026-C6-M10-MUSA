using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 박 색상 콤보 실험 (#222)의 전투 화면 표시. 모든 기기가 Host 스냅샷만 읽어서 같은 콤보를 보여준다.
    /// - 진행 중: 4칸 색 (맞힌 칸은 체크, 다음 칸은 통통 튐), 줄어드는 남은 시간 막대 (숫자 없음)
    /// - 끝남: CLEAR! (성공) / WRONG COLOR, TIME UP (실패) 문구가 잠깐 튀어나옴
    /// 실험이 꺼져 있으면 스냅샷에 콤보가 없으므로 아무것도 그리지 않는다. 화면 표시만 하며 터치를 막지 않는다.
    /// 일시정지 중이거나 전투가 끝나면 숨는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGourdComboHud : MonoBehaviour
    {
        public const string ClearText = "CLEAR!";
        public const string WrongColorText = "WRONG COLOR";
        public const string TimeUpText = "TIME UP";

        private const int TextureSize = 96;
        private const float ResultSeconds = 1.6f;
        private static readonly Color32 InkColor = new Color32(43, 36, 32, 255);
        private static readonly Color PanelColor = new Color(1f, .98f, .93f, .92f);
        private static readonly Color ClearColor = new Color(.98f, .78f, .16f);
        private static readonly Color FailColor = new Color(.91f, .16f, .1f);
        private static Sprite discSprite, checkSprite, roundedSprite;

        [Tooltip("화면에서 콤보 패널 가운데 위치 (0~1, 왼쪽 아래가 0)")]
        [SerializeField] private Vector2 panelAnchor = new Vector2(.5f, .8f);
        [Tooltip("색 칸 하나의 크기 (Canvas 기준 픽셀). 패널/글자/막대가 모두 이 크기에 비례한다.")]
        [SerializeField, Min(16f)] private float slotSize = 40f;

        private LabNetwork network;
        private Canvas canvas;
        private RectTransform layer, panel, timeBar;
        private readonly RectTransform[] slots = new RectTransform[LabGourdCombo.Length];
        private readonly Image[] slotFills = new Image[LabGourdCombo.Length];
        private readonly Image[] slotChecks = new Image[LabGourdCombo.Length];
        private Image timeBarFill;
        private Text resultText;
        private Font font;

        private LabSnapshot seenSnapshot;
        private double seenAtRealtime;
        private uint shownResultCount;
        private float resultElapsed = -1f;
        private LabComboResult shownResult;
        private int lastProgress;
        private uint lastComboCount;
        private float popElapsed = -1f;
        private int popSlot = -1;

        public bool PanelVisible => panel != null && panel.gameObject.activeSelf;
        public bool ResultVisible => resultText != null && resultText.gameObject.activeSelf;
        public string ResultLabel => resultText != null ? resultText.text : string.Empty;
        public bool SlotChecked(int index) => slotChecks[index] != null && slotChecks[index].gameObject.activeSelf;
        public Color SlotColor(int index) => slotFills[index] != null ? slotFills[index].color : Color.clear;

        /// <summary>스냅샷을 읽을 네트워크와, 패널을 그릴 Canvas를 연결한다.</summary>
        public void Bind(LabNetwork target, Canvas uiCanvas)
        {
            network = target;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
        }

        private void LateUpdate()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool battle = snapshot != null && snapshot.phase == LabPhase.Playing && !snapshot.paused;
            bool anyCombo = snapshot != null && snapshot.comboCount > 0;
            double serverNow = battle ? EstimateServerNow(snapshot) : 0d;
            if (!battle || !anyCombo)
            {
                if (layer != null) layer.gameObject.SetActive(false);
                if (snapshot == null || snapshot.phase != LabPhase.Playing) ResetRound();
                return;
            }
            if (!EnsureLayer()) return;
            layer.gameObject.SetActive(true);
            layer.SetAsLastSibling();

            if (snapshot.comboCount < lastComboCount) ResetRound();
            DetectChanges(snapshot);
            DrawCombo(snapshot, serverNow);
            DrawResult();
        }

        /// <summary>스냅샷 사이에도 남은 시간이 매끄럽게 줄도록, 받은 뒤 흐른 실제 시간을 더한다.</summary>
        private double EstimateServerNow(LabSnapshot snapshot)
        {
            double realtime = Time.realtimeSinceStartupAsDouble;
            if (!ReferenceEquals(snapshot, seenSnapshot))
            {
                seenSnapshot = snapshot;
                seenAtRealtime = realtime;
            }
            // 스냅샷이 늦게 와도 최대 1초까지만 앞당겨 본다.
            return snapshot.serverTime + System.Math.Min(1d, realtime - seenAtRealtime);
        }

        private void DetectChanges(LabSnapshot snapshot)
        {
            if (snapshot.comboResultCount != shownResultCount && snapshot.comboResult != LabComboResult.None)
            {
                shownResultCount = snapshot.comboResultCount;
                shownResult = snapshot.comboResult;
                resultElapsed = 0f;
            }
            if (snapshot.comboCount != lastComboCount)
            {
                lastComboCount = snapshot.comboCount;
                lastProgress = 0;
            }
            if (snapshot.comboProgress > lastProgress)
            {
                popSlot = snapshot.comboProgress - 1;
                popElapsed = 0f;
            }
            lastProgress = snapshot.comboProgress;
        }

        private void DrawCombo(LabSnapshot snapshot, double serverNow)
        {
            bool showPanel = snapshot.comboActive && snapshot.comboColors != null
                && snapshot.comboColors.Length == LabGourdCombo.Length;
            // 끝난 직후에는 결과 문구와 함께 마지막 칸 상태를 잠깐 더 보여준다.
            showPanel |= resultElapsed >= 0f && snapshot.comboColors != null
                && snapshot.comboColors.Length == LabGourdCombo.Length;
            panel.gameObject.SetActive(showPanel);
            if (!showPanel) return;

            float bob = Mathf.Sin(Time.unscaledTime * 8f) * .08f;
            if (popElapsed >= 0f) popElapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < LabGourdCombo.Length; i++)
            {
                slotFills[i].color = LabPaintPalette.SrgbColorFor(snapshot.comboColors[i]);
                bool hit = i < snapshot.comboProgress;
                slotChecks[i].gameObject.SetActive(hit);
                float scale = 1f;
                if (snapshot.comboActive && i == snapshot.comboProgress) scale += bob;
                if (i == popSlot && popElapsed >= 0f && popElapsed < .25f)
                    scale += .3f * Mathf.Sin(popElapsed / .25f * Mathf.PI);
                slots[i].localScale = Vector3.one * scale;
            }

            if (snapshot.comboActive)
            {
                // 남은 시간은 숫자 없이 막대로만 보여준다. 3초 이하면 막대가 빨개진다.
                double left = System.Math.Max(0d, snapshot.comboEndsAt - serverNow);
                double limit = snapshot.comboEndsAt - snapshot.comboStartedAt;
                timeBar.anchorMax = new Vector2(limit > 0d ? Mathf.Clamp01((float)(left / limit)) : 0f, 1f);
                timeBarFill.color = left <= 3d ? FailColor : (Color)InkColor;
                timeBar.parent.gameObject.SetActive(true);
            }
            else timeBar.parent.gameObject.SetActive(false);
        }

        private void DrawResult()
        {
            if (resultElapsed < 0f)
            {
                resultText.gameObject.SetActive(false);
                return;
            }
            resultElapsed += Time.unscaledDeltaTime;
            if (resultElapsed >= ResultSeconds)
            {
                resultElapsed = -1f;
                resultText.gameObject.SetActive(false);
                return;
            }
            resultText.gameObject.SetActive(true);
            bool clear = shownResult == LabComboResult.Success;
            resultText.text = clear ? ClearText : shownResult == LabComboResult.Timeout ? TimeUpText : WrongColorText;
            resultText.color = clear ? ClearColor : FailColor;
            float t = resultElapsed;
            float pop = t < .18f ? 1.3f * (1f - Mathf.Pow(1f - t / .18f, 3f)) : Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((t - .18f) / .15f));
            float leave = Mathf.Clamp01((t - (ResultSeconds - .2f)) / .2f);
            resultText.rectTransform.localScale = Vector3.one * pop * (1f - leave);
            resultText.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                clear ? 0f : 6f * Mathf.Sin(t * 30f) * (1f - Mathf.Clamp01(t / .5f)));
        }

        private void ResetRound()
        {
            shownResultCount = 0;
            resultElapsed = -1f;
            lastProgress = 0;
            lastComboCount = 0;
            popElapsed = -1f;
            popSlot = -1;
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
            Text[] existing = canvas.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < existing.Length && font == null; i++) font = existing[i].font;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            layer = NewRect("Gourd Combo", canvasRect);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;

            // 모든 크기를 칸 크기에 비례시킨다 (처음 만든 64px 칸 기준 값 × u).
            float u = slotSize / 64f;
            float gap = slotSize * .22f;
            float width = slotSize * LabGourdCombo.Length + gap * (LabGourdCombo.Length + 1);
            panel = NewRect("Gourd Combo Panel", layer);
            panel.anchorMin = panel.anchorMax = panelAnchor;
            panel.sizeDelta = new Vector2(width, slotSize + gap * 3f + 10f * u);
            Image back = panel.gameObject.AddComponent<Image>();
            back.sprite = RoundedSprite();
            back.type = Image.Type.Sliced;
            back.color = PanelColor;
            back.raycastTarget = false;

            for (int i = 0; i < LabGourdCombo.Length; i++)
            {
                RectTransform slot = NewRect("Combo Slot " + (i + 1), panel);
                slot.anchorMin = slot.anchorMax = new Vector2(0f, 1f);
                slot.pivot = new Vector2(.5f, .5f);
                slot.sizeDelta = new Vector2(slotSize, slotSize);
                slot.anchoredPosition = new Vector2(gap + slotSize * .5f + i * (slotSize + gap), -gap - slotSize * .5f);
                Image fill = slot.gameObject.AddComponent<Image>();
                fill.sprite = DiscSprite();
                fill.raycastTarget = false;
                RectTransform check = NewRect("Check", slot);
                check.anchorMin = Vector2.zero;
                check.anchorMax = Vector2.one;
                check.offsetMin = check.offsetMax = Vector2.zero;
                Image mark = check.gameObject.AddComponent<Image>();
                mark.sprite = CheckSprite();
                mark.raycastTarget = false;
                check.gameObject.SetActive(false);
                slots[i] = slot;
                slotFills[i] = fill;
                slotChecks[i] = mark;
            }

            RectTransform barBack = NewRect("Combo Time Bar", panel);
            barBack.anchorMin = new Vector2(0f, 0f);
            barBack.anchorMax = new Vector2(1f, 0f);
            barBack.pivot = new Vector2(.5f, 0f);
            barBack.offsetMin = new Vector2(gap, gap);
            barBack.offsetMax = new Vector2(-gap, gap + 10f * u);
            Image barBackImage = barBack.gameObject.AddComponent<Image>();
            barBackImage.color = new Color(0f, 0f, 0f, .15f);
            barBackImage.raycastTarget = false;
            timeBar = NewRect("Fill", barBack);
            timeBar.anchorMin = Vector2.zero;
            timeBar.anchorMax = Vector2.one;
            timeBar.offsetMin = timeBar.offsetMax = Vector2.zero;
            timeBarFill = timeBar.gameObject.AddComponent<Image>();
            timeBarFill.color = InkColor;
            timeBarFill.raycastTarget = false;

            resultText = NewText("Combo Result", layer, Mathf.Max(14, Mathf.RoundToInt(54f * u)), TextAnchor.MiddleCenter);
            resultText.rectTransform.anchorMin = resultText.rectTransform.anchorMax = panelAnchor;
            resultText.rectTransform.sizeDelta = new Vector2(width * 1.6f, 80f * u);
            resultText.rectTransform.anchoredPosition = new Vector2(0f, -(panel.sizeDelta.y * .5f + 44f * u));
            Outline resultOutline = resultText.gameObject.AddComponent<Outline>();
            resultOutline.effectColor = InkColor;
            resultOutline.effectDistance = new Vector2(3f, -3f) * Mathf.Max(.5f, u);
            resultText.gameObject.SetActive(false);

            panel.gameObject.SetActive(false);
            return true;
        }

        private Text NewText(string name, Transform parent, int size, TextAnchor alignment)
        {
            Text text = NewRect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = InkColor;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        // ---- 손그림 느낌의 단순 도형: 진한 테두리 원, 흰 체크, 둥근 패널 ----

        private static Sprite DiscSprite()
        {
            if (discSprite != null) return discSprite;
            discSprite = Draw("Lab combo disc", (p, c) =>
            {
                float distance = Vector2.Distance(p, c) - (TextureSize * .5f - 2f);
                float alpha = Mathf.Clamp01(.5f - distance);
                float ink = Mathf.Clamp01(distance + 6f + .5f);
                Color color = Color.Lerp(Color.white, (Color)InkColor, ink);
                color.a = alpha;
                return color;
            }, Vector4.zero);
            return discSprite;
        }

        private static Sprite CheckSprite()
        {
            if (checkSprite != null) return checkSprite;
            float s = TextureSize;
            Vector2 a = new Vector2(s * .26f, s * .52f), b = new Vector2(s * .43f, s * .33f), d = new Vector2(s * .76f, s * .7f);
            checkSprite = Draw("Lab combo check", (p, c) =>
            {
                float distance = Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, b, d));
                float core = Mathf.Clamp01(s * .07f + .5f - distance);
                float edge = Mathf.Clamp01(s * .07f + 4f + .5f - distance);
                Color color = Color.Lerp((Color)InkColor, Color.white, core);
                color.a = edge;
                return color;
            }, Vector4.zero);
            return checkSprite;
        }

        private static Sprite RoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            float radius = TextureSize * .3f;
            roundedSprite = Draw("Lab combo panel", (p, c) =>
            {
                Vector2 q = new Vector2(Mathf.Max(Mathf.Abs(p.x - c.x) - (c.x - radius), 0f),
                    Mathf.Max(Mathf.Abs(p.y - c.y) - (c.y - radius), 0f));
                float distance = q.magnitude - radius + 1f;
                float alpha = Mathf.Clamp01(.5f - distance);
                float ink = Mathf.Clamp01(distance + 4f + .5f);
                Color color = Color.Lerp(Color.white, (Color)InkColor, ink);
                color.a = alpha;
                return color;
            }, new Vector4(radius, radius, radius, radius));
            return roundedSprite;
        }

        private static Sprite Draw(string name, System.Func<Vector2, Vector2, Color> shade, Vector4 border)
        {
            var pixels = new Color[TextureSize * TextureSize];
            Vector2 center = new Vector2(TextureSize * .5f, TextureSize * .5f);
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                    pixels[y * TextureSize + x] = shade(new Vector2(x + .5f, y + .5f), center);
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private void OnDestroy()
        {
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
