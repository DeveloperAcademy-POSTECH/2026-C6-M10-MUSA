using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// #551 지귀 B 나무 물통 (모든 기기).
    /// - 진행바 바로 아래 한 줄 안내: "다 같이 밧줄을 당겨!" → "\"당겨!\"에 폰을 내 쪽으로 홱!" → "지금! 폰을 내 쪽으로 홱! (2/4)"
    ///   → 늦으면 "누가 늦었어! 다시!" → "물벼락!" / "물이 다 졸았다…".
    /// - 내 판 위쪽 끝에 내 밧줄 끝(나무 손잡이 그림). 누를 필요는 없고, 판의 구슬은 평소처럼 쓴다.
    /// - 당기기: 폰 윗부분을 내 쪽으로 홱 젖힌다(가속도계/중력으로 일어선 각도가 tiltWindowSeconds 안에 tiltDegrees 이상 커짐).
    ///   Editor/DEV 빌드는 K를 keyHoldSeconds 누르고 있어도 된다(섬광 흔들기의 K처럼). "당겨!" 전에 젖히면 세지 않고 "아직!".
    /// 판정은 Host(`LabModel.TryJigwiBPull`).
    /// </summary>
    public sealed class LabJigwiBucketHandleView : MonoBehaviour
    {
        [Tooltip("내 밧줄 끝(손잡이 그림)이 놓이는 판 위 자리(0~1). 1이 판의 먼 끝(박 쪽).")]
        [SerializeField] private Vector2 handleSpot = new Vector2(.5f, .88f);
        [Tooltip("이 시간(초) 안에 폰이 이만큼(도) 내 쪽으로 젖혀지면 당긴 것.")]
        [SerializeField, Range(5f, 60f)] private float tiltDegrees = 25f;
        [SerializeField, Range(.1f, 1f)] private float tiltWindowSeconds = .3f;
        [Tooltip("Editor/DEV 빌드: K를 이만큼(초) 누르고 있으면 당긴 것.")]
        [SerializeField, Range(.05f, 1f)] private float keyHoldSeconds = .2f;
        [Tooltip("안내 한 줄의 화면 자리(진행바 바로 아래).")]
        [SerializeField] private Vector2 bannerAnchor = new Vector2(.5f, .835f);
        [Tooltip("결과(물벼락!/다 졸았다) 안내가 남는 시간(초).")]
        [SerializeField, Range(.5f, 4f)] private float resultSeconds = 1.8f;
        [SerializeField, Range(0f, 1f)] private float pullHapticIntensity = 1f;
        [SerializeField, Range(0f, 1f)] private float earlyHapticIntensity = .3f;
        [SerializeField] private Color woodColor = new Color(.69f, .48f, .27f, 1f);
        [SerializeField] private Color pulledColor = new Color(.3f, .62f, .95f, 1f);
        [SerializeField] private Color ropeColor = new Color(.86f, .72f, .5f, 1f);

        private LabNetwork source;
        private Font font;
        private RectTransform root;
        private Image ropeShadow, rope, bar, phone;
        private Text banner, early;
        private static Sprite barSprite, phoneSprite, pixel;
        private float keyHeld, pulledAt = -1f, earlyAt = -1f, cooldownUntil;
        private string countKey;
        private readonly float[] pitchTimes = new float[64];
        private readonly float[] pitches = new float[64];
        private int pitchCount, pitchHead;
        private Vector2 handleAt, ropeTop; private float handleRadius;

        /// <summary>테스트용: 지금 안내 한 줄 ("" = 없음) / 밧줄 끝이 보이는지.</summary>
        public string Banner => banner != null && banner.gameObject.activeSelf ? banner.text : string.Empty;
        public bool HandleVisible => bar != null && bar.gameObject.activeSelf;

        public void Bind(LabNetwork network, Font labelFont)
        {
            source = network;
            if (labelFont != null) font = labelFont;
            Ensure();
            if (font != null) { banner.font = font; early.font = font; }
        }

        private void Ensure()
        {
            if (root != null) return;
            root = transform as RectTransform;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ropeShadow = NewImage("Jigwi Bucket Rope Shadow", Pixel());
            rope = NewImage("Jigwi Bucket Rope", Pixel());
            bar = NewImage("Jigwi Bucket Rope End", Bar());
            phone = NewImage("Jigwi Bucket Tilt Phone", Phone());
            banner = NewText("Jigwi Bucket Banner", 64);
            early = NewText("Jigwi Bucket Too Early", 60); early.text = "아직!";
            Hide();
        }

        private void LateUpdate()
        {
            LabSnapshot snapshot = source != null ? source.Snapshot : null;
            double now = source != null ? source.JigwiABPresentationTime : 0d;
            LabJigwiABRound round = Showing(snapshot, now);
            if (round == null) { Hide(); return; }
            Ensure();
            bool live = round.resolution == LabJigwiABResolution.None && round.Active(now);
            string key = round.id + "@" + round.countdownAt.ToString("R");
            if (countKey != key) { countKey = key; pulledAt = -1f; }
            bool pulled = MinePulled(round, source.LocalPlayerId) || source.JigwiBucketPulledThisCount(round);
            if (live && Time.unscaledTime >= cooldownUntil && ReadPull())
            {
                bool window = round.countdownAt >= 0d && now >= round.GoAt && now <= round.GoAt + round.PullSeconds;
                if (window && !pulled && source.RequestJigwiBucketPull())
                {
                    pulled = true; pulledAt = Time.unscaledTime;
                    LabHaptics.PlayDirect(source.config, LabHapticStyle.Heavy, pullHapticIntensity, .25f, pullHapticIntensity, .8f);
                }
                else if (!pulled)
                {
                    // Too early (or between counts): it does not count. "아직!" and my rope jiggles.
                    earlyAt = Time.unscaledTime;
                    LabHaptics.PlayDirect(source.config, LabHapticStyle.Light, earlyHapticIntensity);
                }
                cooldownUntil = Time.unscaledTime + .4f; pitchCount = 0; keyHeld = 0f;
            }
            DrawBanner(round, snapshot, now);
            if (live && source.board != null && TryPlace(source.board)) DrawRope(round, now, pulled);
            else HideRope();
        }

        /// <summary>안내를 보여 줄 물통 B: 예고부터 끝난 뒤 resultSeconds까지.</summary>
        private LabJigwiABRound Showing(LabSnapshot snapshot, double now)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.jigwiAB?.enabled != true) return null;
            foreach (var round in snapshot.jigwiAB.rounds ?? Array.Empty<LabJigwiABRound>())
            {
                if (round == null || !round.bucket || now < round.warningAt) continue;
                if (round.resolution == LabJigwiABResolution.None && now < round.endsAt) return round;
                if (round.resolution != LabJigwiABResolution.None && round.resolvedAt >= 0d && now < round.resolvedAt + resultSeconds
                    && (round.resolution == LabJigwiABResolution.Completed || round.resolution == LabJigwiABResolution.Expired)) return round;
            }
            return null;
        }

        private static bool MinePulled(LabJigwiABRound round, ulong me)
        {
            foreach (var seat in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                if (seat != null && seat.playerId == me && seat.launchedAt >= 0d) return true;
            return false;
        }

        private void DrawBanner(LabJigwiABRound round, LabSnapshot snapshot, double now)
        {
            int total = round.players?.Length ?? 0, done = 0;
            foreach (var seat in round.players ?? Array.Empty<LabJigwiABPlayerFire>()) if (seat != null && seat.launchedAt >= 0d) done++;
            string text; Color color = new Color(1f, .93f, .7f, 1f);
            float pop = 0f;
            if (round.resolution == LabJigwiABResolution.Completed) { text = "물벼락!"; color = new Color(.55f, .88f, 1f, 1f); pop = Pop(now - round.resolvedAt); }
            else if (round.resolution == LabJigwiABResolution.Expired) { text = "물이 다 졸았다…"; color = new Color(1f, .6f, .45f, 1f); }
            else if (now < round.activeAt) text = "다 같이 밧줄을 당겨!";
            else if (round.countdownAt < 0d) text = "물이 졸고 있어…";
            else if (now < round.countdownAt) text = round.attempts > 0 ? "누가 늦었어! 다시!" : "다 같이 밧줄을 당겨!";
            else if (now < round.GoAt) text = "\"당겨!\"에 폰을 내 쪽으로 홱!";
            else { text = "지금! 폰을 내 쪽으로 홱! (" + done + "/" + total + ")"; color = new Color(.55f, .88f, 1f, 1f); pop = Pop(now - round.GoAt); }
            if (round.resolution == LabJigwiABResolution.None && round.attempts > 0 && now < round.countdownAt) pop = Pop(now - (round.countdownAt - LabJigwiABTimeline.BucketRetrySeconds));
            Show(banner, true);
            banner.text = text; banner.color = color;
            banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = bannerAnchor;
            banner.rectTransform.anchoredPosition = Vector2.zero; banner.rectTransform.sizeDelta = new Vector2(1000f, 100f);
            banner.rectTransform.localScale = Vector3.one * (1f + .25f * pop);
        }

        private static float Pop(double since) => since < 0d ? 0f : Mathf.Exp(-(float)since * 7f);

        private void DrawRope(LabJigwiABRound round, double now, bool pulled)
        {
            float r = handleRadius, t = Time.unscaledTime;
            float yank = pulledAt >= 0f ? Mathf.Exp(-(t - pulledAt) * 6f) : 0f;
            bool counting = round.countdownAt >= 0d && now >= round.countdownAt && now < round.GoAt;
            bool window = round.countdownAt >= 0d && now >= round.GoAt;
            Vector2 at = handleAt + new Vector2(0f, pulled ? -r * (.5f + .8f * yank) : 0f);
            float jiggle = earlyAt >= 0f ? Mathf.Exp(-(t - earlyAt) * 5f) * Mathf.Sin(t * 40f) * r * .4f : 0f;
            at.x += jiggle;
            // Taut while the bucket is up (counting/pulling) or once I pulled; slack and swaying otherwise.
            bool taut = pulled || counting || window;
            Vector2 top = ropeTop + new Vector2(taut ? 0f : Mathf.Sin(t * 1.7f) * r * .6f, 0f);
            Vector2 mid = (at + top) * .5f; float len = Vector2.Distance(at, top);
            float angle = Mathf.Atan2(top.y - at.y, top.x - at.x) * Mathf.Rad2Deg - 90f;
            float shiver = counting ? Mathf.Sin(t * 60f) * 1.5f : 0f;
            Show(ropeShadow, true); Show(rope, true); Show(bar, true);
            Place(ropeShadow.rectTransform, mid + new Vector2(shiver, 0f), new Vector2(r * (taut ? .42f : .34f), len), angle);
            Place(rope.rectTransform, mid + new Vector2(shiver, 0f), new Vector2(r * (taut ? .26f : .2f), len), angle);
            ropeShadow.color = new Color(.12f, .07f, .03f, taut ? .95f : .6f);
            rope.color = pulled ? pulledColor : new Color(ropeColor.r, ropeColor.g, ropeColor.b, taut ? 1f : .75f);
            Place(bar.rectTransform, at, new Vector2(r * 2.8f, r * 1.05f), 0f);
            bar.color = pulled ? Color.Lerp(woodColor, pulledColor, .55f) : woodColor;
            // "당겨!" and I have not pulled: a little phone rocking its top edge toward me, beside the rope end.
            bool tiltHint = window && !pulled && now <= round.GoAt + round.PullSeconds;
            Show(phone, tiltHint);
            if (tiltHint)
            {
                float rock = Mathf.Repeat(t * 2.4f, 1f), lean = rock < .35f ? rock / .35f : Mathf.Max(0f, 1f - (rock - .35f) / .4f);
                Place(phone.rectTransform, handleAt + new Vector2(r * 3.2f, -r * .6f), new Vector2(r * 1.3f, r * 2.3f), 0f);
                phone.rectTransform.localScale = new Vector3(1f + .25f * lean, 1f - .45f * lean, 1f);
                phone.color = Color.white;
            }
            bool tooEarly = earlyAt >= 0f && t - earlyAt < .7f;
            Show(early, tooEarly);
            if (tooEarly)
            {
                Place(early.rectTransform, handleAt + new Vector2(-r * 3.2f, r * .2f), new Vector2(r * 4f, r * 1.4f), 0f);
                early.color = new Color(1f, .75f, .4f, 1f - Mathf.Clamp01((t - earlyAt - .4f) / .3f));
                early.rectTransform.localScale = Vector3.one * (1f + .3f * Mathf.Exp(-(t - earlyAt) * 8f));
            }
        }

        // ---------- input ----------
        /// <summary>폰 윗부분이 내 쪽으로 홱 젖혀졌는지(최근 tiltWindowSeconds 안에 tiltDegrees 이상 일어섬), 또는 K.</summary>
        private bool ReadPull()
        {
            if ((Application.isEditor || Debug.isDebugBuild) && Keyboard.current != null && Keyboard.current.kKey.isPressed)
            {
                keyHeld += Time.unscaledDeltaTime;
                if (keyHeld >= keyHoldSeconds) return true;
            }
            else keyHeld = 0f;
            if (!TryPitch(out float pitch)) return false;
            float now = Time.unscaledTime;
            pitches[pitchHead] = pitch; pitchTimes[pitchHead] = now;
            pitchHead = (pitchHead + 1) % pitches.Length; pitchCount = Math.Min(pitches.Length, pitchCount + 1);
            float lowest = pitch;
            for (int i = 0; i < pitchCount; i++)
            {
                int k = (pitchHead - 1 - i + pitches.Length) % pitches.Length;
                if (now - pitchTimes[k] <= tiltWindowSeconds) lowest = Mathf.Min(lowest, pitches[k]);
            }
            return pitch - lowest >= tiltDegrees;
        }

        /// <summary>폰이 바닥에서 얼마나 일어섰는지(도): 화면이 하늘을 보고 누우면 0, 나를 보고 서면 90. 윗부분을 내 쪽으로 젖히면 커진다.</summary>
        private static bool TryPitch(out float pitch)
        {
            pitch = 0f;
            Vector3 g;
            GravitySensor gravity = GravitySensor.current;
            Accelerometer accel = Accelerometer.current;
            if (gravity != null)
            {
                if (!gravity.enabled) InputSystem.EnableDevice(gravity);
                g = gravity.gravity.ReadValue();
            }
            else if (accel != null)
            {
                if (!accel.enabled) InputSystem.EnableDevice(accel);
                g = accel.acceleration.ReadValue();
            }
            else return false;
            if (g.sqrMagnitude < .01f) return false;
            pitch = Mathf.Atan2(-g.y, -g.z) * Mathf.Rad2Deg;
            return true;
        }

        // ---------- layout ----------
        private void Hide()
        {
            Show(banner, false); Show(early, false); HideRope();
            earlyAt = -1f;
        }

        private void HideRope()
        {
            foreach (Graphic part in new Graphic[] { ropeShadow, rope, bar, phone, early }) Show(part, false);
        }

        private static void Show(Graphic part, bool on) { if (part != null && part.gameObject.activeSelf != on) part.gameObject.SetActive(on); }

        private static void Place(RectTransform rect, Vector2 at, Vector2 size, float degrees)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = at; rect.sizeDelta = size; rect.localEulerAngles = new Vector3(0f, 0f, degrees);
        }

        private bool TryPlace(LabOrbBoard board)
        {
            if (!board.TryGetScreenSpot(handleSpot, out Vector2 screen, out float pixels)) return false;
            if (!TryLocal(screen, out handleAt) || !TryLocal(screen + Vector2.right * pixels, out Vector2 edge)) return false;
            handleRadius = Mathf.Max(10f, Vector2.Distance(handleAt, edge));
            // The rope goes up and away over the board's far edge, toward the bucket above the boss.
            ropeTop = handleAt + Vector2.up * handleRadius * 7f;
            return true;
        }

        /// <summary>화면 점 → 이 화면(root) 가운데 원점 좌표.</summary>
        private bool TryLocal(Vector2 screen, out Vector2 local)
        {
            local = default;
            if (root == null) return false;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, eye, out local)) return false;
            local -= (new Vector2(.5f, .5f) - root.pivot) * root.rect.size;
            return true;
        }

        private Text NewText(string name, int size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer; go.transform.SetParent(root, false);
            var text = go.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(.1f, .06f, .02f, .9f); outline.effectDistance = new Vector2(4f, -4f);
            return text;
        }

        private Image NewImage(string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer; go.transform.SetParent(root, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        // ---------- procedural sprites ----------
        private static Sprite Pixel() => pixel != null ? pixel : pixel = Build("Jigwi bucket rope", 4, 4, (u, v) => Color.white);

        /// <summary>A wooden toggle: a fat rounded bar with dark ink outline, grain lines and two rope wraps.</summary>
        private static Sprite Bar() => barSprite != null ? barSprite : barSprite = Build("Jigwi bucket handle", 192, 72, (u, v) =>
        {
            float x = u * 192f, y = v * 72f;
            float d = RoundBox(x, y, 6f, 10f, 186f, 62f, 26f);
            if (d > 0f) return Color.clear;
            if (d > -6f) return new Color(.12f, .07f, .03f, 1f);
            bool wrap = Mathf.Abs(x - 60f) < 9f || Mathf.Abs(x - 132f) < 9f;
            if (wrap) return Mathf.Repeat(y + x * .6f, 10f) < 5f ? new Color(.93f, .83f, .62f, 1f) : new Color(.78f, .66f, .45f, 1f);
            float grain = .9f + .1f * Mathf.Sin(y * .55f + Mathf.Sin(x * .05f) * 3f);
            float light = y > 44f ? 1.12f : 1f;
            return new Color(grain * light, grain * light, grain * light, 1f);
        });

        /// <summary>A small phone outline with a lit screen.</summary>
        private static Sprite Phone() => phoneSprite != null ? phoneSprite : phoneSprite = Build("Jigwi bucket phone", 64, 112, (u, v) =>
        {
            float x = u * 64f, y = v * 112f;
            float d = RoundBox(x, y, 3f, 3f, 61f, 109f, 12f);
            if (d > 0f) return Color.clear;
            if (d > -5f) return new Color(.5f, .87f, 1f, 1f);
            if (RoundBox(x, y, 10f, 12f, 54f, 100f, 4f) < 0f) return new Color(.5f, .87f, 1f, .45f);
            return new Color(.08f, .06f, .05f, 1f);
        });

        private static float RoundBox(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            float cx = Mathf.Clamp(x, x0 + r, x1 - r), cy = Mathf.Clamp(y, y0 + r, y1 - r);
            return Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - r;
        }

        private static Sprite Build(string name, int w, int h, Func<float, float, Color> paint)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = name + " (#551 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = paint((x + .5f) / w, (y + .5f) / h);
            texture.SetPixels(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(.5f, .5f), Mathf.Max(w, h));
            sprite.name = texture.name; sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
