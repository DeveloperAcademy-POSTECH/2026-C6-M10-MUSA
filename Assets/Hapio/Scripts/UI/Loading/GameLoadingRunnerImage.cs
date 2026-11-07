using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    internal sealed class GameLoadingRunnerImage : UnityEngine.UI.Image
    {
        private const int Columns = 20;
        private const int HairRows = 11;

        private Vector2 forelockTip;
        private float wind;

        public void SetForelockWind(Vector2 tip, float strength)
        {
            if (tip == forelockTip && strength == wind) return;
            forelockTip = tip;
            wind = strength;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            base.OnPopulateMesh(mesh);
            if (wind <= 0f || mesh.currentVertCount != 4) return;

            // Subdivide the top of the normal Image quad, keeping its aspect, tint and atlas UVs.
            UIVertex topLeft = default, bottomRight = default;
            mesh.PopulateUIVertex(ref topLeft, 1);
            mesh.PopulateUIVertex(ref bottomRight, 3);
            mesh.Clear();

            Vector3 size = bottomRight.position - topLeft.position;
            Vector4 uvSize = bottomRight.uv0 - topLeft.uv0;
            Vector3 lift = new Vector3(-.006f, .025f, 0f) * (-size.y * wind);
            int stride = Columns + 1;
            for (int row = 0; row <= HairRows + 1; row++)
            {
                float y = row <= HairRows ? row * .02f : 1f;
                for (int column = 0; column <= Columns; column++)
                {
                    float x = column / (float)Columns;
                    var vertex = topLeft;
                    vertex.position += new Vector3(x * size.x, y * size.y);
                    vertex.position += lift * ForelockWeight(x, y);
                    vertex.uv0 += new Vector4(x * uvSize.x, y * uvSize.y);
                    mesh.AddVert(vertex);

                    if (row == 0 || column == 0) continue;
                    int bottom = row * stride + column;
                    mesh.AddTriangle(bottom - stride - 1, bottom - stride, bottom);
                    mesh.AddTriangle(bottom, bottom - 1, bottom - stride - 1);
                }
            }
        }

        private float ForelockWeight(float x, float y)
        {
            // Fade toward the hairline, with a shorter reach toward the face.
            float dx = (x - forelockTip.x) / .18f;
            float dy = (y - forelockTip.y) / (y < forelockTip.y ? .12f : .075f);
            float falloff = Mathf.Clamp01(1f - dx * dx - dy * dy);
            return falloff * falloff;
        }
    }
}
