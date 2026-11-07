using System;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// #551 지귀 B 3-2-1 화면 (모든 기기). 지목된 사람마다 P번호 동그라미가 뜨고, 특수 물구슬을 잡고 있으면 파랗게 빛난다
    /// (내 동그라미는 테두리가 굵다). 아직 안 잡은 자리 동그라미 위에는 손가락이 꾹 누르는 모양이 되풀이되고, 내 판의 특수
    /// 물구슬 위에도 "꾹!" 손가락과 퍼지는 고리가 떠서 꾹 잡고 있어야 한다는 걸 모두가 안다. 잡으면 구슬 둘레가 파랗게 차오른다.
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
        private static Sprite disc, ring, finger;
        // #551 "꾹" hints: a finger pressing over each waiting seat circle, and over my special Water on the board.
        private readonly Image[] seatFingers = new Image[LabJigwiABTimeline.SlotCount];
        private Image orbFinger, orbRipple, orbRippleB, orbCharge;
        private Text orbLabel;
        /// <summary>테스트용: 내 특수 물구슬 위 "꾹!" 안내가 보이는지.</summary>
        public bool HoldHintVisible => orbFinger != null && orbFinger.gameObject.activeSelf;

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
                seatFingers[i] = NewImage("Jigwi B Seat Finger " + i, root, Finger());
                seatFingers[i].gameObject.SetActive(false);
            }
            orbRipple = NewImage("Jigwi B Hold Ripple", root, Ring());
            orbRippleB = NewImage("Jigwi B Hold Ripple 2", root, Ring());
            orbCharge = NewImage("Jigwi B Hold Charge", root, Ring());
            orbFinger = NewImage("Jigwi B Hold Finger", root, Finger());
            orbLabel = NewText("Jigwi B Hold Label", root, 64);
            orbLabel.text = "꾹!";
            var labelOutline = orbLabel.gameObject.AddComponent<Outline>();
            labelOutline.effectColor = new Color(.1f, .06f, .02f, .9f); labelOutline.effectDistance = new Vector2(3f, -3f);
            foreach (Graphic part in new Graphic[] { orbRipple, orbRippleB, orbCharge, orbFinger, orbLabel }) part.gameObject.SetActive(false);
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
            DrawSeats(snapshot, round, localPlayer, now, !afterGo);
            DrawHoldHint(round, localPlayer, now, picked && !afterGo);
            if (round.countdownAt < 0d)
            {
                // Waiting for everyone to hold: no number; a cancelled count simply disappears.
                if (big.gameObject.activeSelf) big.gameObject.SetActive(false);
                countedRound = null; lastBeat = -1;
                return;
            }
            if (countedRound != round.id || Math.Abs(countedFrom - round.countdownAt) > 1e-6) { countedRound = round.id; countedFrom = round.countdownAt; lastBeat = -1; }
            double t = now - round.countdownAt;
            double beatSeconds = LabJigwiABTimeline.CountdownSeconds / 3d;
            int beat = t < LabJigwiABTimeline.CountdownSeconds ? Mathf.Clamp((int)(t / beatSeconds), 0, 2) : 3;
            bool showing = beat < 3 || t < LabJigwiABTimeline.CountdownSeconds + LabJigwiABTimeline.ReleaseSeconds + .3d;
            if (big.gameObject.activeSelf != showing) big.gameObject.SetActive(showing);
            if (!showing) return;
            double since = beat < 3 ? t - beat * beatSeconds : t - LabJigwiABTimeline.CountdownSeconds;
            big.text = beat < 3 ? (3 - beat).ToString() : "지금!";
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
                if (!on) { if (seatFingers[i].gameObject.activeSelf) seatFingers[i].gameObject.SetActive(false); continue; }
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
                // A finger keeps pressing down on every seat that is not holding yet ("꾹 잡아!").
                Image hand = seatFingers[i];
                if (hand.gameObject.activeSelf == ready) hand.gameObject.SetActive(!ready);
                if (!ready)
                {
                    float press = Press((float)now);
                    hand.rectTransform.anchorMin = hand.rectTransform.anchorMax = new Vector2(.5f, .6f);
                    hand.rectTransform.anchoredPosition = at + new Vector2(size * .28f, size * (.75f - .3f * press));
                    hand.rectTransform.sizeDelta = new Vector2(size * .5f, size * .75f);
                    hand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
                    hand.color = new Color(1f, 1f, 1f, .95f);
                    chips[i].rectTransform.localScale = Vector3.one * (1f - .08f * press);
                }
                else chips[i].rectTransform.localScale = Vector3.one;
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
        /// 내 판의 특수 물구슬 위: 잡기 전에는 손가락이 꾹 누르고 "꾹!"과 고리가 퍼진다. 잡고 있으면 손가락은 사라지고
        /// 구슬 둘레가 파랗게 빛나며 차오른다(모두 잡기를 기다리는 중).
        /// </summary>
        private void DrawHoldHint(LabJigwiABRound round, ulong localPlayer, double now, bool show)
        {
            string orbId = null;
            if (show && source != null && source.Snapshot?.orbs != null)
                foreach (var orb in source.Snapshot.orbs)
                    if (orb != null && orb.jigwiABSupplyEventId == round.id && orb.originOwner == localPlayer && !orb.inFlight) orbId = orb.id;
            Vector2 at = default; float radius = 0f;
            bool placed = orbId != null && TryOrbOnScreen(orbId, out at, out radius);
            bool holding = placed && source.board != null && source.board.HeldOrbId == orbId;
            bool hint = placed && !holding;
            foreach (Graphic part in new Graphic[] { orbFinger, orbLabel, orbRipple, orbRippleB })
                if (part.gameObject.activeSelf != hint) part.gameObject.SetActive(hint);
            if (orbCharge.gameObject.activeSelf != holding) orbCharge.gameObject.SetActive(holding);
            if (!placed) return;
            float t = (float)now;
            if (hint)
            {
                float press = Press(t);
                Place(orbFinger.rectTransform, at + new Vector2(radius * .55f, radius * (1.9f - .75f * press)), new Vector2(radius * 1.1f, radius * 1.65f));
                orbFinger.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
                orbFinger.color = Color.white;
                Place(orbLabel.rectTransform, at + new Vector2(-radius * 1.5f, radius * 1.5f), new Vector2(radius * 3f, radius * 1.4f));
                orbLabel.color = new Color(1f, .9f, .45f, 1f);
                orbLabel.rectTransform.localScale = Vector3.one * (1f + .15f * press);
                // Two ripples spread from the orb each time the finger presses.
                float u = Mathf.Repeat(t, 1.1f) / 1.1f;
                Ripple(orbRipple, at, radius, u);
                Ripple(orbRippleB, at, radius, Mathf.Repeat(u + .5f, 1f));
            }
            else
            {
                float breath = .5f + .5f * Mathf.Sin(t * 10f);
                Place(orbCharge.rectTransform, at, Vector2.one * radius * (2.5f + .2f * breath));
                orbCharge.color = new Color(.35f, .75f, 1f, .7f + .3f * breath);
            }
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
            foreach (Image hand in seatFingers) if (hand != null && hand.gameObject.activeSelf) hand.gameObject.SetActive(false);
            foreach (Graphic part in new Graphic[] { orbFinger, orbLabel, orbRipple, orbRippleB, orbCharge })
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
        /// <summary>#551: a simple pointing finger (fingertip down) with a dark outline, like a "press here" icon.</summary>
        private static Sprite Finger()
        {
            if (finger != null) return finger;
            const int w = 64, h = 96;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = "Jigwi B finger (#551 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w, v = (y + .5f) / h;
                    // Finger: a capsule from the fingertip (bottom) up; palm: a rounded block at the top.
                    float digit = Capsule(u, v, .5f, .12f, .5f, .62f, .16f);
                    float palm = Capsule(u, v, .5f, .72f, .5f, .86f, .3f);
                    float d = Mathf.Min(digit, palm);
                    float fill = Mathf.Clamp01(-d * 120f), edge = Mathf.Clamp01((-d + .045f) * 120f);
                    Color skin = new Color(1f, .93f, .82f, 1f), ink = new Color(.12f, .08f, .05f, 1f);
                    Color c = Color.Lerp(ink, skin, Mathf.Clamp01((-d - .035f) * 120f));
                    c.a = Mathf.Max(fill, edge);
                    pixels[y * w + x] = c;
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            finger = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(.5f, .5f), w);
            finger.name = texture.name; finger.hideFlags = HideFlags.DontSave;
            return finger;
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
