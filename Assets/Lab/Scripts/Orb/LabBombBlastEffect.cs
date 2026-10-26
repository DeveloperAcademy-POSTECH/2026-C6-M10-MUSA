using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// A cartoon blast drawn on the orb board where a locked bomb went off: a jagged star burst in
    /// red, orange and yellow, a ring racing out to the blast reach, puffs of smoke, and a big
    /// hand-lettered "뻥이요!" (#305, a cream outline behind the ink keeps it readable on the burst) that pops
    /// and floats away. Presentation only: it owns no collider, never touches physics, and removes itself when done.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabBombBlastEffect : MonoBehaviour
    {
        public const string Text = "뻥이요!";
        public const string ArtPath = "Bomb/BombPpeong";
        public const string OutlinePath = "Bomb/BombPpeongOutline";
        public const float Seconds = .9f;
        // In front of every orb, drum and item marker on the board plane (z = 0), facing its camera.
        private const float FrontZ = -1.5f;
        // The lettering's height, in blast reaches.
        private const float LabelHeight = .45f;
        private const int PuffCount = 7;
        private static readonly Color[] BurstColors =
        {
            new Color(.86f, .13f, .1f), new Color(1f, .55f, .08f), new Color(1f, .93f, .4f)
        };
        private static readonly float[] BurstSizes = { 1.45f, 1.1f, .7f };
        private static readonly float[] BurstTurns = { 0f, 9f, 21f };
        private static readonly Color RingColor = new Color(1f, .97f, .85f);
        private static readonly Color PuffColor = new Color(.42f, .38f, .36f);
        private static Sprite starSprite;
        private static Sprite ringSprite;
        private static Sprite puffSprite;

        private readonly SpriteRenderer[] bursts = new SpriteRenderer[3];
        private readonly SpriteRenderer[] puffs = new SpriteRenderer[PuffCount];
        private readonly Vector2[] puffDirections = new Vector2[PuffCount];
        private SpriteRenderer ring;
        private SpriteRenderer label;
        private SpriteRenderer labelOutline;
        private float labelScale;
        private float reach;
        private float elapsed;

        public string Label => label != null && label.sprite != null ? Text : string.Empty;

        /// <summary>Starts a blast at a board point (world XY); reach is the shockwave radius in world units.</summary>
        public static LabBombBlastEffect Play(Transform board, Vector2 center, float reach)
        {
            var root = new GameObject("Bomb Blast Effect");
            root.layer = board.gameObject.layer;
            root.transform.SetParent(board, false);
            root.transform.position = new Vector3(center.x, center.y, FrontZ);
            var effect = root.AddComponent<LabBombBlastEffect>();
            effect.Build(Mathf.Max(.01f, reach));
            return effect;
        }

        private void Build(float blastReach)
        {
            reach = blastReach;
            ring = CreateSprite("Shock Ring", Ring(), RingColor, 29);
            for (int i = 0; i < PuffCount; i++)
            {
                puffs[i] = CreateSprite("Smoke Puff", Puff(), PuffColor, 28);
                float angle = (i + .5f * Wobble(i)) / PuffCount * Mathf.PI * 2f;
                puffDirections[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            for (int i = 0; i < bursts.Length; i++)
            {
                bursts[i] = CreateSprite("Burst", Star(), BurstColors[i], 30 + i);
                bursts[i].transform.localRotation = Quaternion.Euler(0f, 0f, BurstTurns[i]);
            }
            Sprite art = Resources.Load<Sprite>(ArtPath), outline = Resources.Load<Sprite>(OutlinePath);
            if (art != null)
            {
                if (outline != null) labelOutline = CreateSprite("뻥이요 Outline", outline, Color.white, 33);
                label = CreateSprite("뻥이요", art, Color.white, 34);
                labelScale = LabelHeight * reach / Mathf.Max(.0001f, art.bounds.size.y);
            }
            Animate();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            Animate();
            if (elapsed >= Seconds) Destroy(gameObject);
        }

        private void Animate()
        {
            float t = elapsed;
            // The burst pops past full size, settles, then shrinks a little as it fades.
            float pop = t < .12f ? EaseOut(t / .12f) * 1.15f : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - .12f) / .1f));
            float fade = 1f - Mathf.Clamp01((t - .45f) / .3f);
            for (int i = 0; i < bursts.Length; i++)
            {
                bursts[i].transform.localScale = Vector3.one * (reach * BurstSizes[i] * pop * Mathf.Lerp(.85f, 1f, fade));
                bursts[i].transform.localRotation = Quaternion.Euler(0f, 0f, BurstTurns[i] + t * (i % 2 == 0 ? 25f : -25f));
                bursts[i].color = WithAlpha(BurstColors[i], fade);
            }

            // The ring races out to the shockwave's reach and fades as it gets there.
            float race = Mathf.Clamp01(t / .3f);
            ring.transform.localScale = Vector3.one * (2f * reach * EaseOut(race));
            ring.color = WithAlpha(RingColor, 1f - race);

            // Smoke drifts outward, swelling and thinning.
            float drift = Mathf.Clamp01(t / .7f);
            for (int i = 0; i < PuffCount; i++)
            {
                puffs[i].transform.localPosition = (Vector3)(puffDirections[i] * reach * .75f * EaseOut(drift))
                    + new Vector3(0f, 0f, .01f);
                puffs[i].transform.localScale = Vector3.one * (reach * .35f * (.6f + drift));
                puffs[i].color = WithAlpha(PuffColor, .7f * (1f - drift));
            }

            if (label == null) return;
            // "뻥이요!" pops in, holds, then floats up and fades.
            float textPop = t < .15f ? EaseOut(t / .15f) * 1.25f : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - .15f) / .1f));
            float leave = Mathf.Clamp01((t - .6f) / .3f);
            float rise = reach * (.1f + .25f * leave);
            label.transform.localScale = Vector3.one * (labelScale * textPop);
            label.transform.localPosition = new Vector3(0f, rise, -.02f);
            label.color = WithAlpha(Color.white, 1f - leave);
            if (labelOutline == null) return;
            labelOutline.transform.localScale = label.transform.localScale;
            labelOutline.transform.localPosition = new Vector3(0f, rise, -.01f);
            labelOutline.color = WithAlpha(Color.white, 1f - leave);
        }

        private SpriteRenderer CreateSprite(string partName, Sprite sprite, Color color, int order)
        {
            var part = new GameObject(partName);
            part.layer = gameObject.layer;
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
        // A fixed 0..1 value per index, so spikes and puffs look hand-drawn rather than regular.
        private static float Wobble(int index) => Mathf.Repeat(Mathf.Sin(index * 12.9898f + 1.7f) * 43758.547f, 1f);

        /// <summary>A jagged comic burst: ten sharp, uneven, leaning spikes around a solid centre, one unit wide.</summary>
        private static Sprite Star()
        {
            if (starSprite != null) return starSprite;
            const int size = 128;
            const int spikes = 10;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f;
                    float dy = (y + .5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float turn = (Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 1f) * spikes;
                    int spike = (int)turn % spikes;
                    float along = turn - Mathf.Floor(turn);
                    // Each spike's point leans a little to one side, like a hand drawing.
                    float peak = .5f + .3f * (Wobble(spike + 31) - .5f);
                    float tip = along < peak ? along / peak : (1f - along) / (1f - peak);
                    float edge = Mathf.Lerp(.6f, .84f + .16f * Wobble(spike), tip);
                    float alpha = Mathf.Clamp01((edge - radius) * size * .5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            starSprite = CreateGeneratedSprite("Lab bomb burst", size, pixels);
            return starSprite;
        }

        /// <summary>A thin ring whose outer edge is the sprite edge.</summary>
        private static Sprite Ring()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128;
            const float thickness = 5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size * .5f;
                    float dy = y + .5f - size * .5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(size * .5f - distance) * Mathf.Clamp01(distance - (size * .5f - thickness));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            ringSprite = CreateGeneratedSprite("Lab bomb ring", size, pixels);
            return ringSprite;
        }

        /// <summary>A soft round puff, densest in the middle.</summary>
        private static Sprite Puff()
        {
            if (puffSprite != null) return puffSprite;
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f;
                    float dy = (y + .5f) / size * 2f - 1f;
                    float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            puffSprite = CreateGeneratedSprite("Lab bomb puff", size, pixels);
            return puffSprite;
        }

        private static Sprite CreateGeneratedSprite(string spriteName, int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = spriteName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
