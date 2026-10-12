using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Item presentation on the orb root: an icon on a disc and a lock badge with the swipes
    /// still needed, all facing the front-on board camera. It follows the Visual mount's
    /// rise animation but not its rolling rotation, and owns no collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabOrbItemMarker : MonoBehaviour
    {
        private const int BadgeFontSize = 64;
        private static readonly Color DiscColor = new Color(.97f, .97f, .97f);
        private static readonly Color BadgeColor = new Color(.12f, .12f, .14f, .95f);
        private static readonly Dictionary<Texture2D, Sprite> iconSprites = new Dictionary<Texture2D, Sprite>();
        private static Sprite discSprite;
        private static Sprite squareSprite;

        private SpriteRenderer disc;
        private SpriteRenderer icon;
        private SpriteRenderer badge;
        private TextMesh badgeCount;
        private Transform follow;
        private float radius;

        public int ShownLockSwipes { get; private set; }
        public bool IconVisible => icon != null && icon.gameObject.activeSelf;
        public bool BadgeVisible => badge != null && badge.gameObject.activeSelf;
        public string BadgeText => badgeCount != null ? badgeCount.text : string.Empty;

        public static LabOrbItemMarker Create(Transform orbRoot)
        {
            var marker = new GameObject("Item Marker");
            marker.layer = orbRoot.gameObject.layer;
            marker.transform.SetParent(orbRoot, false);
            return marker.AddComponent<LabOrbItemMarker>();
        }

        /// <summary>Shows the icon (when assigned) and a badge while lockSwipes is above zero.</summary>
        public void Show(Texture2D iconTexture, int lockSwipes, float orbRadius, Transform followMount)
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

            ShownLockSwipes = Mathf.Max(0, lockSwipes);
            bool locked = ShownLockSwipes > 0;
            badge.gameObject.SetActive(locked);
            badge.transform.localPosition = new Vector3(.62f * radius, -.62f * radius, -.01f);
            badge.transform.localScale = Vector3.one * (.62f * radius);
            if (badgeCount != null)
            {
                badgeCount.gameObject.SetActive(locked);
                badgeCount.text = ShownLockSwipes.ToString();
                badgeCount.transform.localPosition = new Vector3(.62f * radius, -.62f * radius, -.02f);
                badgeCount.characterSize = .5f * radius / (BadgeFontSize * .1f);
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
            var count = new GameObject("Lock Count");
            count.layer = gameObject.layer;
            count.transform.SetParent(transform, false);
            badgeCount = count.AddComponent<TextMesh>();
            badgeCount.font = font;
            badgeCount.fontSize = BadgeFontSize;
            badgeCount.anchor = TextAnchor.MiddleCenter;
            badgeCount.alignment = TextAlignment.Center;
            badgeCount.color = Color.white;
            var renderer = count.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 3;
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
