using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #719 콤보 가이딩: 콤보 중 지금 맞혀야 할 칸과 같은 색의 결합 구슬 둘레에 그 색 빛 테두리가 생겨 두근거린다.
    /// 판이 넓어도 "이걸 던지면 된다"가 바로 보인다. 표시만 바꾼다 (판 주인 기기에서만, 판정은 Host 그대로).
    /// </summary>
    public sealed class LabOrbComboHint : MonoBehaviour
    {
        /// <summary>테두리 크기 (구슬 반지름 배).</summary>
        public const float RingScale = 3f; // #719 QA: 3.9 was too big (rings of neighbours overlapped)
        /// <summary>#719: 콤보 시계 째깍(똑딱)마다 1로 튀었다가 다음 째깍 전에 0으로 줄어드는 값. LabGourdComboHud가 매 프레임 정한다.</summary>
        public static float Beat;
        private const int RingOrder = 1;
        private static Sprite ringSprite;
        private SpriteRenderer ring;
        private Color tint = Color.white;
        private float radius = .3f;

        public bool Visible => ring != null && ring.gameObject.activeSelf;

        public static LabOrbComboHint Create(Transform orbRoot)
        {
            var item = new GameObject("Combo Hint");
            item.layer = orbRoot.gameObject.layer;
            item.transform.SetParent(orbRoot, false);
            return item.AddComponent<LabOrbComboHint>();
        }

        public void Show(bool shown, float orbRadius, Color color)
        {
            if (ring == null)
            {
                if (!shown) return;
                var part = new GameObject("Combo Hint Ring");
                part.layer = gameObject.layer;
                part.transform.SetParent(transform, false);
                ring = part.AddComponent<SpriteRenderer>();
                ring.sprite = RingSprite();
                ring.sortingOrder = RingOrder;
            }
            if (ring.gameObject.activeSelf != shown) ring.gameObject.SetActive(shown);
            radius = Mathf.Max(.01f, orbRadius);
            tint = color;
            Pose();
        }

        private void Update()
        {
            if (!Visible) return;
            Pose();
        }

        private void Pose()
        {
            if (ring == null) return;
            // #719: swells on each tick of the combo clock (똑딱) and settles before the next.
            float beat = Mathf.Clamp01(Beat);
            ring.transform.localScale = Vector3.one * (RingScale * radius * (1f + .14f * beat));
            ring.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(.55f, 1f, Mathf.Clamp01(beat)));
        }

        /// <summary>One unit across: a soft white ring (tinted by the colour to throw), bright on the inside edge.</summary>
        private static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size * 2f - 1f, v = (y + .5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(u * u + v * v);
                    float alpha = Mathf.Clamp01(1f - Mathf.Abs(distance - .72f) / (distance < .72f ? .1f : .26f));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Lab combo hint ring", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            ringSprite.name = "Lab combo hint ring";
            ringSprite.hideFlags = HideFlags.DontSave;
            return ringSprite;
        }
    }
}
