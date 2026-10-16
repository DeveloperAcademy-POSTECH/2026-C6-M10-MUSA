using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Item presentation on the orb root: an icon on a disc, a padlock over the dimmed icon while
    /// the item is locked and a badge (remaining unlock handoffs, or a relay orb's x1/x2/MAX stage),
    /// all facing the front-on board camera. A bomb with cartoon art shows that art instead, with
    /// the seconds left on its fuse in red on the ball and a spark flickering on the fuse while it
    /// burns. As the bomb moves it rolls: the ball and its highlight stay put like a lit sphere
    /// while the cap and fuse tilt and swing around it in 3D, never turning away behind it. On unlock the padlock rises and fades while the icon sharpens, then a gold
    /// ring shrinks onto the orb and the orb glows once. It follows the Visual mount's rise
    /// animation but not its rolling rotation, and owns no collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabOrbItemMarker : MonoBehaviour
    {
        private const int BadgeFontSize = 64;
        private static readonly Color DiscColor = new Color(.97f, .97f, .97f);
        private static readonly Color BadgeColor = new Color(.12f, .12f, .14f, .95f);
        private static readonly Color TimeOutlineColor = Color.white;
        private static readonly Color SparkColor = new Color(1f, .93f, .45f);
        private static readonly Color SparkDotColor = new Color(1f, .62f, .15f);
        // The white outline around the bomb's red seconds, in orb radii, drawn by offset copies.
        private const float TimeOutline = .06f;
        private static readonly Vector2[] OutlineDirections =
        {
            new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f)
        };
        private const int SparkDotCount = 3;
        // Sorting over the bomb's ball (order 0): the cap and fuse, then the spark.
        private const int FuseOrder = 1, SparkOrder = 6;
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
        private Vector2 fuseTip;
        private float ballRadius;
        private Vector3 lastRootPosition;
        private bool hasLastRootPosition;
        private TextMesh bombTime;
        private readonly TextMesh[] bombTimeOutline = new TextMesh[4];
        private SpriteRenderer spark;
        private readonly SpriteRenderer[] sparkDots = new SpriteRenderer[SparkDotCount];
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
        public bool BombTimeVisible => bombTime != null && bombTime.gameObject.activeSelf;
        /// <summary>The seconds left on the fuse, written on the bomb art; empty when it has no fuse.</summary>
        public string BombTimeText => BombTimeVisible ? bombTime.text : string.Empty;
        public Color BombTimeColor => bombTime != null ? bombTime.color : Color.clear;
        /// <summary>The spark flickering on the bomb art's fuse while a locked bomb burns.</summary>
        public bool SparkVisible => spark != null && spark.gameObject.activeSelf;
        public bool FuseArtVisible => fuseArt != null && fuseArt.gameObject.activeInHierarchy;
        /// <summary>How the bomb has rolled from the pose drawn in its images.</summary>
        public Quaternion BombTurn => bombTurn;
        /// <summary>Where the rolled cap points; it never turns away from the camera (z stays at or below 0).</summary>
        public Vector3 CapDirection { get; private set; }
        public int FuseSortingOrder => fuseArt != null ? fuseArt.sortingOrder : 0;
        /// <summary>The fuse tip, where the spark burns, from the ball centre.</summary>
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
        /// timeText (the seconds left on its fuse) in red on its ball; null or empty hides it.
        /// </summary>
        public void ShowBomb(LabOrbAppearance look, string timeText, float orbRadius, Transform followMount)
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
            fuseTip = look.BombFuseTip * ballRadius;
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

            bool counting = !string.IsNullOrEmpty(timeText);
            if (bombTime == null) return;
            float size = look.BombTimeSize * radius / (BadgeFontSize * .1f);
            bombTime.gameObject.SetActive(counting);
            bombTime.text = counting ? timeText : string.Empty;
            bombTime.color = look.BombTimeColor;
            bombTime.characterSize = size;
            bombTime.transform.localPosition = new Vector3(0f, 0f, -.02f);
            for (int i = 0; i < bombTimeOutline.Length; i++)
            {
                TextMesh outline = bombTimeOutline[i];
                outline.gameObject.SetActive(counting);
                outline.text = bombTime.text;
                outline.characterSize = size;
                Vector2 offset = OutlineDirections[i] * (TimeOutline * radius);
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
                if (bombTime != null) bombTime.gameObject.SetActive(false);
                foreach (TextMesh outline in bombTimeOutline)
                    if (outline != null) outline.gameObject.SetActive(false);
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
            // The bomb art never shows a padlock.
            bool covered = locked && lockTexture != null && !bombMode;
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
            unlockLook = LookOrDefault(look);
            unlockFromAlpha = icon.color.a;
            unlockElapsed = 0f;
            AnimateUnlock();
        }

        /// <summary>
        /// A locked bomb's seconds left on its fuse (negative when it has none). The bomb art's
        /// spark flickers while it burns. In its last seconds the bomb flashes red, faster and
        /// faster as the fuse runs out. Call after SetLocked.
        /// </summary>
        public void SetFuse(float secondsLeft, LabOrbAppearance look)
        {
            EnsureParts();
            fuseLeft = secondsLeft;
            fuseLook = LookOrDefault(look);
            SetSpark(bombMode && fuseLeft >= 0f);
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

            // The spark rides the fuse tip through the same aim and shortening.
            var aim = new Vector2(Mathf.Cos(facing), Mathf.Sin(facing));
            var side = new Vector2(-aim.y, aim.x);
            var tipSide = new Vector2(-along.y, along.x);
            FuseTipPosition = aim * (Vector2.Dot(fuseTip, along) * reach) + side * Vector2.Dot(fuseTip, tipSide);
            sparkOffset = FuseTipPosition;
        }

        private void SetSpark(bool burning)
        {
            spark.gameObject.SetActive(burning);
            foreach (SpriteRenderer dot in sparkDots) dot.gameObject.SetActive(burning);
            if (burning) AnimateSpark(0f);
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
            spark.transform.localPosition = new Vector3(sparkOffset.x, sparkOffset.y, -.01f);
            spark.transform.localScale = Vector3.one * (radius * Mathf.Lerp(.45f, .9f, flicker));
            spark.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.PerlinNoise(.91f, sparkTime * 6f) * 180f);
            spark.color = new Color(SparkColor.r, SparkColor.g, SparkColor.b, Mathf.Lerp(.65f, 1f, flicker));
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
            spark = CreateSprite("Fuse Spark", Spark(), SparkColor, SparkOrder);
            for (int i = 0; i < SparkDotCount; i++) sparkDots[i] = CreateSprite("Fuse Spark Dot", Glow(), SparkDotColor, SparkOrder);
            SetSpark(false);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;
            badgeCount = CreateText("Lock Count", font, Color.white);
            for (int i = 0; i < bombTimeOutline.Length; i++)
            {
                bombTimeOutline[i] = CreateText("Bomb Time Outline", font, TimeOutlineColor, 4, FontStyle.Bold);
                bombTimeOutline[i].gameObject.SetActive(false);
            }
            bombTime = CreateText("Bomb Time", font, Color.red, 5, FontStyle.Bold);
            bombTime.gameObject.SetActive(false);
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

        /// <summary>A small eight-pointed star for the burning fuse tip, one unit wide.</summary>
        private static Sprite Spark()
        {
            if (sparkSprite != null) return sparkSprite;
            const int size = 64;
            const int points = 8;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f;
                    float dy = (y + .5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float turn = (Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 1f) * points;
                    float along = Mathf.Abs(turn - Mathf.Floor(turn) - .5f) * 2f; // 1 at a point, 0 between
                    float edge = Mathf.Lerp(.35f, 1f, along * along);
                    float alpha = Mathf.Clamp01((edge - distance) * size * .5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            sparkSprite = CreateGeneratedSprite("Lab fuse spark", size, pixels);
            return sparkSprite;
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
