using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Item presentation on the orb root: an icon on a disc, a padlock over the dimmed icon while
    /// the item is locked and a badge (remaining unlock handoffs, or a relay orb's x1/x2/MAX stage),
    /// all facing the front-on board camera. A bomb with cartoon art shows that art instead, with
    /// its unlock handoffs left in red on the ball. Its time left is the fuse: a rope burning down
    /// toward the cap over the fuse less its last warning seconds, a spark flickering where it burns,
    /// then the bomb flashing red. Once unlocked the spark goes out in a puff of smoke and a rainbow
    /// ring circles the ball to show it can be thrown (#212). As the bomb moves it rolls: the ball
    /// and its highlight stay put like a lit sphere while the cap and rope tilt and swing around it
    /// in 3D, never turning away behind it. On unlock the padlock rises and fades while the icon sharpens, then a gold
    /// ring shrinks onto the orb and the orb glows once. It follows the Visual mount's rise
    /// animation but not its rolling rotation, and owns no collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabOrbItemMarker : MonoBehaviour
    {
        private const int BadgeFontSize = 64;
        private static readonly Color DiscColor = new Color(.97f, .97f, .97f);
        private static readonly Color BadgeColor = new Color(.12f, .12f, .14f, .95f);
        private static readonly Color CountOutlineColor = Color.white;
        // The spark is the original picture's two-tone star: orange with a yellow heart.
        private static readonly Color SparkColor = new Color(1f, .34f, .15f);
        private static readonly Color SparkHeartColor = new Color(1f, .8f, .15f);
        private static readonly Color SparkDotColor = new Color(1f, .62f, .15f);
        private static readonly Color SmokeColor = new Color(.55f, .52f, .5f);
        // The white outline around the bomb's red count, in orb radii, drawn by offset copies.
        private const float CountOutline = .06f;
        private static readonly Vector2[] OutlineDirections =
        {
            new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f)
        };
        private const int SparkDotCount = 3;
        private const int SmokePuffs = 3;
        private const float SmokeSeconds = .9f;
        // Sorting around the bomb's ball (order 0): the rainbow ring behind it, the rope and then
        // the cap over it (the rope starts under the cap), then the spark.
        private const int RainbowOrder = -1, RopeOrder = 1, FuseOrder = 2, SparkOrder = 6;
        // The rope's course in the images' pose, from inside the cap outward, in ball image
        // half-widths: the original picture's S-shaped rope carried on in the same waves, long
        // enough for three times its length (#212). The appearance sets how much of it is used.
        private static readonly Vector2[] RopePath =
        {
            new Vector2(.715f, .947f), new Vector2(.87f, 1.024f), new Vector2(1.014f, .918f),
            new Vector2(1.159f, .831f), new Vector2(1.314f, .85f), new Vector2(1.449f, .937f),
            new Vector2(1.575f, .995f), new Vector2(1.71f, .96f), new Vector2(1.84f, .88f),
            new Vector2(1.98f, .86f), new Vector2(2.12f, .92f), new Vector2(2.25f, .99f),
            new Vector2(2.39f, .97f)
        };
        // How long the rope in the original picture is, in ball image half-widths.
        private const float PictureRopeLength = .6f;
        // Enough dots for the longest rope to read as one line.
        private const int RopeDotCount = 60;
        private const float RainbowTurnDegrees = 120f;
        private static Vector2[] ropeCourse;
        private static float[] ropeCourseLengths;
        private static Vector2[] ropeSamples;
        private static float ropeSamplesLength = -1f;
        // Seen end-on, a cap pointing at the camera would flatten to a line; keep some length as depth would.
        private const float MinReach = .3f;
        // A jump (a press snapping the bomb under the finger, or a snapshot) turns it at most this much in one frame.
        private const float MaxRollDegreesPerFrame = 30f;
        private static readonly Dictionary<Texture2D, Sprite> iconSprites = new Dictionary<Texture2D, Sprite>();
        private static Sprite discSprite;
        private static Sprite squareSprite;
        private static Sprite ringSprite;
        private static Sprite glowSprite;
        private static Sprite sparkSprite;
        private static Sprite rainbowSprite;
        private static LabOrbAppearance defaultLook;

        private SpriteRenderer disc;
        private SpriteRenderer icon;
        private SpriteRenderer padlock;
        private SpriteRenderer ring;
        private SpriteRenderer glow;
        private SpriteRenderer warningGlow;
        private SpriteRenderer badge;
        private TextMesh badgeCount;
        private SpriteRenderer bombArt;
        private Transform fusePivot;
        private SpriteRenderer fuseArt;
        private Quaternion bombTurn = Quaternion.identity;
        private float capAngle;
        private float ballRadius;
        private float ropeLength = 2f * PictureRopeLength;
        private Vector3 lastRootPosition;
        private bool hasLastRootPosition;
        private TextMesh bombCount;
        private readonly TextMesh[] bombCountOutline = new TextMesh[4];
        private SpriteRenderer spark;
        private SpriteRenderer sparkHeart;
        private readonly SpriteRenderer[] sparkDots = new SpriteRenderer[SparkDotCount];
        private readonly SpriteRenderer[] ropeDots = new SpriteRenderer[RopeDotCount];
        private readonly SpriteRenderer[] smokePuffs = new SpriteRenderer[SmokePuffs];
        private SpriteRenderer rainbow;
        private float ropeShare = 1f;
        private float fuseTotal = 15f;
        private bool burning;
        private float smokeElapsed = -1f;
        private Vector2 smokeAt;
        private float rainbowTime;
        private bool bombMode;
        private Vector2 sparkOffset;
        private float sparkTime;
        private Transform follow;
        private float radius;
        private float unlockElapsed = -1f;
        private float unlockFromAlpha;
        private LabOrbAppearance unlockLook;
        private LabOrbAppearance fuseLook;
        private float fuseLeft = -1f;
        private float warningPhase;

        public bool IconVisible => icon != null && icon.gameObject.activeSelf;
        public float IconAlpha => icon != null ? icon.color.a : 0f;
        public bool LockVisible => padlock != null && padlock.gameObject.activeSelf;
        public bool UnlockPlaying => unlockElapsed >= 0f;
        public bool RingVisible => ring != null && ring.gameObject.activeSelf;
        public float RingDiameter => ring != null ? ring.transform.localScale.x : 0f;
        public bool GlowVisible => glow != null && glow.gameObject.activeSelf;
        /// <summary>How red the bomb's fuse warning is right now, 0..1; 0 when it is not warning.</summary>
        public float WarningLevel { get; private set; }
        /// <summary>Flashes per second of the fuse warning; 0 when it is not warning.</summary>
        public float WarningHz { get; private set; }
        public bool BadgeVisible => badge != null && badge.gameObject.activeSelf;
        public string BadgeText => badgeCount != null ? badgeCount.text : string.Empty;
        /// <summary>The cartoon bomb art is drawn in place of the disc and icon.</summary>
        public bool BombArtVisible => bombArt != null && bombArt.gameObject.activeSelf;
        /// <summary>The bomb art's current tint; it turns red while the fuse warning flashes.</summary>
        public Color BombArtColor => bombArt != null ? bombArt.color : Color.clear;
        public bool BombCountVisible => bombCount != null && bombCount.gameObject.activeSelf;
        /// <summary>The unlock handoffs left, written on the bomb art; empty once it is open.</summary>
        public string BombCountText => BombCountVisible ? bombCount.text : string.Empty;
        public Color BombCountColor => bombCount != null ? bombCount.color : Color.clear;
        /// <summary>How much of the fuse rope is left, 1 unburnt to 0 burnt down to the cap.</summary>
        public float RopeShare => ropeShare;
        /// <summary>How many of the rope's dots are drawn.</summary>
        public int RopeDotsShown
        {
            get
            {
                int shown = 0;
                foreach (SpriteRenderer dot in ropeDots) if (dot != null && dot.gameObject.activeSelf) shown++;
                return shown;
            }
        }
        /// <summary>The rainbow ring that marks an unlocked bomb as throwable.</summary>
        public bool RainbowVisible => rainbow != null && rainbow.gameObject.activeSelf;
        /// <summary>The puff of smoke where the spark went out when the bomb unlocked.</summary>
        public bool SmokeVisible => smokeElapsed >= 0f;
        /// <summary>The spark flickering on the bomb art's fuse while a locked bomb burns.</summary>
        public bool SparkVisible => spark != null && spark.gameObject.activeSelf;
        public bool FuseArtVisible => fuseArt != null && fuseArt.gameObject.activeInHierarchy;
        /// <summary>How the bomb has rolled from the pose drawn in its images.</summary>
        public Quaternion BombTurn => bombTurn;
        /// <summary>Where the rolled cap points; it never turns away from the camera (z stays at or below 0).</summary>
        public Vector3 CapDirection { get; private set; }
        public int FuseSortingOrder => fuseArt != null ? fuseArt.sortingOrder : 0;
        /// <summary>Where the rope burns (its end), from the ball centre: the spark sits here.</summary>
        public Vector2 FuseTipPosition { get; private set; }

        /// <summary>Sets how the bomb has rolled, for example a random tumble when it lands.</summary>
        public void SetBombTurn(Quaternion turn)
        {
            bombTurn = turn;
            KeepCapInView();
            if (bombMode) UpdateBombPose();
        }

        public static LabOrbItemMarker Create(Transform orbRoot)
        {
            var marker = new GameObject("Item Marker");
            marker.layer = orbRoot.gameObject.layer;
            marker.transform.SetParent(orbRoot, false);
            return marker.AddComponent<LabOrbItemMarker>();
        }

        /// <summary>Shows the icon (when assigned) and badge; null or empty text hides the badge.</summary>
        public void Show(Texture2D iconTexture, string badgeText, float orbRadius, Transform followMount)
        {
            EnsureParts();
            radius = orbRadius;
            follow = followMount;
            FollowMount();
            SetBombMode(false);

            bool hasIcon = iconTexture != null;
            disc.gameObject.SetActive(hasIcon);
            icon.gameObject.SetActive(hasIcon);
            if (hasIcon)
            {
                disc.transform.localScale = Vector3.one * (1.6f * radius);
                icon.sprite = SpriteFor(iconTexture);
                icon.transform.localScale = Vector3.one * (1.15f * radius);
            }

            bool visible = !string.IsNullOrEmpty(badgeText);
            badge.gameObject.SetActive(visible);
            badge.transform.localPosition = new Vector3(.62f * radius, -.62f * radius, -.01f);
            // Widen the square for longer labels such as "x2" or "MAX".
            float width = Mathf.Max(1f, .55f * (visible ? badgeText.Length : 1));
            badge.transform.localScale = new Vector3(.62f * radius * width, .62f * radius, 1f);
            if (badgeCount != null)
            {
                badgeCount.gameObject.SetActive(visible);
                badgeCount.text = visible ? badgeText : string.Empty;
                badgeCount.transform.localPosition = new Vector3(.62f * radius, -.62f * radius, -.02f);
                badgeCount.characterSize = .5f * radius / (BadgeFontSize * .1f);
            }
        }

        /// <summary>
        /// Shows the appearance's cartoon bomb in place of the disc, icon, padlock and badge, with
        /// countText (the unlock handoffs left) in red on its ball; null or empty hides it.
        /// </summary>
        public void ShowBomb(LabOrbAppearance look, string countText, float orbRadius, Transform followMount)
        {
            EnsureParts();
            look = LookOrDefault(look);
            radius = orbRadius;
            follow = followMount;
            FollowMount();
            SetBombMode(look.BombArt != null);
            if (!bombMode) return;

            // Sprites are one unit wide; the ball image width is set in orb diameters and the fuse
            // canvas, centred on the ball, in ball image widths.
            float bodyWidth = look.BombBodySize * 2f * radius;
            ballRadius = .5f * bodyWidth;
            bombArt.sprite = SpriteFor(look.BombArt);
            bombArt.transform.localScale = Vector3.one * bodyWidth;
            bombArt.transform.localPosition = Vector3.zero;
            capAngle = look.BombCapAngle;
            ropeLength = look.BombRopeLength * PictureRopeLength;
            float ropeWidth = look.BombRopeThickness * ballRadius;
            foreach (SpriteRenderer dot in ropeDots)
            {
                dot.transform.localScale = Vector3.one * ropeWidth;
                dot.color = look.BombRopeColor;
            }
            // Just outside the ball, so its inner edge hides behind it as a glowing rim.
            rainbow.transform.localScale = Vector3.one * (2.5f * ballRadius);
            bool hasFuse = look.BombFuseArt != null;
            fuseArt.gameObject.SetActive(hasFuse);
            if (hasFuse)
            {
                fuseArt.sprite = SpriteFor(look.BombFuseArt);
                fuseArt.transform.localScale = Vector3.one * (bodyWidth * look.BombFuseCanvasSize);
                // Turn the cap onto the pivot's x axis, so the pivot can aim it and shorten it as it tilts.
                fuseArt.transform.localRotation = Quaternion.Euler(0f, 0f, -capAngle);
            }
            UpdateBombPose();

            bool counting = !string.IsNullOrEmpty(countText);
            if (bombCount == null) return;
            float size = look.BombCountSize * radius / (BadgeFontSize * .1f);
            bombCount.gameObject.SetActive(counting);
            bombCount.text = counting ? countText : string.Empty;
            bombCount.color = look.BombCountColor;
            bombCount.characterSize = size;
            bombCount.transform.localPosition = new Vector3(0f, 0f, -.02f);
            for (int i = 0; i < bombCountOutline.Length; i++)
            {
                TextMesh outline = bombCountOutline[i];
                outline.gameObject.SetActive(counting);
                outline.text = bombCount.text;
                outline.characterSize = size;
                Vector2 offset = OutlineDirections[i] * (CountOutline * radius);
                outline.transform.localPosition = new Vector3(offset.x, offset.y, -.015f);
            }
        }

        private void SetBombMode(bool on)
        {
            if (on && !bombMode) hasLastRootPosition = false;
            bombMode = on;
            bombArt.gameObject.SetActive(on);
            fusePivot.gameObject.SetActive(on);
            if (on)
            {
                disc.gameObject.SetActive(false);
                icon.gameObject.SetActive(false);
                padlock.gameObject.SetActive(false);
                badge.gameObject.SetActive(false);
                if (badgeCount != null) badgeCount.gameObject.SetActive(false);
            }
            else
            {
                if (bombCount != null) bombCount.gameObject.SetActive(false);
                foreach (TextMesh outline in bombCountOutline)
                    if (outline != null) outline.gameObject.SetActive(false);
                foreach (SpriteRenderer dot in ropeDots) dot.gameObject.SetActive(false);
                rainbow.gameObject.SetActive(false);
                StopSmoke();
            }
            if (!on) SetSpark(false);
        }

        /// <summary>
        /// Covers the icon with the appearance's padlock and dims it while locked. Without a
        /// padlock image only the dimming and the count badge mark the lock. Call after Show.
        /// </summary>
        public void SetLocked(bool locked, LabOrbAppearance look)
        {
            EnsureParts();
            // The unlock effect owns the padlock and icon until it ends.
            if (!locked && UnlockPlaying) return;
            StopUnlock();
            look = LookOrDefault(look);
            Texture2D lockTexture = look.LockIcon;
            // The bomb art never shows a padlock; once open, a rainbow ring says it can be thrown.
            bool covered = locked && lockTexture != null && !bombMode;
            rainbow.gameObject.SetActive(bombMode && !locked);
            padlock.gameObject.SetActive(covered);
            if (covered)
            {
                padlock.sprite = SpriteFor(lockTexture);
                // The sprite is one unit wide; size its longer side in orb radii.
                float aspect = (float)lockTexture.height / lockTexture.width;
                padlock.transform.localScale = Vector3.one * (look.LockIconSize * radius / Mathf.Max(1f, aspect));
                padlock.transform.localPosition = new Vector3(0f, 0f, -.005f);
            }
            icon.color = new Color(1f, 1f, 1f, locked ? look.LockedIconAlpha : 1f);
        }

        /// <summary>
        /// Starts from the locked look, then lifts and fades the padlock while the icon sharpens,
        /// lands a shrinking ring on the orb and glows once. Call after Show.
        /// </summary>
        public void PlayUnlock(LabOrbAppearance look)
        {
            SetLocked(true, look);
            // The effect starts from the locked look, but an unlocked bomb keeps its rainbow throughout.
            rainbow.gameObject.SetActive(bombMode);
            unlockLook = LookOrDefault(look);
            unlockFromAlpha = icon.color.a;
            unlockElapsed = 0f;
            AnimateUnlock();
        }

        /// <summary>
        /// A locked bomb's seconds left on its fuse of totalSeconds (negative when it has none).
        /// The bomb art's rope burns down toward the cap over the fuse less its warning seconds,
        /// with a spark flickering where it burns; then the bomb flashes red, faster and faster as
        /// the fuse runs out. A fuse that stops burning, because the bomb unlocked, goes out in a
        /// puff of smoke and leaves the rope as long as it was. Call after SetLocked.
        /// </summary>
        public void SetFuse(float secondsLeft, float totalSeconds, LabOrbAppearance look)
        {
            EnsureParts();
            fuseLeft = secondsLeft;
            fuseLook = LookOrDefault(look);
            if (totalSeconds > 0f) fuseTotal = totalSeconds;
            bool wasBurning = burning;
            burning = bombMode && fuseLeft >= 0f;
            if (burning)
            {
                float warning = fuseLook.BombWarningSeconds;
                ropeShare = Mathf.Clamp01((fuseLeft - warning) / Mathf.Max(.01f, fuseTotal - warning));
            }
            else if (wasBurning && bombMode) StartSmoke();
            SetSpark(burning);
            if (bombMode) UpdateBombPose();
            AnimateWarning(0f);
        }

        private void LateUpdate()
        {
            FollowMount();
            if (bombMode)
            {
                RollBomb();
                UpdateBombPose();
            }
            AnimateWarning(Time.deltaTime);
            AnimateSpark(Time.deltaTime);
            AnimateSmoke(Time.deltaTime);
            AnimateRainbow(Time.deltaTime);
            if (!UnlockPlaying) return;
            unlockElapsed += Time.deltaTime;
            AnimateUnlock();
        }

        private void AnimateWarning(float step)
        {
            LabOrbAppearance look = fuseLook != null ? fuseLook : LookOrDefault(null);
            float from = look.BombWarningSeconds;
            if (fuseLeft < 0f || from <= 0f || fuseLeft > from)
            {
                if (WarningHz > 0f) StopWarning();
                return;
            }
            // Snapshots update the seconds left; the phase keeps the flashing smooth between them.
            float urgency = 1f - Mathf.Clamp01(fuseLeft / from);
            WarningHz = Mathf.Lerp(look.BombWarningSlowHz, look.BombWarningFastHz, urgency);
            warningPhase = Mathf.Repeat(warningPhase + step * WarningHz, 1f);
            WarningLevel = .5f - .5f * Mathf.Cos(warningPhase * Mathf.PI * 2f);
            Color red = look.BombWarningColor;
            disc.color = Color.Lerp(DiscColor, red, WarningLevel);
            Color tint = Color.Lerp(Color.white, red, .8f * WarningLevel);
            icon.color = new Color(tint.r, tint.g, tint.b, icon.color.a);
            bombArt.color = Color.Lerp(Color.white, red, .75f * WarningLevel);
            fuseArt.color = bombArt.color;
            warningGlow.gameObject.SetActive(true);
            warningGlow.transform.localScale = Vector3.one * (2.4f * radius);
            warningGlow.color = new Color(red.r, red.g, red.b, .6f * WarningLevel);
        }

        private void StopWarning()
        {
            WarningLevel = 0f;
            WarningHz = 0f;
            warningPhase = 0f;
            disc.color = DiscColor;
            icon.color = new Color(1f, 1f, 1f, icon.color.a);
            bombArt.color = Color.white;
            fuseArt.color = Color.white;
            warningGlow.gameObject.SetActive(false);
        }

        /// <summary>
        /// Rolls the bomb by how far it moved since the last frame, like a ball rolling on the board
        /// behind it: the face toward the camera turns the way it travels.
        /// </summary>
        private void RollBomb()
        {
            Transform root = transform.parent;
            if (root == null) return;
            Vector3 position = root.localPosition;
            if (hasLastRootPosition && ballRadius > 0f)
            {
                Vector3 moved = position - lastRootPosition;
                moved.z = 0f;
                float distance = moved.magnitude;
                if (distance > 1e-5f)
                {
                    float degrees = Mathf.Min(distance / ballRadius * Mathf.Rad2Deg, MaxRollDegreesPerFrame);
                    bombTurn = Quaternion.AngleAxis(degrees, Vector3.Cross(Vector3.back, moved / distance)) * bombTurn;
                    KeepCapInView();
                }
            }
            lastRootPosition = position;
            hasLastRootPosition = true;
        }

        /// <summary>
        /// The cap may roll toward the camera or out to the ball's outline, but never on round behind
        /// the ball where it would vanish: a roll that would carry it there slides it along the outline.
        /// </summary>
        private void KeepCapInView()
        {
            float authored = capAngle * Mathf.Deg2Rad;
            Vector3 cap = bombTurn * new Vector3(Mathf.Cos(authored), Mathf.Sin(authored), 0f);
            // The board camera looks along +z, so a cap with positive z faces away from it.
            if (cap.z > 0f)
            {
                var outline = new Vector3(cap.x, cap.y, 0f);
                if (outline.sqrMagnitude < 1e-6f) outline = new Vector3(CapDirection.x, CapDirection.y, 0f);
                if (outline.sqrMagnitude < 1e-6f) outline = new Vector3(Mathf.Cos(authored), Mathf.Sin(authored), 0f);
                bombTurn = Quaternion.FromToRotation(cap, outline.normalized) * bombTurn;
                cap = bombTurn * new Vector3(Mathf.Cos(authored), Mathf.Sin(authored), 0f);
            }
            CapDirection = cap;
        }

        /// <summary>
        /// Places the cap and fuse for the bomb's roll. They stand on the ball where the rolled cap
        /// direction points: aimed along its direction across the screen and shortened as it tilts
        /// toward the camera, always drawn over the ball.
        /// </summary>
        private void UpdateBombPose()
        {
            float authored = capAngle * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(authored), Mathf.Sin(authored));
            KeepCapInView();
            var across = new Vector2(CapDirection.x, CapDirection.y);
            float reach = Mathf.Max(across.magnitude, MinReach);
            float facing = across.sqrMagnitude > 1e-8f ? Mathf.Atan2(across.y, across.x) : authored;
            fusePivot.localPosition = new Vector3(0f, 0f, -.005f);
            fusePivot.localRotation = Quaternion.Euler(0f, 0f, facing * Mathf.Rad2Deg);
            fusePivot.localScale = new Vector3(reach, 1f, 1f);
            fuseArt.sortingOrder = FuseOrder;

            // The rope and its spark ride the cap through the same aim and shortening. The rope is
            // drawn up to where it has burnt; the spark sits at that end.
            var aim = new Vector2(Mathf.Cos(facing), Mathf.Sin(facing));
            var side = new Vector2(-aim.y, aim.x);
            var tipSide = new Vector2(-along.y, along.x);
            Vector2 Place(Vector2 authoredPoint)
            {
                Vector2 point = authoredPoint * ballRadius;
                return aim * (Vector2.Dot(point, along) * reach) + side * Vector2.Dot(point, tipSide);
            }
            Vector2[] samples = RopeSamples(ropeLength);
            for (int i = 0; i < RopeDotCount; i++)
            {
                float share = i / (float)(RopeDotCount - 1);
                SpriteRenderer dot = ropeDots[i];
                bool shown = share <= ropeShare + 1e-4f;
                dot.gameObject.SetActive(shown);
                if (!shown) continue;
                Vector2 at = Place(samples[i]);
                dot.transform.localPosition = new Vector3(at.x, at.y, -.004f);
            }
            float end = ropeShare * (RopeDotCount - 1);
            int below = Mathf.Min(RopeDotCount - 2, Mathf.FloorToInt(end));
            FuseTipPosition = Place(Vector2.Lerp(samples[below], samples[below + 1], end - below));
            sparkOffset = FuseTipPosition;
        }

        /// <summary>
        /// The first length (ball image half-widths) of RopePath, smoothed into a curve through its
        /// points and sampled into RopeDotCount points an even distance apart, so the dots read as one
        /// rope and burn down at a steady pace.
        /// </summary>
        private static Vector2[] RopeSamples(float length)
        {
            if (ropeSamples != null && Mathf.Approximately(ropeSamplesLength, length)) return ropeSamples;
            RopeCourse(out Vector2[] points, out float[] lengths);
            int dense = points.Length - 1;
            float wanted = Mathf.Clamp(length, .01f, lengths[dense]);
            var samples = new Vector2[RopeDotCount];
            int at = 0;
            for (int i = 0; i < RopeDotCount; i++)
            {
                float target = wanted * i / (RopeDotCount - 1);
                while (at < dense - 1 && lengths[at + 1] < target) at++;
                float segment = Mathf.Max(1e-6f, lengths[at + 1] - lengths[at]);
                samples[i] = Vector2.Lerp(points[at], points[at + 1], Mathf.Clamp01((target - lengths[at]) / segment));
            }
            ropeSamples = samples;
            ropeSamplesLength = length;
            return ropeSamples;
        }

        /// <summary>The whole of RopePath as a dense Catmull-Rom curve with its running length.</summary>
        private static void RopeCourse(out Vector2[] points, out float[] lengths)
        {
            if (ropeCourse != null)
            {
                points = ropeCourse;
                lengths = ropeCourseLengths;
                return;
            }
            const int dense = 480;
            points = new Vector2[dense + 1];
            lengths = new float[dense + 1];
            int spans = RopePath.Length - 1;
            for (int i = 0; i <= dense; i++)
            {
                float t = i / (float)dense * spans;
                int span = Mathf.Min(spans - 1, Mathf.FloorToInt(t));
                float u = t - span;
                Vector2 p0 = RopePath[Mathf.Max(0, span - 1)], p1 = RopePath[span];
                Vector2 p2 = RopePath[span + 1], p3 = RopePath[Mathf.Min(spans, span + 2)];
                // Catmull-Rom through p1..p2.
                points[i] = .5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u
                    + (3f * p1 - p0 - 3f * p2 + p3) * u * u * u);
                if (i > 0) lengths[i] = lengths[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            }
            ropeCourse = points;
            ropeCourseLengths = lengths;
        }

        private void SetSpark(bool lit)
        {
            spark.gameObject.SetActive(lit);
            sparkHeart.gameObject.SetActive(lit);
            foreach (SpriteRenderer dot in sparkDots) dot.gameObject.SetActive(lit);
            if (lit) AnimateSpark(0f);
        }

        private void StartSmoke()
        {
            smokeElapsed = 0f;
            smokeAt = FuseTipPosition;
            AnimateSmoke(0f);
        }

        private void StopSmoke()
        {
            smokeElapsed = -1f;
            foreach (SpriteRenderer puff in smokePuffs) puff.gameObject.SetActive(false);
        }

        /// <summary>A few grey puffs rise, swell and fade where the spark went out.</summary>
        private void AnimateSmoke(float step)
        {
            if (smokeElapsed < 0f) return;
            smokeElapsed += step;
            float t = smokeElapsed / SmokeSeconds;
            if (t >= 1f)
            {
                StopSmoke();
                return;
            }
            for (int i = 0; i < SmokePuffs; i++)
            {
                SpriteRenderer puff = smokePuffs[i];
                float own = Mathf.Clamp01(t * 1.3f - i * .15f);
                puff.gameObject.SetActive(own > 0f && own < 1f);
                float drift = (i - 1) * .25f * ballRadius * own;
                puff.transform.localPosition = new Vector3(smokeAt.x + drift, smokeAt.y + .9f * ballRadius * own, -.011f);
                puff.transform.localScale = Vector3.one * (ballRadius * (.25f + .45f * own));
                puff.color = new Color(SmokeColor.r, SmokeColor.g, SmokeColor.b, .65f * (1f - own));
            }
        }

        /// <summary>The unlocked bomb's rainbow ring turns slowly and breathes.</summary>
        private void AnimateRainbow(float step)
        {
            if (!RainbowVisible) return;
            rainbowTime += step;
            rainbow.transform.localRotation = Quaternion.Euler(0f, 0f, -rainbowTime * RainbowTurnDegrees);
            rainbow.color = new Color(1f, 1f, 1f, .8f + .2f * Mathf.Sin(rainbowTime * Mathf.PI * 2f));
        }

        /// <summary>
        /// The burning fuse: a bright star that flickers in size and turn at the fuse tip, and a few
        /// sparks that shoot out from it and fade, each in a new direction.
        /// </summary>
        private void AnimateSpark(float step)
        {
            if (!SparkVisible) return;
            sparkTime += step;
            float flicker = Mathf.PerlinNoise(sparkTime * 14f, .37f);
            float turn = Mathf.PerlinNoise(.91f, sparkTime * 6f) * 180f;
            // The picture's star at about 60%, so it marks where the rope burns without hiding how
            // much is left; it swells and shrinks as it burns.
            float size = (ballRadius > 0f ? ballRadius : radius) * Mathf.Lerp(.48f, .62f, flicker);
            spark.transform.localPosition = new Vector3(sparkOffset.x, sparkOffset.y, -.01f);
            spark.transform.localScale = Vector3.one * size;
            spark.transform.localRotation = Quaternion.Euler(0f, 0f, turn);
            spark.color = new Color(SparkColor.r, SparkColor.g, SparkColor.b, Mathf.Lerp(.75f, 1f, flicker));
            sparkHeart.transform.localPosition = new Vector3(sparkOffset.x, sparkOffset.y, -.011f);
            sparkHeart.transform.localScale = Vector3.one * (.55f * size);
            sparkHeart.transform.localRotation = Quaternion.Euler(0f, 0f, -turn * .5f);
            sparkHeart.color = SparkHeartColor;
            for (int i = 0; i < SparkDotCount; i++)
            {
                float cycle = sparkTime * 4f + i / (float)SparkDotCount;
                float t = cycle - Mathf.Floor(cycle);
                float angle = Wobble((int)Mathf.Floor(cycle) * SparkDotCount + i) * Mathf.PI * 2f;
                Vector2 flight = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * .9f * t);
                SpriteRenderer dot = sparkDots[i];
                dot.transform.localPosition = new Vector3(sparkOffset.x + flight.x, sparkOffset.y + flight.y, -.012f);
                dot.transform.localScale = Vector3.one * (radius * .22f * (1f - t));
                dot.color = new Color(SparkDotColor.r, SparkDotColor.g, SparkDotColor.b, 1f - t);
            }
        }

        // A fixed 0..1 value per index, so each spark leaves in its own direction.
        private static float Wobble(int index) => Mathf.Repeat(Mathf.Sin(index * 12.9898f + 1.7f) * 43758.547f, 1f);

        private void AnimateUnlock()
        {
            LabOrbAppearance look = unlockLook != null ? unlockLook : LookOrDefault(null);
            float elapsed = unlockElapsed;

            // The padlock rises and fades while the icon sharpens.
            float lift = Mathf.Clamp01(elapsed / look.UnlockLockSeconds);
            float rise = 1f - (1f - lift) * (1f - lift);
            padlock.transform.localPosition = new Vector3(0f, look.UnlockLockRise * radius * rise, -.005f);
            padlock.color = new Color(1f, 1f, 1f, 1f - lift);
            icon.color = new Color(1f, 1f, 1f, Mathf.Lerp(unlockFromAlpha, 1f, lift));
            if (lift >= 1f) padlock.gameObject.SetActive(false);

            // A gold ring shrinks onto the orb, slowing as it lands; the orb then glows once
            // while the ring fades away.
            float shrink = (elapsed - look.UnlockRingDelay) / look.UnlockRingSeconds;
            float flash = (elapsed - look.UnlockRingDelay - look.UnlockRingSeconds) / look.UnlockGlowSeconds;
            ring.gameObject.SetActive(shrink >= 0f && flash < 1f);
            if (ring.gameObject.activeSelf)
            {
                float t = Mathf.Clamp01(shrink);
                float landing = 1f - (1f - t) * (1f - t) * (1f - t);
                ring.transform.localScale = Vector3.one * (2f * radius * Mathf.Lerp(look.UnlockRingStartScale, 1f, landing));
                Color color = look.UnlockRingColor;
                color.a *= Mathf.Clamp01(t * 5f) * (1f - Mathf.Clamp01(flash));
                ring.color = color;
            }
            glow.gameObject.SetActive(flash >= 0f && flash < 1f);
            if (glow.gameObject.activeSelf)
            {
                glow.transform.localScale = Vector3.one * (2.6f * radius);
                Color color = look.UnlockGlowColor;
                color.a *= Mathf.Sin(Mathf.PI * flash);
                glow.color = color;
            }

            if (elapsed >= look.UnlockEffectSeconds) StopUnlock();
        }

        private void StopUnlock()
        {
            unlockElapsed = -1f;
            unlockLook = null;
            padlock.color = Color.white;
            ring.gameObject.SetActive(false);
            glow.gameObject.SetActive(false);
        }

        /// <summary>The assigned appearance, or one holding the default lock and unlock look.</summary>
        private static LabOrbAppearance LookOrDefault(LabOrbAppearance look)
        {
            if (look != null) return look;
            if (defaultLook == null)
            {
                defaultLook = ScriptableObject.CreateInstance<LabOrbAppearance>();
                defaultLook.hideFlags = HideFlags.DontSave;
            }
            return defaultLook;
        }

        private void FollowMount()
        {
            Vector3 offset = follow != null ? follow.localPosition : Vector3.zero;
            transform.localPosition = new Vector3(offset.x, offset.y, -1.02f * radius);
        }

        private void EnsureParts()
        {
            if (disc != null) return;
            // The glow sits behind the disc so it lights the orb's rim, not the icon.
            glow = CreateSprite("Unlock Glow", Glow(), Color.white, -1);
            glow.gameObject.SetActive(false);
            warningGlow = CreateSprite("Fuse Warning Glow", Glow(), Color.white, -2);
            warningGlow.gameObject.SetActive(false);
            disc = CreateSprite("Icon Disc", Disc(), DiscColor, 0);
            ring = CreateSprite("Unlock Ring", Ring(), Color.white, 0);
            ring.transform.localPosition = new Vector3(0f, 0f, -.003f);
            ring.gameObject.SetActive(false);
            icon = CreateSprite("Icon", null, Color.white, 1);
            padlock = CreateSprite("Padlock", null, Color.white, 2);
            padlock.gameObject.SetActive(false);
            badge = CreateSprite("Lock Badge", Square(), BadgeColor, 3);
            bombArt = CreateSprite("Bomb Art", null, Color.white, 0);
            bombArt.gameObject.SetActive(false);
            fusePivot = new GameObject("Bomb Fuse Pivot").transform;
            fusePivot.gameObject.layer = gameObject.layer;
            fusePivot.SetParent(transform, false);
            fuseArt = CreateSprite("Bomb Fuse Art", null, Color.white, FuseOrder);
            fuseArt.transform.SetParent(fusePivot, false);
            fusePivot.gameObject.SetActive(false);
            for (int i = 0; i < RopeDotCount; i++)
            {
                ropeDots[i] = CreateSprite("Fuse Rope", Disc(), Color.white, RopeOrder);
                ropeDots[i].gameObject.SetActive(false);
            }
            rainbow = CreateSprite("Unlocked Rainbow Ring", RainbowRing(), Color.white, RainbowOrder);
            rainbow.gameObject.SetActive(false);
            spark = CreateSprite("Fuse Spark", Spark(), SparkColor, SparkOrder);
            sparkHeart = CreateSprite("Fuse Spark Heart", Spark(), SparkHeartColor, SparkOrder + 1);
            for (int i = 0; i < SparkDotCount; i++) sparkDots[i] = CreateSprite("Fuse Spark Dot", Glow(), SparkDotColor, SparkOrder);
            for (int i = 0; i < SmokePuffs; i++)
            {
                smokePuffs[i] = CreateSprite("Fuse Smoke", Glow(), SmokeColor, SparkOrder);
                smokePuffs[i].gameObject.SetActive(false);
            }
            SetSpark(false);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;
            badgeCount = CreateText("Lock Count", font, Color.white);
            for (int i = 0; i < bombCountOutline.Length; i++)
            {
                bombCountOutline[i] = CreateText("Bomb Count Outline", font, CountOutlineColor, 4, FontStyle.Bold);
                bombCountOutline[i].gameObject.SetActive(false);
            }
            bombCount = CreateText("Bomb Count", font, Color.red, 5, FontStyle.Bold);
            bombCount.gameObject.SetActive(false);
        }

        private TextMesh CreateText(string label, Font font, Color color, int order = 4,
            FontStyle style = FontStyle.Normal)
        {
            var part = new GameObject(label);
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            var text = part.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = BadgeFontSize;
            text.fontStyle = style;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = order;
            return text;
        }

        private SpriteRenderer CreateSprite(string label, Sprite sprite, Color color, int order)
        {
            var part = new GameObject(label);
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite SpriteFor(Texture2D texture)
        {
            if (iconSprites.TryGetValue(texture, out var sprite) && sprite != null) return sprite;
            // One world unit wide, so the caller's scale is the icon size.
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(.5f, .5f), texture.width);
            sprite.name = texture.name + " (item icon)";
            sprite.hideFlags = HideFlags.DontSave;
            iconSprites[texture] = sprite;
            return sprite;
        }

        private static Sprite Disc()
        {
            if (discSprite != null) return discSprite;
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size * .5f;
                    float dy = y + .5f - size * .5f;
                    float alpha = Mathf.Clamp01(size * .5f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            discSprite = CreateGeneratedSprite("Lab item disc", size, pixels);
            return discSprite;
        }

        /// <summary>A thin ring whose outer edge is the sprite edge, so scale 1 sits on the orb outline.</summary>
        private static Sprite Ring()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128;
            const float thickness = 7f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size * .5f;
                    float dy = y + .5f - size * .5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(size * .5f - distance)
                        * Mathf.Clamp01(distance - (size * .5f - thickness));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            ringSprite = CreateGeneratedSprite("Lab unlock ring", size, pixels);
            return ringSprite;
        }

        /// <summary>A soft disc: solid to 70% of its radius, then fading to the edge.</summary>
        private static Sprite Glow()
        {
            if (glowSprite != null) return glowSprite;
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size * .5f;
                    float dy = y + .5f - size * .5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) / (size * .5f);
                    float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.7f, 1f, distance));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            glowSprite = CreateGeneratedSprite("Lab unlock glow", size, pixels);
            return glowSprite;
        }

        /// <summary>A jagged, hand-drawn star like the spark in the bomb picture, one unit wide.</summary>
        private static Sprite Spark()
        {
            if (sparkSprite != null) return sparkSprite;
            const int size = 96;
            const int points = 9;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f;
                    float dy = (y + .5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float turn = (Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 1f) * points;
                    int point = (int)turn % points;
                    float along = turn - Mathf.Floor(turn);
                    // Each point leans a little and reaches its own length, like the drawn star.
                    float peak = .5f + .3f * (Wobble(point + 17) - .5f);
                    float tip = along < peak ? along / peak : (1f - along) / (1f - peak);
                    float edge = Mathf.Lerp(.45f, .78f + .22f * Wobble(point + 5), tip);
                    float alpha = Mathf.Clamp01((edge - distance) * size * .5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            sparkSprite = CreateGeneratedSprite("Lab fuse spark", size, pixels);
            return sparkSprite;
        }

        /// <summary>
        /// A soft ring whose colour runs round the rainbow, one unit wide: brightest just outside a
        /// ball of 0.8 of its half-width, so the part inside hides behind the bomb.
        /// </summary>
        private static Sprite RainbowRing()
        {
            if (rainbowSprite != null) return rainbowSprite;
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f;
                    float dy = (y + .5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float band = 1f - Mathf.Abs(distance - .86f) / .14f;
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(band));
                    float hue = Mathf.Repeat(Mathf.Atan2(dy, dx) / (Mathf.PI * 2f), 1f);
                    Color color = Color.HSVToRGB(hue, .75f, 1f);
                    pixels[y * size + x] = new Color32((byte)(color.r * 255f), (byte)(color.g * 255f),
                        (byte)(color.b * 255f), (byte)Mathf.RoundToInt(alpha * 255f));
                }
            rainbowSprite = CreateGeneratedSprite("Lab bomb rainbow ring", size, pixels);
            return rainbowSprite;
        }

        private static Sprite Square()
        {
            if (squareSprite != null) return squareSprite;
            const int size = 4;
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            squareSprite = CreateGeneratedSprite("Lab lock badge", size, pixels);
            return squareSprite;
        }

        private static Sprite CreateGeneratedSprite(string label, int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = label,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            sprite.name = label;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
