using System;
using System.Collections.Generic;
using C6.Prototype.Presentation;
using UnityEngine;

namespace C6.Prototype.Orbs
{
    /// <summary>Local orb artwork and selection shape. Never changes host data or runs physics.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public sealed class OrbView : MonoBehaviour
    {
        private const int CircleResolution = 128;
        private static readonly Color Ivory = new Color(0.94f, 0.94f, 0.84f, 1f);
        private static readonly Color Dark = new Color(0.09f, 0.16f, 0.20f, 1f);
        private static readonly Color Gold = new Color(0.89f, 0.74f, 0.44f, 1f);
        private static readonly Color Teal = new Color(0.38f, 0.82f, 0.75f, 1f);
        private static readonly Color Muted = new Color(0.31f, 0.43f, 0.45f, 1f);
        private static readonly Dictionary<float, CircleAsset> Circles = new Dictionary<float, CircleAsset>();
        private static Texture2D circleTexture;
        private static Material spriteMaterial;
        private static bool ownsMaterial;
        private static int liveViews;
        private static OrbArtSet artwork;

        private SpriteRenderer ring;
        private SpriteRenderer core;
        private SpriteRenderer firstDot;
        private SpriteRenderer secondDot;
        private SpriteRenderer art;
        private bool labelHidden;
        private TextMesh label;
        private MeshRenderer labelRenderer;
        private CircleAsset circle;
        private OrbKind kind;
        private OrbPolarity polarity;
        private string idleLabel;
        private float labelGlyphHeight = 64f;
        private float maximumLabelHalfWidthGlyphs;
        private bool initialized;
        private LocalOrbState localState;
        private bool heldFeedbackEnabled;
        private Transform heldArtwork;
        private SpriteRenderer heldHaloOuter;
        private SpriteRenderer heldHaloInner;
        private SpriteRenderer heldShadow;
        private Vector3 idleLabelPosition;

        public string OrbId { get; private set; }
        /// <summary>오행 v1: Raw element (Combined uses YinElement/YangElement). None when elements are off.</summary>
        public OrbElement Element { get; private set; }
        public OrbElement YinElement { get; private set; }
        public OrbElement YangElement { get; private set; }
        public CircleCollider2D Collider { get; private set; }
        public SpriteRenderer RingRenderer => ring;
        public LocalOrbState LocalState => localState;
        public bool HeldFeedbackActive => initialized && heldFeedbackEnabled && isActiveAndEnabled &&
            localState == LocalOrbState.Dragging;
        public float HeldScale => heldArtwork == null ? 1f : heldArtwork.localScale.x;

        /// <summary>Use a scene-referenced URP sprite material so player builds retain its shader.</summary>
        public static void SetSharedMaterial(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (spriteMaterial == material) return;
            if (liveViews > 0 && spriteMaterial != material)
                throw new InvalidOperationException("Set the orb material before creating fixture views.");
            if (ownsMaterial && spriteMaterial != material)
                ReleaseObject(spriteMaterial);
            spriteMaterial = material;
            ownsMaterial = false;
        }

        /// <summary>#4: optional sprite artwork for views configured after this call. Null keeps generated circles.</summary>
        public static void SetArtwork(OrbArtSet set) => artwork = set;

        public void Configure(string orbId, OrbKind orbKind, OrbPolarity orbPolarity, int layer, float radiusWorld, string displayLabel = null)
        {
            if (string.IsNullOrWhiteSpace(orbId)) throw new ArgumentException("An orb ID is required.", nameof(orbId));
            if (layer < 0 || layer > 31) throw new ArgumentOutOfRangeException(nameof(layer));
            if (float.IsNaN(radiusWorld) || float.IsInfinity(radiusWorld) || radiusWorld <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radiusWorld));
            if (initialized) throw new InvalidOperationException("An OrbView is configured once for one ID.");

            EnsureMaterial();
            circle = AcquireCircle(radiusWorld);
            ++liveViews;
            initialized = true;
            OrbId = orbId;
            kind = orbKind;
            polarity = orbPolarity;
            name = "OrbView-" + orbId;
            gameObject.layer = layer;
            ring = GetComponent<SpriteRenderer>();
            SetRenderer(ring, 40);
            Collider = GetComponent<CircleCollider2D>();
            Collider.radius = radiusWorld;
            Collider.offset = Vector2.zero;
            Collider.isTrigger = true;

            core = Disc("Core", layer, 0.82f, Vector3.zero, 41);
            bool combined = kind == OrbKind.Combined;
            firstDot = Disc("FirstCore", layer, combined ? 0.27f : 0.19f,
                new Vector3(combined ? -radiusWorld * 0.26f : 0f, radiusWorld * 0.08f, 0f), 42);
            if (combined)
                secondDot = Disc("SecondCore", layer, 0.27f,
                    new Vector3(radiusWorld * 0.26f, -radiusWorld * 0.08f, 0f), 43);
            if (combined)
            {
                OrbElements.CombinedElements(orbId, out var yinElement, out var yangElement);
                YinElement = yinElement; YangElement = yangElement; Element = OrbElement.None;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.Log("C6_ORBART  COMBINED  id=" + orbId
                    + "  encoded=" + OrbElements.TryDecodeCombinedId(orbId, out _, out _)
                    + "  yin=" + yinElement + "  yang=" + yangElement
                    + "  art=comb_" + yinElement.ToString().ToLowerInvariant()
                    + "_" + yangElement.ToString().ToLowerInvariant());
#endif
            }
            else
            {
                Element = OrbElements.RawElement(orbId);
                YinElement = orbPolarity == OrbPolarity.Yin ? Element : OrbElement.None;
                YangElement = orbPolarity == OrbPolarity.Yang ? Element : OrbElement.None;
            }
            var artSprite = artwork == null ? null : combined
                ? artwork.CombinedSpriteFor(YinElement, YangElement)
                : artwork.RawSprite(Element, orbPolarity == OrbPolarity.Yin);
            if (artSprite != null)
            {
                // The sprite's full width maps to the collider diameter, so the drawn circle edge is the hit edge.
                var artObject = new GameObject("Artwork", typeof(SpriteRenderer));
                artObject.layer = layer;
                artObject.transform.SetParent(transform, false);
                float fit = radiusWorld * 2f / Mathf.Max(0.0001f, artSprite.bounds.size.x);
                artObject.transform.localScale = new Vector3(fit, fit, 1f);
                art = artObject.GetComponent<SpriteRenderer>();
                art.sprite = artSprite;
                art.sharedMaterial = spriteMaterial;
                art.sortingOrder = 44;
                labelHidden = artwork.HideLabels;
            }

            var labelObject = new GameObject("OrbLabel", typeof(TextMesh));
            labelObject.layer = layer;
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, -radiusWorld * 1.30f, -0.01f);
            label = labelObject.GetComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 64;
            // TextMesh multiplies font glyph units by characterSize / 10. Font size is
            // atlas detail, so account for actual capital height instead of multiplying it twice.
            label.font.RequestCharactersInTexture("YIN YANG COMBINED LOCKED", label.fontSize);
            if (label.font.GetCharacterInfo('M', out var capital, label.fontSize))
                labelGlyphHeight = Mathf.Max(1f, capital.maxY - capital.minY);
            SetLabelLocalHeight(radiusWorld * 0.56f);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.richText = false;
            labelRenderer = label.GetComponent<MeshRenderer>();
            labelRenderer.sharedMaterial = label.font.material;
            labelRenderer.sortingOrder = 45;
            idleLabel = string.IsNullOrEmpty(displayLabel) ? (combined ? "COMBINED" : polarity == OrbPolarity.Yin ? "YIN" : "YANG") : displayLabel;
            label.font.RequestCharactersInTexture(idleLabel + " LOCKED", label.fontSize);
            // Cache both states now, before any renderer bounds exist. Reserving an action
            // cannot make a wider LOCKED caption suddenly spill past the viewport edge.
            maximumLabelHalfWidthGlyphs = Mathf.Max(MeasureLabelHalfWidth(idleLabel), MeasureLabelHalfWidth("LOCKED"));
            idleLabelPosition = label.transform.localPosition;
            SetLocalState(LocalOrbState.Idle);
            if (heldFeedbackEnabled) EnsureHeldArtwork();
        }

        /// <summary>
        /// Optional T09 held feedback. Only child artwork changes; the selection collider,
        /// canonical position and root scale remain controlled by the input controller.
        /// Earlier scenes retain their original presentation unless they explicitly opt in.
        /// </summary>
        public void SetHeldFeedbackEnabled(bool value)
        {
            heldFeedbackEnabled = value;
            if (!initialized) return;
            if (value) EnsureHeldArtwork();
            RefreshHeldVisual();
        }

        private void EnsureHeldArtwork()
        {
            if (heldArtwork != null) return;
            var artwork = new GameObject("HeldArtwork", typeof(SpriteRenderer));
            artwork.layer = gameObject.layer;
            heldArtwork = artwork.transform;
            heldArtwork.SetParent(transform, false);
            var originalRing = ring;
            ring = artwork.GetComponent<SpriteRenderer>();
            SetRenderer(ring, 40);
            ring.color = originalRing.color;
            // The root SpriteRenderer is kept for the existing required-component contract.
            // Moving only its artwork into a child allows scaling without changing hit testing.
            originalRing.enabled = false;
            core.transform.SetParent(heldArtwork, false);
            firstDot.transform.SetParent(heldArtwork, false);
            if (secondDot != null) secondDot.transform.SetParent(heldArtwork, false);
            if (art != null) art.transform.SetParent(heldArtwork, false);
            ApplyArtwork(localState == LocalOrbState.Pending);
            heldShadow = Disc("HeldShadow", gameObject.layer, 1f, Vector3.zero, 87);
            heldHaloOuter = Disc("HeldHaloOuter", gameObject.layer, 1f, Vector3.zero, 88);
            heldHaloInner = Disc("HeldHaloInner", gameObject.layer, 1f, Vector3.zero, 89);
            RefreshHeldVisual();
        }

        private void LateUpdate()
        {
            if (heldArtwork != null && HeldFeedbackActive) RefreshHeldVisual();
        }

        private void RefreshHeldVisual()
        {
            if (heldArtwork == null) return;
            bool active = HeldFeedbackActive;
            // The artwork stays centered on the canonical position. The pulsing halo and
            // detached shadow suggest hovering without offsetting the visible drop center.
            float wave = active ? Mathf.Sin(Time.unscaledTime * 4f) : 0f;
            float scale = active ? 1.24f + wave * 0.025f : 1f;
            heldArtwork.localScale = new Vector3(scale, scale, 1f);
            heldHaloOuter.enabled = active;
            heldHaloInner.enabled = active;
            heldShadow.enabled = active;
            ring.sortingOrder = active ? 90 : 40;
            core.sortingOrder = active ? 91 : 41;
            firstDot.sortingOrder = active ? 92 : 42;
            if (secondDot != null) secondDot.sortingOrder = active ? 93 : 43;
            if (art != null) art.sortingOrder = active ? 94 : 44;
            labelRenderer.sortingOrder = active ? 95 : 45;
            label.transform.localPosition = active
                ? new Vector3(idleLabelPosition.x, -circle.Radius * 1.72f, idleLabelPosition.z)
                : idleLabelPosition;
            if (!active) return;
            Color glow = kind == OrbKind.Combined ? Teal : polarity == OrbPolarity.Yin ? Ivory : Gold;
            heldHaloOuter.transform.localScale = Vector3.one * (1.58f + wave * 0.055f);
            heldHaloOuter.color = new Color(glow.r, glow.g, glow.b, 0.10f + wave * 0.025f);
            heldHaloInner.transform.localScale = Vector3.one * (1.40f + wave * 0.035f);
            heldHaloInner.color = new Color(glow.r, glow.g, glow.b, 0.20f + wave * 0.035f);
            heldShadow.transform.localPosition = new Vector3(0f, -circle.Radius * 1.03f, 0f);
            heldShadow.transform.localScale = new Vector3(1.05f - wave * 0.05f, 0.23f, 1f);
            heldShadow.color = new Color(0.015f, 0.04f, 0.045f, 0.32f);
        }

        private void OnDisable()
        {
            if (!initialized || heldArtwork == null) return;
            if (localState == LocalOrbState.Dragging) SetLocalState(LocalOrbState.Idle);
            else RefreshHeldVisual();
        }

        /// <summary>Call after view scaling at creation/geometry changes; no per-frame font measurement.</summary>
        public void SetLabelPixelHeight(Camera camera, float pixels = 12f)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (!camera.orthographic) throw new ArgumentException("Orb labels require the configured orthographic camera.", nameof(camera));
            if (float.IsNaN(pixels) || float.IsInfinity(pixels) || pixels <= 0f)
                throw new ArgumentOutOfRangeException(nameof(pixels));
            if (label == null || camera.pixelRect.height <= 0f) return;
            float worldHeight = pixels * (2f * camera.orthographicSize) / camera.pixelRect.height;
            float scale = Mathf.Max(0.0001f, Mathf.Abs(label.transform.lossyScale.y));
            SetLabelLocalHeight(worldHeight / scale);
        }

        public float LabelHalfWidthWorld => label == null ? 0f : maximumLabelHalfWidthGlyphs *
            label.characterSize * 0.1f * Mathf.Abs(label.transform.lossyScale.x);

        /// <summary>Current horizontal caption extent, from cached font metrics rather than renderer bounds.</summary>
        public float GetLabelHalfWidthPixels(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (!camera.orthographic) throw new ArgumentException("Orb labels require the configured orthographic camera.", nameof(camera));
            if (camera.orthographicSize <= 0f || camera.pixelRect.height <= 0f) return 0f;
            return LabelHalfWidthWorld * camera.pixelRect.height / (2f * camera.orthographicSize);
        }

        private float MeasureLabelHalfWidth(string value)
        {
            float advance = 0f;
            float inkMin = 0f;
            float inkMax = 0f;
            foreach (char character in value)
            {
                if (label.font.GetCharacterInfo(character, out var glyph, label.fontSize))
                {
                    inkMin = Mathf.Min(inkMin, advance + glyph.minX);
                    inkMax = Mathf.Max(inkMax, advance + glyph.maxX);
                    advance += glyph.advance;
                }
                else
                {
                    // Conservative fallback for an unavailable glyph; configured labels are ASCII.
                    advance += label.fontSize;
                    inkMax = Mathf.Max(inkMax, advance);
                }
            }
            inkMax = Mathf.Max(inkMax, advance);
            float center = advance * 0.5f;
            return Mathf.Max(Mathf.Abs(inkMin - center), Mathf.Abs(inkMax - center));
        }

        private void SetLabelLocalHeight(float localHeight)
        {
            label.characterSize = localHeight * 10f / labelGlyphHeight;
        }

        public void SetLocalState(LocalOrbState state)
        {
            localState = state;
            if (!initialized) return;
            bool pending = state == LocalOrbState.Pending;
            bool combined = kind == OrbKind.Combined;
            bool yin = polarity == OrbPolarity.Yin;
            ring.color = pending ? Gold : state == LocalOrbState.Dragging ? Color.white : combined ? Teal : yin ? Ivory : Gold;
            core.color = pending ? Muted : combined ? new Color(0.08f, 0.28f, 0.29f, 1f) : yin ? Dark : Ivory;
            firstDot.color = pending ? new Color(0.57f, 0.62f, 0.58f, 1f) : combined || yin ? Ivory : Dark;
            if (secondDot != null) secondDot.color = pending ? Muted : Dark;
            label.text = pending ? "LOCKED" : labelHidden ? string.Empty : idleLabel;
            label.color = pending ? Gold : combined ? Teal : Ivory;
            ApplyArtwork(pending);
            RefreshHeldVisual();
        }

        // With artwork, the generated ring/core layers stay allocated (held feedback, tests) but hidden.
        private void ApplyArtwork(bool pending)
        {
            if (art == null) return;
            art.color = pending ? new Color(0.62f, 0.62f, 0.62f, 1f) : Color.white;
            ring.enabled = false;
            core.enabled = false;
            firstDot.enabled = false;
            if (secondDot != null) secondDot.enabled = false;
        }

        private SpriteRenderer Disc(string objectName, int layer, float scale, Vector3 position, int order)
        {
            var child = new GameObject(objectName, typeof(SpriteRenderer));
            child.layer = layer;
            child.transform.SetParent(transform, false);
            child.transform.localPosition = position;
            child.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = child.GetComponent<SpriteRenderer>();
            SetRenderer(renderer, order);
            return renderer;
        }

        private void SetRenderer(SpriteRenderer renderer, int order)
        {
            renderer.sprite = circle.Sprite;
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingOrder = order;
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            initialized = false;
            --circle.Users;
            if (circle.Users == 0)
            {
                Circles.Remove(circle.Radius);
                ReleaseObject(circle.Sprite);
            }
            circle = null;
            --liveViews;
            if (liveViews != 0) return;
            ReleaseObject(circleTexture);
            circleTexture = null;
            if (ownsMaterial)
            {
                ReleaseObject(spriteMaterial);
                spriteMaterial = null;
                ownsMaterial = false;
            }
        }

        private static void EnsureMaterial()
        {
            if (spriteMaterial != null) return;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                throw new InvalidOperationException("T05 requires a scene reference to the URP Sprite-Unlit-Default material.");
            spriteMaterial = new Material(shader) { name = "T05 Runtime Orb Unlit", hideFlags = HideFlags.DontSave };
            ownsMaterial = true;
        }

        private static CircleAsset AcquireCircle(float radius)
        {
            if (Circles.TryGetValue(radius, out var existing))
            {
                ++existing.Users;
                return existing;
            }
            if (circleTexture == null)
            {
                circleTexture = new Texture2D(CircleResolution, CircleResolution, TextureFormat.RGBA32, false)
                {
                    name = "T05 Generated Circle",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
                var pixels = new Color32[CircleResolution * CircleResolution];
                float center = (CircleResolution - 1f) * 0.5f;
                for (int y = 0; y < CircleResolution; ++y)
                for (int x = 0; x < CircleResolution; ++x)
                {
                    float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(CircleResolution * 0.5f - distance) * 255f);
                    pixels[y * CircleResolution + x] = new Color32(255, 255, 255, alpha);
                }
                circleTexture.SetPixels32(pixels);
                circleTexture.Apply(false, true);
            }
            var sprite = Sprite.Create(circleTexture, new Rect(0f, 0f, CircleResolution, CircleResolution),
                new Vector2(0.5f, 0.5f), CircleResolution / (radius * 2f), 0, SpriteMeshType.FullRect);
            sprite.name = "T05 Circle " + radius;
            sprite.hideFlags = HideFlags.DontSave;
            var asset = new CircleAsset { Radius = radius, Sprite = sprite, Users = 1 };
            Circles.Add(radius, asset);
            return asset;
        }

        private static void ReleaseObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private sealed class CircleAsset
        {
            public float Radius;
            public Sprite Sprite;
            public int Users;
        }
    }
}
