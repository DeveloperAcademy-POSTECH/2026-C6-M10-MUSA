using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 타이틀에 낙엽이 바람에 날려 떨어진다 (#467). 정해진 수의 나뭇잎을 돌려 쓴다. 위나 오른쪽에서 나와 왼쪽 아래로
    /// 흔들리며 돌고 뒤집히다가, 화면을 벗어나면 다시 위·오른쪽에서 나온다. 깃발과 같은 돌풍(TitleWindWave.Gust)이 불면
    /// 왼쪽으로 더 빨리 쓸려 간다. 고정된 씨앗으로 무작위를 정하므로 켤 때마다 같은 모습으로 시작한다.
    /// 구름처럼 게임 시계가 멈춰도 움직인다. 좌표는 이 사각형(타이틀 그림 402x874)의 왼쪽 위 기준이다.
    /// 나뭇잎 오브젝트는 실행 중에 만들고 프리팹에는 저장하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class TitleFallingLeaves : MonoBehaviour
    {
        private const float Margin = 40;
        private const float MaxStep = .1f;

        [InspectorName("나뭇잎 그림")] public Sprite[] sprites;
        [InspectorName("나뭇잎 수"), Range(0, 24)] public int count = 9;
        // 배경에 그려진 낙엽(8~12)보다 커야 앞에서 날리는 것으로 보인다.
        [InspectorName("크기 (최소~최대)")] public Vector2 size = new Vector2(20, 32);
        [InspectorName("떨어지는 속도 (최소~최대, 초당)")] public Vector2 fall = new Vector2(40, 70);
        [InspectorName("바람 속도 (초당, 왼쪽으로)")] public float wind = 20;
        [InspectorName("돌풍 때 더 쓸려 가는 속도 (초당)"), Min(0)] public float gustPush = 110;
        [InspectorName("돌풍 간격(초)"), Min(1)] public float gustSeconds = 6;
        [InspectorName("좌우 흔들림 폭"), Min(0)] public float sway = 12;
        [InspectorName("회전 속도 (최대, 도/초)"), Min(0)] public float spin = 140;
        [InspectorName("씨앗")] public int seed = 467;

        private struct Leaf
        {
            public RectTransform rect;
            public Vector2 position;
            public float fall, angle, spin, swayPhase, swaySpeed, flipPhase, flipSpeed;
        }

        private Leaf[] leaves = new Leaf[0];
        private System.Random random;
        private float elapsed;

        public int Count => leaves.Length;
        public int Recycled { get; private set; }
        public RectTransform LeafAt(int index) => leaves[index].rect;
        /// <summary>떨어지는 중인 위치(흔들림 전, 왼쪽 위 기준·아래로 +).</summary>
        public Vector2 PositionAt(int index) => leaves[index].position;
        private Vector2 Area => ((RectTransform)transform).rect.size;

        private void OnEnable() => Restart();
        private void Update() => Advance(Mathf.Min(Time.unscaledDeltaTime, MaxStep));

        /// <summary>바람이 이 순간 나뭇잎을 미는 속도 (초당, 왼쪽 -x·아래 +y). 돌풍이면 옆으로 더, 아래로 조금 더 빠르다.</summary>
        public static Vector2 Drift(float gust, float wind, float gustPush, float fall) =>
            new Vector2(-(wind + gustPush * gust), fall * (1f + .4f * gust));

        /// <summary>씨앗으로 처음 모습을 다시 만든다. 나뭇잎이 처음부터 화면 곳곳에 흩어져 있다.</summary>
        public void Restart()
        {
            random = new System.Random(seed);
            elapsed = 0;
            Recycled = 0;
            int wanted = sprites == null || sprites.Length == 0 ? 0 : Mathf.Max(0, count);
            if (leaves.Length != wanted)
            {
                foreach (var leaf in leaves)
                {
                    if (leaf.rect == null) continue;
                    if (Application.isPlaying) Destroy(leaf.rect.gameObject); else DestroyImmediate(leaf.rect.gameObject);
                }
                leaves = new Leaf[wanted];
                for (int i = 0; i < wanted; i++) leaves[i].rect = Create(i);
            }
            for (int i = 0; i < leaves.Length; i++) Spawn(ref leaves[i], true);
            Place();
        }

        public void Advance(float seconds)
        {
            if (seconds <= 0f || leaves.Length == 0) return;
            elapsed += seconds;
            float gust = TitleWindWave.Gust(elapsed, gustSeconds);
            Vector2 area = Area;
            for (int i = 0; i < leaves.Length; i++)
            {
                ref Leaf leaf = ref leaves[i];
                leaf.position += Drift(gust, wind, gustPush, leaf.fall) * seconds;
                leaf.angle += leaf.spin * (1f + gust) * seconds;
                if (leaf.position.y > area.y + Margin || leaf.position.x < -Margin) { Spawn(ref leaf, false); Recycled++; }
            }
            Place();
        }

        private RectTransform Create(int index)
        {
            var rect = (RectTransform)new GameObject("Leaf " + index, typeof(RectTransform)).transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return rect;
        }

        // 처음에는 화면 곳곳에서, 그다음부터는 위쪽(오른쪽으로 더 넓게)이나 오른쪽 가장자리 위쪽에서 나온다.
        private void Spawn(ref Leaf leaf, bool anywhere)
        {
            Vector2 area = Area;
            if (anywhere) leaf.position = new Vector2(Range(0, area.x + Margin), Range(-Margin, area.y));
            else if (Range(0, 1) < .35f) leaf.position = new Vector2(area.x + Margin * .5f, Range(-Margin, area.y * .6f));
            else leaf.position = new Vector2(Range(0, area.x + Margin * 4), -Margin * .5f);
            leaf.fall = Range(fall.x, fall.y);
            leaf.angle = Range(0, 360);
            leaf.spin = Range(-spin, spin);
            leaf.swayPhase = Range(0, 2 * Mathf.PI);
            leaf.swaySpeed = Range(1.5f, 3.5f);
            leaf.flipPhase = Range(0, 2 * Mathf.PI);
            leaf.flipSpeed = Range(2f, 5f);
            var image = leaf.rect.GetComponent<Image>();
            image.sprite = sprites[random.Next(sprites.Length)];
            Vector2 shape = image.sprite.rect.size;
            leaf.rect.sizeDelta = shape / Mathf.Max(shape.x, shape.y) * Range(size.x, size.y);
        }

        private void Place()
        {
            foreach (var leaf in leaves)
            {
                float swayed = leaf.position.x + sway * Mathf.Sin(leaf.swayPhase + leaf.swaySpeed * elapsed);
                leaf.rect.anchoredPosition = new Vector2(swayed, -leaf.position.y);
                leaf.rect.localEulerAngles = new Vector3(0, 0, leaf.angle);
                // 바람에 뒤집히며 떨어진다: 가로로 접혔다 펴지기를 되풀이한다.
                leaf.rect.localScale = new Vector3(Mathf.Cos(leaf.flipPhase + leaf.flipSpeed * elapsed), 1, 1);
            }
        }

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
