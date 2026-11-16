using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>Rainbow ink moves across the title; Outline/Shadow run afterwards and keep their ink.</summary>
    [DisallowMultipleComponent]
    public sealed class LabFeverRainbowText : BaseMeshEffect
    {
        [SerializeField, Min(.01f)] private float cyclesPerSecond = .22f;
        private void LateUpdate()
        {
            if (IsActive()) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount == 0) return;
            UIVertex vertex = default(UIVertex);
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                left = Mathf.Min(left, vertex.position.x);
                right = Mathf.Max(right, vertex.position.x);
            }
            float phase = Time.unscaledTime * cyclesPerSecond;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                Color ink = Color.HSVToRGB(Mathf.Repeat(Mathf.InverseLerp(left, right, vertex.position.x) + phase, 1f), .8f, 1f);
                ink.a = vertex.color.a / 255f;
                vertex.color = ink;
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
