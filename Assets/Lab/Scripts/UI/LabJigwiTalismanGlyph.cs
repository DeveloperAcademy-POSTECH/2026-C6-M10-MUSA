using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// #533 지귀 부적: 글꼴의 글자 하나("물")를 자기 RectTransform 칸에 꽉 차게 그리는 UI 그림.
    /// Text와 달리 줄 높이·여백 없이 글자 모양 범위만 칸에 맞추므로, 판정 점선(글자 칸 0~1 좌표)과 정확히 겹친다.
    /// Mask의 그림으로 쓰면 자식(먹 붓 점)이 글자 모양 안에만 보인다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LabJigwiTalismanGlyph : MaskableGraphic
    {
        private Font font;
        private char character = '물';

        public Font GlyphFont
        {
            get => font;
            set
            {
                if (font == value) return;
                font = value;
                SetAllDirty();
            }
        }

        public char Character
        {
            get => character;
            set { if (character == value) return; character = value; SetVerticesDirty(); }
        }

        public override Texture mainTexture => font != null && font.material != null && font.material.mainTexture != null
            ? font.material.mainTexture : s_WhiteTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            Font.textureRebuilt += OnFontRebuilt;
        }

        protected override void OnDisable()
        {
            Font.textureRebuilt -= OnFontRebuilt;
            base.OnDisable();
        }

        private void OnFontRebuilt(Font rebuilt)
        {
            if (rebuilt != font || this == null) return;
            SetAllDirty();
        }

        /// <summary>글자를 그릴 글꼴 크기(픽셀). 칸 크기에 맞추되 너무 작거나 크지 않게.</summary>
        private int PixelSize => Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(rectTransform.rect.width, rectTransform.rect.height)), 64, 256);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (font == null) return;
            int size = PixelSize;
            string text = character.ToString();
            font.RequestCharactersInTexture(text, size, FontStyle.Normal);
            if (!font.GetCharacterInfo(character, out CharacterInfo info, size, FontStyle.Normal)) return;
            Rect r = GetPixelAdjustedRect();
            Color32 c = color;
            vh.AddVert(new Vector3(r.xMin, r.yMin), c, info.uvBottomLeft);
            vh.AddVert(new Vector3(r.xMin, r.yMax), c, info.uvTopLeft);
            vh.AddVert(new Vector3(r.xMax, r.yMax), c, info.uvTopRight);
            vh.AddVert(new Vector3(r.xMax, r.yMin), c, info.uvBottomRight);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
