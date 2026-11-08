using System;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// #551 지귀 B 3-2-1 화면 (모든 기기). 지목된 사람마다 P번호 동그라미가 뜨고, 특수 물구슬을 잡고 있으면 파랗게 빛난다
    /// (내 동그라미는 테두리가 굵다). 아직 안 잡은 자리 동그라미는 빈 고리가 두근거리고, 내 판의 특수 물구슬 둘레에도 빈 고리가
    /// 두근거리며 "꾹!"이 떠서 꾹 잡고 있어야 한다는 걸 모두가 안다. 잡으면 고리가 파랗게 한 바퀴 차오른다(게이지).
    /// "던져!" 뒤에는 내 구슬에서 위로 솟는 화살표가 위로 휙 던지라고 알린다.
    /// 모두 잡으면 가운데에 큰 "3 → 2 → 1 → 지금!"이 북소리와 함께 박자마다 쿵 하고
    /// 커졌다 줄어든다. 지목된 사람 폰은 박자마다 진동하고 "지금!"에 크게 진동한다. 판정은 Host(`LabModel.TrySetJigwiBReady`).
    /// </summary>
    public sealed class LabJigwiBCountdownView : MonoBehaviour
    {
        [Tooltip("3-2-1 박자마다 나는 북소리 폴더.")]
        [SerializeField] private string beatSoundFolder = "JigwiBBeat";
        [Tooltip("\"지금!\"에 나는 큰 북·징 소리 폴더.")]
        [SerializeField] private string goSoundFolder = "JigwiBGo";
        [SerializeField, Range(0f, 1f)] private float beatSoundVolume = .9f;
        [SerializeField, Range(0f, 1f)] private float goSoundVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float beatHapticIntensity = .55f;
        [SerializeField, Range(0f, 1f)] private float goHapticIntensity = 1f;
        [SerializeField] private Color numberColor = new Color(1f, .9f, .45f, 1f);
        [SerializeField] private Color goColor = new Color(.45f, .85f, 1f, 1f);
        [SerializeField] private Color readyColor = new Color(.3f, .7f, 1f, 1f);
        [SerializeField] private Color waitingColor = new Color(1f, 1f, 1f, .45f);

        private LabNetwork source;
        private Font font;
        private RectTransform root;
        private Text big;
        private Outline bigOutline;
        private readonly Image[] chips = new Image[LabJigwiABTimeline.SlotCount];
        private readonly Image[] rings = new Image[LabJigwiABTimeline.SlotCount];
        private readonly Text[] chipLabels = new Text[LabJigwiABTimeline.SlotCount];
        private AudioSource sfx;
        private string countedRound;
        private double countedFrom = -1d;
        private int lastBeat = -1;
        private static Sprite disc, ring, arrow;
        // #551 "꾹" hints: an empty ring breathing round each waiting seat and round my special Water; holding fills it.
        private Image orbRing, orbRipple, orbCharge;
        private Text orbLabel;
        private readonly Image[] throwArrows = new Image[3];
        private float holdStartedAt = -1f;
        /// <summary>테스트용: 내 특수 물구슬 둘레 "꾹!" 안내가 보이는지.</summary>
        public bool HoldHintVisible => orbLabel != null && orbLabel.gameObject.activeSelf;
        /// <summary>테스트용: "던져!" 화살표가 보이는지.</summary>
        public bool ThrowHintVisible => throwArrows[0] != null && throwArrows[0].gameObject.activeSelf;

        /// <summary>테스트용: 지금 크게 보이는 글자 ("" = 없음).</summary>
        public string Shown => big != null && big.gameObject.activeSelf ? big.text : string.Empty;
        /// <summary>테스트용: 박자 소리가 난 횟수 ("지금!" 포함).</summary>
        public int BeatCount { get; private set; }

        public void Bind(LabNetwork network, Font labelFont)
        {
            source = network;
            if (labelFont != null) font = labelFont;
            Ensure();
            if (big != null && font != null) big.font = font;
            foreach (Text label in chipLabels) if (label != null && font != null) label.font = font;
        }

        private void Ensure()
        {
            if (root != null) return;
            root = transform as RectTransform;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            big = NewText("Jigwi B Countdown", root, 220);
            big.rectTransform.anchorMin = big.rectTransform.anchorMax = new Vector2(.5f, .74f);
            big.rectTransform.sizeDelta = new Vector2(900f, 360f);
            bigOutline = big.gameObject.AddComponent<Outline>();
            bigOutline.effectColor = new Color(.1f, .06f, .02f, .9f); bigOutline.effectDistance = new Vector2(6f, -6f);
            big.gameObject.SetActive(false);
            for (int i = 0; i < chips.Length; i++)
            {
                rings[i] = NewImage("Jigwi B Seat Ring " + i, root, Ring());
                chips[i] = NewImage("Jigwi B Seat " + i, root, Disc());
                chipLabels[i] = NewText("Jigwi B Seat Label " + i, chips[i].rectTransform, 44);
                chipLabels[i].rectTransform.anchorMin = Vector2.zero; chipLabels[i].rectTransform.anchorMax = Vector2.one;
                chipLabels[i].rectTransform.offsetMin = chipLabels[i].rectTransform.offsetMax = Vector2.zero;
                rings[i].gameObject.SetActive(false); chips[i].gameObject.SetActive(false);
            }
            orbRipple = NewImage("Jigwi B Hold Ripple", root, Ring());
            orbRing = NewImage("Jigwi B Hold Ring", root, Ring());
            orbCharge = NewImage("Jigwi B Hold Gauge", root, Ring());
            orbCharge.type = Image.Type.Filled; orbCharge.fillMethod = Image.FillMethod.Radial360;
            orbCharge.fillOrigin = (int)Image.Origin360.Top; orbCharge.fillClockwise = true;
            for (int i = 0; i < throwArrows.Length; i++)
            {
                throwArrows[i] = NewImage("Jigwi B Throw Arrow " + i, root, Arrow());
                throwArrows[i].gameObject.SetActive(false);
            }
            orbLabel = NewText("Jigwi B Hold Label", root, 64);
            orbLabel.text = "꾹!";
            var labelOutline = orbLabel.gameObject.AddComponent<Outline>();
            labelOutline.effectColor = new Color(.1f, .06f, .02f, .9f); labelOutline.effectDistance = new Vector2(3f, -3f);
            foreach (Graphic part in new Graphic[] { orbRipple, orbRing, orbCharge, orbLabel }) part.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (source == null) { Hide(); return; }
            LabSnapshot snapshot = source.Snapshot;
            double now = source.JigwiABPresentationTime;
            LabJigwiABRound round = snapshot != null && snapshot.phase == LabPhase.Playing && snapshot.jigwiAB?.enabled == true
                ? snapshot.jigwiAB.Active(now) : null;
            if (round == null || round.kind != LabJigwiABKind.GroupFire || !round.Active(now) || round.syncWindowSeconds <= 0f)
            { Hide(); return; }
            Present(snapshot, round, source.LocalPlayerId, now);
        }

        /// <summary>테스트와 LateUpdate가 같이 쓰는 그리기.</summary>
        public void Present(LabSnapshot snapshot, LabJigwiABRound round, ulong localPlayer, double now)
        {
            Ensure();
            bool picked = false;
            foreach (var seat in round.players) if (seat != null && seat.playerId == localPlayer) picked = true;
            bool afterGo = round.countdownAt >= 0d && now >= round.GoAt;
            // #551 나무 물통: the bucket and ropes show who holds; the handle view has its own hints.
            DrawSeats(snapshot, round, localPlayer, now, !afterGo && !round.bucket);
            DrawHoldHint(round, localPlayer, now, picked && !afterGo && !round.bucket);
            DrawThrowHint(round, localPlayer, now, picked && afterGo && !round.bucket);
            big.rectTransform.anchorMin = big.rectTransform.anchorMax = round.bucket ? new Vector2(afterGo ? .7f : .76f, .66f) : new Vector2(.5f, .74f);
            big.fontSize = round.bucket ? (afterGo ? 90 : 110) : 220;
            if (round.countdownAt < 0d || now < round.countdownAt)
            {
                // Waiting for everyone to hold: no number; a cancelled count simply disappears.
                if (big.gameObject.activeSelf) big.gameObject.SetActive(false);
                countedRound = null; lastBeat = -1;
                return;
            }
            if (countedRound != round.id || Math.Abs(countedFrom - round.countdownAt) > 1e-6) { countedRound = round.id; countedFrom = round.countdownAt; lastBeat = -1; }
            double t = now - round.countdownAt;
            double beatSeconds = round.CountdownLength / 3d;
            int beat = t < round.CountdownLength ? Mathf.Clamp((int)(t / beatSeconds), 0, 2) : 3;
            bool showing = beat < 3 || t < round.CountdownLength + (round.bucket ? .8d : LabJigwiABTimeline.ReleaseSeconds + .3d);
            if (big.gameObject.activeSelf != showing) big.gameObject.SetActive(showing);
            if (!showing) return;
            double since = beat < 3 ? t - beat * beatSeconds : t - round.CountdownLength;
            big.text = beat < 3 ? (3 - beat).ToString() : round.bucket ? "당겨!" : "던져!";
            big.color = beat < 3 ? numberColor : goColor;
            // Each beat lands big and settles (a drum hit you can see).
            float punch = Mathf.Exp(-(float)since * 9f);
            big.rectTransform.localScale = Vector3.one * (1f + (beat < 3 ? .45f : .7f) * punch);
            if (beat > lastBeat)
            {
                lastBeat = beat;
                BeatCount++;
                Play(beat < 3 ? beatSoundFolder : goSoundFolder, beat < 3 ? beatSoundVolume : goSoundVolume);
                if (picked && source != null)
                    LabHaptics.PlayDirect(source.config, beat < 3 ? LabHapticStyle.Medium : LabHapticStyle.Heavy,
                        beat < 3 ? beatHapticIntensity : goHapticIntensity, beat < 3 ? 0f : .25f, goHapticIntensity, .8f);
            }
        }

        /// <summary>지목된 자리마다 P번호 동그라미. 잡고 있으면 파랗게 빛나고, 내 자리는 테두리가 굵다.</summary>
        private void DrawSeats(LabSnapshot snapshot, LabJigwiABRound round, ulong localPlayer, double now, bool show)
        {
            int count = 0;
            float pulse = .5f + .5f * Mathf.Sin((float)now * 8f);
            for (int i = 0; i < chips.Length; i++)
            {
                LabJigwiABPlayerFire seat = show && round.players != null && i < round.players.Length ? round.players[i] : null;
                bool on = seat != null;
                if (chips[i].gameObject.activeSelf != on) { chips[i].gameObject.SetActive(on); rings[i].gameObject.SetActive(on); }
                if (!on) continue;
                count++;
                bool ready = seat.readyAt >= 0d;
                bool mine = seat.playerId == localPlayer;
                chips[i].color = ready ? readyColor : waitingColor;
                chipLabels[i].text = "P" + SlotOf(snapshot, seat.playerId);
                chipLabels[i].color = ready ? Color.white : new Color(.15f, .1f, .05f, .9f);
                rings[i].color = mine ? new Color(1f, .9f, .45f, ready ? 1f : .55f + .45f * pulse) : new Color(1f, 1f, 1f, ready ? .8f : .3f);
            }
            float size = 120f, gap = 30f;
            float width = count * size + Math.Max(0, count - 1) * gap;
            for (int i = 0, k = 0; i < chips.Length; i++)
            {
                if (!chips[i].gameObject.activeSelf) continue;
                Vector2 at = new Vector2(-width * .5f + size * .5f + k++ * (size + gap), 0f);
                bool ready = round.players[i].readyAt >= 0d;
                foreach (RectTransform rect in new[] { chips[i].rectTransform, rings[i].rectTransform })
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .6f);
                    rect.anchoredPosition = at;
                }
                chips[i].rectTransform.sizeDelta = Vector2.one * size * (ready ? 1f + .06f * pulse : 1f);
                // A seat not holding yet: its empty ring breathes in and out ("꾹 잡아!"); a holding seat stays still.
                float press = ready ? 0f : Press((float)now);
                chips[i].rectTransform.localScale = Vector3.one * (1f - .08f * press);
                rings[i].rectTransform.localScale = Vector3.one * (ready ? 1f : 1f + .18f * (1f - press));
                rings[i].rectTransform.sizeDelta = Vector2.one * size * (round.players[i].playerId == localPlayer ? 1.3f : 1.12f);
            }
        }

        /// <summary>0 → 1 → 0 press cycle (down quickly, hold, lift), about once a second.</summary>
        private static float Press(float time)
        {
            float u = Mathf.Repeat(time, 1.1f) / 1.1f;
            if (u < .25f) return Mathf.SmoothStep(0f, 1f, u / .25f);
            if (u < .6f) return 1f;
            if (u < .8f) return 1f - Mathf.SmoothStep(0f, 1f, (u - .6f) / .2f);
            return 0f;
        }

        /// <summary>
        /// 내 판의 특수 물구슬 둘레: 잡기 전에는 빈 고리가 두근거리고 "꾹!"이 뜨며 고리가 하나 퍼진다. 잡으면 고리가 파랗게
        /// 한 바퀴 차오르고(게이지) 차오른 채 빛난다(모두 잡기를 기다리는 중).
        /// </summary>
        private void DrawHoldHint(LabJigwiABRound round, ulong localPlayer, double now, bool show)
        {
            string orbId = MyOrb(round, localPlayer, show);
            Vector2 at = default; float radius = 0f;
            bool placed = orbId != null && TryOrbOnScreen(orbId, out at, out radius);
            bool holding = placed && source.board != null && source.board.HeldOrbId == orbId;
            if (!holding) holdStartedAt = -1f; else if (holdStartedAt < 0f) holdStartedAt = Time.unscaledTime;
            bool hint = placed && !holding;
            foreach (Graphic part in new Graphic[] { orbLabel, orbRipple })
                if (part.gameObject.activeSelf != hint) part.gameObject.SetActive(hint);
            if (orbRing.gameObject.activeSelf != placed) orbRing.gameObject.SetActive(placed);
            if (orbCharge.gameObject.activeSelf != holding) orbCharge.gameObject.SetActive(holding);
            if (!placed) return;
            float t = (float)now;
            float press = Press(t);
            Place(orbRing.rectTransform, at, Vector2.one * radius * (holding ? 2.6f : 2.6f + .5f * (1f - press)));
            orbRing.color = holding ? new Color(1f, 1f, 1f, .35f) : new Color(1f, .95f, .7f, .95f);
            if (hint)
            {
                Place(orbLabel.rectTransform, at + new Vector2(0f, radius * 2.2f), new Vector2(radius * 3f, radius * 1.4f));
                orbLabel.color = new Color(1f, .9f, .45f, 1f);
                orbLabel.rectTransform.localScale = Vector3.one * (1f + .18f * press);
                Ripple(orbRipple, at, radius, Mathf.Repeat(t, 1.1f) / 1.1f);
            }
            else
            {
                // The gauge fills once round in 0.4 s, then glows full while waiting for the others.
                float fill = Mathf.Clamp01((Time.unscaledTime - holdStartedAt) / .4f);
                float breath = .5f + .5f * Mathf.Sin(t * 10f);
                Place(orbCharge.rectTransform, at, Vector2.one * radius * 2.6f);
                orbCharge.fillAmount = fill;
                orbCharge.color = new Color(.35f, .75f, 1f, fill < 1f ? 1f : .75f + .25f * breath);
            }
        }

        /// <summary>"던져!" 동안 내 구슬에서 위로 솟는 화살표 세 개 (아직 안 던졌을 때만).</summary>
        private void DrawThrowHint(LabJigwiABRound round, ulong localPlayer, double now, bool show)
        {
            bool launched = false;
            foreach (var seat in round.players) if (seat != null && seat.playerId == localPlayer && seat.launchedAt >= 0d) launched = true;
            string orbId = MyOrb(round, localPlayer, show && !launched);
            Vector2 at = default; float radius = 0f;
            bool placed = orbId != null && TryOrbOnScreen(orbId, out at, out radius);
            for (int i = 0; i < throwArrows.Length; i++)
            {
                Image arrowImage = throwArrows[i];
                if (arrowImage.gameObject.activeSelf != placed) arrowImage.gameObject.SetActive(placed);
                if (!placed) continue;
                float u = Mathf.Repeat((float)now * 2.2f + i / 3f, 1f);
                Place(arrowImage.rectTransform, at + new Vector2(0f, radius * (1.4f + 3.2f * u)), new Vector2(radius * 1.6f, radius * 1.1f));
                arrowImage.color = new Color(.45f, .85f, 1f, Mathf.Sin(Mathf.PI * u));
            }
        }

        private string MyOrb(LabJigwiABRound round, ulong localPlayer, bool show)
        {
            if (!show || source == null || source.Snapshot?.orbs == null) return null;
            foreach (var orb in source.Snapshot.orbs)
                if (orb != null && orb.jigwiABSupplyEventId == round.id && orb.originOwner == localPlayer && !orb.inFlight) return orb.id;
            return null;
        }

        private static void Ripple(Image image, Vector2 at, float radius, float u)
        {
            Place(image.rectTransform, at, Vector2.one * radius * (2.1f + 2.2f * u));
            image.color = new Color(1f, .95f, .6f, .9f * (1f - u));
        }

        private static void Place(RectTransform rect, Vector2 at, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = at; rect.sizeDelta = size;
        }

        /// <summary>판 위 구슬의 화면 자리(이 화면 root 기준, 가운데 원점)와 화면 반지름.</summary>
        private bool TryOrbOnScreen(string orbId, out Vector2 local, out float radius)
        {
            local = default; radius = 0f;
            LabOrbBoard board = source != null ? source.board : null;
            if (board == null || root == null || !board.TryGetScreenPoint(orbId, out Vector2 screen)) return false;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, eye, out local)) return false;
            if (!board.TryGetScreenSpot(new Vector2(.5f, .5f), out Vector2 a, out float pixels)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, a, eye, out Vector2 p0)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, a + Vector2.right * pixels, eye, out Vector2 p1)) return false;
            radius = Mathf.Max(10f, Vector2.Distance(p0, p1));
            // Local points are relative to the root's pivot; Place anchors at the centre, so shift by the pivot offset.
            Vector2 pivotShift = (new Vector2(.5f, .5f) - root.pivot) * root.rect.size;
            local -= pivotShift;
            return true;
        }

        private static int SlotOf(LabSnapshot snapshot, ulong player)
        {
            foreach (var value in snapshot?.players ?? Array.Empty<LabPlayerState>())
                if (value != null && value.id == player) return value.seat + 1;
            return 0;
        }

        private void Hide()
        {
            if (big != null && big.gameObject.activeSelf) big.gameObject.SetActive(false);
            for (int i = 0; i < chips.Length; i++)
            {
                if (chips[i] != null && chips[i].gameObject.activeSelf) chips[i].gameObject.SetActive(false);
                if (rings[i] != null && rings[i].gameObject.activeSelf) rings[i].gameObject.SetActive(false);
            }
            countedRound = null; lastBeat = -1;
            foreach (Image hint in throwArrows) if (hint != null && hint.gameObject.activeSelf) hint.gameObject.SetActive(false);
            foreach (Graphic part in new Graphic[] { orbRing, orbLabel, orbRipple, orbCharge })
                if (part != null && part.gameObject.activeSelf) part.gameObject.SetActive(false);
        }

        private void Play(string folder, float volume)
        {
            if (string.IsNullOrEmpty(folder) || volume <= 0f) return;
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/" + folder);
            if (clips == null || clips.Length == 0) return;
            if (sfx == null) { sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false; sfx.spatialBlend = 0f; }
            sfx.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)], volume * LabAudioSettings.SfxVolume);
        }

        private Text NewText(string label, RectTransform parent, int size)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Image NewImage(string label, RectTransform parent, Sprite sprite)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        private static Sprite Disc() => disc != null ? disc : disc = Make("Jigwi B seat disc", d => Mathf.Clamp01((.48f - d) * 60f));
        /// <summary>#551: a fat upward chevron ("위로 휙!") with a dark outline.</summary>
        private static Sprite Arrow()
        {
            if (arrow != null) return arrow;
            const int w = 96, h = 64;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = "Jigwi B throw arrow (#551 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w, v = (y + .5f) / h;
                    // Two thick strokes meeting at the top centre.
                    float d = Mathf.Min(Capsule(u, v, .14f, .22f, .5f, .78f, .11f), Capsule(u, v, .86f, .22f, .5f, .78f, .11f));
                    Color c = Color.Lerp(new Color(.08f, .2f, .4f, 1f), Color.white, Mathf.Clamp01((-d - .03f) * 150f));
                    c.a = Mathf.Clamp01((-d + .03f) * 150f);
                    pixels[y * w + x] = c;
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            arrow = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(.5f, .5f), w);
            arrow.name = texture.name; arrow.hideFlags = HideFlags.DontSave;
            return arrow;
        }

        /// <summary>Distance to a rounded box (x0..x1, y0..y1, corner radius r); negative inside.</summary>
        private static float Box(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            float cx = Mathf.Clamp(x, x0 + r, x1 - r), cy = Mathf.Clamp(y, y0 + r, y1 - r);
            return Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - r;
        }

        /// <summary>Signed distance (in texture units) to a capsule from (ax, ay) to (bx, by) with radius r; negative inside.</summary>
        private static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            var p = new Vector2(x, y); var a = new Vector2(ax, ay); var b = new Vector2(bx, by);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t) - r;
        }

        private static Sprite Ring() => ring != null ? ring : ring = Make("Jigwi B seat ring", d => Mathf.Clamp01(1f - Mathf.Abs(d - .44f) * 40f));

        private static Sprite Make(string name, Func<float, float> alpha)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = name + " (#551 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size - .5f, dy = (y + .5f) / size - .5f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha(Mathf.Sqrt(dx * dx + dy * dy)));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            sprite.name = texture.name; sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
