using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 콤보 성공 보상(BIG, #257) 동안 전투 화면 위에서 부적이 쏟아진다(#268, 임시 그림 `Resources/Combo/Bujeok`).
    /// 그림이 없으면 예전처럼 노란 별이 쏟아진다. 남은 시간이 줄수록 별이 줄어들어
    /// 숫자 대신 남은 시간을 보여준다. 콤보 HUD가 매 프레임 <see cref="Step"/>을 부르고, 별은 화면 맨 위에서
    /// 조합판 위쪽 가장자리까지 떨어지며 사라진다. 화면 표시만 하며 터치를 막지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabComboRewardStars : MonoBehaviour
    {
        public const int MaximumStars = 56;
        private const int TextureSize = 64;
        private static readonly Color32 InkColor = new Color32(43, 36, 32, 255);
        private static readonly Color StarColor = new Color(1f, .82f, .2f, 1f);
        private static Sprite starSprite;
        /// <summary>쏟아지는 부적 그림 (#268). 이 경로에 그림을 덮어쓰면 그대로 바뀐다.</summary>
        public const string BujeokPath = "Combo/Bujeok";
        private Sprite fallingSprite;
        private bool bujeok;
        private Color baseColor = StarColor;
        /// <summary>부적은 별보다 크게 그리는 대신 이 비율만큼만 떨어뜨린다 (#268).</summary>
        public const float BujeokCountScale = .5f;
        /// <summary>부적 크기 = 별 크기 × 이 값 (#268). 세로 기준이고 폭은 그 절반이다.</summary>
        public const float BujeokSizeScale = 2.7f;

        [Tooltip("보상이 막 시작됐을 때 초당 떨어지는 별 수. 남은 시간 비율만큼 줄어든다.")]
        [SerializeField, Min(0f)] private float starsPerSecond = 16f;
        [Tooltip("보상이 끝나기 직전에도 떨어지는 최소 별 수 (초당)")]
        [SerializeField, Min(0f)] private float minimumStarsPerSecond = 2.5f;
        [Tooltip("보상이 시작될 때 한꺼번에 쏟아지는 별 수")]
        [SerializeField, Range(0, MaximumStars)] private int startBurst = 14;

        private RectTransform area;
        private readonly Image[] stars = new Image[MaximumStars];
        private readonly Vector2[] velocity = new Vector2[MaximumStars];
        private readonly float[] spin = new float[MaximumStars];
        private readonly float[] sway = new float[MaximumStars];
        private readonly float[] age = new float[MaximumStars];
        private readonly bool[] alive = new bool[MaximumStars];
        private float unit = 1f;
        private float pending;
        private uint random = 0x9e3779b9u;

        public bool Emitting { get; private set; }
        public int ActiveCount { get; private set; }
        public float StarsPerSecondNow { get; private set; }
        public int StartBurst => startBurst;
        /// <summary>별 대신 부적 그림을 쓰는지 (#268)</summary>
        public bool UsesBujeok => bujeok;

        /// <summary>HUD 레이어 안에 별 영역을 만든다. unit은 HUD 크기 비율(칸 크기 / 64).</summary>
        public void Initialize(RectTransform parent, float hudUnit)
        {
            if (area != null) return;
            unit = Mathf.Max(.1f, hudUnit);
            area = new GameObject("Combo Reward Stars", typeof(RectTransform)).GetComponent<RectTransform>();
            area.gameObject.layer = parent.gameObject.layer;
            area.SetParent(parent, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = area.offsetMax = Vector2.zero;
            Sprite art = Resources.Load<Sprite>(BujeokPath);
            bujeok = art != null;
            fallingSprite = bujeok ? art : StarSprite();
            // 부적 그림은 제 색 그대로, 별은 노랗게 칠한다.
            baseColor = bujeok ? Color.white : StarColor;
            for (int i = 0; i < MaximumStars; i++)
            {
                var rect = new GameObject("Star", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.gameObject.layer = area.gameObject.layer;
                rect.SetParent(area, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                Image image = rect.gameObject.AddComponent<Image>();
                image.sprite = fallingSprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = baseColor;
                rect.gameObject.SetActive(false);
                stars[i] = image;
            }
        }

        public RectTransform Area => area;

        /// <summary>보상이 막 시작됐다: 별이 한꺼번에 쏟아진다.</summary>
        public void Burst(float bottomY) =>
            Burst(bujeok ? Mathf.RoundToInt(startBurst * BujeokCountScale) : startBurst, bottomY);

        private void Burst(int count, float bottomY)
        {
            for (int i = 0; i < count; i++) Spawn(bottomY, true);
        }

        /// <summary>
        /// 한 프레임 진행. emitting이면 남은 시간 비율(1 → 0)만큼 별을 새로 떨어뜨린다. bottomY는 별이 사라지는
        /// 높이(이 영역 기준 로컬 y, 보통 조합판 위쪽 가장자리)다. 멈춰도 이미 떨어지던 별은 끝까지 떨어진다.
        /// </summary>
        public void Step(bool emitting, float fraction, float deltaTime, float bottomY)
        {
            if (area == null) return;
            Emitting = emitting;
            deltaTime = Mathf.Max(0f, deltaTime);
            float countScale = bujeok ? BujeokCountScale : 1f;
            StarsPerSecondNow = emitting
                ? Mathf.Max(minimumStarsPerSecond, starsPerSecond * Mathf.Clamp01(fraction)) * countScale : 0f;
            pending += StarsPerSecondNow * deltaTime;
            while (pending >= 1f)
            {
                pending -= 1f;
                Spawn(bottomY, false);
            }
            if (!emitting) pending = 0f;

            int count = 0;
            float top = area.rect.yMax;
            for (int i = 0; i < MaximumStars; i++)
            {
                if (!alive[i]) continue;
                RectTransform rect = stars[i].rectTransform;
                age[i] += deltaTime;
                Vector2 position = rect.anchoredPosition + velocity[i] * deltaTime;
                position.x += Mathf.Sin(age[i] * 3f + sway[i]) * 18f * unit * deltaTime;
                rect.anchoredPosition = position;
                rect.localRotation = Quaternion.Euler(0f, 0f, rect.localEulerAngles.z + spin[i] * deltaTime);
                // 떨어지는 길의 마지막 30%에서 점점 투명해진다.
                float travel = Mathf.InverseLerp(top, bottomY, position.y);
                Color color = baseColor;
                color.a = travel < .7f ? 1f : Mathf.Clamp01(1f - (travel - .7f) / .3f);
                stars[i].color = color;
                if (position.y <= bottomY || color.a <= 0f)
                {
                    alive[i] = false;
                    rect.gameObject.SetActive(false);
                    continue;
                }
                count++;
            }
            ActiveCount = count;
        }

        /// <summary>남은 별을 모두 지운다 (새 판, 전투 끝).</summary>
        public void Clear()
        {
            pending = 0f;
            Emitting = false;
            ActiveCount = 0;
            for (int i = 0; i < MaximumStars; i++)
            {
                alive[i] = false;
                if (stars[i] != null) stars[i].gameObject.SetActive(false);
            }
        }

        private void Spawn(float bottomY, bool burst)
        {
            int slot = -1;
            for (int i = 0; i < MaximumStars && slot < 0; i++) if (!alive[i]) slot = i;
            if (slot < 0) return;
            Rect rect = area.rect;
            RectTransform star = stars[slot].rectTransform;
            float size = Mathf.Lerp(18f, 34f, Next()) * unit;
            // 부적은 세로로 긴 종이라 별보다 조금 크게, 폭은 높이의 절반으로 그린다.
            if (bujeok) size *= BujeokSizeScale;
            star.sizeDelta = bujeok ? new Vector2(size * .5f, size) : new Vector2(size, size);
            float x = Mathf.Lerp(rect.xMin + size, rect.xMax - size, Next());
            // 한꺼번에 쏟아질 때는 위쪽 띠 여러 높이에서 시작해 비처럼 보이게 한다.
            float y = rect.yMax + size + (burst ? Next() * (rect.yMax - bottomY) * .35f * -1f : 0f);
            star.anchoredPosition = new Vector2(x, y);
            // 부적은 거꾸로 뒤집히지 않고 살짝 기운 채 팔랑이며, 별은 아무 방향으로나 돈다.
            star.localRotation = Quaternion.Euler(0f, 0f, bujeok ? Mathf.Lerp(-35f, 35f, Next()) : Next() * 360f);
            velocity[slot] = new Vector2(Mathf.Lerp(-30f, 30f, Next()), -Mathf.Lerp(260f, 460f, Next())) * unit;
            spin[slot] = bujeok ? Mathf.Lerp(-70f, 70f, Next()) : Mathf.Lerp(-220f, 220f, Next());
            sway[slot] = Next() * Mathf.PI * 2f;
            age[slot] = 0f;
            alive[slot] = true;
            stars[slot].color = baseColor;
            star.gameObject.SetActive(true);
        }

        private float Next()
        {
            random ^= random << 13;
            random ^= random >> 17;
            random ^= random << 5;
            return (random & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>진한 테두리의 노란 다섯 꼭짓점 별 (HUD의 손그림 느낌과 같게).</summary>
        private static Sprite StarSprite()
        {
            if (starSprite != null) return starSprite;
            var pixels = new Color[TextureSize * TextureSize];
            Vector2 c = new Vector2(TextureSize * .5f, TextureSize * .5f);
            float outer = TextureSize * .48f, inner = outer * .45f;
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * .5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                points[i] = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    Vector2 p = new Vector2(x + .5f, y + .5f);
                    float edge = float.MaxValue;
                    bool inside = false;
                    for (int i = 0, j = 9; i < 10; j = i++)
                    {
                        Vector2 a = points[i], b = points[j];
                        if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                        Vector2 ab = b - a;
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                        edge = Mathf.Min(edge, Vector2.Distance(p, a + ab * t));
                    }
                    float signed = inside ? -edge : edge;
                    float alpha = Mathf.Clamp01(.5f - signed);
                    float ink = Mathf.Clamp01(signed + 4f + .5f);
                    Color color = Color.Lerp(Color.white, (Color)InkColor, ink);
                    color.a = alpha;
                    pixels[y * TextureSize + x] = color;
                }
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "Lab combo reward star", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            starSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(.5f, .5f), 100f);
            starSprite.name = "Lab combo reward star";
            starSprite.hideFlags = HideFlags.DontSave;
            return starSprite;
        }

        private void OnDestroy()
        {
            if (area != null) Destroy(area.gameObject);
        }
    }
}
