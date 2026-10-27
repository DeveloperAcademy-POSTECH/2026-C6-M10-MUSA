using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// The fire on an orb a bomb blast flung (#213). Since #326 it is drawn from the appearance's fire pictures
    /// (red, orange and yellow cartoon flames with an ink outline) sitting on top of the ball, flipping through
    /// the frames so it licks, with the warm halo and rising embers around it. Without pictures it falls back to
    /// the original code-drawn yellow fire described below.
    /// The code-drawn fallback: Tongues of flame rise from behind the orb,
    /// where the ball hides their roots so its colour stays readable, with small licks over its top
    /// edge, a warm halo and rising embers. It burns for the seconds the Host gives, flares up as it
    /// catches and dies down at the end; a paused battle (time scale 0) pauses it. It sits on the
    /// orb's root, so it never rolls with the ball, and everything is sized in orb radii.
    /// </summary>
    public sealed class LabOrbBurn : MonoBehaviour
    {
        private const float CatchSeconds = .2f, DieSeconds = .45f, ResyncSeconds = .3f;
        private const int EmberCount = 5;
        private const float EmberSeconds = .9f;
        private const int HaloOrder = 0, EmberOrder = 4, FrontOrder = 8;
        private static readonly Color Outer = new Color(1f, .62f, .09f);
        private static readonly Color Middle = new Color(1f, .85f, .16f);
        private static readonly Color Core = new Color(1f, .98f, .72f);
        private static readonly Color EmberColor = new Color(1f, .86f, .22f);
        private static readonly Color HaloColor = new Color(1f, .85f, .16f, .5f);

        private readonly struct Tongue
        {
            public readonly Vector2 Base;
            public readonly float Width, Height, Lean;
            public readonly bool Front;

            public Tongue(float x, float y, float width, float height, float lean, bool front = false)
            {
                Base = new Vector2(x, y);
                Width = width;
                Height = height;
                Lean = lean;
                Front = front;
            }
        }

        // Base, width and height in orb radii and lean in degrees: five behind the ball, then three
        // short licks over its top edge.
        private static readonly Tongue[] Tongues =
        {
            new Tongue(0f, -.2f, 1.7f, 3f, 0f),
            new Tongue(-.6f, -.3f, 1.2f, 2.3f, 25f),
            new Tongue(.6f, -.3f, 1.15f, 2.15f, -25f),
            new Tongue(-.95f, -.6f, .8f, 1.4f, 48f),
            new Tongue(.95f, -.6f, .8f, 1.3f, -48f),
            new Tongue(-.45f, .62f, .42f, .95f, 16f, true),
            new Tongue(0f, .72f, .48f, 1.1f, 0f, true),
            new Tongue(.45f, .62f, .42f, .9f, -16f, true)
        };

        private sealed class Flame
        {
            public SpriteRenderer Renderer;
            public int Tongue;
            public float Share, Lift, Alpha, Seed;
        }

        private static Sprite tongueSprite;
        private static Sprite fadedTongueSprite;
        private static Sprite haloSprite;
        private static Sprite emberSprite;

        private Flame[] flames;
        // #326: the picture fire. art is null while the code-drawn fallback is used.
        private SpriteRenderer art;
        private Sprite[] artFrames;
        private float artFps = 12f, artWidth = 3.2f;
        private Vector2 artCentre = new Vector2(.5f, .0625f), artOffset;
        private bool artHalo = true, artEmbers = true;
        private SpriteRenderer halo;
        private readonly SpriteRenderer[] embers = new SpriteRenderer[EmberCount];
        private float radius = .32f;
        private float secondsLeft;
        private float age;
        private float life;
        private bool burning;

        /// <summary>Burning now; it goes out by itself when its seconds run down.</summary>
        public bool Burning => burning;
        /// <summary>Seconds this screen still shows it burning.</summary>
        public float SecondsLeft => burning ? secondsLeft : 0f;
        /// <summary>How big the fire is now: 0 out, about 1 at full, briefly above as it catches.</summary>
        public float Life => burning ? life : 0f;
        /// <summary>How high the tallest flame's tip reaches above the orb centre at full size, in world units.</summary>
        public float FullHeight => art != null && artFrames.Length > 0
            ? ((1f - artCentre.y) * artWidth * ArtAspect + artOffset.y) * radius
            : (Tongues[0].Base.y + Tongues[0].Height) * radius;
        /// <summary>The fire is drawn from pictures (#326) rather than the code-drawn fallback.</summary>
        public bool UsesArt => art != null;
        /// <summary>The picture shown now, as an index into the frames; -1 without pictures.</summary>
        public int ArtFrame { get; private set; } = -1;
        /// <summary>Depth of the picture fire: in front of the ball, so it sits on it.</summary>
        public float ArtDepth => art != null ? art.transform.localPosition.z : 0f;
        private float ArtAspect
        {
            get
            {
                Vector2 size = artFrames[0].bounds.size;
                return size.x > 0f ? size.y / size.x : 1f;
            }
        }
        /// <summary>Flame, halo and ember sprites shown now.</summary>
        public int PartsShown
        {
            get
            {
                int shown = 0;
                foreach (SpriteRenderer part in GetComponentsInChildren<SpriteRenderer>()) if (part.gameObject.activeSelf) shown++;
                return shown;
            }
        }
        /// <summary>Depth of the flames behind the ball's front (the ball hides their roots) and of the licks over it.</summary>
        public float BackDepth => flames != null && flames.Length > 0 ? flames[0].Renderer.transform.localPosition.z : 0f;
        public float FrontDepth
        {
            get
            {
                if (flames == null) return 0f;
                foreach (Flame flame in flames)
                    if (Tongues[flame.Tongue].Front) return flame.Renderer.transform.localPosition.z;
                return 0f;
            }
        }

        public static LabOrbBurn Create(Transform orbRoot)
        {
            var burn = new GameObject("Blast Fire");
            burn.layer = orbRoot.gameObject.layer;
            burn.transform.SetParent(orbRoot, false);
            return burn.AddComponent<LabOrbBurn>();
        }

        /// <summary>
        /// Burns for the Host's seconds. A fire already burning keeps its own countdown unless the
        /// Host's differs by more than a moment; zero or less lets it die down.
        /// </summary>
        public void Burn(float seconds, float orbRadius, LabOrbAppearance look = null)
        {
            if (flames == null && look != null) UseLook(look);
            SetRadius(orbRadius);
            if (!(seconds > 0f) || float.IsInfinity(seconds))
            {
                if (burning) secondsLeft = Mathf.Min(secondsLeft, DieSeconds * .5f);
                return;
            }
            EnsureParts();
            if (!burning)
            {
                burning = true;
                age = 0f;
                secondsLeft = seconds;
                SetShown(true);
            }
            else if (Mathf.Abs(secondsLeft - seconds) > ResyncSeconds) secondsLeft = seconds;
            Pose();
        }

        /// <summary>Follows the orb's radius, as when a blast shrinks it.</summary>
        public void SetRadius(float orbRadius)
        {
            if (orbRadius > 0f && !float.IsInfinity(orbRadius)) radius = orbRadius;
            if (burning) Pose();
        }

        private void Update()
        {
            if (!burning) return;
            float step = Time.deltaTime;
            age += step;
            secondsLeft -= step;
            if (secondsLeft <= 0f)
            {
                burning = false;
                SetShown(false);
                return;
            }
            Pose();
        }

        /// <summary>Takes the fire pictures and their settings from the appearance, once, before the fire is built.</summary>
        private void UseLook(LabOrbAppearance look)
        {
            Sprite[] frames = look.BlastFireFrames;
            artFrames = frames != null && frames.Length > 0 ? frames : null;
            artFps = look.BlastFireFramesPerSecond;
            artWidth = look.BlastFireWidth;
            artCentre = look.BlastFireOrbCentre;
            artOffset = look.BlastFireOffset;
            artHalo = look.BlastFireHalo;
            artEmbers = look.BlastFireEmbers;
        }

        private void Pose()
        {
            float caught = Mathf.Clamp01(age / CatchSeconds);
            // Catches with a short flare past full size, then dies down over its last moments.
            float flare = 1f + 2.2f * Mathf.Pow(caught - 1f, 3f) + 1.2f * Mathf.Pow(caught - 1f, 2f);
            life = Mathf.Max(0f, flare) * Mathf.Clamp01(secondsLeft / DieSeconds);

            if (art != null) PoseArt();

            foreach (Flame flame in flames)
            {
                Tongue tongue = Tongues[flame.Tongue];
                float flicker = .85f + .3f * Mathf.PerlinNoise(flame.Seed, age * 6f);
                float sway = (Mathf.PerlinNoise(flame.Seed + 7.3f, age * 4.8f) - .5f) * (tongue.Front ? 6f : 12f);
                Transform part = flame.Renderer.transform;
                // Behind: in the ball's middle, so its front hides their roots. Licks: just in front of it.
                part.localPosition = new Vector3(tongue.Base.x * radius, (tongue.Base.y + flame.Lift) * radius,
                    tongue.Front ? -1.05f * radius : 0f);
                part.localRotation = Quaternion.Euler(0f, 0f, tongue.Lean + sway);
                // The sprite is 1 wide and 2 tall.
                part.localScale = new Vector3(tongue.Width * flame.Share * life * radius,
                    tongue.Height * flame.Share * flicker * life * radius * .5f, 1f);
                Color color = flame.Renderer.color;
                color.a = flame.Alpha;
                flame.Renderer.color = color;
            }

            PoseGlow();
        }

        /// <summary>The picture fire: flips frames, grows with the fire's life and sits on top of the ball.</summary>
        private void PoseArt()
        {
            ArtFrame = Mathf.FloorToInt(age * artFps) % artFrames.Length;
            Sprite frame = artFrames[ArtFrame];
            art.sprite = frame;
            Bounds bounds = frame.bounds;
            float scale = bounds.size.x > 0f ? artWidth * radius * life / bounds.size.x : 0f;
            // The picture's orb-centre point lands on the ball's centre (plus the offset), so it grows out of the ball.
            Vector2 centre = new Vector2(bounds.min.x + artCentre.x * bounds.size.x, bounds.min.y + artCentre.y * bounds.size.y);
            Transform part = art.transform;
            part.localScale = new Vector3(scale, scale, 1f);
            part.localPosition = new Vector3(artOffset.x * radius - centre.x * scale, artOffset.y * radius - centre.y * scale,
                -1.05f * radius);
        }

        private void PoseGlow()
        {
            halo.transform.localPosition = new Vector3(0f, .25f * radius, 0f);
            halo.transform.localScale = Vector3.one * (4.4f * radius * Mathf.Min(1f, life));

            for (int i = 0; i < EmberCount; i++)
            {
                float run = age / EmberSeconds + i / (float)EmberCount;
                float phase = run - Mathf.Floor(run);
                int round = Mathf.FloorToInt(run);
                float start = Hash(i, round) * 1.4f - .7f;
                float x = start + Mathf.Sin(phase * Mathf.PI * 2f + i) * .12f;
                float y = 1.1f + phase * 1.9f;
                Transform ember = embers[i].transform;
                ember.localPosition = new Vector3(x * radius, y * radius, -1.05f * radius);
                ember.localScale = Vector3.one * (.16f * (1f - .5f * phase) * radius * Mathf.Min(1f, life));
                embers[i].color = new Color(EmberColor.r, EmberColor.g, EmberColor.b, 1f - phase);
            }
        }

        private void EnsureParts()
        {
            if (flames != null) return;
            halo = CreatePart("Fire Halo", Halo(), HaloColor, HaloOrder);
            if (artFrames != null)
            {
                // #326: one picture in front of the ball instead of the code-drawn tongues.
                art = CreatePart("Fire Art", artFrames[0], Color.white, FrontOrder + 4);
                flames = System.Array.Empty<Flame>();
                for (int i = 0; i < EmberCount; i++) embers[i] = CreatePart("Fire Ember " + i, Ember(), EmberColor, EmberOrder);
                SetShown(false);
                return;
            }
            var made = new System.Collections.Generic.List<Flame>();
            // Outer and middle layers on every tongue, a pale core on the three biggest behind.
            AddLayer(made, Outer, 1f, 0f, .95f, 1, 5);
            AddLayer(made, Middle, .78f, .08f, 1f, 2, 5);
            AddLayer(made, Core, .5f, .15f, 1f, 3, 3);
            flames = made.ToArray();
            for (int i = 0; i < EmberCount; i++) embers[i] = CreatePart("Fire Ember " + i, Ember(), EmberColor, EmberOrder);
            SetShown(false);
        }

        private void AddLayer(System.Collections.Generic.List<Flame> made, Color color, float share, float lift,
            float alpha, int order, int backTongues)
        {
            for (int i = 0; i < Tongues.Length; i++)
            {
                bool front = Tongues[i].Front;
                if (!front && i >= backTongues) continue;
                if (front && color == Core) continue;
                SpriteRenderer renderer = CreatePart("Fire Tongue " + i, front ? FadedTongue() : TongueSprite(),
                    color, front ? FrontOrder + order : order);
                made.Add(new Flame { Renderer = renderer, Tongue = i, Share = share, Lift = lift,
                    Alpha = front ? .9f * alpha : alpha, Seed = 11.7f * i + 3.1f * order });
            }
        }

        private void SetShown(bool shown)
        {
            if (flames == null) return;
            foreach (Flame flame in flames) flame.Renderer.gameObject.SetActive(shown);
            if (art != null) art.gameObject.SetActive(shown);
            halo.gameObject.SetActive(shown && (art == null || artHalo));
            foreach (SpriteRenderer ember in embers) ember.gameObject.SetActive(shown && (art == null || artEmbers));
        }

        private SpriteRenderer CreatePart(string label, Sprite sprite, Color color, int order)
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

        private static float Hash(int index, int round) =>
            Mathf.Repeat(Mathf.Sin(index * 12.9898f + round * 78.233f) * 43758.547f, 1f);

        /// <summary>A flame tongue 1 wide and 2 tall with its root at the pivot: a round bulb drawn up to a point.</summary>
        private static Sprite TongueSprite() =>
            tongueSprite != null ? tongueSprite : tongueSprite = CreateTongue("Lab fire tongue", false);

        /// <summary>The same tongue fading in from its root, for licks drawn over the ball.</summary>
        private static Sprite FadedTongue() =>
            fadedTongueSprite != null ? fadedTongueSprite : fadedTongueSprite = CreateTongue("Lab fire lick", true);

        private static Sprite CreateTongue(string label, bool fadeRoot)
        {
            const int width = 32, height = 64;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = (x + .5f) / width - .5f;
                    float v = (y + .5f) / height * 2f;
                    float edge = v < .5f
                        ? .5f - Mathf.Sqrt(u * u + (v - .5f) * (v - .5f))
                        : .5f * Mathf.Pow((2f - v) / 1.5f, 1.6f) - Mathf.Abs(u);
                    float alpha = Mathf.Clamp01(edge / .06f);
                    if (fadeRoot) alpha *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.15f, 1f, v));
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            return CreateGeneratedSprite(label, width, height, pixels, new Vector2(.5f, 0f));
        }

        /// <summary>A soft warm disc, 1 unit across.</summary>
        private static Sprite Halo()
        {
            if (haloSprite != null) return haloSprite;
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size * .5f, dy = y + .5f - size * .5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) / (size * .5f);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            return haloSprite = CreateGeneratedSprite("Lab fire halo", size, size, pixels, new Vector2(.5f, .5f));
        }

        /// <summary>A small hard dot, 1 unit across.</summary>
        private static Sprite Ember()
        {
            if (emberSprite != null) return emberSprite;
            const int size = 16;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size * .5f, dy = y + .5f - size * .5f;
                    float alpha = Mathf.Clamp01(size * .5f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            return emberSprite = CreateGeneratedSprite("Lab fire ember", size, size, pixels, new Vector2(.5f, .5f));
        }

        private static Sprite CreateGeneratedSprite(string label, int width, int height, Color32[] pixels, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = label,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            // One unit per texture width, so a tongue is 1 wide and 2 tall.
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, width);
            sprite.name = label;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
