using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// Non-colliding ink/flame marks follow the actual painted shell. The Host snapshot alone
    /// chooses roots, affected players and helper relationships; these meshes never judge hits.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabJigwiStagePresentation : MonoBehaviour
    {
        [SerializeField, Range(.001f, .08f)] private float surfaceLift = .015f;
        [SerializeField, Range(.1f, 1f)] private float flameOpacity = .76f;
        [SerializeField, Range(.1f, 1f)] private float extinguishSeconds = .45f;
        [SerializeField] private Color ink = new Color(.055f, .035f, .025f, 1f);
        [SerializeField] private Color flame = new Color(1f, .88f, .7f, .76f);
        [Header("Surface flame — visual settings only")]
        [SerializeField] private Material flameMaterial;
        [SerializeField, Range(0f, 8f)] private float flameAnimationSpeed = 2.4f;
        [SerializeField, Range(0f, .1f)] private float flameSway = .03f;
        [SerializeField, Range(.25f, 1f)] private float patchFlameShare = .62f;
        [SerializeField, Range(0f, .4f)] private float scorchWashOpacity = .13f;
        [Header("Optional audio — missing clips keep subtitle/visual feedback")]
        [SerializeField] private AudioClip shoutVoice;
        [SerializeField] private AudioClip igniteSound, extinguishSound, blockedSound;
        [Tooltip("Optional dedicated source; never assign the background-music source.")]
        [SerializeField] private AudioSource stageAudio;
        [SerializeField, Range(0f, 1f)] private float audioVolume = .85f;
        private const int Segments = 48, Rings = 8;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int FlameTimeId = Shader.PropertyToID("_FlameTime");
        private static readonly int FlameSpeedId = Shader.PropertyToID("_FlameSpeed");
        private static readonly int SwayId = Shader.PropertyToID("_Sway");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private readonly Dictionary<string, GroupArt> groups = new Dictionary<string, GroupArt>(StringComparer.Ordinal);
        private readonly HashSet<string> keep = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> removeKeys = new List<string>();
        private readonly HashSet<int> voicedSlots = new HashSet<int>();
        private readonly HashSet<string> soundedResolutions = new HashSet<string>(StringComparer.Ordinal);
        private bool audioInitialized, resumeAudioBaseline, applicationPaused, applicationFocused = true;
        private double audioObservedAt;
        private LabTarget target;
        private LabJigwiStageSnapshot state;
        private LabSnapshot latest;
        private ulong localPlayer;
        private double receivedTime, receivedAt, stageStartedAt = -1d;
        private uint round;
        private bool paused;
        private Material material, runtimeFlameMaterial;
        private bool missingFlameReported;
        private readonly List<BlockedMark> blocked = new List<BlockedMark>();

        public int VisibleRoots { get; private set; }
        public int VisibleFirePatches { get; private set; }
        public int VisibleGroups => groups.Count;

        public void Apply(LabJigwiStageSnapshot value, LabTarget battleTarget, LabSnapshot snapshot,
            ulong player, double now, bool isPaused = false)
        {
            if (target != battleTarget || latest != null && snapshot != null && round != snapshot.round
                || value != null && stageStartedAt != value.startedAt)
                ClearMarks();
            target = battleTarget;
            latest = snapshot;
            state = value;
            localPlayer = player;
            round = snapshot != null ? snapshot.round : 0u;
            stageStartedAt = value != null ? value.startedAt : -1d;
            receivedTime = now;
            receivedAt = Time.unscaledTimeAsDouble;
            paused = isPaused || snapshot != null && snapshot.paused;
            ObserveAudio(now);
            Render(now);
        }

        private void LateUpdate()
        {
            if (state == null) return;
            Render(receivedTime + (paused ? 0d : Time.unscaledTimeAsDouble - receivedAt));
        }

        private void Render(double now)
        {
            VisibleRoots = VisibleFirePatches = 0;
            if (state == null || !state.enabled || target == null || target.Hitbox == null
                || target.VisualMount == null || latest == null || latest.phase != LabPhase.Playing)
            {
                ClearMarks();
                return;
            }
            keep.Clear();
            foreach (LabJigwiStageGroup group in state.groups ?? Array.Empty<LabJigwiStageGroup>())
            {
                if (group == null || string.IsNullOrEmpty(group.id) || !group.geometryConfigured) continue;
                bool rootHere = group.helperPlayer == localPlayer;
                bool fireHere = group.affectedPlayer == localPlayer;
                bool live = group.Warning(now) || group.Active(now);
                groups.TryGetValue(group.id, out GroupArt art);
                bool release = !live && art != null && art.WasLive
                    && group.resolution == LabJigwiStageResolution.Extinguished
                    && group.resolvedAt >= 0d && now >= group.resolvedAt
                    && now - group.resolvedAt < extinguishSeconds;
                if ((!rootHere && !fireHere) || !live && !release) continue;
                if (art == null)
                {
                    art = new GroupArt(target.Hitbox.transform, group.id, Material, FlameMaterial,
                        target.VisualMount.gameObject.layer);
                    groups.Add(group.id, art);
                }
                keep.Add(group.id);
                art.WasLive |= live;
                float fade = release ? 1f - Mathf.Clamp01((float)(now - group.resolvedAt) / extinguishSeconds) : 1f;
                bool warning = group.Warning(now);
                float pulse = paused ? .5f : .5f + .5f * Mathf.Sin((float)now * (warning ? 11f : 6f));
                int patches = warning ? 1 : group.VisiblePatchCount(now);
                if (release) patches = group.patchDirections != null ? group.patchDirections.Length : 0;
                art.Update(this, group, rootHere, fireHere, patches, warning, fade, pulse, now);
                if (live && art.RootVisible) VisibleRoots++;
                if (live) VisibleFirePatches += art.PatchCount;
            }
            AnimateBlocked(now);
            removeKeys.Clear();
            foreach (string key in groups.Keys) if (!keep.Contains(key)) removeKeys.Add(key);
            foreach (string key in removeKeys)
            {
                groups[key].Dispose();
                groups.Remove(key);
            }
        }

        private void ObserveAudio(double now)
        {
            if (state == null || !state.enabled || latest == null || latest.phase != LabPhase.Playing) return;
            bool baseline = !audioInitialized || paused || applicationPaused || !applicationFocused
                || resumeAudioBaseline || now < audioObservedAt;
            if (paused && stageAudio != null) stageAudio.Stop();
            foreach (LabJigwiStageGroup group in state.groups ?? Array.Empty<LabJigwiStageGroup>())
            {
                if (group == null) continue;
                if (group.warningAt <= now && voicedSlots.Add(group.slot)
                    && !baseline && group.warningAt >= audioObservedAt && group.Warning(now))
                {
                    PlayOptionalClip(shoutVoice);
                    PlayOptionalClip(igniteSound);
                }
                if (group.resolution != LabJigwiStageResolution.None && soundedResolutions.Add(group.id)
                    && !baseline && group.resolution == LabJigwiStageResolution.Extinguished
                    && group.resolvedAt >= audioObservedAt && group.resolvedAt <= now
                    && now - group.resolvedAt < extinguishSeconds
                    && (group.helperPlayer == localPlayer || group.affectedPlayer == localPlayer)
                    && groups.TryGetValue(group.id, out GroupArt art) && art.WasLive)
                    PlayOptionalClip(extinguishSound);
            }
            audioInitialized = true;
            audioObservedAt = now;
            resumeAudioBaseline = paused;
        }

        private void PlayOptionalClip(AudioClip clip)
        {
            if (clip == null || paused || applicationPaused || !applicationFocused || resumeAudioBaseline || LabAudioSettings.SfxVolume <= 0f) return;
            if (stageAudio == null)
            {
                var child = new GameObject("Jigwi stage optional audio");
                child.transform.SetParent(transform, false);
                stageAudio = child.AddComponent<AudioSource>();
                stageAudio.playOnAwake = false; stageAudio.loop = false; stageAudio.spatialBlend = 0f;
            }
            stageAudio.PlayOneShot(clip, audioVolume * LabAudioSettings.SfxVolume);
        }

        private void OnEnable()
        {
            applicationPaused = false;
            applicationFocused = Application.isFocused;
            resumeAudioBaseline = true;
        }

        private void OnApplicationPause(bool suspended)
        {
            applicationPaused = suspended;
            resumeAudioBaseline = true;
            if (suspended && stageAudio != null) stageAudio.Stop();
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            resumeAudioBaseline = true;
            if (!focused && stageAudio != null) stageAudio.Stop();
        }

        /// <summary>Project a paint-frame direction onto this exact collider, not an approximate sphere.</summary>
        public static bool TrySurfacePoint(LabTarget battleTarget, Vector3 localDirection,
            out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            if (battleTarget == null || battleTarget.Hitbox == null || battleTarget.VisualMount == null
                || localDirection.sqrMagnitude < .000001f) return false;
            Collider hitbox = battleTarget.Hitbox;
            Vector3 direction = hitbox.transform.TransformVector(localDirection.normalized).normalized;
            Vector3 origin = hitbox.transform.TransformPoint(Vector3.zero);
            Bounds bounds = hitbox.bounds;
            float distance = Mathf.Max(.5f, bounds.extents.magnitude * 3f + Vector3.Distance(origin, bounds.center));
            var ray = new Ray(origin + direction * distance, -direction);
            if (!hitbox.Raycast(ray, out RaycastHit hit, distance * 2f)) return false;
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        private bool BuildCap(Mesh mesh, Vector3 direction, float degrees, float lift, bool rough)
        {
            direction.Normalize();
            Vector3 tangent = Vector3.Cross(Mathf.Abs(direction.y) < .9f ? Vector3.up : Vector3.right, direction).normalized;
            Vector3 bitangent = Vector3.Cross(direction, tangent).normalized;
            var vertices = new Vector3[1 + Rings * Segments];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[Segments * (1 + (Rings - 1) * 2) * 3];
            if (!TrySurfacePoint(target, direction, out Vector3 center, out Vector3 normal)) return false;
            Transform frame = target.Hitbox.transform;
            vertices[0] = frame.InverseTransformPoint(center + normal * lift);
            uvs[0] = new Vector2(.5f, .5f);
            int triangle = 0;
            // Concentric projected rings prevent a large centre fan from cutting through the curved shell.
            // The mesh is cached by Part; neither this allocation nor the collider rays run every frame.
            for (int ring = 1; ring <= Rings; ring++)
            {
                float share = ring / (float)Rings;
                int first = 1 + (ring - 1) * Segments;
                int previous = first - Segments;
                for (int i = 0; i < Segments; i++)
                {
                    float a = i * Mathf.PI * 2f / Segments;
                    float x = Mathf.Cos(a), y = Mathf.Sin(a);
                    float lobe = rough ? .92f + .08f * Mathf.Sin(a * 3f + .7f) : 1f;
                    float radial = share * lobe;
                    float angle = Mathf.Clamp(degrees, .01f, 89f) * radial * Mathf.Deg2Rad;
                    Vector3 rayDirection = direction * Mathf.Cos(angle)
                        + (tangent * x + bitangent * y) * Mathf.Sin(angle);
                    if (!TrySurfacePoint(target, rayDirection, out Vector3 point, out Vector3 outward)) return false;
                    int index = first + i, next = first + (i + 1) % Segments;
                    vertices[index] = frame.InverseTransformPoint(point + outward * lift);
                    uvs[index] = new Vector2(.5f + x * radial * .5f, .5f + y * radial * .5f);
                    if (ring == 1)
                    {
                        triangles[triangle++] = 0;
                        triangles[triangle++] = index;
                        triangles[triangle++] = next;
                    }
                    else
                    {
                        int inner = previous + i, innerNext = previous + (i + 1) % Segments;
                        triangles[triangle++] = inner;
                        triangles[triangle++] = index;
                        triangles[triangle++] = next;
                        triangles[triangle++] = inner;
                        triangles[triangle++] = next;
                        triangles[triangle++] = innerNext;
                    }
                }
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return true;
        }

        private Material FlameMaterial
        {
            get
            {
                if (runtimeFlameMaterial != null) return runtimeFlameMaterial;
                Material source = flameMaterial != null ? flameMaterial : Resources.Load<Material>("Jigwi/SurfaceFlame");
                if (source != null)
                    runtimeFlameMaterial = new Material(source);
                else
                {
                    if (!missingFlameReported)
                    {
                        Debug.LogWarning("Jigwi surface flame material is missing: Resources/Jigwi/SurfaceFlame. "
                            + "Using the unlit texture fallback; gameplay and surface targets are unchanged.", this);
                        missingFlameReported = true;
                    }
                    runtimeFlameMaterial = new Material(Material);
                    Texture2D texture = Resources.Load<Texture2D>("Jigwi/SurfaceFlameWash");
                    if (texture != null)
                    {
                        if (runtimeFlameMaterial.HasProperty("_BaseMap")) runtimeFlameMaterial.SetTexture("_BaseMap", texture);
                        if (runtimeFlameMaterial.HasProperty("_MainTex")) runtimeFlameMaterial.SetTexture("_MainTex", texture);
                    }
                }
                runtimeFlameMaterial.name = "Jigwi surface flame (runtime)";
                runtimeFlameMaterial.hideFlags = HideFlags.DontSave;
                return runtimeFlameMaterial;
            }
        }

        private Material Material
        {
            get
            {
                if (material != null) return material;
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException("Jigwi presentation needs the project's unlit shader.");
                material = new Material(shader) { name = "Jigwi surface ink (runtime)", hideFlags = HideFlags.DontSave,
                    renderQueue = (int)RenderQueue.Transparent + 15 };
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                return material;
            }
        }

        /// <summary>A small rejection mark, never a paint stamp or a collider.</summary>
        public void PlayBlocked(Vector3 targetLocalPoint)
        {
            if (target == null || target.Hitbox == null || state == null || !state.enabled) return;
            if (!TrySurfacePoint(target, targetLocalPoint.normalized, out Vector3 point, out Vector3 normal)) return;
            double now = receivedTime + (paused ? 0d : Time.unscaledTimeAsDouble - receivedAt);
            if (blocked.Count >= 12) { blocked[0].Dispose(); blocked.RemoveAt(0); }
            PlayOptionalClip(blockedSound);
            blocked.Add(new BlockedMark(target.Hitbox.transform, Material, point, normal,
                Mathf.Max(.04f, target.Hitbox.bounds.extents.magnitude * .075f), surfaceLift * 4f, now));
        }

        private void AnimateBlocked(double now)
        {
            for (int i = blocked.Count - 1; i >= 0; i--)
            {
                float age = Mathf.Max(0f, (float)(now - blocked[i].At));
                if (age >= .4f) { blocked[i].Dispose(); blocked.RemoveAt(i); }
                else blocked[i].Tint(new Color(1f, .6f, .08f, 1f - age / .4f));
            }
        }

        public void ClearMarks()
        {
            state = null; latest = null;
            audioInitialized = false; resumeAudioBaseline = false;
            voicedSlots.Clear(); soundedResolutions.Clear();
            if (stageAudio != null) stageAudio.Stop();
            foreach (GroupArt art in groups.Values) art.Dispose();
            groups.Clear();
            foreach (BlockedMark mark in blocked) mark.Dispose();
            blocked.Clear();
            VisibleRoots = VisibleFirePatches = 0;
        }

        private void OnDisable() => ClearMarks();
        private void OnDestroy()
        {
            ClearMarks();
            if (material != null) DisposeObject(material);
            if (runtimeFlameMaterial != null) DisposeObject(runtimeFlameMaterial);
        }

        private static void DisposeObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        private sealed class BlockedMark
        {
            public readonly double At;
            private readonly GameObject root;
            private readonly LineRenderer[] lines = new LineRenderer[2];
            private readonly MaterialPropertyBlock color = new MaterialPropertyBlock();
            public BlockedMark(Transform parent, Material material, Vector3 point, Vector3 normal,
                float size, float lift, double at)
            {
                At = at;
                root = new GameObject("Jigwi blocked impact"); root.transform.SetParent(parent, false);
                root.layer = parent.gameObject.layer;
                Vector3 tangent = Vector3.Cross(Mathf.Abs(normal.y) < .9f ? Vector3.up : Vector3.right, normal).normalized;
                Vector3 vertical = Vector3.Cross(normal, tangent).normalized;
                for (int i = 0; i < lines.Length; i++)
                {
                    var child = new GameObject("Rejection ink " + i) { layer = root.layer };
                    child.transform.SetParent(root.transform, false);
                    var line = child.AddComponent<LineRenderer>(); lines[i] = line;
                    line.sharedMaterial = material; line.useWorldSpace = false; line.positionCount = 3;
                    line.widthMultiplier = size * .22f; line.numCapVertices = 2;
                    line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                    Vector3 center = point + normal * lift;
                    Vector3 side = tangent * (i == 0 ? -1f : 1f);
                    line.SetPositions(new[] { parent.InverseTransformPoint(center + side * size + vertical * size * .7f),
                        parent.InverseTransformPoint(center + side * size * .45f),
                        parent.InverseTransformPoint(center + side * size - vertical * size * .7f) });
                }
                Tint(new Color(1f, .6f, .08f));
            }
            public void Tint(Color tint)
            {
                color.SetColor("_BaseColor", tint); color.SetColor("_Color", tint);
                foreach (LineRenderer line in lines) line.SetPropertyBlock(color);
            }
            public void Dispose() { root.SetActive(false); DisposeObject(root); }
        }

        private sealed class Part
        {
            public readonly GameObject Object;
            public readonly Mesh Mesh;
            public readonly MeshRenderer Renderer;
            private Vector3 direction, frameScale;
            private float angle, lift;
            private Mesh colliderMesh;
            private bool hasShape, shapeAttempted, roughShape;
            public bool Visible => Object.activeInHierarchy && Renderer.enabled && Mesh.vertexCount > 0;
            private readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
            public Part(string name, Transform parent, Material material, int layer)
            {
                Object = new GameObject(name) { layer = layer };
                Object.transform.SetParent(parent, false);
                Mesh = new Mesh { name = name + " mesh", hideFlags = HideFlags.DontSave };
                Object.AddComponent<MeshFilter>().sharedMesh = Mesh;
                Renderer = Object.AddComponent<MeshRenderer>();
                Renderer.sharedMaterial = material;
                Renderer.shadowCastingMode = ShadowCastingMode.Off;
                Renderer.receiveShadows = false;
                Renderer.lightProbeUsage = LightProbeUsage.Off;
                Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            public void Draw(LabJigwiStagePresentation owner, Vector3 axis, float degrees, float surfaceOffset,
                Color color, bool show, bool rough, float flameTime = 0f, float seed = 0f)
            {
                Object.SetActive(show);
                if (!show) return;
                Mesh source = owner.target.Hitbox.sharedMesh;
                Vector3 scale = owner.target.Hitbox.transform.lossyScale;
                if (!shapeAttempted || (direction - axis).sqrMagnitude > .000001f || angle != degrees
                    || lift != surfaceOffset || colliderMesh != source || frameScale != scale || roughShape != rough)
                {
                    hasShape = owner.BuildCap(Mesh, axis, degrees, surfaceOffset, rough);
                    shapeAttempted = true;
                    direction = axis; angle = degrees; lift = surfaceOffset; colliderMesh = source;
                    frameScale = scale; roughShape = rough;
                }
                Renderer.enabled = hasShape;
                tint.SetColor(BaseColorId, color); tint.SetColor(ColorId, color);
                // The paused/held battle clock is supplied by Apply/Render, never shader _Time.
                tint.SetFloat(FlameTimeId, flameTime);
                tint.SetFloat(FlameSpeedId, owner.flameAnimationSpeed);
                tint.SetFloat(SwayId, owner.flameSway);
                tint.SetFloat(SeedId, seed);
                Renderer.SetPropertyBlock(tint);
            }
            public void Dispose() => DisposeObject(Mesh);
        }

        private sealed class GroupArt
        {
            private readonly GameObject root;
            private readonly Part source;
            private readonly Part[] patches = new Part[3], washes = new Part[3];
            private readonly LineRenderer[] links = new LineRenderer[3];
            private readonly MaterialPropertyBlock linkTint = new MaterialPropertyBlock();
            private readonly Vector3[][] linkPoints = { new Vector3[7], new Vector3[7], new Vector3[7] };
            private readonly Vector3[] linkSourceDirections = new Vector3[3], linkPatchDirections = new Vector3[3];
            private readonly bool[] linkAttempted = new bool[3], linkValid = new bool[3];
            private Mesh linkMesh;
            private Vector3 linkFrameScale;
            private float linkLift;
            public bool WasLive;
            public bool RootVisible => source.Visible;
            public int PatchCount { get; private set; }
            public GroupArt(Transform parent, string id, Material inkMaterial, Material flameMaterial, int layer)
            {
                root = new GameObject("Jigwi " + id) { layer = layer };
                root.transform.SetParent(parent, false);
                source = new Part("Source flame", root.transform, flameMaterial, layer);
                for (int i = 0; i < patches.Length; i++)
                {
                    washes[i] = new Part("Fire scorch wash " + (i + 1), root.transform, inkMaterial, layer);
                    patches[i] = new Part("Fire patch " + (i + 1), root.transform, flameMaterial, layer);
                    var link = new GameObject("Source connection " + (i + 1)) { layer = layer };
                    link.transform.SetParent(root.transform, false);
                    links[i] = link.AddComponent<LineRenderer>();
                    links[i].sharedMaterial = inkMaterial;
                    links[i].useWorldSpace = false;
                    links[i].shadowCastingMode = ShadowCastingMode.Off;
                    links[i].receiveShadows = false;
                    links[i].numCapVertices = 2;
                }
            }
            public void Update(LabJigwiStagePresentation owner, LabJigwiStageGroup group, bool rootHere,
                bool fireHere, int count, bool warning, float fade, float pulse, double now)
            {
                float offset = owner.surfaceLift;
                float animationTime = (float)Math.Max(0d, now - group.warningAt);
                float seed = group.slot * 1.618f + .37f;
                // Preserve the generated ink/wash colours; the source is no longer three opaque discs.
                Color sourceColor = Color.white;
                sourceColor.a = (warning ? .55f + .4f * pulse : 1f) * fade;
                source.Draw(owner, group.rootDirection, group.rootAngleDegrees, offset * 2.5f,
                    sourceColor, rootHere, false, animationTime, seed);
                Vector3[] directions = group.patchDirections ?? Array.Empty<Vector3>();
                PatchCount = 0;
                Mesh mesh = owner.target.Hitbox.sharedMesh;
                Vector3 scale = owner.target.Hitbox.transform.lossyScale;
                bool frameChanged = linkMesh != mesh || linkFrameScale != scale || linkLift != offset;
                linkMesh = mesh; linkFrameScale = scale; linkLift = offset;
                if (frameChanged) Array.Clear(linkAttempted, 0, linkAttempted.Length);
                for (int i = 0; i < patches.Length; i++)
                {
                    Vector3 axis = i < directions.Length ? directions[i] : Vector3.up;
                    bool show = fireHere && i < count && i < directions.Length;
                    Color wash = owner.ink;
                    wash.a = owner.scorchWashOpacity * (warning ? .5f + .5f * pulse : 1f) * fade;
                    // This restrained wash shows the unchanged full blocked cone; the smaller licking
                    // flame is presentation only, not a newly narrowed collision/paint-blocking region.
                    washes[i].Draw(owner, axis, group.fireAngleDegrees, offset, wash, show, true);
                    Color fire = owner.flame;
                    fire.a = owner.flameOpacity * (warning ? .2f + .25f * pulse : .88f + .12f * pulse) * fade;
                    patches[i].Draw(owner, axis, group.fireAngleDegrees * owner.patchFlameShare, offset * 2f,
                        fire, show, false, animationTime, seed + (i + 1) * 2.113f);
                    if (patches[i].Visible) PatchCount++;
                    LineRenderer line = links[i];
                    line.enabled = rootHere && fireHere && i < count && i < directions.Length && fade > 0f;
                    if (!line.enabled) continue;
                    if (frameChanged || !linkAttempted[i] || linkSourceDirections[i] != group.rootDirection
                        || linkPatchDirections[i] != axis)
                    {
                        linkAttempted[i] = true;
                        linkSourceDirections[i] = group.rootDirection; linkPatchDirections[i] = axis;
                        Vector3[] points = linkPoints[i];
                        linkValid[i] = true;
                        for (int j = 0; j < points.Length; j++)
                        {
                            Vector3 d = Vector3.Slerp(group.rootDirection, axis, j / (float)(points.Length - 1)).normalized;
                            if (!TrySurfacePoint(owner.target, d, out Vector3 point, out Vector3 outward))
                            { linkValid[i] = false; break; }
                            points[j] = owner.target.Hitbox.transform.InverseTransformPoint(point + outward * offset * 1.5f);
                        }
                        if (linkValid[i])
                        {
                            line.positionCount = points.Length;
                            line.SetPositions(points);
                        }
                    }
                    line.enabled = linkValid[i];
                    if (!line.enabled) continue;
                    line.widthMultiplier = Mathf.Max(.008f, owner.target.Hitbox.bounds.extents.magnitude * .018f);
                    Color linkColor = owner.ink; linkColor.a *= fade * (warning ? .5f : .9f);
                    line.startColor = line.endColor = linkColor;
                    linkTint.SetColor(BaseColorId, linkColor); linkTint.SetColor(ColorId, linkColor);
                    line.SetPropertyBlock(linkTint);
                }
            }
            public void Dispose()
            {
                root.SetActive(false);
                source.Dispose();
                foreach (Part part in patches) part.Dispose();
                foreach (Part part in washes) part.Dispose();
                DisposeObject(root);
            }
        }
    }
}
