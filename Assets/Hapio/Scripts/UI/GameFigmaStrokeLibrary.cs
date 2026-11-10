using System;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Original Figma brush surfaces. Bounds retain the stroke outside the component's click/layout rect.</summary>
    public sealed class GameFigmaStrokeLibrary : ScriptableObject
    {
        [Serializable] public sealed class Surface
        {
            public string name, source;
            public Sprite sprite;
            public Vector2 size, offset, exportSize;

            public Rect Bounds(Rect logical)
            {
                float x = logical.width / size.x, y = logical.height / size.y;
                return new Rect(logical.xMin + offset.x * x,
                    logical.yMax - (offset.y + exportSize.y) * y, exportSize.x * x, exportSize.y * y);
            }

            public void Draw(VertexHelper mesh, Rect logical, Color tint)
            {
                mesh.Clear();
                if (sprite == null) return;
                Rect r = Bounds(logical); Vector4 uv = DataUtility.GetOuterUV(sprite);
                mesh.AddVert(new Vector3(r.xMin,r.yMin),tint,new Vector2(uv.x,uv.y));
                mesh.AddVert(new Vector3(r.xMin,r.yMax),tint,new Vector2(uv.x,uv.w));
                mesh.AddVert(new Vector3(r.xMax,r.yMax),tint,new Vector2(uv.z,uv.w));
                mesh.AddVert(new Vector3(r.xMax,r.yMin),tint,new Vector2(uv.z,uv.y));
                mesh.AddTriangle(0,1,2); mesh.AddTriangle(2,3,0);
            }
        }
        public Surface[] surfaces;
        private static GameFigmaStrokeLibrary cached;
        public static Surface Find(string name)
        {
            if (cached == null) cached = Resources.Load<GameFigmaStrokeLibrary>("Hapio/UI/FigmaStrokeLibrary");
            return cached == null ? null : Array.Find(cached.surfaces, item => item.name == name);
        }
    }
}
