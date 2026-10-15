using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Item presentation on the orb root: an icon on a disc, a padlock over the dimmed icon while
    /// the item is locked, a badge (remaining unlock handoffs, or a relay orb's x1/x2/MAX stage)
    /// and a bomb's fuse seconds above the orb, all facing the front-on board camera. On unlock
    /// the padlock rises and fades while the icon sharpens, then a gold ring shrinks onto the orb
    /// and the orb glows once. It follows the Visual mount's
    /// rise animation but not its rolling rotation, and owns no collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabOrbItemMarker : MonoBehaviour
    {
        private const int BadgeFontSize = 64;
        private static readonly Color DiscColor = new Color(.97f, .97f, .97f);
        private static readonly Color BadgeColor = new Color(.12f, .12f, .14f, .95f);
        private static readonly Color FuseColor = new Color(.95f, .2f, .15f);
        private static readonly Dictionary<Texture2D, Sprite> iconSprites = new Dictionary<Texture2D, Sprite>();
        private static Sprite discSprite;
        private static Sprite squareSprite;
        private static Sprite ringSprite;
        private static Sprite glowSprite;
        private static LabOrbAppearance defaultLook;

        private SpriteRenderer disc;
        private SpriteRenderer icon;
        private SpriteRenderer padlock;
        private SpriteRenderer ring;
        private SpriteRenderer glow;
        private SpriteRenderer warningGlow;
        private SpriteRenderer badge;
        private TextMesh badgeCount;
        private TextMesh fuse;
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
        public bool FuseVisible => fuse != null && fuse.gameObject.activeSelf;
        public string FuseText => fuse != null ? fuse.text : string.Empty;

        public static LabOrbItemMarker Create(Transform orbRoot)
        {
            var marker = new GameObject("Item Marker");
            marker.layer = orbRoot.gameObject.layer;
            marker.transform.SetParent(orbRoot, false);
            return marker.AddComponent<LabOrbItemMarker>();
        }

        /// <summary>Shows the icon (when assigned), badge and fuse; null or empty text hides that part.</summary>
        public void Show(Texture2D iconTexture, string badgeText, string fuseText, float orbRadius, Transform followMount)
        {
            EnsureParts();
            radius = orbRadius;
            follow = followMount;
            FollowMount();

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
            if (fuse != null)
            {
                bool burning = !string.IsNullOrEmpty(fuseText);
                fuse.gameObject.SetActive(burning);
                fuse.text = burning ? fuseText : string.Empty;
                fuse.transform.localPosition = new Vector3(0f, 1.3f * radius, -.02f);
                fuse.characterSize = .7f * radius / (BadgeFontSize * .1f);
            }
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
            bool covered = locked && lockTexture != null;
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
        /// A locked bomb's seconds left on its fuse (negative when it has none). In its last seconds
        /// the bomb flashes red, faster and faster as the fuse runs out. Call after SetLocked.
        /// </summary>
        public void SetFuse(float secondsLeft, LabOrbAppearance look)
        {
            EnsureParts();
            fuseLeft = secondsLeft;
            fuseLook = LookOrDefault(look);
            AnimateWarning(0f);
        }

        private void LateUpdate()
        {
            FollowMount();
            AnimateWarning(Time.deltaTime);
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
            warningGlow.gameObject.SetActive(false);
        }

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
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;
            badgeCount = CreateText("Lock Count", font, Color.white);
            fuse = CreateText("Fuse Seconds", font, FuseColor);
        }

        private TextMesh CreateText(string label, Font font, Color color)
        {
            var part = new GameObject(label);
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            var text = part.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = BadgeFontSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 4;
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
