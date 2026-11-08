using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// #551 지귀 B 나무 물통 (개별, 여우구슬처럼 따로따로, 2D).
    /// - 내 차례(seat.readyAt)가 되면 박 바로 위에 🪣 내 양동이가 끈에 매달려 내려온다. 쏟을 시간은 BucketSeatSeconds(8초),
    ///   양동이 아래 물 막대가 줄어든다. 못 쏟으면 양동이가 떨어지고 **지귀 김이 조합판을 뒤덮는다** — 판을 문질러 닦아야(남은 김이
    ///   steamClearLeft 아래) 다시 구슬을 쓸 수 있다(그동안 판 입력 막힘). 폰을 좌우로 기울이면 양동이가 그만큼 같은 쪽으로 기운다.
    /// - 일정 각도(sloshDegrees)를 넘겨 꺾을 때마다 물이 "출렁"(방향을 바꿔 꺾어야 다음 출렁). 좌→우→좌 3번 출렁이면 마지막으로
    ///   꺾은 쪽으로 양동이가 뒤집혀 박에 물을 쏟는다(Host `LabModel.TryJigwiBPull`이 그 사람 쪽에 큰 물 칠). 멈춰 있으면 출렁이 가라앉는다.
    /// - 햅틱이 손맛: 기울일 때 각도마다 약한 톡, 출렁 1·2·3번 점점 세게, 쏟을 때 길고 세게.
    /// - Editor/DEV 빌드: A/D 또는 ←/→를 누르고 있으면 그쪽으로 기운다(섬광 흔들기의 K처럼 키보드 대체).
    /// - 판 가운데 📱 그림이 좌우로 기울며 할 일을 보여 주고, 박 아래 한 줄 안내.
    /// </summary>
    public sealed class LabJigwiBucketHandleView : MonoBehaviour
    {
        [Tooltip("이 각도(도)를 넘겨 꺾어야 출렁 한 번.")]
        [SerializeField, Range(5f, 60f)] private float sloshDegrees = 20f;
        [Tooltip("출렁 몇 번에 쏟는지.")]
        [SerializeField, Range(1, 6)] private int sloshesToPour = 3;
        [Tooltip("이 시간(초) 동안 다음 출렁이 없으면 출렁이 가라앉아 처음부터.")]
        [SerializeField, Range(.5f, 6f)] private float sloshResetSeconds = 2.5f;
        [Tooltip("Editor/DEV 빌드: A/D, ←/→를 누르고 있을 때의 기울기(도).")]
        [SerializeField, Range(10f, 60f)] private float keyTiltDegrees = 35f;
        [Tooltip("양동이가 폰 기울기를 따라가는 비율(폰 1도 = 양동이 몇 도).")]
        [SerializeField, Range(.3f, 2f)] private float bucketFollow = 1f;
        [Tooltip("안내 한 줄의 화면 자리(박 아래).")]
        [SerializeField] private Vector2 bannerAnchor = new Vector2(.5f, .6f);
        [SerializeField, Range(24, 96)] private int bannerMaxFontSize = 54;
        [Tooltip("박(물을 맞는 곳)의 화면 높이(0~1).")]
        [SerializeField, Range(0f, 1f)] private float gourdAnchorY = .7f;
        [Tooltip("양동이 끈이 묶인 화면 자리(0~1)와 양동이 폭(화면 폭 비율). 박 바로 위.")]
        [SerializeField] private Vector2 bucketHang = new Vector2(.5f, .85f);
        [SerializeField, Range(.1f, .4f)] private float bucketWidth = .2f;
        [Tooltip("좌우로 기울이라는 📱 그림의 화면 자리와 크기.")]
        [SerializeField] private Vector2 cueAnchor = new Vector2(.5f, .42f);
        [SerializeField, Range(60f, 400f)] private float cueSize = 150f;
        [SerializeField, Range(.5f, 4f)] private float resultSeconds = 1.8f;
        [SerializeField, Range(0f, 1f)] private float tiltHapticIntensity = .25f;
        [SerializeField, Range(0f, 1f)] private float sloshHapticIntensity = .9f;
        [SerializeField, Range(0f, 1f)] private float pourHapticIntensity = 1f;

        private LabNetwork source;
        private Font font;
        private RectTransform root, cue;
        private Text banner, cueLabel;
        private Image flash, sheet, cuePhone, pail, hangRope;
        private readonly Image[] pips = new Image[6];
        private readonly Image[] drops = new Image[48];
        private readonly Vector2[] dropAt = new Vector2[48], dropVelocity = new Vector2[48];
        private readonly float[] dropBorn = new float[48];
        private static Sprite pixel, discSprite, sheetSprite, phoneEmojiSprite, pailSprite, vignetteSprite;
        private string shownRound;
        private float roll, keyRoll, bucketAngle, dropIn, flashAt = -1f, pourAt = -1f, lastSloshAt = -1f, sloshAt = -1f;
        private Color flashColor;
        private int sloshes, side, pourSide, tiltStep, nextDrop;
        private bool poured, splashed;
        // #551 failure: Jigwi's steam fogs my board until I rub it clear.
        [Tooltip("김이 이만큼(0~1) 남을 때까지 문지르면 닦인 것.")]
        [SerializeField, Range(.05f, .8f)] private float steamClearLeft = .35f;
        [Tooltip("문지르는 손가락 굵기(판 폭 비율).")]
        [SerializeField, Range(.03f, .3f)] private float steamBrush = .11f;
        private const int SteamW = 48, SteamH = 64;
        private RawImage steam;
        private Texture2D steamTexture;
        private readonly float[] steamAlpha = new float[SteamW * SteamH];
        private Color32[] steamPixels;
        private Text steamLabel;
        private string steamRound;
        private bool steamOn, steamBlocking, rubbing;
        private float steamAt, steamClearedAt = -1f, steamLeft, lastTickLeft;
        private Vector2 lastRub;
        private Image waterBack, waterFill;

        /// <summary>테스트용: 지금 안내 한 줄 ("" = 없음) / 양동이가 보이는지 / 지금까지 출렁 수.</summary>
        public string Banner => banner != null && banner.gameObject.activeSelf ? banner.text : string.Empty;
        public bool HandleVisible => pail != null && pail.gameObject.activeSelf;
        public int Sloshes => sloshes;

        public void Bind(LabNetwork network, Font labelFont)
        {
            source = network;
            if (labelFont != null) font = labelFont;
            Ensure();
            if (font != null) { banner.font = font; cueLabel.font = font; steamLabel.font = font; }
        }

        private void Ensure()
        {
            if (root != null) return;
            root = transform as RectTransform;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hangRope = NewImage("Jigwi Bucket Hang Rope", Pixel(), root);
            var ink = hangRope.gameObject.AddComponent<Outline>();
            ink.effectColor = new Color(.17f, .14f, .12f, 1f); ink.effectDistance = new Vector2(2f, -2f);
            sheet = NewImage("Jigwi Bucket Downpour", Sheet(), root);
            pail = NewImage("Jigwi Bucket Pail", Pail(), root);
            for (int i = 0; i < pips.Length; i++) pips[i] = NewImage("Jigwi Bucket Slosh " + i, Disc(), root);
            for (int i = 0; i < drops.Length; i++) drops[i] = NewImage("Jigwi Bucket Drop " + i, Disc(), root);
            banner = NewText("Jigwi Bucket Banner", 54, root);
            banner.resizeTextForBestFit = true; banner.resizeTextMinSize = 24;
            banner.horizontalOverflow = HorizontalWrapMode.Wrap; banner.verticalOverflow = VerticalWrapMode.Truncate;
            cue = new GameObject("Jigwi Bucket Tilt Cue", typeof(RectTransform)).GetComponent<RectTransform>();
            cue.gameObject.layer = gameObject.layer; cue.SetParent(root, false); cue.sizeDelta = Vector2.zero;
            cuePhone = NewImage("Phone", PhoneEmoji(), cue);
            cueLabel = NewText("Label", 44, cue);
            waterBack = NewImage("Jigwi Bucket Water Back", Pixel(), root);
            waterFill = NewImage("Jigwi Bucket Water", Pixel(), root);
            var steamObject = new GameObject("Jigwi Bucket Steam", typeof(RectTransform));
            steamObject.layer = gameObject.layer; steamObject.transform.SetParent(root, false);
            steam = steamObject.AddComponent<RawImage>(); steam.raycastTarget = false;
            steamLabel = NewText("Jigwi Bucket Steam Label", 56, root); steamLabel.text = "문질러 닦아라!";
            flash = NewImage("Jigwi Bucket Flash", Vignette(), root);
            flash.rectTransform.anchorMin = Vector2.zero; flash.rectTransform.anchorMax = Vector2.one;
            flash.rectTransform.offsetMin = flash.rectTransform.offsetMax = Vector2.zero;
            Hide();
        }

        private void LateUpdate()
        {
            LabSnapshot snapshot = source != null ? source.Snapshot : null;
            double now = source != null ? source.JigwiABPresentationTime : 0d;
            UpdateSteam(snapshot);
            LabJigwiABRound round = Showing(snapshot, now);
            if (round == null) { Hide(); return; }
            Ensure();
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            LabJigwiABPlayerFire me = null;
            foreach (var seat in round.players ?? Array.Empty<LabJigwiABPlayerFire>()) if (seat != null && seat.playerId == source.LocalPlayerId) me = seat;
            if (me == null || me.readyAt < 0d) { Hide(); return; } // a bucket B I am not in (or not mine yet)
            double arrive = me.readyAt, deadline = arrive + LabJigwiABTimeline.BucketSeatSeconds;
            if (shownRound != round.id)
            {
                shownRound = round.id; bucketAngle = 0f; dropIn = 1f; sloshes = 0; side = 0; poured = false; splashed = false;
                pourAt = lastSloshAt = sloshAt = -1f; tiltStep = 0;
            }
            if (!poured && (me.launchedAt >= 0d || source.JigwiBucketPulledThisCount(round)))
            { poured = true; if (pourAt < 0f) { pourAt = t; pourSide = side != 0 ? side : 1; } }
            bool failed = !poured && now > deadline;
            // My own bucket's time: from its arrival to its deadline (others' buckets come and go on their own screens).
            bool live = !poured && !failed && now >= arrive && round.resolution == LabJigwiABResolution.None;
            bool visible = now >= arrive - .7d && (poured ? t - pourAt < 1.9f : now < deadline + 1.2d);
            if (failed) StartSteam(round.id);
            if (!visible) { Hide(); return; }
            ReadRoll(dt);
            if (live) Slosh(t);
            DrawBanner(now, arrive, live);
            DrawBucket(now, arrive, deadline, failed, t, dt, live);
            DrawCue(live, t);
            DrawWater(t, dt);
        }

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

        private static bool MinePoured(LabJigwiABRound round, ulong me)
        {
            foreach (var seat in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                if (seat != null && seat.playerId == me && seat.launchedAt >= 0d) return true;
            return false;
        }

        // ---------- input ----------
        /// <summary>폰 좌우 기울기(도): 오른쪽이 내려가면 +, 왼쪽이 내려가면 −. Editor/DEV는 A/D, ←/→.</summary>
        private void ReadRoll(float dt)
        {
            float keys = 0f;
            if ((Application.isEditor || Debug.isDebugBuild) && Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) keys -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) keys += 1f;
            }
            keyRoll = Mathf.MoveTowards(keyRoll, keys * keyTiltDegrees, dt * keyTiltDegrees * 6f);
            float sensor = TryRoll(out float value) ? value : 0f;
            roll = Mathf.Abs(keyRoll) > .01f ? keyRoll : sensor;
        }

        private static bool TryRoll(out float degrees)
        {
            degrees = 0f;
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
            degrees = Mathf.Atan2(g.x, Mathf.Sqrt(g.y * g.y + g.z * g.z)) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>출렁 세기: sloshDegrees를 넘겨 꺾을 때(처음, 또는 반대쪽으로 바꿔서)마다 한 번. 다 차면 그쪽으로 쏟는다.</summary>
        private void Slosh(float t)
        {
            // A soft tick every 10 degrees of tilt: the water getting heavy in the hand.
            int step = Mathf.Min(6, Mathf.FloorToInt(Mathf.Abs(roll) / 10f));
            if (step > tiltStep && source != null)
                LabHaptics.PlayDirect(source.config, LabHapticStyle.Light, tiltHapticIntensity * (.6f + .2f * step));
            tiltStep = step;
            if (sloshes > 0 && lastSloshAt >= 0f && t - lastSloshAt > sloshResetSeconds) { sloshes = 0; side = 0; }
            int lean = roll >= sloshDegrees ? 1 : roll <= -sloshDegrees ? -1 : 0;
            if (lean == 0 || lean == side) return;
            side = lean; sloshes++; lastSloshAt = sloshAt = t;
            SpillDrops(4 + 3 * sloshes);
            if (sloshes >= sloshesToPour)
            {
                if (source.RequestJigwiBucketPull())
                {
                    poured = true; pourAt = t; pourSide = side;
                    LabHaptics.PlayDirect(source.config, LabHapticStyle.Heavy, pourHapticIntensity, .35f, pourHapticIntensity, .9f);
                }
                return;
            }
            float strength = Mathf.Lerp(.45f, 1f, (sloshes - 1f) / Mathf.Max(1f, sloshesToPour - 1f)) * sloshHapticIntensity;
            LabHaptics.PlayDirect(source.config, LabHapticStyle.Medium, strength, .06f + .04f * sloshes, strength, .6f);
        }

        // ---------- drawing ----------
        private void DrawBanner(double now, double arrive, bool live)
        {
            string text = now < arrive ? "양동이가 온다!" : live ? "폰을 좌우로 흔들어 양동이를 쏟아라!" : null;
            Show(banner, text != null);
            if (text == null) return;
            banner.text = text; banner.color = new Color(1f, .93f, .7f, 1f);
            banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = bannerAnchor;
            banner.resizeTextMaxSize = bannerMaxFontSize;
            banner.rectTransform.anchoredPosition = Vector2.zero;
            banner.rectTransform.sizeDelta = new Vector2(root.rect.width * .86f, bannerMaxFontSize * 1.6f);
        }

        private float BucketSize => root.rect.width * bucketWidth;
        private Vector2 HangPoint => new Vector2((bucketHang.x - .5f) * root.rect.width, (bucketHang.y - .5f) * root.rect.height);
        private Vector2 bucketCenter; private float bucketDrawAngle;
        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>
        /// 양동이: 끈에 매달려 예고 동안 위에서 내려오고, 폰 기울기를 따라 같은 쪽으로 기운다(끈 묶인 곳이 축). 출렁마다 한 번 크게
        /// 흔들리고, 다 출렁이면 그쪽으로 뒤집혀 쏟는다. 물이 다 졸면 아래로 떨어진다.
        /// </summary>
        private void DrawBucket(double now, double arrive, double deadline, bool failed, float t, float dt, bool tilting)
        {
            float size = BucketSize, alpha = 1f;
            Vector2 hang = HangPoint;
            // It comes down from above over the 0.7 s before it arrives.
            dropIn = Mathf.Clamp01((float)((arrive - now) / .7d));
            hang.y += root.rect.height * .3f * dropIn * dropIn;
            float target;
            if (poured)
            {
                float since = pourAt >= 0f ? t - pourAt : 1f;
                target = -pourSide * 120f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(since / .25f));
                alpha = 1f - Mathf.Clamp01((since - 1.4f) / .4f);
                bucketAngle = target;
            }
            else if (failed)
            {
                // Out of time: the bucket drops onto my board (and the steam rises from it).
                float since = (float)(now - deadline);
                hang.y -= since * since * root.rect.height * 1.2f;
                alpha = 1f - Mathf.Clamp01((since - .5f) / .4f);
                bucketAngle = Mathf.Lerp(bucketAngle, 15f, Mathf.Clamp01(dt * 6f));
            }
            else
            {
                // Follow the phone (right side down → bucket tips right = clockwise), plus a kick at each slosh.
                target = tilting ? Mathf.Clamp(-roll * bucketFollow, -45f, 45f) : Mathf.Sin(t * 1.3f) * 3f;
                bucketAngle = Mathf.Lerp(bucketAngle, target, Mathf.Clamp01(dt * 10f));
            }
            float kick = sloshAt >= 0f && !poured ? Mathf.Exp(-(t - sloshAt) * 6f) * Mathf.Sin((t - sloshAt) * 25f) * 12f * -side : 0f;
            float angle = bucketAngle + kick;
            // It turns about its own middle; the rope follows the handle.
            Vector2 center = hang + new Vector2(0f, -size * .55f);
            Vector2 handle = center + Rotate(new Vector2(0f, size * .42f), angle);
            Show(pail, alpha > 0f);
            Place(pail.rectTransform, center, Vector2.one * size, angle);
            pail.color = new Color(1f, 1f, 1f, alpha);
            bucketCenter = center; bucketDrawAngle = angle;
            // The rope it hangs from, up to the top of the screen like the boss's own string.
            Vector2 top = new Vector2(hang.x, root.rect.height * .5f), rope = handle - top;
            Show(hangRope, alpha > 0f && !failed); // a falling bucket has let go of its rope
            Place(hangRope.rectTransform, (top + handle) * .5f, new Vector2(6f, Mathf.Max(1f, rope.magnitude)),
                Mathf.Atan2(rope.y, rope.x) * Mathf.Rad2Deg + 90f);
            hangRope.color = new Color(.86f, .72f, .5f, alpha);
            // Water left (my time) under the bucket.
            float left = tilting ? Mathf.Clamp01((float)((deadline - now) / LabJigwiABTimeline.BucketSeatSeconds)) : 0f;
            Show(waterBack, left > 0f); Show(waterFill, left > 0f);
            if (left > 0f)
            {
                float width = size * 1.1f, height = size * .08f;
                Vector2 at = hang + new Vector2(0f, -size * 1.45f);
                Place(waterBack.rectTransform, at, new Vector2(width + 6f, height + 6f), 0f);
                waterBack.color = new Color(.1f, .06f, .02f, .8f);
                Place(waterFill.rectTransform, at + new Vector2(-width * (1f - left) * .5f, 0f), new Vector2(width * left, height), 0f);
                waterFill.color = Color.Lerp(new Color(1f, .45f, .3f, 1f), new Color(.45f, .8f, 1f, 1f), left);
            }
            // Slosh count under the bucket: ● filled per slosh.
            for (int i = 0; i < pips.Length; i++)
            {
                bool on = i < sloshesToPour && tilting && !poured;
                Show(pips[i], on);
                if (!on) continue;
                float x = (i - (sloshesToPour - 1) * .5f) * size * .22f;
                bool lit = i < sloshes;
                float pop = lit && i == sloshes - 1 && sloshAt >= 0f ? Mathf.Exp(-(t - sloshAt) * 8f) : 0f;
                Place(pips[i].rectTransform, hang + new Vector2(x, -size * 1.25f), Vector2.one * size * (.15f + .1f * pop), 0f);
                pips[i].color = lit ? new Color(.45f, .78f, 1f, 1f) : new Color(1f, 1f, 1f, .45f);
            }
        }

        /// <summary>판 가운데 📱: 좌우로 기울며 할 일을 보여 준다. 출렁 수가 아래에.</summary>
        private void DrawCue(bool show, float t)
        {
            if (cue.gameObject.activeSelf != show) cue.gameObject.SetActive(show);
            if (!show) return;
            float size = cueSize;
            cue.anchorMin = cue.anchorMax = cueAnchor; cue.anchoredPosition = Vector2.zero;
            // Left, right, left… with a pause at each side (like the rhythm it asks for).
            float u = Mathf.Repeat(t / 1.4f, 1f);
            float swing = Mathf.Sin(u * Mathf.PI * 2f);
            float lean = Mathf.Sign(swing) * Mathf.Pow(Mathf.Abs(swing), .5f) * 28f;
            Place(cuePhone.rectTransform, new Vector2(0f, size * .15f), new Vector2(size * .58f, size), -lean);
            cuePhone.color = new Color(1f, 1f, 1f, .9f);
            cueLabel.text = sloshes == 0 ? "좌우로 기울여!" : "출렁 " + sloshes + "/" + sloshesToPour;
            cueLabel.fontSize = Mathf.RoundToInt(size * .26f);
            cueLabel.color = sloshes == 0 ? new Color(1f, .93f, .7f, 1f) : new Color(.55f, .88f, 1f, 1f);
            Place(cueLabel.rectTransform, new Vector2(0f, -size * .55f), new Vector2(size * 3f, size * .4f), 0f);
        }

        private void Flash(Color color) { flashAt = Time.unscaledTime; flashColor = color; }

        /// <summary>A few drops slop over the rim toward the side the bucket leans.</summary>
        private void SpillDrops(int count)
        {
            Vector2 mouth = bucketCenter + Rotate(new Vector2(0f, BucketSize * .24f), bucketDrawAngle);
            for (int k = 0; k < count; k++)
            {
                int i = nextDrop; nextDrop = (nextDrop + 1) % drops.Length;
                dropAt[i] = mouth + new Vector2(UnityEngine.Random.Range(-.3f, .3f) * BucketSize, 0f);
                dropVelocity[i] = new Vector2(side * UnityEngine.Random.Range(.15f, .4f), UnityEngine.Random.Range(.1f, .35f)) * root.rect.height;
                dropBorn[i] = Time.unscaledTime;
            }
        }

        /// <summary>
        /// 쏟을 때: 뒤집힌 양동이 입구에서 물줄기가 박으로 떨어지고 물방울이 박 근처로 튄다. 화면 가장자리가 하늘색으로 번쩍인다.
        /// </summary>
        private void DrawWater(float t, float dt)
        {
            float sp = poured && pourAt >= 0f ? t - pourAt : 99f;
            Vector2 boss = new Vector2(0f, (gourdAnchorY - .5f) * root.rect.height);
            if (poured && !splashed && sp >= .25f)
            {
                splashed = true;
                Flash(new Color(.6f, .88f, 1f, .6f));
                for (int k = 0; k < 24; k++)
                {
                    int i = nextDrop; nextDrop = (nextDrop + 1) % drops.Length;
                    float a = UnityEngine.Random.Range(.12f, .88f) * Mathf.PI;
                    dropAt[i] = boss + new Vector2(UnityEngine.Random.Range(-1f, 1f) * root.rect.width * .08f, root.rect.height * .03f);
                    dropVelocity[i] = new Vector2(Mathf.Cos(a) * 1.3f, Mathf.Sin(a)) * UnityEngine.Random.Range(.12f, .3f) * root.rect.height;
                    dropBorn[i] = t;
                }
            }
            Vector2 mouth = bucketCenter + Rotate(new Vector2(0f, BucketSize * .24f), bucketDrawAngle);
            bool sheetOn = sp >= .12f && sp < .9f;
            Show(sheet, sheetOn);
            if (sheetOn)
            {
                float pour = Mathf.Clamp01((sp - .12f) / .15f);
                float top = mouth.y, bottom = Mathf.Lerp(top, boss.y + root.rect.height * .03f, pour);
                Place(sheet.rectTransform, new Vector2(Mathf.Lerp(mouth.x, boss.x, .4f), (top + bottom) * .5f),
                    new Vector2(BucketSize * .75f, Mathf.Max(20f, top - bottom + 30f)), 0f);
                sheet.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((sp - .55f) / .35f));
            }
            for (int i = 0; i < drops.Length; i++)
            {
                float age = dropBorn[i] > 0f ? t - dropBorn[i] : 99f;
                bool on = age < .9f;
                Show(drops[i], on);
                if (!on) continue;
                dropVelocity[i].y -= root.rect.height * 1.6f * dt;
                dropAt[i] += dropVelocity[i] * dt;
                Place(drops[i].rectTransform, dropAt[i], Vector2.one * (10f + (i % 4) * 5f), 0f);
                drops[i].color = new Color(.6f, .85f, 1f, 1f - Mathf.Clamp01((age - .5f) / .4f));
            }
            float f = flashAt >= 0f ? 1f - Mathf.Clamp01((t - flashAt) / .45f) : 0f;
            Show(flash, f > 0f);
            if (f > 0f) flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * f);
        }

        // ---------- failure: steam over my board ----------
        /// <summary>내 양동이를 제시간에 못 쏟았다: 지귀 김이 조합판을 뒤덮는다(한 회차에 한 번). 판 입력이 막힌다.</summary>
        private void StartSteam(string roundId)
        {
            if (steamRound == roundId) return;
            steamRound = roundId; steamOn = true; steamAt = Time.unscaledTime; steamClearedAt = -1f; rubbing = false;
            if (steamTexture == null)
            {
                steamTexture = new Texture2D(SteamW, SteamH, TextureFormat.RGBA32, false)
                { name = "Jigwi steam (#551 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                steamPixels = new Color32[SteamW * SteamH];
                steam.texture = steamTexture;
            }
            // Thick, uneven fog: pale grey with a warm (Jigwi) tint, clumpy so rubbing shows streaks.
            for (int y = 0; y < SteamH; y++)
                for (int x = 0; x < SteamW; x++)
                {
                    float n = .5f + .25f * Mathf.Sin(x * .41f + y * .17f) + .25f * Mathf.Sin(x * .13f - y * .37f + 1.7f);
                    steamAlpha[y * SteamW + x] = Mathf.Lerp(.82f, 1f, n);
                }
            WriteSteam();
            steamLeft = lastTickLeft = 1f;
            if (source != null && source.board != null) { source.board.SetUiInputBlocked(true); steamBlocking = true; }
            if (source != null) LabHaptics.PlayDirect(source.config, LabHapticStyle.Heavy, .8f, .3f, .5f, .3f);
        }

        private void WriteSteam()
        {
            for (int i = 0; i < steamAlpha.Length; i++)
            {
                int x = i % SteamW, y = i / SteamW;
                float warm = .5f + .5f * Mathf.Sin(x * .3f + y * .2f);
                byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(steamAlpha[i]) * .93f);
                steamPixels[i] = new Color32((byte)(222 + 18 * warm), (byte)(214 + 12 * warm), 205, a);
            }
            steamTexture.SetPixels32(steamPixels); steamTexture.Apply(false, false);
        }

        /// <summary>김 덮개를 판 위에 맞추고, 문지른 자리를 지우고, 충분히 닦이면 걷어 내고 판을 다시 연다.</summary>
        private void UpdateSteam(LabSnapshot snapshot)
        {
            if (!steamOn) return;
            bool battle = snapshot != null && snapshot.phase == LabPhase.Playing;
            float t = Time.unscaledTime;
            if (!battle) { EndSteam(); return; }
            Ensure();
            Camera view = source.board != null ? source.board.ViewCamera : null;
            Rect board = view != null ? view.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height * .46f);
            PlaceScreenRect(steam.rectTransform, board);
            if (!steam.gameObject.activeSelf) steam.gameObject.SetActive(true);
            float fadeIn = Mathf.Clamp01((t - steamAt) / .35f);
            float fadeOut = steamClearedAt >= 0f ? 1f - Mathf.Clamp01((t - steamClearedAt) / .4f) : 1f;
            steam.color = new Color(1f, 1f, 1f, fadeIn * fadeOut);
            if (steamClearedAt >= 0f)
            {
                if (fadeOut <= 0f) EndSteam();
                Show(steamLabel, false);
                return;
            }
            // Rub: every finger (or mouse) drag over the board wipes a stroke out of the fog.
            if (TryPointer(out Vector2 screen) && board.Contains(screen))
            {
                Vector2 uv = new Vector2((screen.x - board.xMin) / board.width, (screen.y - board.yMin) / board.height);
                if (rubbing) Wipe(lastRub, uv, board); else Wipe(uv, uv, board);
                lastRub = uv; rubbing = true;
            }
            else rubbing = false;
            Show(steamLabel, true);
            PlaceScreenRect(steamLabel.rectTransform, new Rect(board.xMin, board.center.y - board.height * .08f, board.width, board.height * .16f));
            steamLabel.color = new Color(.35f, .22f, .15f, Mathf.Clamp01(steamLeft * 1.4f) * fadeIn);
            steamLabel.rectTransform.localScale = Vector3.one * (1f + .06f * Mathf.Sin(t * 6f));
        }

        private void Wipe(Vector2 from, Vector2 to, Rect board)
        {
            float radius = steamBrush * SteamW; // in texels (the brush is a share of the board width)
            float aspect = board.height / Mathf.Max(1f, board.width) * SteamW / SteamH; // keep the brush round on screen
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) * SteamW / (radius * .5f)));
            bool changed = false;
            for (int k = 0; k <= steps; k++)
            {
                Vector2 uv = Vector2.Lerp(from, to, k / (float)steps);
                float cx = uv.x * SteamW, cy = uv.y * SteamH;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius)), x1 = Mathf.Min(SteamW - 1, Mathf.CeilToInt(cx + radius));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius / aspect)), y1 = Mathf.Min(SteamH - 1, Mathf.CeilToInt(cy + radius / aspect));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = (x + .5f - cx) / radius, dy = (y + .5f - cy) * aspect / radius;
                        float d = dx * dx + dy * dy;
                        if (d > 1f) continue;
                        int i = y * SteamW + x;
                        float next = Mathf.Min(steamAlpha[i], steamAlpha[i] * (.25f + .75f * d));
                        if (next < steamAlpha[i] - .001f) { steamAlpha[i] = next; changed = true; }
                    }
            }
            if (!changed) return;
            WriteSteam();
            float sum = 0f; foreach (float a in steamAlpha) sum += a;
            steamLeft = sum / steamAlpha.Length;
            // A light tick for every bit wiped away, a firm one when it is clear.
            if (lastTickLeft - steamLeft >= .06f && source != null)
            { lastTickLeft = steamLeft; LabHaptics.PlayDirect(source.config, LabHapticStyle.Light, .35f); }
            if (steamLeft <= steamClearLeft)
            {
                steamClearedAt = Time.unscaledTime;
                if (source != null) LabHaptics.PlayDirect(source.config, LabHapticStyle.Medium, .7f);
                if (steamBlocking && source != null && source.board != null) source.board.SetUiInputBlocked(false);
                steamBlocking = false;
            }
        }

        private void EndSteam()
        {
            steamOn = false; rubbing = false;
            if (steam != null && steam.gameObject.activeSelf) steam.gameObject.SetActive(false);
            Show(steamLabel, false);
            if (steamBlocking && source != null && source.board != null) source.board.SetUiInputBlocked(false);
            steamBlocking = false;
        }

        private void OnDisable() => EndSteam();

        private static bool TryPointer(out Vector2 screen)
        {
            screen = default;
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && touch.press.isPressed) { screen = touch.position.ReadValue(); return true; }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) { screen = Mouse.current.position.ReadValue(); return true; }
            return false;
        }

        /// <summary>화면 픽셀 사각형을 root 기준 위치·크기로 옮긴다.</summary>
        private void PlaceScreenRect(RectTransform target, Rect pixels)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pixels.min, eye, out Vector2 low)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pixels.max, eye, out Vector2 high)) return;
            target.anchorMin = target.anchorMax = target.pivot = new Vector2(.5f, .5f);
            target.localPosition = new Vector3((low.x + high.x) * .5f, (low.y + high.y) * .5f, 0f);
            target.sizeDelta = new Vector2(Mathf.Abs(high.x - low.x), Mathf.Abs(high.y - low.y));
            target.localEulerAngles = Vector3.zero;
        }

        // ---------- layout ----------
        private void Hide()
        {
            if (root == null) return;
            foreach (Graphic part in new Graphic[] { banner, flash, sheet, pail, hangRope, waterBack, waterFill }) Show(part, false);
            foreach (Image pip in pips) Show(pip, false);
            foreach (Image drop in drops) Show(drop, false);
            for (int i = 0; i < dropBorn.Length; i++) dropBorn[i] = 0f;
            if (cue != null && cue.gameObject.activeSelf) cue.gameObject.SetActive(false);
        }

        private static void Show(Graphic part, bool on) { if (part != null && part.gameObject.activeSelf != on) part.gameObject.SetActive(on); }

        private static void Place(RectTransform rect, Vector2 at, Vector2 size, float degrees)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = at; rect.sizeDelta = size; rect.localEulerAngles = new Vector3(0f, 0f, degrees);
        }

        private Text NewText(string name, int size, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer; go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(.1f, .06f, .02f, .9f); outline.effectDistance = new Vector2(4f, -4f);
            return text;
        }

        private Image NewImage(string name, Sprite sprite, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer; go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        // ---------- procedural sprites ----------
        /// <summary>📱 phone emoji: dark rounded body with an ink outline, a sky screen with a shine, a notch and a home bar.</summary>
        private static Sprite PhoneEmoji() => phoneEmojiSprite != null ? phoneEmojiSprite : phoneEmojiSprite = Build("Jigwi bucket phone emoji", 116, 200, (u, v) =>
        {
            float x = u * 116f, y = v * 200f;
            float d = RoundBox(x, y, 4f, 4f, 112f, 196f, 22f);
            if (d > 0f) return Color.clear;
            if (d > -5f) return new Color(.17f, .14f, .12f, 1f);
            if (RoundBox(x, y, 42f, 176f, 74f, 186f, 5f) < 0f) return new Color(.1f, .1f, .12f, 1f); // notch
            if (RoundBox(x, y, 14f, 14f, 102f, 186f, 12f) < 0f)
            {
                if (RoundBox(x, y, 40f, 20f, 76f, 25f, 2.5f) < 0f) return new Color(.95f, .97f, 1f, 1f); // home bar
                float shine = x + (200f - y) * .5f;
                bool glare = shine > 30f && shine < 52f;
                Color sky = Color.Lerp(new Color(.4f, .74f, 1f, 1f), new Color(.75f, .9f, 1f, 1f), v);
                return glare ? Color.Lerp(sky, Color.white, .55f) : sky;
            }
            return new Color(.3f, .32f, .38f, 1f); // body
        });
        /// <summary>🪣 wooden bucket: a tapered tub (wide mouth on top) with two metal bands, water at the mouth and a rope handle.</summary>
        private static Sprite Pail() => pailSprite != null ? pailSprite : pailSprite = Build("Jigwi bucket pail", 160, 160, (u, v) =>
        {
            float x = u * 160f, y = v * 160f;
            // Rope handle: an arc above the mouth.
            float arc = Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(80f, 112f)) - 46f);
            bool handle = y > 118f && arc < 4f, handleInk = y > 116f && arc < 7f;
            // Tub: from y 18 (bottom, 36..124) to y 118 (mouth, 22..138).
            float k = Mathf.InverseLerp(18f, 118f, y), half = Mathf.Lerp(44f, 58f, k);
            float side = Mathf.Abs(x - 80f) - half;
            bool inTub = y >= 18f && y <= 118f && side < 0f;
            bool tubInk = y >= 12f && y <= 124f && side < 6f;
            bool mouth = Mathf.Pow((x - 80f) / 58f, 2f) + Mathf.Pow((y - 118f) / 12f, 2f) < 1f;
            bool mouthInk = Mathf.Pow((x - 80f) / 64f, 2f) + Mathf.Pow((y - 118f) / 17f, 2f) < 1f;
            if (mouth) return Mathf.Pow((x - 80f) / 50f, 2f) + Mathf.Pow((y - 117f) / 8f, 2f) < 1f ? new Color(.45f, .78f, 1f, 1f) : new Color(.62f, .41f, .22f, 1f);
            if (inTub)
            {
                bool band = Mathf.Abs(y - 40f) < 6f || Mathf.Abs(y - 96f) < 6f;
                if (band) return new Color(.35f, .36f, .4f, 1f);
                bool stave = Mathf.Repeat(x - 80f + half, 22f) < 2.5f;
                return stave ? new Color(.5f, .32f, .17f, 1f) : new Color(.74f, .52f, .3f, 1f);
            }
            if (handle) return new Color(.88f, .76f, .55f, 1f);
            if (tubInk || mouthInk || handleInk) return new Color(.17f, .14f, .12f, 1f);
            return Color.clear;
        });
        /// <summary>Screen-edge glow (clear in the middle) so a flash never hides the screen.</summary>
        private static Sprite Vignette() => vignetteSprite != null ? vignetteSprite : vignetteSprite = Build("Jigwi bucket vignette", 64, 128, (u, v) =>
        {
            float e = Mathf.Min(Mathf.Min(u, 1f - u) * 2f, Mathf.Min(v, 1f - v) * 4f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - e / .35f));
        });
        private static Sprite Disc() => discSprite != null ? discSprite : discSprite = Build("Jigwi bucket drop", 48, 48, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f));
            float edge = Mathf.Clamp01((.46f - d) * 40f);
            return Color.Lerp(new Color(.85f, .95f, 1f, edge), new Color(1f, 1f, 1f, edge), d < .2f && u < .5f && v > .5f ? 1f : 0f);
        });
        /// <summary>A falling sheet of water: bright streaks, solid at the front (bottom), thinning toward the top.</summary>
        private static Sprite Sheet() => sheetSprite != null ? sheetSprite : sheetSprite = Build("Jigwi bucket downpour", 128, 256, (u, v) =>
        {
            float side = Mathf.Clamp01((.5f - Mathf.Abs(u - .5f)) * 6f);
            float streak = .65f + .35f * Mathf.Sin(u * 70f + Mathf.Sin(u * 13f) * 3f);
            float a = side * Mathf.Lerp(1f, .25f, v) * streak;
            return new Color(Mathf.Lerp(.35f, .8f, streak), Mathf.Lerp(.65f, .92f, streak), 1f, a);
        });
        private static Sprite Pixel() => pixel != null ? pixel : pixel = Build("Jigwi bucket rope", 4, 4, (u, v) => Color.white);

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
