using System;
using System.Collections.Generic;
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
    /// - #595 연기(round.smoke): 양동이 없이 박이 달아오르다 seat.readyAt에 바로 실패 흐름(지귀 대사 → 연기가 화면을 덮음)으로 간다.
    ///   모든 자리가 같은 때에 덮이고, 각자 90% 문질러 걷어 낸다.
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
        [SerializeField] private Vector2 bucketHang = new Vector2(.5f, .93f);
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
        [SerializeField, Range(.05f, .8f)] private float steamClearLeft = .1f;
        [Tooltip("#696 연기를 이만큼(0~1) 걷어 내기 시작하면 Host에 검은 파편을 달라고 한다. 파편은 아직 연기 밑 판 구석구석에 깔려 있어, 닦는 동안 하나씩 드러난다.")]
        [SerializeField, Range(0f, .5f)] private float sootRevealWiped = .03f;
        [Tooltip("#696 화면 위/아래 가장자리에서 연기가 옅어지는 띠의 폭(화면 높이 비율). 손가락이 닿기 어려운 끝자락은 덜 닦아도 걷힌다.")]
        [SerializeField, Range(0f, .25f)] private float steamEdgeBand = .08f;
        [Tooltip("#696 가장자리 끝(맨 위/맨 아래)의 연기 진하기(0~1). 띠 안쪽으로 갈수록 1까지 진해진다.")]
        [SerializeField, Range(0f, 1f)] private float steamEdgeAlpha = .3f;
        [Tooltip("문지르는 손가락 굵기(판 폭 비율).")]
        [SerializeField, Range(.03f, .3f)] private float steamBrush = .14f;
        private const int SteamW = 144, SteamH = 312; // the whole screen, fine enough that wiped edges stay smooth
        private RawImage steam;
        private Texture2D steamTexture;
        private readonly float[] steamAlpha = new float[SteamW * SteamH];
        private Color32[] steamPixels;
        private Image steamHand; // a small hand rubbing back and forth: "wipe it" without words
        private static Sprite handSprite;
        private string steamRound;
        private bool steamOn, steamBlocking, rubbing;
        private float steamAt, steamClearedAt = -1f, steamLeft, lastTickLeft;
        /// <summary>#696: how much smoke there was at the start (the thin edges make it a little under 1).</summary>
        private float steamStartLeft = 1f;
        // #713: soot shards already uncovered under this smoke (each rings the reveal sound once).
        private readonly HashSet<string> sootRevealed = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> sootShards = new List<string>();
        /// <summary>#713: smoke this thin (0~1) over a shard's centre counts as uncovered.</summary>
        private const float SootRevealAlpha = .4f;
        private Vector2 lastRub;
        private Image waterBack, waterFill;
        // #551 threat: the boss glows red-hot toward me while my bucket waits; failing = Jigwi's bubble, then his smoke on my board.
        [Tooltip("양동이가 오기 전 박이 달아오르기 시작하는 시간(초).")]
        [SerializeField, Range(0f, 3f)] private float threatLeadSeconds = 1.2f;
        [Tooltip("실패 말풍선과 함께 나오는 지귀 대사 \"연기로 뒤덮어주마!\" 폴더.")]
        [SerializeField] private string failVoiceFolder = "JigwiVoiceBucketFail";
        [Tooltip("박을 식혔을 때(쏟았을 때) 나는 소리 폴더.")]
        [SerializeField] private string douseSoundFolder = "JigwiFireDouse";
        [Tooltip("박이 달아오르는 동안 반복되는 지글지글 소리 폴더(뜨거울수록 크고 높게). 없으면 조용히 넘어간다.")]
        [SerializeField] private string heatLoopFolder = "JigwiBucketHeat";
        [Tooltip("양동이를 쏟는 순간 물이 쏴아 쏟아지는 소리 폴더.")]
        [SerializeField] private string pourSoundFolder = "JigwiBucketPour";
        [Tooltip("실패 때 연기가 판으로 뿜어져 뒤덮는 소리 폴더.")]
        [SerializeField] private string smokeSoundFolder = "JigwiBucketSmoke";
        [Tooltip("내가 쏟아 박이 식을 때 지귀 비명 \"앗 차가!!\" 폴더(강한 햅틱과 함께).")]
        [SerializeField] private string pourVoiceFolder = "JigwiVoiceBucketPour";
        [Tooltip("연기를 문질러 걷어낼 때 부채질 휘익 소리 폴더(여러 개면 무작위).")]
        [SerializeField] private string rubSoundFolder = "JigwiBucketFan";
        [Tooltip("출렁할 때마다 찰랑 소리 폴더(기운 쪽 스피커로 치우쳐 난다).")]
        [SerializeField] private string sloshSoundFolder = "JigwiBucketSlosh";
        [Tooltip("연기를 90% 문질러 완전히 걷힌 순간 나는 소리 폴더.")]
        [SerializeField] private string clearSoundFolder = "JigwiBucketClear";
        [Tooltip("실패 연기가 화면을 다 덮는 순간 쿵 하는 소리 폴더.")]
        [SerializeField] private string fogHitSoundFolder = "JigwiBucketFogHit";
        [SerializeField, Range(0f, 1f)] private float heatVolume = .6f, pourVolume = .9f, smokeVolume = .9f, rubVolume = .8f,
            sloshVolume = .8f, fogHitVolume = 1f, clearVolume = .9f;
        [Tooltip("\"앗 차가!!\" 때 햅틱 세기.")]
        [SerializeField, Range(0f, 1f)] private float pourVoiceHapticIntensity = 1f;
        [Tooltip("쏟기 시작(양동이가 다 뒤집힘)부터 물줄기가 박에 닿을 때까지(초). 닿는 순간 불 꺼지는 소리/흔들림/햅틱.")]
        [SerializeField, Range(.2f, 1f)] private float waterReachSeconds = .5f;
        [Tooltip("물이 닿은 뒤 지귀 비명까지(초).")]
        [SerializeField, Range(0f, 1f)] private float screamDelaySeconds = .2f;
        [Tooltip("쏟을 때 화면 흔들림 세기(박 크기 대비).")]
        [SerializeField, Range(0f, .3f)] private float pourShake = .08f;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f, douseVolume = .9f;
        private Image glow, glowCore, gaugeBack, gaugeFill;
        private readonly Image[] gaugeTicks = new Image[5];
        private readonly Image[] blow = new Image[16]; // the smoke Jigwi blows onto my board when I fail
        private readonly Vector2[] blowTo = new Vector2[16];
        private readonly Image[] smoke = new Image[10];
        private static Sprite glowSprite;
        private float heat, failAt = -1f, quenchAt = -1f;
        private bool failSpoken, pourSounded, smokeSounded, screamed;
        private AudioSource heatSource;
        private float lastSqueakLeft = 1f, lastSqueakAt = -1f;
        private AudioSource sfx;

        /// <summary>테스트용: 지금 안내 한 줄 ("" = 없음) / 양동이가 보이는지 / 지금까지 출렁 수.</summary>
        public string Banner => banner != null && banner.gameObject.activeSelf ? banner.text : string.Empty;
        public bool HandleVisible => pail != null && pail.gameObject.activeSelf;
        public int Sloshes => sloshes;

        public void Bind(LabNetwork network, Font labelFont)
        {
            source = network;
            if (labelFont != null) font = labelFont;
            Ensure();
            if (font != null) { banner.font = font; cueLabel.font = font; }
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
            glow = NewImage("Jigwi Threat Glow", Glow(), root);
            glow.transform.SetAsFirstSibling(); // behind the bucket and ropes
            for (int i = 0; i < smoke.Length; i++) smoke[i] = NewImage("Jigwi Threat Smoke " + i, Puff(), root);
            glowCore = NewImage("Jigwi Threat Glow Core", Glow(), root);
            glowCore.transform.SetSiblingIndex(1);
            for (int i = 0; i < blow.Length; i++) blow[i] = NewImage("Jigwi Smoke Billow " + i, Puff(), root);
            gaugeBack = NewImage("Jigwi Bucket Shake Gauge Back", Pixel(), root);
            gaugeFill = NewImage("Jigwi Bucket Shake Gauge", Pixel(), root);
            for (int i = 0; i < gaugeTicks.Length; i++) gaugeTicks[i] = NewImage("Jigwi Bucket Shake Tick " + i, Pixel(), root);
            waterBack = NewImage("Jigwi Bucket Water Back", Pixel(), root);
            waterFill = NewImage("Jigwi Bucket Water", Pixel(), root);
            var steamObject = new GameObject("Jigwi Bucket Steam", typeof(RectTransform));
            steamObject.layer = gameObject.layer; steamObject.transform.SetParent(root, false);
            steam = steamObject.AddComponent<RawImage>(); steam.raycastTarget = false;
            steamHand = NewImage("Jigwi Bucket Steam Hand", Hand(), root);
            // #595: a ring beside the rubbing finger fills as the smoke is wiped (full at the clear line).
            ringBack = NewImage("Jigwi Smoke Wipe Gauge Back", Ring(), root);
            ringFill = NewImage("Jigwi Smoke Wipe Gauge", Ring(), root);
            ringFill.type = Image.Type.Filled; ringFill.fillMethod = Image.FillMethod.Radial360;
            ringFill.fillOrigin = (int)Image.Origin360.Top; ringFill.fillClockwise = true;
            ringBack.gameObject.SetActive(false); ringFill.gameObject.SetActive(false);
            // Both start hidden: they appear only when this seat's bucket ran out.
            steam.gameObject.SetActive(false); steamHand.gameObject.SetActive(false);
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
            bool smokeOnly = round.smoke; // #595: no bucket — the smoke comes the moment the boss has heated up
            double arrive = me.readyAt, deadline = smokeOnly ? arrive : arrive + LabJigwiABTimeline.BucketSeatSeconds;
            if (shownRound != round.id)
            {
                shownRound = round.id; bucketAngle = 0f; dropIn = 1f; sloshes = 0; side = 0; poured = false; splashed = false;
                pourAt = lastSloshAt = sloshAt = -1f; tiltStep = 0; heat = 0f; failAt = quenchAt = -1f; failSpoken = pourSounded = smokeSounded = screamed = false;
            }
            if (!poured && (me.launchedAt >= 0d || source.JigwiBucketPulledThisCount(round)))
            { poured = true; if (pourAt < 0f) { pourAt = t; pourSide = side != 0 ? side : 1; } }
            bool failed = !poured && (smokeOnly ? now >= arrive : now > deadline);
            // My own bucket's time: from its arrival to its deadline (others' buckets come and go on their own screens).
            bool live = !poured && !failed && now >= arrive && round.resolution == LabJigwiABResolution.None;
            // Fail: Jigwi's bubble (and voice) first, then his smoke shoots down and fogs my board.
            if (failed && failAt < 0f) failAt = t;
            // #595: the billows go out one after another (BillowStagger), then the screen is covered.
            if (failAt >= 0f && t - failAt >= 2f + BillowSpread) StartSteam(round.id);
            bool visible = now >= arrive - threatLeadSeconds - .1d && (poured ? t - pourAt < 1.9f : failAt < 0f || t - failAt < 3.4f + BillowSpread);
            if (!visible) { Hide(); return; }
            ReadRoll(dt);
            if (live) Slosh(t);
            Show(banner, false); // no words: the boss's glow, the bucket and the phone picture say it
            DrawThreat(now, arrive, deadline, t, dt);
            if (!smokeOnly && now >= arrive - .7d) DrawBucket(now, arrive, deadline, failed, t, dt, live);
            else { Show(pail, false); Show(hangRope, false); HideGauge(); }
            DrawCue(live, t);
            DrawWater(t, dt);
            DrawFail(t);
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
            PlayPanned(sloshSoundFolder, sloshVolume * (.75f + .25f * Mathf.Min(sloshes, sloshesToPour) / Mathf.Max(1f, sloshesToPour)), lean * .6f);
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
            Show(waterBack, false); Show(waterFill, false);
            foreach (Image pip in pips) Show(pip, false);
            // Shake gauge just above the bucket: how much I have rocked it (sloshes so far + how far toward the next one).
            if (!tilting || alpha <= 0f) { HideGauge(); return; }
            int want = side == 0 ? 0 : -side; // the next slosh must go the other way
            float toward = want == 0 ? Mathf.Clamp01(Mathf.Abs(roll) / sloshDegrees) : Mathf.Clamp01(roll * want / sloshDegrees);
            float fill = Mathf.Clamp01((sloshes + toward * .95f) / Mathf.Max(1, sloshesToPour));
            float width = size * 1.15f, height = size * .11f;
            Vector2 gaugeAt = center + new Vector2(0f, size * .66f);
            float pop = sloshAt >= 0f ? Mathf.Exp(-(t - sloshAt) * 8f) : 0f;
            Show(gaugeBack, true); Show(gaugeFill, true);
            Place(gaugeBack.rectTransform, gaugeAt, new Vector2(width + 8f, height + 8f) * (1f + .12f * pop), 0f);
            gaugeBack.color = new Color(.12f, .08f, .05f, .85f);
            Place(gaugeFill.rectTransform, gaugeAt + new Vector2(-width * (1f - fill) * .5f, 0f), new Vector2(width * fill, height), 0f);
            gaugeFill.color = Color.Lerp(new Color(.45f, .78f, 1f, 1f), Color.white, pop * .6f);
            for (int i = 0; i < gaugeTicks.Length; i++)
            {
                bool on = i < sloshesToPour - 1;
                Show(gaugeTicks[i], on);
                if (!on) continue;
                float x = -width * .5f + width * (i + 1f) / sloshesToPour;
                Place(gaugeTicks[i].rectTransform, gaugeAt + new Vector2(x, 0f), new Vector2(3f, height + 4f), 0f);
                gaugeTicks[i].color = new Color(.12f, .08f, .05f, .9f);
            }
        }

        /// <summary>박이 보이는 3D 화면을 잠깐 흔든다(지귀 물 맞을 때와 같은 `LabJigwiWaterFx.Shake`). 세기는 박 크기에 맞춘다.</summary>
        private void ShakeView(float seconds)
        {
            Collider hitbox = source != null && source.target != null ? source.target.Hitbox : null;
            if (hitbox == null) return;
            LabJigwiWaterFx.Get(hitbox.gameObject.layer);
            LabJigwiWaterFx.Shake(hitbox.bounds.extents.magnitude * pourShake, seconds);
        }

        private void HideGauge()
        {
            Show(gaugeBack, false); Show(gaugeFill, false);
            foreach (Image tick in gaugeTicks) Show(tick, false);
            Show(waterBack, false); Show(waterFill, false);
            foreach (Image pip in pips) Show(pip, false);
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
            float lean = Mathf.Sign(swing) * Mathf.Pow(Mathf.Abs(swing), .5f); // -1 (left side down) … 1 (right side down)
            // The whole phone tilts (like Temple Run): it turns about its upright middle, so it narrows and slides to that side,
            // the far edge darkening, instead of spinning like a steering wheel.
            Place(cuePhone.rectTransform, new Vector2(lean * size * .12f, size * .15f), new Vector2(size * .58f, size), 0f);
            cuePhone.rectTransform.localScale = new Vector3(1f - .45f * Mathf.Abs(lean), 1f, 1f);
            float shade = 1f - .2f * Mathf.Abs(lean);
            cuePhone.color = new Color(shade, shade, shade, .95f);
            Show(cueLabel, false); // no words (the ● under the bucket count the sloshes)
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
            // The bucket tips over (.25 s), then the water falls and lands on the boss at waterReachSeconds.
            const float tip = .25f;
            float reach = Mathf.Max(tip + .1f, waterReachSeconds);
            if (poured && !pourSounded && sp >= tip) { pourSounded = true; Play(pourSoundFolder, pourVolume); }
            Vector2 boss = Boss;
            if (poured && !screamed && splashed && sp >= reach + screamDelaySeconds) { screamed = true; Play(pourVoiceFolder, voiceVolume); }
            if (poured && !splashed && sp >= reach)
            {
                splashed = true; quenchAt = t;
                Flash(new Color(.6f, .88f, 1f, .6f));
                Play(douseSoundFolder, douseVolume);
                ShakeView(.35f);
                if (source != null) LabHaptics.PlayDirect(source.config, LabHapticStyle.Heavy, pourVoiceHapticIntensity, .5f, pourVoiceHapticIntensity, 1f);
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
            bool sheetOn = sp >= tip && sp < reach + .65f;
            Show(sheet, sheetOn);
            if (sheetOn)
            {
                float pour = Mathf.Clamp01((sp - tip) / (reach - tip));
                pour *= pour; // falling: slow at first, fast at the end
                float top = mouth.y, bottom = Mathf.Lerp(top, boss.y + root.rect.height * .03f, pour);
                Place(sheet.rectTransform, new Vector2(Mathf.Lerp(mouth.x, boss.x, .4f), (top + bottom) * .5f),
                    new Vector2(BucketSize * .75f, Mathf.Max(20f, top - bottom + 30f)), 0f);
                sheet.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((sp - reach - .3f) / .35f));
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

        // ---------- threat: the boss heats up toward me ----------
        /// <summary>박이 화면에서 있는 자리(이 화면 가운데 기준). 박 충돌체를 전투 카메라로 비춰 찾고, 못 찾으면 gourdAnchorY.</summary>
        private Vector2 Boss
        {
            get
            {
                Collider hitbox = source != null && source.target != null ? source.target.Hitbox : null;
                Camera eye = Camera.main;
                if (hitbox != null && eye != null)
                {
                    Vector3 screen = eye.WorldToScreenPoint(LabHangingTarget.DrawnPoint(hitbox, hitbox.bounds.center));
                    if (screen.z > 0f && TryLocalPoint(screen, out Vector2 local)) return local;
                }
                return new Vector2(0f, (gourdAnchorY - .5f) * root.rect.height);
            }
        }

        private bool TryLocalPoint(Vector2 screen, out Vector2 local)
        {
            local = default;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, eye, out local)) return false;
            local -= (new Vector2(.5f, .5f) - root.pivot) * root.rect.size;
            return true;
        }

        /// <summary>
        /// 위협: 내 양동이가 오기 조금 전부터 박이 내 쪽으로 시뻘겋게 달아오르고 연기가 모인다. 남은 시간이 줄수록 더 뜨겁다(이게 시간).
        /// 쏟으면 하얀 김을 뿜으며 식고, 못 쏟으면 가장 뜨거운 채로 실패로 넘어간다.
        /// </summary>
        private void DrawThreat(double now, double arrive, double deadline, float t, float dt)
        {
            float target;
            if (splashed) target = 0f; // stays hot until the water actually lands
            else if (now < arrive) target = .55f * Mathf.Clamp01((float)((now - (arrive - threatLeadSeconds)) / Mathf.Max(.01f, threatLeadSeconds)));
            else target = Mathf.Lerp(.55f, 1f, Mathf.Clamp01((float)((now - arrive) / LabJigwiABTimeline.BucketSeatSeconds)));
            heat = Mathf.MoveTowards(heat, target, dt * (splashed ? 2.5f : 2f));
            Vector2 boss = Boss;
            // Clearly hot from the start; by the end it throbs fast and glows white-hot in the middle.
            float size = root.rect.width * .7f, pulse = .88f + .12f * Mathf.Sin(t * (5f + 14f * heat * heat));
            // On a fail the boss breathes in (swells, 0.6–1.0 s) and then coughs the smoke out (a short burst).
            float since = failAt >= 0f ? t - failAt : -1f;
            if (since >= .6f && since < 1f) pulse *= 1f + .5f * Mathf.SmoothStep(0f, 1f, (since - .6f) / .4f) + .04f * Mathf.Sin(t * 60f);
            else if (since >= 1f && since < 1.3f) pulse *= 1.5f - 1.4f * (since - 1f);
            UpdateHeatSound();
            Show(glow, heat > .01f); Show(glowCore, heat > .01f);
            if (heat > .01f)
            {
                Place(glow.rectTransform, boss, Vector2.one * size * (.8f + .45f * heat) * pulse, 0f);
                glow.color = new Color(1f, Mathf.Lerp(.5f, .18f, heat), .1f, Mathf.Clamp01(.35f + .65f * heat));
                Place(glowCore.rectTransform, boss, Vector2.one * root.rect.width * .3f * (.9f + .2f * heat) * pulse, 0f);
                glowCore.color = Color.Lerp(new Color(1f, .45f, .2f, .45f * heat), new Color(1f, .95f, .7f, .75f), Mathf.Clamp01((heat - .7f) / .3f));
            }
            // Smoke gathers and rises from the hot boss; after a pour the puffs turn white (steam) and blow away.
            bool quench = quenchAt >= 0f && t - quenchAt < 1.2f;
            for (int i = 0; i < smoke.Length; i++)
            {
                float u = Mathf.Repeat(t * .45f + i / (float)smoke.Length, 1f);
                float a = quench ? (1f - (t - quenchAt) / 1.2f) * (1f - u) : heat * Mathf.Sin(u * Mathf.PI) * .75f;
                Show(smoke[i], a > .02f);
                if (a <= .02f) continue;
                float x = Mathf.Sin(i * 2.3f + t * .8f) * root.rect.width * (.05f + .08f * u);
                Place(smoke[i].rectTransform, boss + new Vector2(x, root.rect.width * (.06f + .22f * u)),
                    Vector2.one * root.rect.width * (.08f + .1f * u) * (quench ? 1.4f : 1f), 0f);
                float grey = quench ? .97f : Mathf.Lerp(.55f, .3f, heat);
                smoke[i].color = new Color(grey, grey * .96f, grey * .93f, a);
            }
        }

        /// <summary>
        /// 실패: 지귀 목소리 → 1초 뒤 박이 부풀었다 뿜으면
        /// 달아오를 때 박 위에 피어오르던 그 연기가 그대로 부풀어 화면 전체(3D 영역과 판)로 퍼져 회색으로 채우고 → 그 위에 두꺼운 연기 막이 깔린다.
        /// </summary>
        // #595: more time between the puffs Jigwi blows out (was .06 / .04 s), so the smoke clearly spreads out puff by puff.
        private const float BillowColumnStagger = .2f, BillowRowStagger = .15f;
        private static float BillowSpread => 3f * (BillowColumnStagger - .06f) + 3f * (BillowRowStagger - .04f);
        private void DrawFail(float t)
        {
            float since = failAt >= 0f ? t - failAt : -1f;
            float w = root.rect.width, h = root.rect.height;
            if (since >= 0f && !failSpoken)
            {
                failSpoken = true;
                Play(failVoiceFolder, voiceVolume);
                if (source != null) LabHaptics.PlayDirect(source.config, LabHapticStyle.Rigid, .6f);
                // Where each billow ends up: spread over the whole screen, more of them over the 3D area (upper half).
                for (int i = 0; i < blow.Length; i++)
                {
                    float col = (i % 4 + UnityEngine.Random.Range(.1f, .9f)) / 4f - .5f;
                    float row = (i / 4 + UnityEngine.Random.Range(.1f, .9f)) / ((blow.Length + 3) / 4) - .5f;
                    blowTo[i] = new Vector2(col * w * 1.05f, (row * .95f + .12f) * h);
                }
            }
            if (since < 0f) { foreach (Image puff in blow) Show(puff, false); return; }
            float s = since - 1f;
            if (s >= 0f && !smokeSounded) { smokeSounded = true; Play(smokeSoundFolder, smokeVolume); ShakeView(.5f); }
            // Billows start exactly where the threat smoke was rising (just above the boss) and swell outward until they meet.
            Vector2 boss = Boss;
            for (int i = 0; i < blow.Length; i++)
            {
                float k = s - (i % 4) * BillowColumnStagger - (i / 4) * BillowRowStagger;
                bool on = k >= 0f && k < 2.2f;
                Show(blow[i], on);
                if (!on) continue;
                float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(k / 1f), 3f); // fast out, slow in
                Vector2 from = boss + new Vector2(Mathf.Sin(i * 2.3f) * w * .08f, w * (.08f + .12f * ((i * 7) % 5) / 5f));
                Vector2 at = Vector2.Lerp(from, blowTo[i], grow) + new Vector2(Mathf.Sin(k * 3f + i) * w * .02f, Mathf.Cos(k * 2.5f + i) * w * .02f);
                float size = Mathf.Lerp(.16f, .75f + .1f * (i % 3), grow) * w * (1f + .04f * Mathf.Sin(k * 4f + i));
                Place(blow[i].rectTransform, at, Vector2.one * size, (i % 2 == 0 ? 1f : -1f) * k * 25f);
                float grey = Mathf.Lerp(.3f, .52f, grow);
                float alpha = Mathf.Lerp(.75f, .95f, grow) * (1f - Mathf.Clamp01((k - 1.7f) / .5f));
                blow[i].color = new Color(grey, grey * .97f, grey * .95f, alpha);
            }
            // The smoke closing over the screen: a thump.
            if (s >= .5f + BillowSpread && s - Time.unscaledDeltaTime < .5f + BillowSpread && source != null)
                LabHaptics.PlayDirect(source.config, LabHapticStyle.Heavy, .9f, .25f, .6f, .4f);
        }

        /// <summary>지글지글 반복: 열기(heat)에 따라 크기와 높이가 오르고, 식으면(heat 0) 멈춘다.</summary>
        private void UpdateHeatSound()
        {
            if (heat <= .01f || heatVolume <= 0f) { StopHeatSound(); return; }
            if (heatSource == null)
            {
                AudioClip[] clips = string.IsNullOrEmpty(heatLoopFolder) ? null : Resources.LoadAll<AudioClip>("Audio/" + heatLoopFolder);
                if (clips == null || clips.Length == 0) return;
                heatSource = gameObject.AddComponent<AudioSource>();
                heatSource.playOnAwake = false; heatSource.spatialBlend = 0f; heatSource.loop = true; heatSource.clip = clips[0];
            }
            if (!heatSource.isPlaying) heatSource.Play();
            heatSource.volume = Mathf.Pow(heat, 1.3f) * heatVolume * LabAudioSettings.SfxVolume;
            heatSource.pitch = .9f + .3f * heat;
        }

        private void StopHeatSound() { if (heatSource != null && heatSource.isPlaying) heatSource.Stop(); }

        private AudioSource panned;
        /// <summary>한 번 나는 소리를 왼쪽(-1)~오른쪽(+1)으로 치우쳐 낸다.</summary>
        private void PlayPanned(string folder, float volume, float pan)
        {
            if (string.IsNullOrEmpty(folder) || volume <= 0f) return;
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/" + folder);
            if (clips == null || clips.Length == 0) return;
            if (panned == null) { panned = gameObject.AddComponent<AudioSource>(); panned.playOnAwake = false; panned.spatialBlend = 0f; }
            panned.panStereo = Mathf.Clamp(pan, -1f, 1f);
            panned.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)], volume * LabAudioSettings.SfxVolume);
        }

        private void Play(string folder, float volume)
        {
            if (string.IsNullOrEmpty(folder) || volume <= 0f) return;
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/" + folder);
            if (clips == null || clips.Length == 0) return;
            if (sfx == null) { sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false; sfx.spatialBlend = 0f; }
            sfx.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)], volume * LabAudioSettings.SfxVolume);
        }

        // ---------- failure: Jigwi's smoke over the whole screen ----------
        /// <summary>내 양동이를 제시간에 못 쏟았다: 지귀 연기가 화면 전체(3D 영역과 조합판)를 덮는다(한 회차에 한 번). 판 입력이 막히고, 90% 문질러야 걷힌다.</summary>
        private void StartSteam(string roundId)
        {
            if (steamRound == roundId) return;
            steamRound = roundId; steamOn = true; steamAt = Time.unscaledTime; steamClearedAt = -1f; rubbing = false;
            sootRevealed.Clear();
            lastSqueakLeft = 1f; lastSqueakAt = -1f;
            thickestSpot = handSpot = new Vector2(.5f, .66f); WipeGauge = 0f; touched = false; // #595
            if (steamTexture == null)
            {
                steamTexture = new Texture2D(SteamW, SteamH, TextureFormat.RGBA32, false)
                { name = "Jigwi steam (#551 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                steamPixels = new Color32[SteamW * SteamH];
                steam.texture = steamTexture;
            }
            // Fully opaque until rubbed away: nothing of the boss or the board shows through.
            // #696: except a thinner band at the very top and bottom (hard to reach past the notch/home bar),
            // which therefore weighs less in what is left to wipe.
            for (int y = 0; y < SteamH; y++)
            {
                float v = (y + .5f) / SteamH;
                float edge = steamEdgeBand > 0f ? Mathf.Clamp01(Mathf.Min(v, 1f - v) / steamEdgeBand) : 1f;
                float alpha = Mathf.Lerp(steamEdgeAlpha, 1f, edge * edge * (3f - 2f * edge));
                for (int x = 0; x < SteamW; x++) steamAlpha[y * SteamW + x] = alpha;
            }
            WriteSteam();
            float startSum = 0f; foreach (float a in steamAlpha) startSum += a;
            steamStartLeft = steamLeft = lastTickLeft = startSum / steamAlpha.Length;
            lastSqueakLeft = steamLeft;
            if (source != null && source.board != null) { source.board.SetUiInputBlocked(true); steamBlocking = true; }
            if (source != null) source.JigwiSmokeWiping = true; // #696 iOS edge gestures wait while rubbing
            if (source != null) LabHaptics.PlayDirect(source.config, LabHapticStyle.Heavy, 1f, .45f, .8f, .3f);
            Play(fogHitSoundFolder, fogHitVolume);
        }

        private void WriteSteam()
        {
            for (int i = 0; i < steamAlpha.Length; i++)
            {
                int x = i % SteamW, y = i / SteamW;
                float warm = .5f + .5f * Mathf.Sin(x * .3f + y * .2f);
                byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(steamAlpha[i]));
                // #595: drawn light and tinted down to Jigwi's smoke grey (SmokeGrey).
                steamPixels[i] = new Color32((byte)(230 + 25 * warm), (byte)(219 + 18 * warm), 212, a);
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
            // The smoke covers the whole screen: the 3D view (so the boss is hidden) and my board (blocked) alike.
            Rect board = new Rect(0f, 0f, Screen.width, Screen.height);
            PlaceScreenRect(steam.rectTransform, board);
            if (!steam.gameObject.activeSelf) steam.gameObject.SetActive(true);
            float fadeIn = Mathf.Clamp01((t - steamAt) / .5f);
            // #724 UT: when it counts as wiped the rest of the smoke is blown away at once: it swells outward and thins out.
            float blown = steamClearedAt >= 0f ? Mathf.Clamp01((t - steamClearedAt) / ClearBlowSeconds) : 0f;
            float fadeOut = 1f - blown * blown * (3f - 2f * blown);
            WipeGauge = WipedShare;
            steam.color = new Color(SmokeGrey, SmokeGrey * .98f, SmokeGrey * .96f, fadeIn * fadeOut);
            steam.rectTransform.localScale = Vector3.one * (1f + .45f * (1f - (1f - blown) * (1f - blown)));
            DriveWipePuffs(board, t);
            if (steamClearedAt >= 0f)
            {
                RevealSootShards(true); // #713: whatever was still hidden shows as the smoke lifts
                if (fadeOut <= 0f && t - steamClearedAt >= ClearPuffSeconds) EndSteam(); // #724 after the blown puffs are gone
                Show(steamHand, false);
                // The full ring flashes and swells away with the smoke.
                float burst = Mathf.Clamp01((t - steamClearedAt) / .4f);
                DrawWipeGauge(1f, 1f + .5f * burst, 1f - burst, board);
                return;
            }
            // Rub: every finger (or mouse) drag over the board wipes a stroke out of the fog.
            if (TryPointer(out Vector2 screen) && board.Contains(screen))
            {
                lastPointer = screen; touched = true;
                Vector2 uv = new Vector2((screen.x - board.xMin) / board.width, (screen.y - board.yMin) / board.height);
                if (rubbing) Wipe(lastRub, uv, board); else Wipe(uv, uv, board);
                if (rubbing) EmitWipePuffs(lastRub, uv, board, t);
                lastRub = uv; rubbing = true;
            }
            else rubbing = false;
            RevealSootShards(false);
            // A small hand rubs back and forth where the most smoke is left (hidden while a finger is already rubbing).
            bool hint = !rubbing;
            Show(steamHand, hint);
            if (hint)
            {
                float hand = board.width * .16f;
                float u = Mathf.Sin(t * 7f);
                handSpot = Vector2.Lerp(handSpot, thickestSpot, Mathf.Clamp01(Time.unscaledDeltaTime * 3f)); // glides there
                Vector2 centre = new Vector2(board.xMin + board.width * handSpot.x + u * board.width * .12f,
                    board.yMin + board.height * handSpot.y + Mathf.Abs(u) * hand * .12f);
                PlaceScreenRect(steamHand.rectTransform, new Rect(centre.x - hand * .33f, centre.y - hand * .5f, hand * .67f, hand));
                steamHand.rectTransform.localEulerAngles = new Vector3(0f, 0f, -u * 12f);
                steamHand.color = new Color(1f, 1f, 1f, Mathf.Clamp01(steamLeft * 1.6f) * fadeIn);
            }
            DrawWipeGauge(WipeGauge, 1f, touched ? fadeIn : 0f, board); // only once touched; it stays where the finger was
        }

        // ---------- #595 how much is left: throb, the hand goes to the thickest smoke, the rubbing sound rises ----------
        /// <summary>The smoke grey the light texture is tinted down to.</summary>
        private const float SmokeGrey = .55f;
        private Image ringBack, ringFill;
        private Vector2 lastPointer;
        private bool touched;
        private static Sprite ringSprite;

        /// <summary>#595: a ring around the touch (where the finger is, or was last), filling as the smoke is wiped.</summary>
        private void DrawWipeGauge(float wiped, float scale, float alpha, Rect board)
        {
            Show(ringBack, alpha > .01f); Show(ringFill, alpha > .01f);
            if (alpha <= .01f) return;
            float size = board.width * .22f; // wider than a fingertip, so it shows around it
            Vector2 at = lastPointer;
            at.x = Mathf.Clamp(at.x, board.xMin + size * .6f, board.xMax - size * .6f);
            at.y = Mathf.Clamp(at.y, board.yMin + size * .6f, board.yMax - size * .6f);
            Rect rect = new Rect(at.x - size * .5f, at.y - size * .5f, size, size);
            PlaceScreenRect(ringBack.rectTransform, rect); PlaceScreenRect(ringFill.rectTransform, rect);
            ringBack.rectTransform.localScale = ringFill.rectTransform.localScale = Vector3.one * scale;
            ringBack.color = new Color(.1f, .08f, .07f, .55f * alpha);
            ringFill.fillAmount = wiped;
            ringFill.color = wiped >= 1f ? new Color(.55f, 1f, .7f, alpha) : new Color(1f, .93f, .7f, alpha);
        }

        /// <summary>A thick round ring (white, tinted by colour).</summary>
        private static Sprite Ring() => ringSprite != null ? ringSprite : ringSprite = Build("Jigwi wipe ring", 96, 96, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 30f) * Mathf.Clamp01((d - .7f) * 30f));
        });
        private Vector2 thickestSpot = new Vector2(.5f, .66f), handSpot = new Vector2(.5f, .66f);
        private AudioSource rubSource;
        /// <summary>테스트용: 연기를 얼마나 닦았는지(0~1, 1 = 다 닦음).</summary>
        public float WipeGauge { get; private set; }

        /// <summary>Where the most smoke is left, on a coarse 4 × 8 grid of the screen (0~1).</summary>
        private void FindThickest()
        {
            const int cols = 4, rows = 8;
            float best = -1f;
            for (int cy = 0; cy < rows; cy++)
                for (int cx = 0; cx < cols; cx++)
                {
                    float sum = 0f;
                    int x0 = cx * SteamW / cols, x1 = (cx + 1) * SteamW / cols, y0 = cy * SteamH / rows, y1 = (cy + 1) * SteamH / rows;
                    for (int y = y0; y < y1; y += 2)
                        for (int x = x0; x < x1; x += 2) sum += steamAlpha[y * SteamW + x];
                    if (sum > best) { best = sum; thickestSpot = new Vector2((cx + .5f) / cols, (cy + .5f) / rows); }
                }
        }

        /// <summary>The rubbing whoosh, higher the more is wiped.</summary>
        private void PlayRub(float progress)
        {
            if (string.IsNullOrEmpty(rubSoundFolder) || rubVolume <= 0f) return;
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/" + rubSoundFolder);
            if (clips == null || clips.Length == 0) return;
            if (rubSource == null) { rubSource = gameObject.AddComponent<AudioSource>(); rubSource.playOnAwake = false; rubSource.spatialBlend = 0f; }
            rubSource.pitch = Mathf.Lerp(.9f, 1.5f, progress);
            rubSource.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)], rubVolume * LabAudioSettings.SfxVolume);
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
                        float next = Mathf.Min(steamAlpha[i], steamAlpha[i] * (.08f + .92f * d));
                        if (next < steamAlpha[i] - .001f) { steamAlpha[i] = next; changed = true; }
                    }
            }
            if (!changed) return;
            WriteSteam();
            float sum = 0f; foreach (float a in steamAlpha) sum += a;
            steamLeft = sum / steamAlpha.Length;
            FindThickest();
            float wiped = WipedShare;
            // A light tick for every bit wiped away, a firm one when it is clear.
            if (lastTickLeft - steamLeft >= .06f && source != null)
            { lastTickLeft = steamLeft; LabHaptics.PlayDirect(source.config, LabHapticStyle.Light, Mathf.Lerp(.3f, .75f, wiped)); } // #595 firmer toward the end
            // 휘익: a fanning whoosh for every few percent rubbed away, at most every 0.25 s.
            if (lastSqueakLeft - steamLeft >= .04f && Time.unscaledTime - lastSqueakAt >= .25f)
            { lastSqueakLeft = steamLeft; lastSqueakAt = Time.unscaledTime; PlayRub(wiped); }
            // #696: once rubbing starts the Host lays the soot shards under the smoke, so wiping uncovers them one by one.
            if (steamStartLeft - steamLeft >= sootRevealWiped && source != null) source.RequestJigwiSmokeClear(steamRound);
            if (steamLeft <= steamClearLeft)
            {
                steamClearedAt = Time.unscaledTime;
                BlowAwayPuffs(Time.unscaledTime);
                if (source != null) source.RequestJigwiSmokeWiped(steamRound); // #713: everyone wiped = the smoke is over
                if (source != null) LabHaptics.PlayDirect(source.config, LabHapticStyle.Medium, .7f);
                Play(clearSoundFolder, clearVolume);
                if (steamBlocking && source != null && source.board != null) source.board.SetUiInputBlocked(false);
                steamBlocking = false;
                if (source != null) source.JigwiSmokeWiping = false;
            }
        }

        /// <summary>#696: 0 at the start of the smoke, 1 when it counts as wiped (the thin edges are already counted as thin).</summary>
        private float WipedShare => Mathf.Clamp01((steamStartLeft - steamLeft) / Mathf.Max(.01f, steamStartLeft - steamClearLeft));

        /// <summary>
        /// #713: a soot shard lying under the smoke is "found" once the smoke over its centre is rubbed thin
        /// (or all at once when the smoke lifts); each one rings the reveal sound once (this device only).
        /// </summary>
        private void RevealSootShards(bool all)
        {
            if (source == null || source.board == null) return;
            source.board.CollectSootShards(sootShards);
            int found = 0;
            foreach (string id in sootShards)
            {
                if (sootRevealed.Contains(id)) continue;
                bool open = all;
                if (!open && source.board.TryGetScreenPoint(id, out Vector2 screen) && Screen.width > 0 && Screen.height > 0)
                    open = SmokeAt(screen.x / Screen.width, screen.y / Screen.height) <= SootRevealAlpha;
                if (open && sootRevealed.Add(id)) found++;
            }
            source.NotifySootShardRevealed(found);
        }

        /// <summary>#713: the smoke left around a screen point (0~1 each way), averaged over a few texels.</summary>
        private float SmokeAt(float u, float v)
        {
            if (!(u >= 0f && u <= 1f && v >= 0f && v <= 1f)) return 1f;
            int cx = Mathf.Clamp(Mathf.FloorToInt(u * SteamW), 0, SteamW - 1), cy = Mathf.Clamp(Mathf.FloorToInt(v * SteamH), 0, SteamH - 1);
            float sum = 0f; int n = 0;
            for (int y = Mathf.Max(0, cy - 2); y <= Mathf.Min(SteamH - 1, cy + 2); y++)
                for (int x = Mathf.Max(0, cx - 2); x <= Mathf.Min(SteamW - 1, cx + 2); x++) { sum += steamAlpha[y * SteamW + x]; n++; }
            return n > 0 ? sum / n : 1f;
        }

        // ---------- #724 UT: smoke scatters from the fingertip, and is blown away at once when wiped ----------
        public const float ClearBlowSeconds = .55f, WipePuffSeconds = .5f, ClearPuffSeconds = .7f;
        private const int WipePuffCount = 18, ClearPuffCount = 12;
        private Image[] wipePuffs;
        private readonly Vector2[] wipePuffFrom = new Vector2[WipePuffCount + ClearPuffCount], wipePuffVelocity = new Vector2[WipePuffCount + ClearPuffCount];
        private readonly float[] wipePuffBorn = new float[WipePuffCount + ClearPuffCount], wipePuffSize = new float[WipePuffCount + ClearPuffCount];
        private int nextWipePuff;
        private float lastWipePuffAt = float.NegativeInfinity;
        /// <summary>Smoke puffs flying off now (tests and checks).</summary>
        public int WipePuffsShown { get; private set; }

        private void EnsureWipePuffs()
        {
            if (wipePuffs != null || steam == null) return;
            wipePuffs = new Image[WipePuffCount + ClearPuffCount];
            int above = steam.transform.GetSiblingIndex() + 1;
            for (int i = 0; i < wipePuffs.Length; i++)
            {
                wipePuffs[i] = NewImage("Jigwi Smoke Wipe Puff " + i, Puff(), root);
                wipePuffs[i].transform.SetSiblingIndex(above + i); // over the smoke, under the hand and the ring
                wipePuffs[i].gameObject.SetActive(false);
                wipePuffBorn[i] = float.NegativeInfinity;
            }
        }

        /// <summary>A rubbing finger moved from a to b (0~1 of the board): a couple of puffs flick out sideways from the fingertip.</summary>
        private void EmitWipePuffs(Vector2 a, Vector2 b, Rect board, float t)
        {
            EnsureWipePuffs();
            if (wipePuffs == null || t - lastWipePuffAt < .035f) return;
            Vector2 move = new Vector2((b.x - a.x) * board.width, (b.y - a.y) * board.height);
            if (move.sqrMagnitude < 4f) return;
            lastWipePuffAt = t;
            Vector2 tip = new Vector2(board.xMin + b.x * board.width, board.yMin + b.y * board.height);
            Vector2 dir = move.normalized, side = new Vector2(-dir.y, dir.x);
            for (int k = 0; k < 2; k++)
            {
                int i = nextWipePuff; nextWipePuff = (nextWipePuff + 1) % WipePuffCount;
                float sign = k == 0 ? 1f : -1f;
                wipePuffFrom[i] = tip;
                wipePuffVelocity[i] = (side * sign * UnityEngine.Random.Range(.55f, 1f) + dir * UnityEngine.Random.Range(.1f, .5f))
                    * board.width * UnityEngine.Random.Range(.35f, .55f);
                wipePuffSize[i] = board.width * UnityEngine.Random.Range(.07f, .11f);
                wipePuffBorn[i] = t;
            }
        }

        /// <summary>The smoke counts as wiped: big puffs burst from all over the screen and fly off past the edges.</summary>
        private void BlowAwayPuffs(float t)
        {
            EnsureWipePuffs();
            if (wipePuffs == null) return;
            Rect board = new Rect(0f, 0f, Screen.width, Screen.height);
            Vector2 middle = board.center;
            for (int k = 0; k < ClearPuffCount; k++)
            {
                int i = WipePuffCount + k;
                float angle = (k + UnityEngine.Random.Range(-.3f, .3f)) / ClearPuffCount * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                wipePuffFrom[i] = middle + Vector2.Scale(dir, new Vector2(board.width * .28f, board.height * .3f));
                wipePuffVelocity[i] = dir * board.height * UnityEngine.Random.Range(.7f, 1.1f);
                wipePuffSize[i] = board.width * UnityEngine.Random.Range(.35f, .55f);
                wipePuffBorn[i] = t;
            }
        }

        private void DriveWipePuffs(Rect board, float t)
        {
            if (wipePuffs == null) { WipePuffsShown = 0; return; }
            int shown = 0;
            for (int i = 0; i < wipePuffs.Length; i++)
            {
                bool clear = i >= WipePuffCount;
                float life = clear ? ClearPuffSeconds : WipePuffSeconds;
                float age = t - wipePuffBorn[i];
                bool on = age >= 0f && age < life;
                if (wipePuffs[i].gameObject.activeSelf != on) wipePuffs[i].gameObject.SetActive(on);
                if (!on) continue;
                shown++;
                float u = age / life;
                // Flicked off fast, slowing like smoke; it grows and thins as it goes.
                float travel = (1f - (1f - u) * (1f - u)) * life;
                Vector2 at = wipePuffFrom[i] + wipePuffVelocity[i] * travel;
                float size = wipePuffSize[i] * Mathf.Lerp(.6f, clear ? 1.6f : 1.5f, u);
                PlaceScreenRect(wipePuffs[i].rectTransform, new Rect(at.x - size * .5f, at.y - size * .5f, size, size));
                wipePuffs[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, (i * 53f + u * 90f) % 360f);
                float alpha = (clear ? .85f : .6f) * (1f - u) * Mathf.Clamp01(u * 8f);
                wipePuffs[i].color = new Color(SmokeGrey, SmokeGrey * .98f, SmokeGrey * .96f, alpha);
            }
            WipePuffsShown = shown;
        }

        private void EndSteam()
        {
            steamOn = false; rubbing = false;
            if (steam != null && steam.gameObject.activeSelf) steam.gameObject.SetActive(false);
            if (steam != null) steam.rectTransform.localScale = Vector3.one;
            if (wipePuffs != null) foreach (Image puff in wipePuffs) if (puff != null) puff.gameObject.SetActive(false);
            WipePuffsShown = 0;
            Show(steamHand, false);
            WipeGauge = 0f;
            Show(ringBack, false); Show(ringFill, false);
            if (steamBlocking && source != null && source.board != null) source.board.SetUiInputBlocked(false);
            steamBlocking = false;
            if (source != null) source.JigwiSmokeWiping = false;
        }

        private void OnDisable() { EndSteam(); StopHeatSound(); }

        private static bool TryPointer(out Vector2 screen)
        {
            screen = default;
            if (LabOrbBoard.ExternalModalOpen) return false;
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
            StopHeatSound();
            if (root == null) return;
            foreach (Graphic part in new Graphic[] { banner, flash, sheet, pail, hangRope, waterBack, waterFill }) Show(part, false);
            foreach (Image pip in pips) Show(pip, false);
            foreach (Image drop in drops) Show(drop, false);
            for (int i = 0; i < dropBorn.Length; i++) dropBorn[i] = 0f;
            if (cue != null && cue.gameObject.activeSelf) cue.gameObject.SetActive(false);
            foreach (Graphic part in new Graphic[] { glow, glowCore }) Show(part, false);
            foreach (Image puff in blow) Show(puff, false);
            HideGauge();
            foreach (Image puff in smoke) Show(puff, false);
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
        /// <summary>
        /// 👆 A small pointing hand (front view): a big round palm, the index finger straight up, the other three fingers folded
        /// to short bumps, the thumb a low bump on the left, thin ink lines between the fingers.
        /// </summary>
        private static Sprite Hand() => handSprite != null ? handSprite : handSprite = Build("Jigwi steam hand", 64, 96, (u, v) =>
        {
            float x = u * 64f, y = v * 96f;
            float palm = RoundBox(x, y, 2.5f, 2.5f, 61.5f, 56f, 24f);
            float index = Capsule(x, y, 19f, 40f, 19f, 86f, 8f);
            float middle = Capsule(x, y, 32f, 44f, 32f, 64f, 5.5f);
            float ring = Capsule(x, y, 43.5f, 44f, 43.5f, 62f, 6f);
            float pinky = Capsule(x, y, 55f, 42f, 55f, 56f, 6f);
            float thumb = Capsule(x, y, 6.5f, 36f, 6.5f, 52f, 5f);
            float d = Mathf.Min(Mathf.Min(palm, Mathf.Min(index, thumb)), Mathf.Min(middle, Mathf.Min(ring, pinky)));
            if (d > 2.5f) return Color.clear;
            Color ink = new Color(.1f, .08f, .07f, 1f);
            if (d > -1.2f) return ink;
            // Lines between the fingers (and beside the index finger) reaching a little into the palm.
            bool line = Mathf.Abs(x - 11f) < .9f && y > 36f && y < 56f
                || Mathf.Abs(x - 26.5f) < .9f && y > 46f && y < 66f
                || Mathf.Abs(x - 37.8f) < .9f && y > 46f && y < 63f
                || Mathf.Abs(x - 49.5f) < .9f && y > 44f && y < 60f;
            if (line) return ink;
            return new Color(1f, .9f, .82f, 1f);
        });
        private static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            var p = new Vector2(x, y); var a = new Vector2(ax, ay); var ab = new Vector2(bx, by) - a;
            float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * k) - r;
        }
        /// <summary>Red-hot glow: a soft radial blob, brightest in the middle.</summary>
        private static Sprite Glow() => glowSprite != null ? glowSprite : glowSprite = Build("Jigwi threat glow", 64, 64, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));
        });
        private static Sprite puffSprite;
        /// <summary>A soft round smoke puff (white, tinted by colour): solid middle, feathered edge.</summary>
        private static Sprite Puff() => puffSprite != null ? puffSprite : puffSprite = Build("Jigwi smoke puff", 64, 64, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2f;
            float lump = .06f * Mathf.Sin(Mathf.Atan2(v - .5f, u - .5f) * 5f);
            return new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f + lump - d) / .35f)));
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
