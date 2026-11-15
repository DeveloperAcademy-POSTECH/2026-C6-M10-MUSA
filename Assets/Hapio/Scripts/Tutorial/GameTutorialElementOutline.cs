using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 오행 색 글자에만 얇은 먹색 테두리 (#727). 리치 텍스트의 &lt;color&gt;는 그 글자 정점에 색을 넣으므로, 정점 색이 오행 색인 글자만 골라
    /// 테두리 사본을 뒤에 깔고 나머지 글자는 그대로 둔다(Unity의 Outline은 글 전체에 두른다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameTutorialElementOutline : BaseMeshEffect
    {
        public Color outlineColor = new Color(.12f, .1f, .09f, 1f);
        [Min(0f)] public float width = 1.2f;
        private static readonly List<UIVertex> Source = new List<UIVertex>();
        private static readonly List<UIVertex> Built = new List<UIVertex>();
        private static readonly Vector2[] Offsets = { new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f) };

        /// <summary>마지막으로 테두리를 두른 글자 수 (검사·확인용).</summary>
        public int OutlinedGlyphs { get; private set; }

        public override void ModifyMesh(VertexHelper mesh)
        {
            OutlinedGlyphs = 0;
            if (!IsActive() || width <= 0f || mesh.currentVertCount == 0) return;
            Source.Clear();
            Built.Clear();
            mesh.GetUIVertexStream(Source);
            // Each glyph is two triangles (6 vertices) of one colour.
            for (int quad = 0; quad + 5 < Source.Count; quad += 6)
            {
                if (!GameTutorialEmphasis.IsElementColor(Source[quad].color)) continue;
                OutlinedGlyphs++;
                foreach (Vector2 offset in Offsets)
                    for (int i = 0; i < 6; i++)
                    {
                        UIVertex copy = Source[quad + i];
                        copy.position += (Vector3)(offset * width);
                        Color32 ink = outlineColor;
                        ink.a = (byte)(ink.a * copy.color.a / 255);
                        copy.color = ink;
                        Built.Add(copy);
                    }
            }
            if (OutlinedGlyphs == 0) return;
            Built.AddRange(Source); // the glyphs themselves over their outlines
            mesh.Clear();
            mesh.AddUIVertexTriangleStream(Built);
        }
    }
}
