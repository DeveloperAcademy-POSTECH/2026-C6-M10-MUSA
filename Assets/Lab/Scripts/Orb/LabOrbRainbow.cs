using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 2:2 청홍 땅따먹기 (#281): a rainbow ring around an orb a fever made or turned. It sits behind the ball on the
    /// orb's root like the fire halo (#213), so it never rolls with the ball and the ball's own colour stays readable;
    /// the ring turns slowly and shimmers. A paused battle (time scale 0) pauses it. Sized in orb radii.
    /// </summary>
    public sealed class LabOrbRainbow : MonoBehaviour
    {
        /// <summary>The ring's default size in orb radii and brightest opacity (#293 tunes both in LabConfig).</summary>
        public const float DefaultRingScale = 2.9f, DefaultRingAlpha = 1f;
        private const float TurnDegreesPerSecond = 140f, ShimmerHz = 2.2f;
        private const int RingOrder = 0;
        private static Sprite ringSprite;
        private SpriteRenderer ring;
        private float age, peak = DefaultRingAlpha;

        public bool Visible => ring != null && ring.gameObject.activeSelf;

        public static LabOrbRainbow Create(Transform orbRoot)
        {
            var item = new GameObject("Fever Rainbow");
            item.layer = orbRoot.gameObject.layer;
            item.transform.SetParent(orbRoot, false);
            return item.AddComponent<LabOrbRainbow>();
        }

        public void Show(bool shown, float radius, float ringScale = DefaultRingScale, float ringAlpha = DefaultRingAlpha)
        {
            peak = Mathf.Clamp01(ringAlpha);
            if (ring == null)
            {
                if (!shown) return;
                var part = new GameObject("Rainbow Ring");
                part.layer = gameObject.layer;
                part.transform.SetParent(transform, false);
                ring = part.AddComponent<SpriteRenderer>();
                ring.sprite = RingSprite();
                ring.sortingOrder = RingOrder;
            }
            if (ring.gameObject.activeSelf != shown) ring.gameObject.SetActive(shown);
            ring.transform.localScale = Vector3.one * (Mathf.Max(.01f, ringScale) * Mathf.Max(.01f, radius));
        }

        private void Update()
        {
            if (!Visible) return;
            age += Time.deltaTime;
            ring.transform.localRotation = Quaternion.Euler(0f, 0f, -age * TurnDegreesPerSecond);
            ring.color = new Color(1f, 1f, 1f, peak * (.78f + .22f * Mathf.Sin(age * ShimmerHz * 2f * Mathf.PI)));
        }

        /// <summary>One unit across: a soft ring whose hue runs once around it.</summary>
        private static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size * 2f - 1f, v = (y + .5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(u * u + v * v);
                    // Strongest at .82 of the radius, fading softly to both sides.
                    float alpha = Mathf.Clamp01(1f - Mathf.Abs(distance - .82f) / .16f);
                    alpha *= alpha;
                    float hue = (Mathf.Atan2(v, u) / (2f * Mathf.PI) + 1f) % 1f;
                    Color color = Color.HSVToRGB(hue, .7f, 1f);
                    color.a = alpha;
                    pixels[y * size + x] = color;
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Lab fever rainbow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            ringSprite.name = "Lab fever rainbow";
            ringSprite.hideFlags = HideFlags.DontSave;
            return ringSprite;
        }
    }
}
