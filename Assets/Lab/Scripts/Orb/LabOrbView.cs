using System;
using System.Collections;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// The collider and Rigidbody2D belong to the orb root. Only Visual is presentation,
    /// so replacing its SpriteRenderer/sprite never changes game physics or orb identity.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class LabOrbView : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private bool tintSpriteByKind = true;

        private static Sprite generatedCircle;
        private string orbId;
        private LabOrbKind kind;
        private Coroutine spawnRiseRoutine;
        private Action spawnRiseCompleted;

        public string OrbId => orbId;
        public LabOrbKind Kind => kind;
        public Transform Visual => visual;
        public bool IsSpawning { get; private set; }
        public Rigidbody2D Body { get; private set; }
        public CircleCollider2D HitCircle { get; private set; }

        private void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            HitCircle = GetComponent<CircleCollider2D>();
            EnsureVisual();
        }

        public void Configure(string id, LabOrbKind orbKind, float radius)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new System.ArgumentException("An orb ID is required.", nameof(id));
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new System.ArgumentOutOfRangeException(nameof(radius));

            if (Body == null) Body = GetComponent<Rigidbody2D>();
            if (HitCircle == null) HitCircle = GetComponent<CircleCollider2D>();
            EnsureVisual();
            orbId = id;
            kind = orbKind;
            HitCircle.radius = radius;
            HitCircle.offset = Vector2.zero;
            if (visual != null) visual.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            ApplyColor();
        }

        /// <summary>
        /// Animates only the replaceable Visual child. The orb root, its Rigidbody2D and
        /// its collider remain at the host-approved position throughout this effect.
        /// Repeated calls during the same rise share its completion instead of replaying it.
        /// Completion also runs if the view is disabled or destroyed, so callers can
        /// release any temporary input/physics lock.
        /// </summary>
        public void PlaySpawnRise(float distance, float duration, Action onComplete = null)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration));

            if (IsSpawning)
            {
                spawnRiseCompleted += onComplete;
                return;
            }

            EnsureVisual();
            if (visual == null || !isActiveAndEnabled || distance == 0f || duration == 0f)
            {
                if (visual != null) visual.localPosition = Vector3.zero;
                onComplete?.Invoke();
                return;
            }

            IsSpawning = true;
            spawnRiseCompleted = onComplete;
            visual.localPosition = new Vector3(0f, -distance, 0f);
            spawnRiseRoutine = StartCoroutine(RiseVisual(duration));
        }

        private IEnumerator RiseVisual(float duration)
        {
            float elapsed = 0f;
            Vector3 from = visual.localPosition;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float remaining = 1f - Mathf.Clamp01(elapsed / duration);
                float eased = 1f - remaining * remaining * remaining;
                if (visual != null) visual.localPosition = Vector3.LerpUnclamped(from, Vector3.zero, eased);
                yield return null;
            }

            spawnRiseRoutine = null;
            CompleteSpawnRise();
        }

        private void OnDisable()
        {
            CompleteSpawnRise();
        }

        private void OnDestroy()
        {
            CompleteSpawnRise();
        }

        private void CompleteSpawnRise()
        {
            if (!IsSpawning) return;
            IsSpawning = false;
            if (spawnRiseRoutine != null)
            {
                StopCoroutine(spawnRiseRoutine);
                spawnRiseRoutine = null;
            }
            if (visual != null) visual.localPosition = Vector3.zero;
            Action completed = spawnRiseCompleted;
            spawnRiseCompleted = null;
            completed?.Invoke();
        }

        private void EnsureVisual()
        {
            if (visual == null)
            {
                var child = new GameObject("Visual - replaceable");
                child.transform.SetParent(transform, false);
                visual = child.transform;
            }
            var renderer = visual.GetComponent<SpriteRenderer>();
            // A custom visual child may use any renderer. Add the procedural disc only to
            // an empty child; a later art prefab can be swapped without touching this root.
            if (renderer == null && visual.GetComponentInChildren<Renderer>() == null)
                renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite == null) renderer.sprite = Circle();
        }

        private void ApplyColor()
        {
            if (!tintSpriteByKind || visual == null) return;
            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            renderer.color = kind switch
            {
                LabOrbKind.Yin => new Color(.17f, .46f, .94f),
                LabOrbKind.Yang => new Color(.96f, .38f, .17f),
                _ => new Color(.53f, .34f, .84f)
            };
        }

        private static Sprite Circle()
        {
            if (generatedCircle != null) return generatedCircle;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Lab procedural circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size * .5f;
                    float dy = y + .5f - size * .5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(size * .5f - distance) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            generatedCircle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            generatedCircle.name = "Lab procedural circle";
            generatedCircle.hideFlags = HideFlags.DontSave;
            return generatedCircle;
        }
    }
}
