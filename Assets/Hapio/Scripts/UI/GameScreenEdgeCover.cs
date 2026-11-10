using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 화면 전체를 덮는 어두운 막이 화면 끝까지 그려지게 한다 (#631).
    /// - 메뉴는 402×874 기준 틀을 화면 안에 맞춰 키운다(<see cref="GameLobbyView"/>의 FitScreen). 화면 비율이 기준과 조금만 달라도
    ///   틀 밖 좌우(또는 위아래)에 얇은 띠가 남고, 틀 안에 저장된 막은 그 띠를 덮지 못해 화면 끝에 밝은 선이 보였다.
    /// - 이 컴포넌트는 막이 "그리는 모양"만 화면 끝까지 늘린다. 저장된 크기·자리, 막 안에 든 창의 자리, 누를 수 있는 영역은 그대로다.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public sealed class GameScreenEdgeCover : BaseMeshEffect
    {
        private static readonly Vector3[] Corners = new Vector3[4];
        private Rect screenWhenDrawn;

        /// <summary>The area last drawn, in this graphic's own local units: its rect grown out to the screen's edges.</summary>
        public Rect DrawnArea { get; private set; }

        /// <summary>Adds the cover to this full-screen dim once (a second call keeps the first).</summary>
        public static GameScreenEdgeCover Ensure(Graphic dim)
        {
            if (dim == null) return null;
            GameScreenEdgeCover cover = dim.GetComponent<GameScreenEdgeCover>();
            return cover != null ? cover : dim.gameObject.AddComponent<GameScreenEdgeCover>();
        }

        /// <summary>The graphic's own rect, grown on each side that stops short of the screen. Never smaller than its own rect.</summary>
        public static Rect Covering(Rect own, Rect screen) => Rect.MinMaxRect(
            Mathf.Min(own.xMin, screen.xMin), Mathf.Min(own.yMin, screen.yMin),
            Mathf.Max(own.xMax, screen.xMax), Mathf.Max(own.yMax, screen.yMax));

        /// <summary>Moves a point drawn inside <paramref name="from"/> to the same place inside <paramref name="to"/>.</summary>
        public static Vector3 Stretch(Vector3 point, Rect from, Rect to) => new Vector3(
            to.xMin + (point.x - from.xMin) * (from.width > 0f ? to.width / from.width : 1f),
            to.yMin + (point.y - from.yMin) * (from.height > 0f ? to.height / from.height : 1f),
            point.z);

        /// <summary>The whole screen (its root canvas) in this graphic's local units.</summary>
        private Rect ScreenInLocal()
        {
            RectTransform rect = graphic.rectTransform;
            Canvas canvas = graphic.canvas;
            if (canvas == null) return rect.rect;
            ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(Corners);
            Vector2 a = rect.InverseTransformPoint(Corners[0]), b = rect.InverseTransformPoint(Corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            Rect own = graphic.rectTransform.rect;
            screenWhenDrawn = ScreenInLocal();
            DrawnArea = Covering(own, screenWhenDrawn);
            if (DrawnArea == own) return;
            UIVertex vertex = default;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                vertex.position = Stretch(vertex.position, own, DrawnArea);
                mesh.SetUIVertex(vertex, i);
            }
        }

        private void LateUpdate()
        {
            // The menu refits its frame every frame; draw again when the screen's edges moved (a turned or resized screen).
            if (!IsActive() || graphic == null) return;
            Rect now = ScreenInLocal();
            if (!Near(now.min, screenWhenDrawn.min) || !Near(now.max, screenWhenDrawn.max)) graphic.SetVerticesDirty();
        }

        private static bool Near(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) < .01f && Mathf.Abs(a.y - b.y) < .01f;
    }
}
