using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 타이틀의 깃발·리본이 바람에 휘날린다 (#467). 그림 한 장을 잘게 나눠, 묶인 곳은 그대로 두고 끝으로 갈수록 크게
    /// 물결치게 한다. 정해진 간격마다 돌풍이 불어 잠깐 더 세게 기울고 흔들린다(같은 타이틀의 모든 조각이 같은 순간에).
    /// 구름처럼 게임 시계가 멈춰도 움직인다. 셰이더 없이 UI 메시만 바꾸며 RectTransform·스프라이트는 건드리지 않는다.
    /// Simple 이미지에 붙인다. 묶인 곳·끝은 그림 사각형 안의 비율(왼쪽 아래 0,0 ~ 오른쪽 위 1,1)이다.
    /// </summary>
    public sealed class TitleWindWave : BaseMeshEffect
    {
        private const float GustShare = .35f;

        [InspectorName("묶인 곳 (0~1)")] public Vector2 pinned = new Vector2(.5f, 1);
        [InspectorName("끝 (0~1)")] public Vector2 tip = new Vector2(.5f, 0);
        [InspectorName("흔들림 폭"), Min(0)] public float amplitude = 3;
        [InspectorName("바람에 기우는 정도")] public float lean;
        [InspectorName("물결 수"), Min(0)] public float waves = 1;
        [InspectorName("초당 물결"), Min(0)] public float speed = .6f;
        [InspectorName("뻣뻣함"), Min(.1f)] public float stiffness = 1.5f;
        [InspectorName("시작 어긋남(초)")] public float phase;
        [InspectorName("돌풍 세기"), Min(0)] public float gustStrength = 1;
        [InspectorName("돌풍 간격(초)"), Min(1)] public float gustSeconds = 6;
        [InspectorName("가로 칸"), Range(1, 32)] public int columns = 8;
        [InspectorName("세로 칸"), Range(1, 48)] public int rows = 16;
        private float elapsed;

        protected override void OnEnable() { elapsed = 0; base.OnEnable(); }
        private void Update() => Sample(elapsed + Time.unscaledDeltaTime);

        /// <summary>그림을 이 시각의 모양으로 다시 그리게 한다.</summary>
        public void Sample(float seconds)
        {
            elapsed = seconds;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        /// <summary>대부분 0이고, 간격마다 한 번 부드럽게 1까지 올랐다가 내려온다(간격의 가운데부터 35% 동안).</summary>
        public static float Gust(float seconds, float period)
        {
            if (period <= 0f) return 0f;
            float t = Mathf.Repeat(seconds, period) / period - .5f;
            if (t < 0f || t >= GustShare) return 0f;
            float rise = Mathf.Sin(Mathf.PI * t / GustShare);
            return rise * rise;
        }

        /// <summary>그림 사각형 안의 한 점이 이 시각에 옮겨지는 양. 묶인 곳은 0이고 끝으로 갈수록 커진다.</summary>
        public Vector2 Offset(Vector2 local, Rect rect, float seconds)
        {
            Vector2 from = rect.min + Vector2.Scale(pinned, rect.size), axis = rect.min + Vector2.Scale(tip, rect.size) - from;
            float length = axis.sqrMagnitude;
            if (length <= 0f) return Vector2.zero;
            float along = Mathf.Clamp01(Vector2.Dot(local - from, axis) / length);
            float wind = 1f + gustStrength * Gust(seconds, gustSeconds);
            float wave = Mathf.Sin(2f * Mathf.PI * (waves * along - speed * (seconds + phase)));
            return new Vector2(-axis.y, axis.x).normalized * (Mathf.Pow(along, stiffness) * wind * (lean + amplitude * wave));
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var vertex = new UIVertex();
            vh.PopulateUIVertex(ref vertex, 0);
            Vector2 min = vertex.position, max = min, uvMin = vertex.uv0, uvMax = uvMin;
            for (int i = 1; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                min = Vector2.Min(min, vertex.position); max = Vector2.Max(max, vertex.position);
                uvMin = Vector2.Min(uvMin, vertex.uv0); uvMax = Vector2.Max(uvMax, vertex.uv0);
            }
            Color32 color = vertex.color;
            Rect rect = graphic.rectTransform.rect;
            int across = Mathf.Max(1, columns), down = Mathf.Max(1, rows);
            vh.Clear();
            for (int y = 0; y <= down; y++)
                for (int x = 0; x <= across; x++)
                {
                    float u = (float)x / across, v = (float)y / down;
                    var point = new Vector2(Mathf.Lerp(min.x, max.x, u), Mathf.Lerp(min.y, max.y, v));
                    vh.AddVert(point + Offset(point, rect, elapsed), color,
                        new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, u), Mathf.Lerp(uvMin.y, uvMax.y, v)));
                }
            for (int y = 0; y < down; y++)
                for (int x = 0; x < across; x++)
                {
                    int corner = y * (across + 1) + x;
                    vh.AddTriangle(corner, corner + across + 1, corner + across + 2);
                    vh.AddTriangle(corner + across + 2, corner + 1, corner);
                }
        }
    }
}
