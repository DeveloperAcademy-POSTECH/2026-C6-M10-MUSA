using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 매혹 (#251): 홀린 사람의 조합판 양쪽 가장자리가 하트색으로 은은하게 빛나며 천천히 밝아졌다 어두워진다.
    /// 넘기기 막기(#215)의 빨간 빛처럼 구슬 가운데 평면에 깔려 구슬이 그 앞을 지나간다. 매혹이 풀리면 부드럽게 사라진다.
    /// 화면 표시만 한다.
    /// </summary>
    public sealed class LabCharmEdgeFlash : MonoBehaviour
    {
        private const float GlowShare = .14f;
        private const float DimAlpha = .22f, BrightAlpha = .4f, BreatheHz = .6f;
        /// <summary>How long the glow takes to fade away when the charm ends.</summary>
        public const float ReleaseSeconds = .45f;
        private const int GlowOrder = -21;
        /// <summary>The charm's heart colour, shared with its gauge.</summary>
        public static readonly Color HeartColor = new Color(.93f, .24f, .36f);
        private static Sprite glowSprite;

        private readonly SpriteRenderer[] sides = new SpriteRenderer[2];
        private readonly Vector3[] baseScale = new Vector3[2];
        private float age;
        private float releaseLeft = -1f;
        private float releaseFrom;
        private bool on;

        public bool Visible => sides[0] != null && sides[0].gameObject.activeSelf;
        /// <summary>Fading away as the charm ends.</summary>
        public bool Releasing => Visible && releaseLeft >= 0f;
        public float Brightness => sides[0] != null ? sides[0].color.a : 0f;
        /// <summary>The glow's sorting order; the board's paper plate must stay below it.</summary>
        public int GlowSortingOrder => GlowOrder;

        public static LabCharmEdgeFlash Create(Transform board)
        {
            var flash = new GameObject("Charm Edge Flash");
            flash.layer = board.gameObject.layer;
            flash.transform.SetParent(board, false);
            return flash.AddComponent<LabCharmEdgeFlash>();
        }

        /// <summary>Lays the glow along both side edges (world x), each over its own height range (bottom, top).</summary>
        public void Show(float leftX, float rightX, Vector2 leftRange, Vector2 rightRange, float boardWidth)
        {
            EnsureParts();
            if (!on || releaseLeft >= 0f) age = 0f;
            on = true;
            releaseLeft = -1f;
            Place(0, leftX, leftRange, boardWidth, 1f);
            Place(1, rightX, rightRange, boardWidth, -1f);
            foreach (SpriteRenderer side in sides) side.gameObject.SetActive(true);
            Pose();
        }

        /// <summary>The charm ended: the glow fades gently away.</summary>
        public void Release()
        {
            if (!on) return;
            on = false;
            releaseLeft = ReleaseSeconds;
            releaseFrom = Brightness;
        }

        public void Hide()
        {
            on = false;
            releaseLeft = -1f;
            foreach (SpriteRenderer side in sides)
                if (side != null) side.gameObject.SetActive(false);
        }

        private void Place(int index, float edgeX, Vector2 range, float boardWidth, float inward)
        {
            float height = Mathf.Max(.01f, range.y - range.x);
            sides[index].transform.position = new Vector3(edgeX, (range.x + range.y) * .5f, 0f);
            // The sprite grows inward from its pivot on the edge; a negative width mirrors it.
            baseScale[index] = new Vector3(inward * GlowShare * boardWidth, height, 1f);
        }

        private void Update()
        {
            if (!Visible) return;
            age += Time.deltaTime;
            if (releaseLeft >= 0f)
            {
                releaseLeft -= Time.deltaTime;
                if (releaseLeft < 0f)
                {
                    Hide();
                    return;
                }
            }
            Pose();
        }

        private void Pose()
        {
            // A soft glow that slowly brightens and dims, both edges together.
            float alpha = Mathf.Lerp(DimAlpha, BrightAlpha, .5f + .5f * Mathf.Sin(age * BreatheHz * 2f * Mathf.PI));
            if (releaseLeft >= 0f) alpha = Mathf.Lerp(0f, releaseFrom, releaseLeft / ReleaseSeconds);
            for (int i = 0; i < sides.Length; i++)
            {
                sides[i].transform.localScale = baseScale[i];
                sides[i].color = new Color(HeartColor.r, HeartColor.g, HeartColor.b, alpha);
            }
        }

        private void EnsureParts()
        {
            for (int i = 0; i < sides.Length; i++)
            {
                if (sides[i] != null) continue;
                var part = new GameObject(i == 0 ? "Charm Edge Flash Left" : "Charm Edge Flash Right");
                part.layer = gameObject.layer;
                part.transform.SetParent(transform, false);
                sides[i] = part.AddComponent<SpriteRenderer>();
                sides[i].sprite = GlowSprite();
                sides[i].sortingOrder = GlowOrder;
            }
        }

        /// <summary>One unit square: strongest at its left edge, fading inward.</summary>
        private static Sprite GlowSprite()
        {
            if (glowSprite != null) return glowSprite;
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fade = 1f - x / (size - 1f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(fade * fade * 255f));
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Lab charm edge flash",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            glowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0f, .5f), size);
            glowSprite.name = texture.name;
            glowSprite.hideFlags = HideFlags.DontSave;
            return glowSprite;
        }
    }
}
