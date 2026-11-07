using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// #540 부적 구슬(물) 3D 연출. 던지면 가운데와 양옆, 3갈래 물구슬로 갈라져 날아가 각각 박을 칠한다.
    /// 떠날 때 물기둥이 소용돌이치며 솟고, 날아가는 동안 굵은 물줄기 꼬리와 흩날리는 물방울·물안개가 따라가고,
    /// 박에 맞으면 큰 물보라(물방울 왕관, 퍼지는 물결 고리, 물안개)가 터진다. 모두 그림만이고 판정에는 손대지 않는다.
    /// </summary>
    public static class LabJigwiWaterVolley
    {
        /// <summary>판 위와 날아갈 때 부적 구슬 크기 배율.</summary>
        public const float SizeScale = 1.5f;
        /// <summary>양옆 물구슬이 바깥으로 벌어지는 각도(도).</summary>
        public const float SpreadDegrees = 4f;
        /// <summary>양옆 물구슬이 출발하는 자리 간격 (구슬 반지름 배).</summary>
        public const float SideOffsetRadii = 2.6f;

        public static float Side(int k) => k == 1 ? -1f : 1f;

        private static Vector3 SideAxis(Vector3 velocity)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            return flat.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
        }

        /// <summary>k번째(1, 2) 옆 물구슬의 출발 자리. 모든 기기가 같은 값을 낸다.</summary>
        public static Vector3 ShardPosition(Vector3 position, Vector3 velocity, int k, float radius)
            => position + SideAxis(velocity) * Side(k) * radius * SideOffsetRadii;

        /// <summary>k번째(1, 2) 옆 물구슬의 출발 속도 (가운데 구슬 속도를 위축으로 조금 돌린 것).</summary>
        public static Vector3 ShardVelocity(Vector3 velocity, int k)
            => Quaternion.AngleAxis(Side(k) * SpreadDegrees, Vector3.up) * velocity;

        /// <summary>날아가는 구슬에 물줄기 꼬리와 흩날리는 물방울을 붙인다.</summary>
        public static void Follow(LabProjectile projectile, float radius)
        {
            if (projectile == null) return;
            var go = new GameObject("Jigwi Water Stream");
            go.layer = projectile.gameObject.layer;
            go.transform.position = projectile.transform.position;
            go.AddComponent<LabJigwiWaterStream>().Begin(projectile.transform, radius,
                projectile.FlightTrail != null ? projectile.FlightTrail.sharedMaterial : null);
        }

        /// <summary>떠나는 자리에서 물기둥이 소용돌이치며 솟는다.</summary>
        public static void Launch(Vector3 position, Vector3 velocity, float radius, int layer)
        {
            LabJigwiWaterFx fx = LabJigwiWaterFx.Get(layer);
            float r = Mathf.Max(.01f, radius);
            Vector3 forward = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.forward;
            const int spiral = 34;
            for (int i = 0; i < spiral; i++)
            {
                float angle = i * 2.39996f; // golden angle: evenly round the column
                float delay = i / (float)spiral * .18f;
                fx.Swirl(position, angle, r * Random.Range(.6f, 1.3f), r * Random.Range(6f, 10f), Random.Range(7f, 11f),
                    r * Random.Range(.22f, .42f), Random.Range(.45f, .7f), delay);
            }
            for (int i = 0; i < 16; i++)
            {
                Vector3 spray = (Vector3.up * 1.4f + forward * .6f + Random.insideUnitSphere * .9f).normalized;
                fx.Drop(position + Random.insideUnitSphere * r * .5f, spray * r * Random.Range(9f, 15f),
                    r * Random.Range(.18f, .34f), Random.Range(.4f, .65f));
            }
            for (int i = 0; i < 2; i++) fx.Ring(position, r * 1.2f, r * (5.5f + i * 2.5f), .45f + i * .12f, i * .07f);
            for (int i = 0; i < 6; i++)
                fx.Mist(position + Random.insideUnitSphere * r, Vector3.up * r * Random.Range(1.5f, 3f) + forward * r * 1.5f,
                    r * Random.Range(1.2f, 1.8f), r * Random.Range(3.5f, 5f), Random.Range(.5f, .75f));
        }

        /// <summary>박에 맞은 자리에서 큰 물보라가 터진다. normal은 박 바깥쪽.</summary>
        public static void Splash(Vector3 point, Vector3 normal, float radius, int layer)
        {
            LabJigwiWaterFx fx = LabJigwiWaterFx.Get(layer);
            float r = Mathf.Max(.01f, radius);
            Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.back;
            fx.Flash(point + n * r * .3f, r * 5f, .18f);
            const int crown = 48;
            for (int i = 0; i < crown; i++)
            {
                Vector3 dir = (n * 1.1f + Random.insideUnitSphere + Vector3.up * .5f).normalized;
                fx.Drop(point + n * r * .3f, dir * r * Random.Range(10f, 22f), r * Random.Range(.18f, .45f),
                    Random.Range(.5f, .85f));
            }
            for (int i = 0; i < 3; i++) fx.Ring(point + n * r * .2f, r * 1.5f, r * (7f + i * 3f), .4f + i * .1f, i * .06f);
            for (int i = 0; i < 9; i++)
                fx.Mist(point + n * r * .5f + Random.insideUnitSphere * r * 1.2f,
                    (n + Random.insideUnitSphere * .6f).normalized * r * Random.Range(2f, 5f) + Vector3.up * r * 1.5f,
                    r * Random.Range(1.6f, 2.6f), r * Random.Range(5f, 8f), Random.Range(.6f, .95f));
        }
    }

    /// <summary>#540: 날아가는 부적 물구슬을 따라가는 굵은 물줄기 꼬리 + 흩날리는 물방울·물안개. 구슬이 사라지면 꼬리를 남겨 흐려진다.</summary>
    public sealed class LabJigwiWaterStream : MonoBehaviour
    {
        private Transform source;
        private float radius, dropDebt, mistDebt, fadeUntil = -1f;
        private Vector3 last, velocity;
        private TrailRenderer trail;

        public void Begin(Transform follow, float orbRadius, Material material)
        {
            source = follow; radius = Mathf.Max(.01f, orbRadius);
            last = follow != null ? follow.position : transform.position;
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = .32f;
            trail.minVertexDistance = radius * .2f;
            trail.widthMultiplier = radius * 2.1f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.35f, .7f), new Keyframe(1f, 0f));
            trail.numCapVertices = 4;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
            trail.sharedMaterial = material != null ? material : LabJigwiWaterFx.LineMaterial;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(.92f, 1f, 1f), 0f), new GradientColorKey(new Color(.4f, .78f, 1f), .35f),
                    new GradientColorKey(new Color(.12f, .45f, .95f), 1f) },
                new[] { new GradientAlphaKey(.95f, 0f), new GradientAlphaKey(.7f, .4f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.sortingOrder = 18;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            if (source != null && source.gameObject.activeInHierarchy)
            {
                Vector3 now = source.position;
                velocity = (now - last) / dt; last = now;
                transform.position = now;
                LabJigwiWaterFx fx = LabJigwiWaterFx.Get(gameObject.layer);
                dropDebt += dt * 70f; mistDebt += dt * 14f;
                while (dropDebt >= 1f)
                {
                    dropDebt -= 1f;
                    Vector3 shed = -velocity * Random.Range(.05f, .18f) + Random.insideUnitSphere * radius * 6f + Vector3.up * radius * 2f;
                    fx.Drop(now + Random.insideUnitSphere * radius * .7f, shed, radius * Random.Range(.16f, .34f), Random.Range(.3f, .55f));
                }
                while (mistDebt >= 1f)
                {
                    mistDebt -= 1f;
                    fx.Mist(now - velocity * dt, -velocity * .05f + Random.insideUnitSphere * radius,
                        radius * .9f, radius * 2.6f, Random.Range(.3f, .45f));
                }
                return;
            }
            // The orb hit or missed: leave the stream where it ended and let it fade.
            if (fadeUntil < 0f) { fadeUntil = Time.time + (trail != null ? trail.time : 0f) + .05f; if (trail != null) trail.emitting = false; }
            if (Time.time >= fadeUntil) Destroy(gameObject);
        }
    }

    /// <summary>#540: 물 연출 알갱이(물방울·소용돌이·물안개·물결 고리·번쩍임)를 한곳에서 움직이고 그리는 카메라 쪽으로 세운다.</summary>
    public sealed class LabJigwiWaterFx : MonoBehaviour
    {
        private enum Kind { Drop, Swirl, Mist, Ring, Flash }

        private sealed class Bit
        {
            public SpriteRenderer Renderer;
            public Kind Kind;
            public Vector3 Position, Velocity, Center;
            public float Age, Life, Size, EndSize, Delay, Angle, Spin, Lift, Radial;
        }

        private const int Capacity = 600;
        private static readonly Dictionary<int, LabJigwiWaterFx> byLayer = new Dictionary<int, LabJigwiWaterFx>();
        private static Sprite dropSprite, mistSprite, ringSprite;
        private static Material lineMaterial;
        private readonly List<Bit> live = new List<Bit>();
        private readonly Stack<Bit> spare = new Stack<Bit>();
        private float gravityScale = 1f;

        /// <summary>물줄기 꼬리 기본 재질 (구슬 기본 꼬리 재질이 없을 때).</summary>
        public static Material LineMaterial => lineMaterial != null ? lineMaterial
            : lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Jigwi Water Line (runtime)", hideFlags = HideFlags.DontSave };

        public static LabJigwiWaterFx Get(int layer)
        {
            if (byLayer.TryGetValue(layer, out var fx) && fx != null) return fx;
            var go = new GameObject("Jigwi Water Fx");
            go.layer = layer;
            fx = go.AddComponent<LabJigwiWaterFx>();
            byLayer[layer] = fx;
            return fx;
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += Face;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= Face;
        private void OnDestroy() { foreach (var pair in new List<KeyValuePair<int, LabJigwiWaterFx>>(byLayer)) if (pair.Value == this) byLayer.Remove(pair.Key); }

        public void Drop(Vector3 at, Vector3 velocity, float size, float life)
        {
            Bit bit = Take(Kind.Drop, DropSprite(), 21); if (bit == null) return;
            bit.Position = at; bit.Velocity = velocity; bit.Size = size; bit.EndSize = size * .55f; bit.Life = life;
            gravityScale = Mathf.Max(gravityScale, 1f);
        }

        public void Swirl(Vector3 center, float angle, float radial, float lift, float spin, float size, float life, float delay)
        {
            Bit bit = Take(Kind.Swirl, DropSprite(), 21); if (bit == null) return;
            bit.Center = center; bit.Angle = angle; bit.Radial = radial; bit.Lift = lift; bit.Spin = spin;
            bit.Size = size; bit.EndSize = size * .4f; bit.Life = life; bit.Delay = delay;
            bit.Position = center;
        }

        public void Mist(Vector3 at, Vector3 velocity, float size, float endSize, float life)
        {
            Bit bit = Take(Kind.Mist, MistSprite(), 19); if (bit == null) return;
            bit.Position = at; bit.Velocity = velocity; bit.Size = size; bit.EndSize = endSize; bit.Life = life;
        }

        public void Ring(Vector3 at, float size, float endSize, float life, float delay)
        {
            Bit bit = Take(Kind.Ring, RingSprite(), 20); if (bit == null) return;
            bit.Position = at; bit.Size = size; bit.EndSize = endSize; bit.Life = life; bit.Delay = delay;
        }

        public void Flash(Vector3 at, float size, float life)
        {
            Bit bit = Take(Kind.Flash, MistSprite(), 22); if (bit == null) return;
            bit.Position = at; bit.Size = size * .6f; bit.EndSize = size; bit.Life = life;
        }

        private Bit Take(Kind kind, Sprite sprite, int order)
        {
            if (live.Count >= Capacity) return null;
            Bit bit = spare.Count > 0 ? spare.Pop() : new Bit();
            if (bit.Renderer == null)
            {
                var part = new GameObject("Water Bit");
                part.layer = gameObject.layer;
                part.transform.SetParent(transform, false);
                bit.Renderer = part.AddComponent<SpriteRenderer>();
                bit.Renderer.shadowCastingMode = ShadowCastingMode.Off; bit.Renderer.receiveShadows = false;
            }
            bit.Kind = kind; bit.Age = 0f; bit.Delay = 0f; bit.Velocity = Vector3.zero;
            bit.Renderer.sprite = sprite; bit.Renderer.sortingOrder = order;
            bit.Renderer.gameObject.SetActive(false);
            live.Add(bit);
            return bit;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Bit bit = live[i];
                bit.Age += dt;
                float local = bit.Age - bit.Delay;
                if (local < 0f) continue;
                float t = local / Mathf.Max(.01f, bit.Life);
                if (t >= 1f)
                {
                    bit.Renderer.gameObject.SetActive(false);
                    live.RemoveAt(i); spare.Push(bit);
                    continue;
                }
                if (!bit.Renderer.gameObject.activeSelf) bit.Renderer.gameObject.SetActive(true);
                float size = Mathf.Lerp(bit.Size, bit.EndSize, t);
                Color color;
                switch (bit.Kind)
                {
                    case Kind.Drop:
                        // Gravity scaled to the drop: bigger effects fall the same way at any world size.
                        bit.Velocity += Vector3.down * (bit.Size * 90f) * dt;
                        bit.Velocity *= Mathf.Exp(-1.2f * dt);
                        bit.Position += bit.Velocity * dt;
                        color = Color.Lerp(new Color(.9f, 1f, 1f, 1f), new Color(.3f, .7f, 1f, 1f), t);
                        color.a = 1f - t * t;
                        break;
                    case Kind.Swirl:
                    {
                        // Up and round the column, widening as it rises: a twisting water spout.
                        float a = bit.Angle + bit.Spin * local;
                        float rise = 1f - (1f - t) * (1f - t);
                        float r = bit.Radial * (1f + 1.6f * t);
                        bit.Position = bit.Center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r + Vector3.up * bit.Lift * rise;
                        color = Color.Lerp(new Color(.85f, .98f, 1f, 1f), new Color(.25f, .62f, 1f, 1f), t);
                        color.a = Mathf.Clamp01(t * 8f) * (1f - t);
                        break;
                    }
                    case Kind.Mist:
                        bit.Velocity *= Mathf.Exp(-2.5f * dt);
                        bit.Position += bit.Velocity * dt;
                        color = new Color(.75f, .92f, 1f, .55f * (1f - t) * Mathf.Clamp01(t * 6f));
                        break;
                    case Kind.Ring:
                        size = Mathf.Lerp(bit.Size, bit.EndSize, 1f - (1f - t) * (1f - t));
                        color = new Color(.7f, .93f, 1f, .9f * (1f - t));
                        break;
                    default:
                        color = new Color(.95f, 1f, 1f, .9f * (1f - t));
                        break;
                }
                bit.Renderer.transform.position = bit.Position;
                bit.Renderer.transform.localScale = Vector3.one * size;
                bit.Renderer.color = color;
            }
        }

        // Every bit stands facing whichever camera draws it (the battle camera has no MainCamera tag).
        private void Face(ScriptableRenderContext context, Camera view)
        {
            if (this == null || view == null) return;
            Quaternion facing = view.transform.rotation;
            for (int i = 0; i < live.Count; i++) if (live[i].Renderer != null) live[i].Renderer.transform.rotation = facing;
        }

        private static Sprite DropSprite() => dropSprite != null ? dropSprite : dropSprite = Make("Jigwi water drop", 48, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float body = Mathf.Clamp01((.48f - d) * 30f);
            float shine = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(-.16f, .16f)) * 8f);
            return new Color(1f, 1f, 1f, body * (.75f + .25f * shine) + shine * .2f);
        });

        private static Sprite MistSprite() => mistSprite != null ? mistSprite : mistSprite = Make("Jigwi water mist", 64, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y) * 2f;
            return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
        });

        private static Sprite RingSprite() => ringSprite != null ? ringSprite : ringSprite = Make("Jigwi water ring", 128, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.Abs(d - .42f) * 22f));
        });

        private static Sprite Make(string name, int size, System.Func<float, float, Color> pixel)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = name + " (#540 runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel((x + .5f) / size - .5f, (y + .5f) / size - .5f);
            texture.SetPixels(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            sprite.name = texture.name; sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
