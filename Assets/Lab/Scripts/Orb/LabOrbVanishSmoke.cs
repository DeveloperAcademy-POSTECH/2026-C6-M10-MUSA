using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// The puff of smoke an orb leaves when its lifetime runs out (#235): a few soft warm-gray puffs
    /// start dense where the ball was, so the ball seems to go up in smoke, then swell, drift up and
    /// fade. Presentation only; it removes itself when done. Sized in orb radii, in front of the board.
    /// </summary>
    public sealed class LabOrbVanishSmoke : MonoBehaviour
    {
        private const int SmokeOrder = 14;
        // Each puff: offset (orb radii) and size (share of the smoke size).
        private static readonly Vector3[] Puffs =
        {
            new Vector3(0f, .05f, 1f), new Vector3(-.45f, .2f, .7f), new Vector3(.45f, .15f, .7f),
            new Vector3(-.2f, .55f, .6f), new Vector3(.25f, .6f, .55f), new Vector3(0f, .9f, .5f)
        };
        private static Sprite puffSprite;

        private readonly SpriteRenderer[] puffs = new SpriteRenderer[Puffs.Length];
        private float radius;
        private float size;
        private float seconds;
        private Color color;
        private float age;

        /// <summary>How far through its puff it is, 0 to 1.</summary>
        public float Progress => seconds > 0f ? Mathf.Clamp01(age / seconds) : 1f;
        public int PuffCount => puffs.Length;

        /// <summary>Lets an orb of this radius go up in smoke at this world position.</summary>
        public static LabOrbVanishSmoke Play(Transform parent, Vector3 worldPosition, float orbRadius, LabOrbAppearance look)
        {
            look = look != null ? look : LabOrbAppearance.Defaults;
            var root = new GameObject("Orb Vanish Smoke");
            if (parent != null)
            {
                root.layer = parent.gameObject.layer;
                root.transform.SetParent(parent, false);
            }
            root.transform.position = new Vector3(worldPosition.x, worldPosition.y, worldPosition.z - 1.2f * orbRadius);
            var smoke = root.AddComponent<LabOrbVanishSmoke>();
            smoke.radius = Mathf.Max(.01f, orbRadius);
            smoke.size = look.ExpirySmokeSize;
            smoke.seconds = look.ExpirySmokeSeconds;
            smoke.color = look.ExpirySmokeColor;
            for (int i = 0; i < smoke.puffs.Length; i++)
            {
                var part = new GameObject("Puff " + i);
                part.layer = root.layer;
                part.transform.SetParent(root.transform, false);
                var renderer = part.AddComponent<SpriteRenderer>();
                renderer.sprite = PuffSprite();
                renderer.sortingOrder = SmokeOrder;
                smoke.puffs[i] = renderer;
            }
            smoke.Pose();
            return smoke;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= seconds)
            {
                Destroy(gameObject);
                return;
            }
            Pose();
        }

        private void Pose()
        {
            float t = Progress;
            float grow = .55f + .9f * t;
            float alpha = color.a * Mathf.Pow(1f - t, 1.3f);
            for (int i = 0; i < puffs.Length; i++)
            {
                Vector3 puff = Puffs[i];
                Transform part = puffs[i].transform;
                part.localPosition = new Vector3(puff.x * (1f + .6f * t) * radius, (puff.y + .9f * t) * radius, -.001f * i);
                part.localScale = Vector3.one * (2f * puff.z * size * grow * radius);
                puffs[i].color = new Color(color.r, color.g, color.b, alpha);
            }
        }

        /// <summary>A soft round puff, 1 unit across, thickest in the middle.</summary>
        private static Sprite PuffSprite()
        {
            if (puffSprite != null) return puffSprite;
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float alpha = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Lab orb vanish puff",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            puffSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            puffSprite.name = texture.name;
            puffSprite.hideFlags = HideFlags.DontSave;
            return puffSprite;
        }
    }
}
