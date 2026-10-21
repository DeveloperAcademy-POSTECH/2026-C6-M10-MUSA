using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// Ink presentation for an existing projectile. Its centreline and stationary brush stamps
    /// use positions the source really visited; no visual force or collider changes the shot.
    /// </summary>
    public sealed class LabElementFlightTrail : MonoBehaviour
    {
        private const int MaximumMotifs = 512;
        private const int MaximumEmissionsPerSample = 64;
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int UvSwapId = Shader.PropertyToID("_UvSwap");
        private static Material fallbackMaterial;

        private readonly LabFlightTrailPath path = new LabFlightTrailPath();
        private readonly Mesh[] meshes = new Mesh[3];
        private readonly MeshRenderer[] ribbons = new MeshRenderer[3];
        private readonly int[] visibleVertexCounts = new int[3];
        private readonly Vector3[] vertices = new Vector3[LabFlightTrailPath.Capacity * 2];
        private readonly Vector2[] uvs = new Vector2[LabFlightTrailPath.Capacity * 2];
        private readonly Color[] colors = new Color[LabFlightTrailPath.Capacity * 2];
        private readonly int[] triangles = CreateTriangles();
        private readonly Vector3[] viewSides = new Vector3[LabFlightTrailPath.Capacity];
        private readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[MaximumMotifs];
        private readonly MotifSample[] motifSamples = new MotifSample[MaximumMotifs];
        private MaterialPropertyBlock ribbonProperties, motifProperties;
        private ParticleSystem motifs;
        private ParticleSystemRenderer motifRenderer;
        private Transform source;
        private LabElementTrailProfile profile;
        private Vector3 lastSamplePosition;
        private float lastSampleTime, travelledDistance, stampRemainder, fadeDeadline;
        private uint randomState, nextMotifSeed;
        private bool emitting, detachedFade;

        private struct MotifSample
        {
            public uint Seed;
            public float Distance, Time, Rotation, SideOffset;
            public Vector3 Position, Tangent;
        }

        public LabElementTrailProfile Profile => profile;
        public LabOrbElement Element => profile != null ? profile.Element : LabOrbElement.None;
        public bool IsEmitting => emitting;
        /// <summary>
        /// Opacity multiplier (0-1) for everything drawn from now on: the whole ribbon each frame and each new
        /// stamp as it is emitted. 1 for thrown orbs; the orb board's shallow trail (#254) lowers it by speed.
        /// </summary>
        public float Intensity
        {
            get => intensity;
            set => intensity = float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
        }
        private float intensity = 1f;
        public int PathCount => path.Count;
        public float PathLength => path.Length;
        public int StrandCount => profile == null ? 0 : profile.Shape switch
        {
            LabElementTrailShape.ParallelInk => 3,
            LabElementTrailShape.Flame => 1,
            LabElementTrailShape.Stream => 1,
            _ => 0
        };
        public ParticleSystem Motifs => motifs;
        public Vector3 GetPathPosition(int index) => path.GetPosition(index);

        /// <summary>Starts or reuses the effect, clearing every old point, particle and fade deadline.</summary>
        public void Begin(Transform source, LabElementTrailProfile profile)
        {
            Clear();
            this.source = source;
            this.profile = profile;
            if (source == null || source == transform || source.IsChildOf(transform) || profile == null)
            {
                this.source = null;
                return;
            }
            // A reused effect may still belong to its previous detached fade root. Give the
            // new source ownership so disabling/destroying that projectile also clears it.
            transform.SetParent(source, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            gameObject.layer = source.gameObject.layer;
            lastSamplePosition = source.position;
            lastSampleTime = Time.time;
            path.Record(lastSamplePosition, lastSampleTime);
            randomState = 0x7f4a7c15u ^ ((uint)profile.Element * 0x9e3779b9u);
            nextMotifSeed = 1;
            EnsureRenderers();
            ApplyMaterials();
            emitting = true;
            if (!enabled) enabled = true;
            motifs.Play(false);
        }

        /// <summary>Leaves the existing world history behind when the projectile disappears.</summary>
        public void StopAndFade(Transform parent)
        {
            if (!emitting) return;
            RecordSource(Time.time);
            emitting = false;
            source = null;
            detachedFade = true;
            fadeDeadline = Time.time + profile.Lifetime;
            transform.SetParent(parent, true);
            motifs.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>Immediate reset for restart, cancellation, destruction or subsequent Begin.</summary>
        public void Clear()
        {
            emitting = detachedFade = false;
            source = null;
            path.Clear();
            travelledDistance = stampRemainder = 0f;
            nextMotifSeed = 1;
            if (motifs != null) motifs.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int i = 0; i < meshes.Length; i++) ClearMesh(i);
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforePipelineCamera;
            Camera.onPreCull += BeforeCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforePipelineCamera;
            Camera.onPreCull -= BeforeCamera;
            Clear();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] == null) continue;
                if (Application.isPlaying) Destroy(meshes[i]);
                else DestroyImmediate(meshes[i]);
                meshes[i] = null;
            }
        }

        private void LateUpdate()
        {
            if (profile == null) return;
            if (emitting)
            {
                if (source == null) { Clear(); return; }
                RecordSource(Time.time);
            }
            path.Trim(Time.time, profile.Lifetime, profile.MaxLength);
            PruneMotifs(null, 0);
            if (path.Count < 2)
                for (int i = 0; i < meshes.Length; i++) ClearMesh(i);
            if (detachedFade && Time.time >= fadeDeadline)
            {
                detachedFade = false;
                Destroy(gameObject);
            }
        }

        private void RecordSource(float now)
        {
            if (source == null || now <= lastSampleTime) return;
            Vector3 position = source.position;
            Vector3 displacement = position - lastSamplePosition;
            float distance = displacement.magnitude;
            if (float.IsNaN(distance) || float.IsInfinity(distance)) return;
            if (distance < .0001f)
            {
                lastSampleTime = now;
                return;
            }
            if (path.Count == 0) path.Record(lastSamplePosition, lastSampleTime);
            if (!path.Record(position, now)) return;
            EmitMotifs(lastSamplePosition, position, distance, lastSampleTime, now);
            travelledDistance += distance;
            lastSamplePosition = position;
            lastSampleTime = now;
            path.Trim(now, profile.Lifetime, profile.MaxLength);
        }

        private void EmitMotifs(Vector3 previous, Vector3 current, float distance, float then, float now)
        {
            float spacing = profile.ParticleSpacing;
            float total = stampRemainder + distance;
            int requested = Mathf.FloorToInt(total / spacing);
            if (requested == 0) { stampRemainder = total; return; }
            bool parallel = profile.Shape == LabElementTrailShape.ParallelInk;
            int strands = parallel ? 3 : 1;
            int count = Mathf.Min(requested, MaximumEmissionsPerSample / strands);
            // An exceptional long frame keeps the newest actual points rather than allocating
            // thousands of particles. Distance phase is retained for the following frame.
            float offset = spacing - stampRemainder + (requested - count) * spacing;
            stampRemainder = total - requested * spacing;
            PruneMotifs(null, count * strands);
            Vector3 tangent = (current - previous) / distance;
            for (int i = 0; i < count; i++, offset += spacing)
            {
                float fraction = Mathf.Clamp01(offset / distance);
                float distanceAtStamp = travelledDistance + distance * fraction;
                float stampedAt = Mathf.Lerp(then, now, fraction);
                if (distanceAtStamp < travelledDistance + distance - profile.MaxLength
                    || stampedAt < now - profile.Lifetime) continue;
                // A separate xorshift generator never consumes the game's UnityEngine.Random state.
                float variation = parallel ? 1f : Mathf.Lerp(.8f, 1.15f, Random01());
                float rotation = parallel ? 0f : (Random01() - .5f) * RotationSpread();
                Vector3 position = Vector3.Lerp(previous, current, fraction);
                for (int strand = 0; strand < strands; strand++)
                {
                    uint seed = nextMotifSeed++;
                    float sideOffset = parallel ? (strand - 1) * profile.Width * .36f : 0f;
                    motifSamples[seed % MaximumMotifs] = new MotifSample
                    {
                        Seed = seed, Distance = distanceAtStamp, Time = stampedAt,
                        Position = position, Tangent = tangent, Rotation = rotation, SideOffset = sideOffset
                    };
                    Color tint = profile.Color;
                    tint.a *= (profile.Shape == LabElementTrailShape.Flame ? .9f : 1f) * intensity;
                    // Metal's small three-line brush footprints keep its silhouette readable
                    // when perspective compresses the travelled centreline to a few pixels.
                    // Their group centre is still a real passed point, never a point projected
                    // behind the orb. All three use the same frame with no random spread.
                    ParticleSystem.EmitParams stamp = new ParticleSystem.EmitParams
                    {
                        position = position + StampSide(Camera.main, position, tangent) * sideOffset,
                        velocity = Vector3.zero,
                        startColor = tint,
                        startSize3D = new Vector3(profile.Width * (parallel ? .24f : 1f), profile.ParticleSize, 1f)
                            * variation,
                        startLifetime = Mathf.Max(.001f, profile.Lifetime - (now - stampedAt)),
                        randomSeed = seed,
                        rotation3D = StampRotation(Camera.main, position, tangent, rotation)
                    };
                    motifs.Emit(stamp, 1);
                }
            }
        }

        private float RotationSpread() => profile.Shape switch
        {
            LabElementTrailShape.Leaves => 74f,
            LabElementTrailShape.DryClumps => 46f,
            LabElementTrailShape.Flame => 28f,
            _ => 10f
        };

        private void PruneMotifs(Camera camera, int reserve)
        {
            if (motifs == null || profile == null) return;
            int count = motifs.GetParticles(particles);
            uint capacity = profile.Shape == LabElementTrailShape.ParallelInk
                ? (uint)(MaximumMotifs / 3 * 3) : MaximumMotifs;
            uint firstKeptSeed = nextMotifSeed + (uint)reserve > capacity
                ? nextMotifSeed + (uint)reserve - capacity : 1u;
            float minimumDistance = travelledDistance - profile.MaxLength;
            float oldestTime = Time.time - profile.Lifetime;
            int kept = 0;
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle particle = particles[i];
                MotifSample sample = motifSamples[particle.randomSeed % MaximumMotifs];
                if (sample.Seed != particle.randomSeed || particle.randomSeed < firstKeptSeed
                    || sample.Distance < minimumDistance || sample.Time < oldestTime)
                {
                    continue;
                }
                else
                {
                    particle.position = sample.Position + (sample.SideOffset == 0f ? Vector3.zero
                        : StampSide(camera != null ? camera : Camera.main, sample.Position, sample.Tangent) * sample.SideOffset);
                    particle.velocity = Vector3.zero;
                    particle.remainingLifetime = Mathf.Min(particle.remainingLifetime,
                        Mathf.Max(0f, profile.Lifetime - (Time.time - sample.Time)));
                    if (camera != null)
                        particle.rotation3D = StampRotation(camera, sample.Position,
                            sample.Tangent, sample.Rotation);
                }
                particles[kept++] = particle;
            }
            motifs.SetParticles(particles, kept);
        }

        private static Vector3 StampRotation(Camera camera, Vector3 position, Vector3 tangent, float twist)
        {
            Vector3 facing = camera != null
                ? (camera.orthographic ? -camera.transform.forward : camera.transform.position - position)
                : Vector3.back;
            if (facing.sqrMagnitude < .000001f) facing = Vector3.back;
            facing.Normalize();
            Vector3 up = Vector3.ProjectOnPlane(tangent, facing);
            if (up.sqrMagnitude < .000001f)
                up = Vector3.ProjectOnPlane(camera != null ? camera.transform.up : Vector3.up, facing);
            if (up.sqrMagnitude < .000001f) up = Vector3.Cross(facing, Vector3.right);
            return (Quaternion.LookRotation(facing, up.normalized)
                * Quaternion.AngleAxis(twist, Vector3.forward)).eulerAngles;
        }

        private static Vector3 StampSide(Camera camera, Vector3 position, Vector3 tangent)
        {
            Vector3 facing = camera != null
                ? (camera.orthographic ? -camera.transform.forward : camera.transform.position - position)
                : Vector3.back;
            Vector3 side = Vector3.Cross(tangent, facing);
            return side.sqrMagnitude > .000001f ? side.normalized
                : (camera != null ? camera.transform.right : Vector3.right);
        }

        private void BeforePipelineCamera(ScriptableRenderContext context, Camera camera) => BeforeCamera(camera);

        private void BeforeCamera(Camera camera)
        {
            if (profile == null || camera == null || !isActiveAndEnabled
                || (camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            // Each camera gets its own view frame. A rotating orb never twists the three
            // Metal strands, and the Editor's side comparison uses the same world history.
            BuildRibbons(camera);
            PruneMotifs(camera, 0);
        }

        private void BuildRibbons(Camera camera)
        {
            int count = path.Count;
            if (count < 2 || path.Length <= .000001f)
            {
                for (int i = 0; i < meshes.Length; i++) ClearMesh(i);
                return;
            }
            Vector3 previousSide = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = path.GetPosition(i);
                Vector3 tangent = i == 0 ? path.GetPosition(1) - position
                    : (i == count - 1 ? position - path.GetPosition(i - 1)
                    : path.GetPosition(i + 1) - path.GetPosition(i - 1));
                Vector3 view = camera.orthographic ? -camera.transform.forward
                    : camera.transform.position - position;
                Vector3 side = Vector3.Cross(tangent, view);
                if (side.sqrMagnitude < .000001f)
                    side = previousSide.sqrMagnitude > .01f ? previousSide : camera.transform.right;
                else side.Normalize();
                if (previousSide.sqrMagnitude > .01f && Vector3.Dot(side, previousSide) < 0f) side = -side;
                viewSides[i] = side;
                previousSide = side;
            }

            for (int strand = 0; strand < StrandCount; strand++)
            {
                float along = 0f;
                Vector3 minimum = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 maximum = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                for (int i = 0; i < count; i++)
                {
                    if (i > 0) along += Vector3.Distance(path.GetPosition(i - 1), path.GetPosition(i));
                    float u = along / path.Length;
                    float taper = profile.Shape == LabElementTrailShape.ParallelInk
                        ? Mathf.Lerp(.14f, 1f, Mathf.Sqrt(u))
                        : Mathf.Lerp(.04f, 1f, Mathf.Sqrt(u));
                    float width = profile.Width * taper;
                    float offset = 0f;
                    if (profile.Shape == LabElementTrailShape.ParallelInk)
                    {
                        offset = (strand - 1) * profile.Width * .36f;
                        width *= .14f;
                    }
                    else if (profile.Shape == LabElementTrailShape.Flame)
                    {
                        width *= .82f + .18f * Mathf.Sin(u * 18f + .7f);
                    }
                    else width *= .8f;
                    Vector3 centre = path.GetPosition(i) + viewSides[i] * offset;
                    int vertex = i * 2;
                    vertices[vertex] = transform.InverseTransformPoint(centre - viewSides[i] * width * .5f);
                    vertices[vertex + 1] = transform.InverseTransformPoint(centre + viewSides[i] * width * .5f);
                    minimum = Vector3.Min(minimum, Vector3.Min(vertices[vertex], vertices[vertex + 1]));
                    maximum = Vector3.Max(maximum, Vector3.Max(vertices[vertex], vertices[vertex + 1]));
                    uvs[vertex] = new Vector2(u, 0f);
                    uvs[vertex + 1] = new Vector2(u, 1f);
                    Color tint = profile.Color;
                    float life = Mathf.Clamp01(1f - (Time.time - path.GetTime(i)) / profile.Lifetime);
                    tint.a *= life * life * .94f * intensity;
                    colors[vertex] = colors[vertex + 1] = tint;
                }
                int vertexCount = count * 2;
                Mesh mesh = meshes[strand];
                if (vertexCount < visibleVertexCounts[strand]) mesh.Clear(false);
                mesh.SetVertices(vertices, 0, vertexCount);
                mesh.SetColors(colors, 0, vertexCount);
                mesh.SetUVs(0, uvs, 0, vertexCount);
                mesh.SetIndices(triangles, 0, (count - 1) * 6, MeshTopology.Triangles, 0, false);
                mesh.bounds = new Bounds((minimum + maximum) * .5f, maximum - minimum);
                visibleVertexCounts[strand] = vertexCount;
            }
        }

        private void EnsureRenderers()
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                if (i < StrandCount && ribbons[i] == null)
                {
                    GameObject ribbon = new GameObject("Ink Ribbon " + (i + 1));
                    ribbon.transform.SetParent(transform, false);
                    meshes[i] = new Mesh { name = "Element flight history " + (i + 1), hideFlags = HideFlags.DontSave };
                    meshes[i].MarkDynamic();
                    ribbon.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                    ribbons[i] = ribbon.AddComponent<MeshRenderer>();
                    ribbons[i].shadowCastingMode = ShadowCastingMode.Off;
                    ribbons[i].receiveShadows = false;
                    ribbons[i].lightProbeUsage = LightProbeUsage.Off;
                    ribbons[i].reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                if (ribbons[i] == null) continue;
                ribbons[i].gameObject.layer = gameObject.layer;
                ribbons[i].gameObject.SetActive(i < StrandCount);
            }
            if (motifs == null)
            {
                GameObject brush = new GameObject("Brush Motifs");
                brush.transform.SetParent(transform, false);
                motifs = brush.AddComponent<ParticleSystem>();
                motifs.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                motifRenderer = brush.GetComponent<ParticleSystemRenderer>();
                var main = motifs.main;
                main.playOnAwake = false;
                main.loop = true;
                main.duration = 1f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                main.useUnscaledTime = false;
                main.startSpeed = 0f;
                main.gravityModifier = 0f;
                main.startRotation3D = true;
                main.startSize3D = true;
                main.maxParticles = MaximumMotifs;
                var emission = motifs.emission; emission.enabled = false;
                var shape = motifs.shape; shape.enabled = false;
                var velocity = motifs.velocityOverLifetime; velocity.enabled = false;
                var force = motifs.forceOverLifetime; force.enabled = false;
                var noise = motifs.noise; noise.enabled = false;
                var inherit = motifs.inheritVelocity; inherit.enabled = false;
                var collision = motifs.collision; collision.enabled = false;
                var trails = motifs.trails; trails.enabled = false;
                var color = motifs.colorOverLifetime;
                color.enabled = true;
                Gradient fade = new Gradient();
                fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.8f, .55f), new GradientAlphaKey(0f, 1f) });
                color.color = fade;
                motifRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                motifRenderer.alignment = ParticleSystemRenderSpace.World;
                motifRenderer.shadowCastingMode = ShadowCastingMode.Off;
                motifRenderer.receiveShadows = false;
                motifRenderer.lightProbeUsage = LightProbeUsage.Off;
                motifRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            motifs.gameObject.layer = gameObject.layer;
            var settings = motifs.main;
            settings.startLifetime = profile.Lifetime;
            settings.startSizeX = profile.Width;
            settings.startSizeY = profile.ParticleSize;
            settings.startSizeZ = 1f;
            settings.startColor = Color.white;
        }

        private void ApplyMaterials()
        {
            Material material = profile.Material != null ? profile.Material : GetFallbackMaterial();
            if (ribbonProperties == null) ribbonProperties = new MaterialPropertyBlock();
            ribbonProperties.Clear();
            ribbonProperties.SetColor(BaseColorId, Color.white);
            ribbonProperties.SetFloat(UvSwapId, 1f);
            ribbonProperties.SetTexture(BaseMapId, profile.RibbonTexture != null ? profile.RibbonTexture : Texture2D.whiteTexture);
            for (int i = 0; i < ribbons.Length; i++)
            {
                if (ribbons[i] == null) continue;
                ribbons[i].sharedMaterial = material;
                ribbons[i].SetPropertyBlock(ribbonProperties);
            }
            if (motifProperties == null) motifProperties = new MaterialPropertyBlock();
            motifProperties.Clear();
            motifProperties.SetColor(BaseColorId, Color.white);
            motifProperties.SetFloat(UvSwapId, 0f);
            motifProperties.SetTexture(BaseMapId, profile.ParticleTexture != null ? profile.ParticleTexture : Texture2D.whiteTexture);
            motifRenderer.sharedMaterial = material;
            motifRenderer.SetPropertyBlock(motifProperties);
        }

        private void ClearMesh(int index)
        {
            if (meshes[index] == null || visibleVertexCounts[index] == 0) return;
            meshes[index].Clear(false);
            visibleVertexCounts[index] = 0;
        }

        private float Random01()
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (randomState & 0x00ffffffu) / 16777216f;
        }

        private static int[] CreateTriangles()
        {
            int[] result = new int[(LabFlightTrailPath.Capacity - 1) * 6];
            for (int i = 0; i < LabFlightTrailPath.Capacity - 1; i++)
            {
                int offset = i * 6, vertex = i * 2;
                result[offset] = vertex;
                result[offset + 1] = vertex + 2;
                result[offset + 2] = vertex + 1;
                result[offset + 3] = vertex + 1;
                result[offset + 4] = vertex + 2;
                result[offset + 5] = vertex + 3;
            }
            return result;
        }

        private static Material GetFallbackMaterial()
        {
            if (fallbackMaterial != null) return fallbackMaterial;
            Shader shader = Shader.Find("C6Lab/Element Ink Trail");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            fallbackMaterial = new Material(shader)
            {
                name = "Element ink trail fallback", hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            return fallbackMaterial;
        }
    }
}
