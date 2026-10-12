using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Item presentation on the orb root: an icon on a disc, a badge (remaining unlock handoffs,
    /// or a relay orb's x1/x2/MAX stage) and a bomb's fuse seconds above the orb, all facing the
    /// front-on board camera. It follows the Visual mount's
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

        private SpriteRenderer disc;
        private SpriteRenderer icon;
        private SpriteRenderer badge;
        private TextMesh badgeCount;
        private TextMesh fuse;
        private Transform follow;
        private float radius;

        public bool IconVisible => icon != null && icon.gameObject.activeSelf;
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

        private void LateUpdate() => FollowMount();

        private void FollowMount()
        {
            Vector3 offset = follow != null ? follow.localPosition : Vector3.zero;
            transform.localPosition = new Vector3(offset.x, offset.y, -1.02f * radius);
        }

        private void EnsureParts()
        {
            if (disc != null) return;
            disc = CreateSprite("Icon Disc", Disc(), DiscColor, 0);
            icon = CreateSprite("Icon", null, Color.white, 1);
            badge = CreateSprite("Lock Badge", Square(), BadgeColor, 2);
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
            renderer.sortingOrder = 3;
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
