using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 (#477)의 한 방향 화살표 (2단계: 음구슬 → 양구슬). 몸통 사각형과 머리 삼각형을 메시 한 장으로 그린다.
    /// 시작·끝 점은 이 그래픽의 local 좌표다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GameTutorialArrow : MaskableGraphic
    {
        private Vector2 from, to;
        private float width = 7f, headLength = 18f, headWidth = 22f;

        public Vector2 From => from;
        public Vector2 To => to;

        public void Set(Vector2 start, Vector2 end, float shaftWidth, float tipLength, float tipWidth)
        {
            shaftWidth = Mathf.Max(0f, shaftWidth);
            tipLength = Mathf.Max(0f, tipLength);
            tipWidth = Mathf.Max(0f, tipWidth);
            if (start == from && end == to && Mathf.Approximately(shaftWidth, width)
                && Mathf.Approximately(tipLength, headLength) && Mathf.Approximately(tipWidth, headWidth)) return;
            from = start;
            to = end;
            width = shaftWidth;
            headLength = tipLength;
            headWidth = tipWidth;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2 line = to - from;
            float length = line.magnitude;
            if (length < 1e-3f) return;
            Vector2 along = line / length, across = new Vector2(-along.y, along.x);
            // A short arrow (two orbs side by side) keeps a shaft: the head takes at most 60% and shrinks in proportion.
            float head = Mathf.Min(headLength, length * .6f);
            float headScale = headLength > 0f ? head / headLength : 1f;
            Vector2 neck = to - along * head;
            Color32 tint = color;
            if (length > head)
            {
                Vector2 side = across * (width * .5f);
                mesh.AddVert(from - side, tint, Vector4.zero);
                mesh.AddVert(from + side, tint, Vector4.zero);
                mesh.AddVert(neck + side, tint, Vector4.zero);
                mesh.AddVert(neck - side, tint, Vector4.zero);
                mesh.AddTriangle(0, 1, 2);
                mesh.AddTriangle(2, 3, 0);
            }
            int tip = mesh.currentVertCount;
            Vector2 wing = across * (headWidth * headScale * .5f);
            mesh.AddVert(neck - wing, tint, Vector4.zero);
            mesh.AddVert(neck + wing, tint, Vector4.zero);
            mesh.AddVert(to, tint, Vector4.zero);
            mesh.AddTriangle(tip, tip + 1, tip + 2);
        }
    }
}
