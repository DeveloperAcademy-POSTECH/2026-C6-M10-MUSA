using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 야광귀 피버타임 (#343) 스태미나 영롱 효과. 피버 동안 스태미나 막대의 차 있는 칸이 오색구슬(#329)처럼 노랑·초록·파랑·
    /// 빨강·갈색 사이를 쉬지 않고 건너가며, 칸마다 조금씩 늦게 바뀌어 왼쪽에서 오른쪽으로 물결이 흐른다. 피버가 시작되는
    /// 순간에는 막대가 살짝 커졌다 돌아오며 하얗게 한 번 번쩍인다. 화면 표시만 하며 스태미나 값·터치는 건드리지 않는다.
    /// 구미호 피버 타임(#334)과는 따로 돌아가고, 야광귀 스테이지에서만 켜진다.
    /// 막대는 흰 칸 모양(StaminaBarFillMask)을 왼쪽부터 채우는 그림 한 장이라, 같은 그림을 칸 수만큼 겹쳐
    /// k번째 겹은 (k+1)칸까지만 채우고 왼쪽 칸일수록 위에 둔다. 그러면 칸마다 제 색이 보인다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabYaguangFeverStaminaGlow : MonoBehaviour
    {
        [Tooltip("한 칸이 다음 색으로 넘어가는 시간 (초). 작을수록 빨리 흐른다.")]
        [Min(.05f)] public float colorSeconds = .45f;
        [Tooltip("옆 칸과 색이 어긋나는 정도 (칸 하나당 색 몇 개만큼). 물결의 폭이다.")]
        [Range(0f, 2f)] public float cellOffset = .5f;
        [Tooltip("칸 색에 흰빛을 섞는 정도 (0 = 원색). 조금 섞으면 더 맑게 빛난다.")]
        [Range(0f, 1f)] public float whiten = .12f;
        [Tooltip("피버가 시작될 때 번쩍이는 시간 (초)")]
        [Min(0f)] public float burstSeconds = .45f;
        [Tooltip("피버가 시작될 때 막대가 커지는 정도 (1 = 그대로)")]
        [Range(1f, 1.5f)] public float burstScale = 1.12f;

        private LabNetwork network;
        private Image fill;
        private RectTransform bar;
        private Image[] layers = new Image[0];
        private bool on;
        private float since;
        private Vector3 barScale = Vector3.one;

        /// <summary>지금 영롱 효과가 켜져 있는지</summary>
        public bool Glowing => on;
        /// <summary>겹쳐 둔 칸 그림 수 (테스트용)</summary>
        public int LayerCount => layers.Length;

        public void Bind(LabNetwork target, Image staminaFill)
        {
            network = target;
            fill = staminaFill;
            bar = fill != null ? fill.transform.parent as RectTransform : null;
            if (bar != null) barScale = bar.localScale;
        }

        private void LateUpdate()
        {
            bool fever = network != null && network.YaguangFeverOn && fill != null;
            if (fever != on)
            {
                on = fever;
                since = 0f;
                if (!on) Hide();
            }
            if (!on) return;
            since += Time.unscaledDeltaTime;
            Show(Cells());
        }

        private void OnDisable()
        {
            on = false;
            Hide();
        }

        /// <summary>막대 칸 수: 최대 스태미나를 구슬 하나 값으로 나눈 수 (기본 120 / 20 = 6칸).</summary>
        private int Cells()
        {
            LabConfig config = network != null ? network.config : null;
            if (config == null || config.GenerateCost <= 0f) return 6;
            return Mathf.Clamp(Mathf.RoundToInt(config.StaminaMax / config.GenerateCost), 1, 12);
        }

        private void Show(int cells)
        {
            EnsureLayers(cells);
            float burst = BurstAmount(since, burstSeconds);
            float time = Time.unscaledTime;
            for (int k = 0; k < layers.Length; k++)
            {
                Image layer = layers[k];
                if (!layer.gameObject.activeSelf) layer.gameObject.SetActive(true);
                layer.sprite = fill.sprite;
                layer.fillAmount = CellFill(fill.fillAmount, k, cells);
                Color color = CellColor(time, k, colorSeconds, cellOffset);
                color = Color.Lerp(color, Color.white, Mathf.Clamp01(whiten + burst));
                color.a = fill.color.a;
                layer.color = color;
            }
            if (bar != null) bar.localScale = barScale * Mathf.Lerp(1f, burstScale, burst);
        }

        private void Hide()
        {
            foreach (Image layer in layers)
                if (layer != null && layer.gameObject.activeSelf) layer.gameObject.SetActive(false);
            if (bar != null) bar.localScale = barScale;
        }

        /// <summary>칸 수만큼 막대 그림을 겹친다. 막대 그림 바로 위에, 왼쪽 칸 겹이 맨 위에 오도록 둔다.</summary>
        private void EnsureLayers(int cells)
        {
            if (layers.Length == cells && System.Array.TrueForAll(layers, layer => layer != null)) return;
            foreach (Image old in layers) if (old != null) Destroy(old.gameObject);
            layers = new Image[cells];
            RectTransform source = fill.rectTransform;
            for (int k = cells - 1; k >= 0; k--)
            {
                var go = new GameObject("Yaguang Fever Stamina " + (k + 1), typeof(RectTransform), typeof(Image));
                go.layer = fill.gameObject.layer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(source.parent, false);
                rect.anchorMin = source.anchorMin; rect.anchorMax = source.anchorMax;
                rect.pivot = source.pivot; rect.anchoredPosition = source.anchoredPosition;
                rect.sizeDelta = source.sizeDelta; rect.localRotation = source.localRotation;
                rect.localScale = source.localScale;
                // 막대 그림 바로 위. 나중에 만든(왼쪽) 겹일수록 위에 온다.
                rect.SetSiblingIndex(source.GetSiblingIndex() + (cells - k));
                var image = go.GetComponent<Image>();
                image.sprite = fill.sprite; image.type = fill.type;
                image.fillMethod = fill.fillMethod; image.fillOrigin = fill.fillOrigin;
                image.fillClockwise = fill.fillClockwise; image.preserveAspect = fill.preserveAspect;
                image.material = fill.material; image.raycastTarget = false;
                layers[k] = image;
            }
        }

        /// <summary>k번째 칸 겹(0부터)이 채울 양: 막대가 찬 만큼과 (k+1)칸 끝 가운데 작은 쪽.</summary>
        public static float CellFill(float fraction, int k, int cells) =>
            cells <= 0 ? 0f : Mathf.Min(Mathf.Clamp01(fraction), (k + 1) / (float)cells);

        /// <summary>time 초에 k번째 칸의 색. 칸마다 cellOffset만큼 늦게 바뀌어 왼쪽에서 오른쪽으로 물결이 흐른다.</summary>
        public static Color CellColor(float time, int k, float colorSeconds, float cellOffset)
        {
            // LabFiveColorSparkle.ColorAt은 0.9초마다 다음 색이다. 그 시계를 colorSeconds에 맞춰 늘이고 줄인다.
            float scaled = Mathf.Max(0f, time) / Mathf.Max(.05f, colorSeconds) - k * cellOffset;
            return LabFiveColorSparkle.ColorAt(Mathf.Repeat(scaled, 5f) * .9f);
        }

        /// <summary>피버가 시작된 지 seconds초 뒤 번쩍이는 정도 (0~1): 금방 차올랐다가 부드럽게 빠진다.</summary>
        public static float BurstAmount(float seconds, float burstSeconds)
        {
            if (burstSeconds <= 0f || seconds < 0f || seconds >= burstSeconds) return 0f;
            float u = seconds / burstSeconds;
            return u < .25f ? u / .25f : 1f - (u - .25f) / .75f;
        }
    }
}
