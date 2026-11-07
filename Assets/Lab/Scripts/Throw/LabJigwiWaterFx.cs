using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>#540: 물 연출 알갱이(물방울·소용돌이·물안개·물결 고리·번쩍임)를 한곳에서 움직이고 그리는 카메라 쪽으로 세운다.</summary>
    public sealed class LabJigwiWaterFx : MonoBehaviour
    {
        private enum Kind { Drop, Swirl, Mist, Ring, Flash, Steam }

        private sealed class Bit
        {
            public SpriteRenderer Renderer;
            public Kind Kind;
            public Vector3 Position, Velocity, Center;
            public float Age, Life, Size, EndSize, Delay, Angle, Spin, Lift, Radial;
            public Color Tint;
        }

        private const int Capacity = 600;
        private static readonly Dictionary<int, LabJigwiWaterFx> byLayer = new Dictionary<int, LabJigwiWaterFx>();
        private static Sprite dropSprite, mistSprite, ringSprite;
        private static Material lineMaterial;
        private readonly List<Bit> live = new List<Bit>();
        private readonly Stack<Bit> spare = new Stack<Bit>();

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

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Face;
            RenderPipelineManager.endCameraRendering += Unshake;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Face;
            RenderPipelineManager.endCameraRendering -= Unshake;
        }
        private void OnDestroy() { foreach (var pair in new List<KeyValuePair<int, LabJigwiWaterFx>>(byLayer)) if (pair.Value == this) byLayer.Remove(pair.Key); }

        public void Drop(Vector3 at, Vector3 velocity, float size, float life)
        {
            Bit bit = Take(Kind.Drop, DropSprite(), 21); if (bit == null) return;
            bit.Position = at; bit.Velocity = velocity; bit.Size = size; bit.EndSize = size * .55f; bit.Life = life;
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

        public void Flash(Vector3 at, float size, float life) => Flash(at, size, life, new Color(.95f, 1f, 1f, .9f));

        /// <summary>A bright burst that quickly fades; a huge one washes over the whole view (screen flash).</summary>
        public void Flash(Vector3 at, float size, float life, Color tint)
        {
            Bit bit = Take(Kind.Flash, MistSprite(), 23); if (bit == null) return;
            bit.Position = at; bit.Size = size * .6f; bit.EndSize = size; bit.Life = life; bit.Tint = tint;
        }

        /// <summary>#540: a thick white puff of steam that rises, swells and slowly thins (fire put out by water).</summary>
        public void Steam(Vector3 at, Vector3 velocity, float size, float endSize, float life, float delay)
        {
            Bit bit = Take(Kind.Steam, MistSprite(), 20); if (bit == null) return;
            bit.Position = at; bit.Velocity = velocity; bit.Size = size; bit.EndSize = endSize; bit.Life = life; bit.Delay = delay;
        }

        // #540: a short camera shake on every perspective camera, applied only while it renders (no rig is moved).
        private static float shakeUntil, shakeStrength, shakeSeconds;
        private static readonly Dictionary<Camera, Vector3> shaken = new Dictionary<Camera, Vector3>();

        /// <summary>Shakes the 3D view for seconds, by strength world units at the start, easing out.</summary>
        public static void Shake(float strength, float seconds)
        {
            shakeStrength = Mathf.Max(shakeStrength * Mathf.Clamp01((shakeUntil - Time.unscaledTime) / Mathf.Max(.01f, shakeSeconds)), strength);
            shakeSeconds = Mathf.Max(.01f, seconds);
            shakeUntil = Time.unscaledTime + shakeSeconds;
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
                    case Kind.Steam:
                        bit.Velocity *= Mathf.Exp(-1.4f * dt);
                        bit.Position += bit.Velocity * dt;
                        size = Mathf.Lerp(bit.Size, bit.EndSize, 1f - (1f - t) * (1f - t));
                        color = new Color(1f, 1f, 1f, .8f * Mathf.Clamp01(t * 5f) * (1f - t) * (1f - t * .3f));
                        break;
                    default:
                        color = bit.Tint; color.a *= 1f - t;
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
            if (!view.orthographic && Time.unscaledTime < shakeUntil && !shaken.ContainsKey(view))
            {
                float left = (shakeUntil - Time.unscaledTime) / shakeSeconds;
                float amount = shakeStrength * left * left;
                float time = Time.unscaledTime * 38f;
                Vector3 offset = view.transform.right * (Mathf.PerlinNoise(time, .3f) - .5f) * 2f * amount
                    + view.transform.up * (Mathf.PerlinNoise(.7f, time) - .5f) * 2f * amount;
                shaken[view] = view.transform.position;
                view.transform.position += offset;
            }
            Quaternion facing = view.transform.rotation;
            for (int i = 0; i < live.Count; i++) if (live[i].Renderer != null) live[i].Renderer.transform.rotation = facing;
        }

        private void Unshake(ScriptableRenderContext context, Camera view)
        {
            if (view != null && shaken.TryGetValue(view, out Vector3 home)) { view.transform.position = home; shaken.Remove(view); }
        }

        internal static Sprite DropSprite() => dropSprite != null ? dropSprite : dropSprite = Make("Jigwi water drop", 48, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float body = Mathf.Clamp01((.48f - d) * 30f);
            float shine = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(-.16f, .16f)) * 8f);
            return new Color(1f, 1f, 1f, body * (.75f + .25f * shine) + shine * .2f);
        });

        internal static Sprite MistSprite() => mistSprite != null ? mistSprite : mistSprite = Make("Jigwi water mist", 64, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y) * 2f;
            return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
        });

        internal static Sprite RingSprite() => ringSprite != null ? ringSprite : ringSprite = Make("Jigwi water ring", 128, (x, y) =>
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
