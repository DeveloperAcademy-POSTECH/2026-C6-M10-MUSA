using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GameFigmaSurface : MaskableGraphic
    {
        [SerializeField] private string surfaceName;
        private GameFigmaStrokeLibrary.Surface surface;
        public string SurfaceName => surfaceName;
        public override Texture mainTexture => Surface?.sprite != null ? Surface.sprite.texture : base.mainTexture;
        private GameFigmaStrokeLibrary.Surface Surface => surface ?? (surface = GameFigmaStrokeLibrary.Find(surfaceName));
        public void Show(string name)
        {
            if (surfaceName == name && surface != null) return;
            surfaceName = name; surface = GameFigmaStrokeLibrary.Find(name); SetAllDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        { mesh.Clear(); Surface?.Draw(mesh,rectTransform.rect,color); }
    }
}
