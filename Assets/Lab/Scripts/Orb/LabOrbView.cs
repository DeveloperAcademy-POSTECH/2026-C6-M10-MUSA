using System;
using System.Collections;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// The orb identity, Rigidbody and SphereCollider belong to this root. Visual is an
    /// art-only mount: replacing its child never changes the collision sphere or orb ID.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class LabOrbView : MonoBehaviour
    {
        // A drum's spin eases toward its drift spin at this rate (per second); a glancing hit adds
        // this share of the contact's sliding speed, up to the maximum (degrees per second).
        private const float DrumSpinEase = 1.5f;
        private const float DrumHitSpin = .35f;
        private const float DrumMaxSpin = 540f;

        [SerializeField] private Transform visual;
        [SerializeField] private bool tintDefaultByKind = true;

        private GameObject replacementVisual;
        private GameObject contactShadow;
        private LabOrbItemMarker itemMarker;
        private LabOrbBurn burn;
        private SpriteRenderer expiryFlash;
        private SpriteRenderer expiryHalo;
        private float lifeLeft = -1f;
        private float flashPhase;
        private static Sprite expiryDiscSprite;
        private static Sprite expiryHaloSprite;
        /// <summary>In front of the orb's art, its icons and its fire's licks.</summary>
        private const int ExpiryOrder = 12;
        /// <summary>The board keeps its own lifetime countdown unless the Host's differs by more than this.</summary>
        private const float LifeResyncSeconds = .3f;
        private SpriteRenderer drumArt;
        private LabOrbSpriteArt orbSpriteArt;
        private GameObject replacementPrefab;
        private LabOrbAppearance appearance;
        private string orbId;
        private LabOrbKind kind;
        private LabOrbElement element;
        private int lockSwipes;
        private int relayStage;
        private float fuseSeconds = -1f;
        private float spinVelocity;
        private float spinTarget;
        private float spinStrength;
        private float visualRadius = .32f;
        private Coroutine spawnRoutine;
        private Action spawnCompleted;
        private Coroutine dropRoutine;
        private Action dropLanded;
        private Coroutine shrinkRoutine;

        public string OrbId => orbId;
        public LabOrbKind Kind => kind;
        public LabOrbElement Element => element;
        /// <summary>Host-approved handoffs still needed before this item can be used.</summary>
        public int LockSwipes => lockSwipes;
        public int RelayStage => relayStage;
        /// <summary>A bomb's remaining seconds before it explodes; negative when not shown.</summary>
        public float FuseSeconds => fuseSeconds;
        /// <summary>How many times this item has started its unlock presentation.</summary>
        public int UnlockEffectPlays { get; private set; }
        public LabOrbItemMarker ItemMarker => itemMarker;
        /// <summary>The yellow fire of an orb a bomb blast flung (#213); null until it first burns.</summary>
        public LabOrbBurn Burn => burn;
        public bool Burning => burn != null && burn.Burning;
        /// <summary>Seconds this screen counts down to the end of the orb's lifetime (#235); negative when not shown.</summary>
        public float LifeSecondsLeft => lifeLeft;
        /// <summary>In its last seconds, flashing white before it goes.</summary>
        public bool ExpiryWarning => !kind.IsDrum() && lifeLeft >= 0f && lifeLeft <= ExpiryLook.ExpiryWarningSeconds
            && ExpiryLook.ExpiryWarningSeconds > 0f;
        /// <summary>How white the flash is now, 0 to 1 times the flash colour's alpha.</summary>
        public float ExpiryFlash => expiryFlash != null && expiryFlash.gameObject.activeSelf ? expiryFlash.color.a : 0f;
        /// <summary>The flash's radius, to cover the orb's art.</summary>
        public float ExpiryFlashRadius => expiryFlash != null ? expiryFlash.transform.localScale.x * .5f : 0f;
        /// <summary>The art squeezing down to a size a blast just shrank the orb to (#213).</summary>
        public bool IsShrinking => shrinkRoutine != null;
        /// <summary>The drum art's spin about the view axis, in degrees per second.</summary>
        public float DrumSpin => spinVelocity;
        /// <summary>A drum shows its flat art instead of the sphere once the appearance provides it.</summary>
        public bool DrumArtVisible => drumArt != null && drumArt.gameObject.activeSelf;
        /// <summary>A bomb shows its flat cartoon art instead of the sphere once the appearance provides it.</summary>
        public bool BombArtVisible => itemMarker != null && itemMarker.BombArtVisible;
        public Transform Visual => visual;
        public bool IsSpawning { get; private set; }
        /// <summary>How long a bomb's whole fuse is (the item lifetime), so its rope burns at the right pace.</summary>
        public float FuseTotalSeconds { get; set; } = 15f;
        /// <summary>A gourd bomb still falling or bouncing.</summary>
        public bool IsDropping { get; private set; }
        public Rigidbody Body { get; private set; }
        public SphereCollider HitSphere { get; private set; }

        /// <summary>다른 구슬과 부딪혔을 때 알린다. (상대 구슬, 서로 다가간 속도 - 월드 단위)</summary>
        public event Action<LabOrbView, float> HitOrb;

        /// <summary>Sets shared Inspector-editable art without changing this orb's physics.</summary>
        public void ConfigureAppearance(LabOrbAppearance value)
        {
            if (appearance == value)
            {
                UpdateVisual();
                return;
            }
            appearance = value;
            SetVisualPrefabInternal(appearance != null ? appearance.VisualPrefab : null);
        }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            HitSphere = GetComponent<SphereCollider>();
            EnsureVisual();
        }

        private void Update()
        {
            if (Body == null || visual == null || visualRadius <= 0f) return;
            if (lifeLeft >= 0f)
            {
                lifeLeft = Mathf.Max(0f, lifeLeft - Time.deltaTime);
                PoseExpiry(Time.deltaTime);
            }
            if (kind.IsDrum())
            {
                // The flat art keeps facing the camera and turns about the view axis instead of rolling.
                spinVelocity = Mathf.Lerp(spinVelocity, spinTarget, 1f - Mathf.Exp(-DrumSpinEase * Time.deltaTime));
                visual.Rotate(0f, 0f, spinVelocity * Time.deltaTime, Space.Self);
                return;
            }
            if (orbSpriteArt != null && orbSpriteArt.gameObject.activeSelf)
            {
                visual.localRotation = Quaternion.identity;
                return;
            }
            Vector3 velocity = Body.linearVelocity;
            velocity.z = 0f;
            if (velocity.sqrMagnitude < .0001f) return;

            // The root is rotation-locked by the board. Rotate art only so the board's
            // planar collision and network position remain unchanged while it rolls.
            Vector3 axis = Vector3.Cross(Vector3.forward, velocity.normalized);
            float degrees = velocity.magnitude / visualRadius * Mathf.Rad2Deg * Time.deltaTime;
            visual.Rotate(axis, degrees, Space.World);
        }

        /// <summary>The board sets the drum's drift spin each physics step; strength scales every spin.</summary>
        public void DriveDrumSpin(float driftDegreesPerSecond, float strength)
        {
            spinStrength = Mathf.Max(0f, strength);
            spinTarget = driftDegreesPerSecond * spinStrength;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (kind.IsDrum()) AddHitSpin(collision);
            // 벽이 아니라 다른 구슬과 부딪혔을 때만 알린다.
            LabOrbView other = collision.rigidbody != null
                ? collision.rigidbody.GetComponent<LabOrbView>()
                : null;
            if (other == null) return;
            HitOrb?.Invoke(other, collision.relativeVelocity.magnitude);
        }

        /// <summary>
        /// A glancing hit drags the drum's rim the way the contact slides, like friction on a real
        /// drum: an orb passing over its top to the left turns it anticlockwise. A head-on hit or a
        /// wall it meets square does not spin it.
        /// </summary>
        private void AddHitSpin(Collision collision)
        {
            if (spinStrength <= 0f || collision.contactCount == 0 || Body == null) return;
            Vector3 rim = collision.GetContact(0).point - transform.position;
            Vector3 otherVelocity = collision.rigidbody != null ? collision.rigidbody.linearVelocity : Vector3.zero;
            Vector3 slide = otherVelocity - Body.linearVelocity;
            float turn = (rim.x * slide.y - rim.y * slide.x) / (visualRadius * visualRadius);
            spinVelocity = Mathf.Clamp(spinVelocity + turn * Mathf.Rad2Deg * DrumHitSpin * spinStrength,
                -DrumMaxSpin, DrumMaxSpin);
        }

        public void Configure(string id, LabOrbKind orbKind, float radius,
            LabOrbElement orbElement = LabOrbElement.None)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An orb ID is required.", nameof(id));
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));

            if (Body == null) Body = GetComponent<Rigidbody>();
            if (HitSphere == null) HitSphere = GetComponent<SphereCollider>();
            orbId = id;
            kind = orbKind;
            element = orbKind == LabOrbKind.Combined ? orbElement : LabOrbElement.None;
            visualRadius = radius;
            HitSphere.radius = radius;
            HitSphere.center = Vector3.zero;
            EnsureVisual();
            UpdateVisual();
            if (burn != null) burn.SetRadius(radius);
        }

        /// <summary>
        /// Seconds left in this orb's lifetime, by the Host clock (#235); negative shows none (a locked bomb
        /// has its own red warning, a drum never ends). In its last seconds the orb flashes white, faster as
        /// the end nears, so a player reaching for it knows it is about to go. The view counts down by itself
        /// and only takes the Host's seconds when they differ by more than a moment.
        /// </summary>
        public void SetLife(float secondsLeft)
        {
            if (!(secondsLeft >= 0f) || float.IsInfinity(secondsLeft))
            {
                lifeLeft = -1f;
                PoseExpiry(0f);
                return;
            }
            if (lifeLeft < 0f || Mathf.Abs(lifeLeft - secondsLeft) > LifeResyncSeconds) lifeLeft = secondsLeft;
            PoseExpiry(0f);
        }

        private LabOrbAppearance ExpiryLook => appearance != null ? appearance : LabOrbAppearance.Defaults;

        private void PoseExpiry(float step)
        {
            LabOrbAppearance look = ExpiryLook;
            float warning = look.ExpiryWarningSeconds;
            if (lifeLeft < 0f || warning <= 0f || lifeLeft > warning || kind.IsDrum())
            {
                flashPhase = 0f;
                if (expiryFlash != null) expiryFlash.gameObject.SetActive(false);
                if (expiryHalo != null) expiryHalo.gameObject.SetActive(false);
                return;
            }
            EnsureExpiryParts();
            // Each blink jumps to white and fades; they come faster as the end nears.
            float urgency = 1f - lifeLeft / warning;
            flashPhase += Mathf.Lerp(look.ExpiryFlashStartHz, look.ExpiryFlashFastHz, urgency) * step;
            float blink = flashPhase - Mathf.Floor(flashPhase);
            float flash = (1f - blink) * (1f - blink);
            Color tint = look.ExpiryFlashColor;
            // Cover the art as drawn: a combined orb's picture is drawn larger, a bomb's art larger still.
            float art = HasBombArt ? visualRadius * look.BombBodySize
                : orbSpriteArt != null && orbSpriteArt.gameObject.activeSelf ? visualRadius * LabOrbSpriteArt.SizeScaleFor(kind)
                : visualRadius;
            expiryFlash.gameObject.SetActive(true);
            expiryHalo.gameObject.SetActive(true);
            expiryFlash.transform.localPosition = new Vector3(0f, 0f, -1.05f * art);
            expiryFlash.transform.localScale = Vector3.one * (2f * art);
            expiryFlash.color = new Color(tint.r, tint.g, tint.b, tint.a * flash);
            expiryHalo.transform.localPosition = new Vector3(0f, 0f, -1.05f * art);
            expiryHalo.transform.localScale = Vector3.one * (2f * 1.45f * art);
            expiryHalo.color = new Color(tint.r, tint.g, tint.b, .55f * tint.a * flash);
        }

        private void EnsureExpiryParts()
        {
            if (expiryFlash != null) return;
            expiryFlash = CreateExpiryPart("Expiry Flash", ExpiryDisc());
            expiryHalo = CreateExpiryPart("Expiry Halo", ExpiryHalo());
        }

        private SpriteRenderer CreateExpiryPart(string label, Sprite sprite)
        {
            var part = new GameObject(label);
            part.layer = gameObject.layer;
            // On the root, not the rolling art, so it never turns with the ball.
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = ExpiryOrder;
            return renderer;
        }

        /// <summary>A disc 1 unit across with a soft rim, laid over the art to whiten it.</summary>
        private static Sprite ExpiryDisc() =>
            expiryDiscSprite != null ? expiryDiscSprite : expiryDiscSprite = ExpirySprite("Lab orb expiry flash", false);

        /// <summary>A soft glow ring 1 unit across that starts at the orb's edge (radius 1/1.45 of it).</summary>
        private static Sprite ExpiryHalo() =>
            expiryHaloSprite != null ? expiryHaloSprite : expiryHaloSprite = ExpirySprite("Lab orb expiry halo", true);

        private static Sprite ExpirySprite(string label, bool halo)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha;
                    if (!halo) alpha = Mathf.Clamp01((1f - d) * size * .5f);
                    else
                    {
                        float edge = 1f / 1.45f;
                        alpha = d < edge ? 0f : Mathf.Clamp01((1f - d) / (1f - edge)) * Mathf.Clamp01((d - edge) * size * .5f);
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
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

        /// <summary>Seconds the Host says this orb still burns after a blast flung it (#213); 0 or less lets the fire die.</summary>
        public void SetBurn(float secondsLeft)
        {
            if (burn == null)
            {
                if (!(secondsLeft > 0f)) return;
                burn = LabOrbBurn.Create(transform);
            }
            burn.Burn(secondsLeft, visualRadius);
        }

        /// <summary>
        /// A blast just shrank this orb (#213). Its body is already small; the art starts at its old
        /// size (fromScale times the new one) and squeezes down to match, dipping just under before
        /// it settles, instead of jumping.
        /// </summary>
        public void PlayShrink(float fromScale, float duration)
        {
            if (visual == null || IsSpawning || IsDropping || !isActiveAndEnabled
                || !(fromScale > 1f) || float.IsInfinity(fromScale) || !(duration > 0f) || float.IsInfinity(duration))
                return;
            if (shrinkRoutine != null) StopCoroutine(shrinkRoutine);
            visual.localScale = Vector3.one * fromScale;
            shrinkRoutine = StartCoroutine(ShrinkVisual(fromScale, duration));
        }

        private IEnumerator ShrinkVisual(float fromScale, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration) - 1f;
                // Ease out with a small overshoot, so it dips just below its new size and settles.
                float eased = 1f + 2.4f * t * t * t + 1.4f * t * t;
                if (visual != null) visual.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, 1f, eased);
            }
            if (visual != null) visual.localScale = Vector3.one;
            shrinkRoutine = null;
        }

        public void SetLockSwipes(int value) => SetItemState(value, relayStage, fuseSeconds);

        /// <summary>Host-approved lock count, relay stage and bomb fuse seconds; they only change item labels.</summary>
        public void SetItemState(int lockCount, int stage, float fuse = -1f)
        {
            lockSwipes = Mathf.Max(0, lockCount);
            relayStage = Mathf.Max(0, stage);
            fuseSeconds = fuse;
            UpdateItemMarker();
        }

        /// <summary>Starts the unlock presentation for an item a handoff just unlocked on this screen.</summary>
        public void PlayUnlockEffect()
        {
            UnlockEffectPlays++;
            if (itemMarker != null) itemMarker.PlayUnlock(appearance);
        }

        /// <summary>
        /// Replaces presentation only. A replacement is authored at unit diameter; the
        /// mount scales it to the configured orb diameter. Any physics components on the
        /// art prefab are disabled and removed so the root remains the only physics body.
        /// Pass null to restore the generated Unity sphere.
        /// </summary>
        public void SetVisualPrefab(GameObject prefab)
        {
            SetVisualPrefabInternal(prefab);
        }

        private void SetVisualPrefabInternal(GameObject prefab)
        {
            EnsureVisual();
            // Unity treats a destroyed prefab reference as null. Clearing the art must
            // still remove its live clone even when that source prefab was destroyed.
            if ((prefab == null && replacementVisual == null)
                || (prefab != null && replacementVisual != null && replacementPrefab == prefab))
            {
                UpdateVisual();
                return;
            }
            if (replacementVisual != null)
            {
                replacementVisual.SetActive(false);
                LabVisualSafety.DestroyVisualObject(replacementVisual);
                replacementVisual = null;
            }

            replacementPrefab = prefab;

            if (prefab != null)
            {
                replacementVisual = Instantiate(prefab, visual);
                replacementVisual.name = "Replacement Visual";
                replacementVisual.transform.localPosition = Vector3.zero;
                replacementVisual.transform.localRotation = Quaternion.identity;
                replacementVisual.transform.localScale = Vector3.one * (2f * visualRadius);
                LabVisualSafety.RemovePhysics(replacementVisual);
            }
            UpdateVisual();
        }

        /// <summary>
        /// Pops the replaceable Visual child out of the drum: it starts at fromOffset (root-local,
        /// usually the drum's centre) at startScale and eases onto the root at full size while the
        /// root already flies on its launch. Physics, the orb ID and the collider size never change.
        /// Repeated calls during the same pop share its completion instead of replaying it.
        /// Completion also runs if the view is disabled or destroyed, so callers can
        /// release any temporary collision lock.
        /// </summary>
        public void PlaySpawnPop(Vector3 fromOffset, float startScale, float duration, Action onComplete = null)
        {
            if (float.IsNaN(fromOffset.x) || float.IsNaN(fromOffset.y) || float.IsNaN(fromOffset.z)
                || float.IsInfinity(fromOffset.x) || float.IsInfinity(fromOffset.y) || float.IsInfinity(fromOffset.z))
                throw new ArgumentOutOfRangeException(nameof(fromOffset));
            if (float.IsNaN(startScale) || float.IsInfinity(startScale) || startScale < 0f)
                throw new ArgumentOutOfRangeException(nameof(startScale));
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration));

            if (IsSpawning)
            {
                spawnCompleted += onComplete;
                return;
            }

            EnsureVisual();
            if (visual == null || !isActiveAndEnabled || duration == 0f)
            {
                ResetVisualPose();
                onComplete?.Invoke();
                return;
            }

            IsSpawning = true;
            spawnCompleted = onComplete;
            visual.localPosition = fromOffset;
            visual.localScale = Vector3.one * startScale;
            spawnRoutine = StartCoroutine(PopVisual(fromOffset, startScale, duration));
        }

        private IEnumerator PopVisual(Vector3 from, float startScale, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Wait first, so the first rendered frame shows the orb small inside the drum.
                yield return null;
                elapsed += Time.deltaTime;
                float remaining = 1f - Mathf.Clamp01(elapsed / duration);
                float eased = 1f - remaining * remaining * remaining;
                if (visual != null)
                {
                    visual.localPosition = Vector3.LerpUnclamped(from, Vector3.zero, eased);
                    visual.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, eased);
                }
            }

            spawnRoutine = null;
            CompleteSpawn();
        }

        /// <summary>
        /// A bomb the gourd dropped (#177): its art falls at once from fromOffset (root-local, above
        /// the orb area) onto the root, speeding up like a dropped thing, and lands with a squash and
        /// two shrinking hops of bounceHeight (root-local). Only the art moves; the body waits on its
        /// landing spot. onLanded runs once it settles, or if the view is disabled or destroyed first.
        /// </summary>
        public void PlayDrop(Vector3 fromOffset, float fallSeconds, float bounceSeconds,
            float bounceHeight, Action onLanded = null)
        {
            EnsureVisual();
            if (IsDropping || visual == null || !isActiveAndEnabled)
            {
                onLanded?.Invoke();
                return;
            }
            CompleteSpawn();
            IsDropping = true;
            dropLanded = onLanded;
            dropRoutine = StartCoroutine(Drop(fromOffset, Mathf.Max(.01f, fallSeconds),
                Mathf.Max(0f, bounceSeconds), Mathf.Max(0f, bounceHeight)));
        }

        private IEnumerator Drop(Vector3 from, float fallSeconds, float bounceSeconds, float bounceHeight)
        {
            // Set before the first frame renders, so it is never seen on its spot before falling.
            visual.localPosition = from;
            SetShadowScale(.3f);
            float elapsed = 0f;
            while (elapsed < fallSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallSeconds);
                // Distance fallen grows with the square of time, like gravity.
                visual.localPosition = from * (1f - t * t);
                SetShadowScale(Mathf.Lerp(.3f, 1f, t));
            }

            elapsed = 0f;
            while (elapsed < bounceSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / bounceSeconds);
                // Two hops, the second a third as high; it squashes at each touchdown and stretches as it rises.
                float hop = u < .6f ? Mathf.Sin(u / .6f * Mathf.PI) : .35f * Mathf.Sin((u - .6f) / .4f * Mathf.PI);
                float touch = u < .6f ? Mathf.Min(u, .6f - u) / .6f : Mathf.Min(u - .6f, 1f - u) / .4f;
                float squash = .22f * (1f - u) * (1f - Mathf.Clamp01(touch * 6f));
                visual.localPosition = Vector3.up * (bounceHeight * hop);
                SetSquash(squash);
                SetShadowScale(Mathf.Lerp(1f, .7f, hop));
            }

            dropRoutine = null;
            CompleteDrop();
        }

        private void CompleteDrop()
        {
            if (!IsDropping) return;
            IsDropping = false;
            if (dropRoutine != null)
            {
                StopCoroutine(dropRoutine);
                dropRoutine = null;
            }
            SetSquash(0f);
            SetShadowScale(1f);
            ResetVisualPose();
            Action landed = dropLanded;
            dropLanded = null;
            landed?.Invoke();
        }

        /// <summary>Squashes the art wide and flat (positive) about its centre; 0 restores it.</summary>
        private void SetSquash(float amount)
        {
            var scale = new Vector3(1f + amount, 1f - amount, 1f);
            if (visual != null) visual.localScale = scale;
            if (itemMarker != null) itemMarker.transform.localScale = scale;
        }

        /// <summary>The shadow under a falling bomb grows as it nears the board.</summary>
        private void SetShadowScale(float share)
        {
            if (contactShadow == null || appearance == null) return;
            Vector2 scale = appearance.ShadowScale;
            contactShadow.transform.localScale = new Vector3(
                Mathf.Max(0f, scale.x) * 2f * visualRadius * share,
                Mathf.Max(0f, scale.y) * 2f * visualRadius * share, 1f);
        }

        private void OnDisable()
        {
            CompleteSpawn();
            CompleteDrop();
            if (shrinkRoutine != null)
            {
                StopCoroutine(shrinkRoutine);
                shrinkRoutine = null;
                if (visual != null) visual.localScale = Vector3.one;
            }
        }

        private void OnDestroy()
        {
            CompleteSpawn();
            CompleteDrop();
        }

        private void CompleteSpawn()
        {
            if (!IsSpawning) return;
            IsSpawning = false;
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }
            ResetVisualPose();
            Action completed = spawnCompleted;
            spawnCompleted = null;
            completed?.Invoke();
        }

        private void ResetVisualPose()
        {
            if (visual == null) return;
            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
        }

        private void EnsureVisual()
        {
            if (visual == null)
            {
                Transform existing = transform.Find("Visual - replaceable");
                if (existing == null)
                {
                    var child = new GameObject("Visual - replaceable");
                    existing = child.transform;
                    existing.SetParent(transform, false);
                }
                visual = existing;
            }
            visual.gameObject.layer = gameObject.layer;
            LabOrbVisualFactory.EnsureSphere(visual, "Default Sphere", visualRadius, kind,
                appearance != null ? appearance.MaterialFor(kind, element) : null, TemporaryColorElement());
        }

        /// <summary>속성 머티리얼이 비어 있을 때만 임시 색을 칠할 속성을 돌려준다.</summary>
        private LabOrbElement TemporaryColorElement()
        {
            if (kind != LabOrbKind.Combined || element == LabOrbElement.None) return LabOrbElement.None;
            return appearance == null || appearance.NeedsTemporaryElementColor(kind, element)
                ? element : LabOrbElement.None;
        }

        private void UpdateVisual()
        {
            MeshRenderer defaultRenderer = LabOrbVisualFactory.EnsureSphere(
                visual, "Default Sphere", visualRadius, kind,
                appearance != null ? appearance.MaterialFor(kind, element) : null, TemporaryColorElement());
            if (defaultRenderer != null)
            {
                if (appearance == null && !tintDefaultByKind)
                    defaultRenderer.SetPropertyBlock(null);
            }
            Sprite drumSprite = kind.IsDrum() && appearance != null ? appearance.DrumSprite : null;
            Sprite orbSprite = appearance != null ? appearance.SpriteFor(kind, element) : null;
            // Flat art (the drum's, or the cartoon bomb's on the item marker) replaces the ball.
            bool flatArt = drumSprite != null || HasBombArt || orbSprite != null;
            Transform defaultSphere = visual.Find("Default Sphere");
            if (defaultSphere != null) defaultSphere.gameObject.SetActive(!flatArt && replacementVisual == null);
            if (replacementVisual != null)
            {
                replacementVisual.SetActive(!flatArt);
                replacementVisual.transform.localScale = Vector3.one * (2f * visualRadius);
                LabOrbVisualFactory.SetLayerRecursively(replacementVisual.transform, visual.gameObject.layer);
                if (appearance != null && replacementPrefab == appearance.VisualPrefab)
                    appearance.ApplyKindMaterial(replacementVisual, kind, element);
            }
            UpdateDrumArt(drumSprite);
            // Element art is 20% larger; Yin/Yang art and every collider retain their original size.
            float spriteRadius = visualRadius * LabOrbSpriteArt.SizeScaleFor(kind);
            orbSpriteArt = LabOrbSpriteArt.Set(visual, orbSpriteArt, orbSprite, spriteRadius, -visualRadius);
            UpdateContactShadow();
            UpdateItemMarker();
        }

        private void UpdateDrumArt(Sprite sprite)
        {
            if (sprite == null)
            {
                if (drumArt != null) drumArt.gameObject.SetActive(false);
                return;
            }
            if (drumArt == null)
            {
                var art = new GameObject("Drum Art");
                art.transform.SetParent(visual, false);
                drumArt = art.AddComponent<SpriteRenderer>();
            }
            drumArt.gameObject.layer = visual.gameObject.layer;
            drumArt.gameObject.SetActive(true);
            drumArt.sprite = sprite;
            // Face the front-on board camera just in front of the collision sphere, at its diameter.
            drumArt.transform.localPosition = new Vector3(0f, 0f, -1.02f * visualRadius);
            drumArt.transform.localRotation = Quaternion.identity;
            drumArt.transform.localScale = Vector3.one * (2f * visualRadius / Mathf.Max(.0001f, sprite.bounds.size.x));
        }

        private void UpdateItemMarker()
        {
            if (!kind.IsItem())
            {
                if (itemMarker != null)
                {
                    itemMarker.gameObject.SetActive(false);
                    LabVisualSafety.DestroyVisualObject(itemMarker.gameObject);
                    itemMarker = null;
                }
                return;
            }
            if (itemMarker == null) itemMarker = LabOrbItemMarker.Create(transform);
            // A bomb shows the unlock handoffs left; its burning rope shows the time left (#212).
            if (HasBombArt)
                itemMarker.ShowBomb(appearance, lockSwipes > 0 ? lockSwipes.ToString() : null, visualRadius, visual);
            else
                itemMarker.Show(appearance != null ? appearance.IconFor(kind) : null, BadgeText(), visualRadius, visual);
            itemMarker.SetLocked(lockSwipes > 0, appearance);
            itemMarker.SetFuse(kind == LabOrbKind.Bomb ? fuseSeconds : -1f, FuseTotalSeconds, appearance);
        }

        private bool HasBombArt => kind == LabOrbKind.Bomb && appearance != null && appearance.BombArt != null;

        private string BadgeText()
        {
            if (lockSwipes > 0) return lockSwipes.ToString();
            if (kind != LabOrbKind.Relay || relayStage <= 0) return null;
            return relayStage >= 3 ? "MAX" : relayStage == 2 ? "×2" : "×1";
        }

        private void UpdateContactShadow()
        {
            if (appearance == null || appearance.ShadowMaterial == null)
            {
                if (contactShadow != null)
                {
                    contactShadow.SetActive(false);
                    LabVisualSafety.DestroyVisualObject(contactShadow);
                    contactShadow = null;
                }
                return;
            }
            if (contactShadow == null)
            {
                contactShadow = new GameObject("Contact Shadow");
                contactShadow.transform.SetParent(transform, false);
                contactShadow.AddComponent<MeshFilter>().sharedMesh = LabOrbVisualFactory.GetShadowQuad();
                var renderer = contactShadow.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            contactShadow.layer = gameObject.layer;
            contactShadow.transform.localPosition = new Vector3(appearance.ShadowOffset.x,
                appearance.ShadowOffset.y, appearance.ShadowDepth);
            contactShadow.transform.localRotation = Quaternion.identity;
            Vector2 scale = appearance.ShadowScale;
            contactShadow.transform.localScale = new Vector3(
                Mathf.Max(0f, scale.x) * 2f * visualRadius,
                Mathf.Max(0f, scale.y) * 2f * visualRadius, 1f);
            MeshRenderer surface = contactShadow.GetComponent<MeshRenderer>();
            surface.sharedMaterial = appearance.ShadowMaterial;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", appearance.ShadowColor);
            properties.SetColor("_Color", appearance.ShadowColor);
            surface.SetPropertyBlock(properties);
        }
    }

    /// <summary>Shared procedural presentation for board and thrown orbs.</summary>
    internal static class LabOrbVisualFactory
    {
        private static Material sphereMaterial;
        private static Mesh sphereMesh;
        private static Mesh shadowQuad;

        /// <summary>A unit UV quad facing the front-on board camera, with no collider.</summary>
        public static Mesh GetShadowQuad()
        {
            if (shadowQuad != null) return shadowQuad;
            shadowQuad = new Mesh { name = "Lab contact-shadow quad", hideFlags = HideFlags.DontSave };
            shadowQuad.vertices = new[]
            {
                new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f),
                new Vector3(.5f, .5f, 0f), new Vector3(-.5f, .5f, 0f)
            };
            shadowQuad.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            shadowQuad.triangles = new[] { 0, 2, 1, 0, 3, 2 }; // Normal faces camera at -Z.
            shadowQuad.RecalculateNormals();
            return shadowQuad;
        }

        /// <summary>
        /// Adds or updates an art-only Unity sphere mesh under mount. It has no collider:
        /// the corresponding orb or projectile root owns the sole physics representation.
        /// </summary>
        public static MeshRenderer EnsureSphere(Transform mount, string childName,
            float radius, LabOrbKind kind, Material appearanceMaterial = null,
            LabOrbElement temporaryElementColor = LabOrbElement.None)
        {
            if (mount == null) throw new ArgumentNullException(nameof(mount));
            Transform child = mount.Find(childName);
            if (child == null)
            {
                var sphere = new GameObject(childName);
                sphere.transform.SetParent(mount, false);
                sphere.AddComponent<MeshFilter>().sharedMesh = GetSphereMesh();
                sphere.AddComponent<MeshRenderer>();
                child = sphere.transform;
            }

            child.gameObject.layer = mount.gameObject.layer;
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one * (2f * radius);
            MeshFilter filter = child.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == null) filter.sharedMesh = GetSphereMesh();
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (appearanceMaterial != null)
                    ApplyAppearanceMaterial(renderer, appearanceMaterial);
                else
                {
                    Material material = GetSphereMaterial();
                    if (material != null) renderer.sharedMaterial = material;
                    ApplyKindColor(renderer, kind);
                }
                // 속성 에셋이 들어오기 전까지 결합 구슬은 임시 오방색으로 구분한다.
                if (kind == LabOrbKind.Combined) ApplyElementColor(renderer, temporaryElementColor);
            }
            EnsureRollMarker(child, material: GetSphereMaterial());
            return renderer;
        }

        public static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        private static Mesh GetSphereMesh()
        {
            if (sphereMesh != null) return sphereMesh;
            sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            if (sphereMesh != null) return sphereMesh;

            // Older Unity installations may not expose the built-in mesh by name.
            // The temporary primitive never joins the art hierarchy or participates
            // in physics; its mesh is still the same Unity 3D sphere.
            GameObject template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            template.SetActive(false);
            sphereMesh = template.GetComponent<MeshFilter>().sharedMesh;
            LabVisualSafety.DestroyVisualObject(template);
            return sphereMesh;
        }

        private static void EnsureRollMarker(Transform sphere, Material material)
        {
            Transform marker = sphere.Find("Roll Marker");
            if (marker == null)
            {
                var markerObject = new GameObject("Roll Marker");
                markerObject.transform.SetParent(sphere, false);
                markerObject.AddComponent<MeshFilter>().sharedMesh = GetSphereMesh();
                markerObject.AddComponent<MeshRenderer>();
                marker = markerObject.transform;
            }
            marker.gameObject.layer = sphere.gameObject.layer;
            marker.localPosition = new Vector3(.18f, .17f, -.43f);
            marker.localRotation = Quaternion.identity;
            marker.localScale = Vector3.one * .12f;
            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (material != null) renderer.sharedMaterial = material;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", new Color(.94f, .91f, .78f));
                properties.SetColor("_Color", new Color(.94f, .91f, .78f));
                renderer.SetPropertyBlock(properties);
            }
        }

        /// <summary>임시 속성 색. 에셋이 들어오면 Inspector의 속성 머티리얼이 우선한다.</summary>
        public static Color TemporaryElementColor(LabOrbElement element)
        {
            return element switch
            {
                LabOrbElement.Fire => new Color(.88f, .16f, .14f),  // 화 · 빨강
                LabOrbElement.Water => new Color(.13f, .42f, .90f), // 수 · 파랑
                LabOrbElement.Wood => new Color(.20f, .66f, .25f),  // 목 · 초록
                LabOrbElement.Metal => new Color(.96f, .80f, .15f), // 금 · 노랑
                LabOrbElement.Earth => new Color(.52f, .33f, .16f), // 토 · 갈색
                _ => new Color(.53f, .34f, .84f)
            };
        }

        public static void ApplyElementColor(Renderer renderer, LabOrbElement element)
        {
            if (renderer == null || element == LabOrbElement.None) return;
            Color color = TemporaryElementColor(element);
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color); // URP Lit
            properties.SetColor("_Color", color);     // Standard shader fallback
            renderer.SetPropertyBlock(properties);
        }

        public static void ApplyKindColor(Renderer renderer, LabOrbKind kind)
        {
            if (renderer == null) return;
            Color color = kind switch
            {
                LabOrbKind.Yin => new Color(.08f, .08f, .09f),  // 음 · 검정
                LabOrbKind.Yang => new Color(.95f, .95f, .93f), // 양 · 흰색
                _ => new Color(.53f, .34f, .84f)
            };
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color); // URP Lit
            properties.SetColor("_Color", color);     // Standard shader fallback
            renderer.SetPropertyBlock(properties);
        }

        /// <summary>
        /// Keep the Inspector material as the color source while binding its current color
        /// explicitly per renderer. This avoids platform/renderer variants displaying a
        /// previous orb's material color on other instances.
        /// </summary>
        public static void ApplyAppearanceMaterial(Renderer renderer, Material material)
        {
            if (renderer == null || material == null) return;
            renderer.sharedMaterial = material;
            Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }

        private static Material GetSphereMaterial()
        {
            if (sphereMaterial != null) return sphereMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            if (shader == null) return null;
            sphereMaterial = new Material(shader)
            {
                name = "Lab orb procedural material",
                hideFlags = HideFlags.DontSave
            };
            return sphereMaterial;
        }
    }
}
