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
        [SerializeField] private Transform visual;
        [SerializeField] private bool tintDefaultByKind = true;

        private GameObject replacementVisual;
        private GameObject contactShadow;
        private LabOrbItemMarker itemMarker;
        private GameObject replacementPrefab;
        private LabOrbAppearance appearance;
        private string orbId;
        private LabOrbKind kind;
        private LabOrbElement element;
        private int lockSwipes;
        private int relayStage;
        private float fuseSeconds = -1f;
        private float visualRadius = .32f;
        private Coroutine spawnRiseRoutine;
        private Action spawnRiseCompleted;

        public string OrbId => orbId;
        public LabOrbKind Kind => kind;
        public LabOrbElement Element => element;
        /// <summary>Host-approved handoffs still needed before this item can be used.</summary>
        public int LockSwipes => lockSwipes;
        public int RelayStage => relayStage;
        /// <summary>A bomb's remaining seconds before it explodes; negative when not shown.</summary>
        public float FuseSeconds => fuseSeconds;
        public LabOrbItemMarker ItemMarker => itemMarker;
        public Transform Visual => visual;
        public bool IsSpawning { get; private set; }
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
            Vector3 velocity = Body.linearVelocity;
            velocity.z = 0f;
            if (velocity.sqrMagnitude < .0001f) return;

            // The root is rotation-locked by the board. Rotate art only so the board's
            // planar collision and network position remain unchanged while it rolls.
            Vector3 axis = Vector3.Cross(Vector3.forward, velocity.normalized);
            float degrees = velocity.magnitude / visualRadius * Mathf.Rad2Deg * Time.deltaTime;
            visual.Rotate(axis, degrees, Space.World);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // 벽이 아니라 다른 구슬과 부딪혔을 때만 알린다.
            LabOrbView other = collision.rigidbody != null
                ? collision.rigidbody.GetComponent<LabOrbView>()
                : null;
            if (other == null) return;
            HitOrb?.Invoke(other, collision.relativeVelocity.magnitude);
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
        }

        public void SetLockSwipes(int value) => SetItemState(value, relayStage, fuseSeconds);

        /// <summary>Host-approved lock count, relay stage and bomb fuse; they only change item labels.</summary>
        public void SetItemState(int lockCount, int stage, float fuse = -1f)
        {
            lockSwipes = Mathf.Max(0, lockCount);
            relayStage = Mathf.Max(0, stage);
            fuseSeconds = fuse;
            UpdateItemMarker();
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
        /// Animates only the replaceable Visual child. The orb root, its Rigidbody and
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
            Transform defaultSphere = visual.Find("Default Sphere");
            if (defaultSphere != null) defaultSphere.gameObject.SetActive(replacementVisual == null);
            if (replacementVisual != null)
            {
                replacementVisual.transform.localScale = Vector3.one * (2f * visualRadius);
                LabOrbVisualFactory.SetLayerRecursively(replacementVisual.transform, visual.gameObject.layer);
                if (appearance != null && replacementPrefab == appearance.VisualPrefab)
                    appearance.ApplyKindMaterial(replacementVisual, kind, element);
            }
            UpdateContactShadow();
            UpdateItemMarker();
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
            string fuseText = kind == LabOrbKind.Bomb && fuseSeconds >= 0f
                ? Mathf.CeilToInt(fuseSeconds).ToString() : null;
            itemMarker.Show(appearance != null ? appearance.IconFor(kind) : null, BadgeText(), fuseText,
                visualRadius, visual);
        }

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
