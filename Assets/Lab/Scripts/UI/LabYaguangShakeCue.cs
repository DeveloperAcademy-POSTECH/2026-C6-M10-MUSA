using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>Figma 4607:4749 / 4773. Presentation only; the HUD owns input and completion.</summary>
    public sealed class LabYaguangShakeCue : MonoBehaviour
    {
        public const string ArtPath = "Yaguang/Shake/";
        private RectTransform layout, phone, track;
        private Image title, guide;
        private Text countLabel;
        private Sprite emptyCell, fullCell;
        private readonly List<Image> cells = new List<Image>();
        public bool GuideVisible => guide != null && guide.gameObject.activeSelf;
        public int FilledCells { get; private set; }
        public int CellCount => cells.Count;
        public float PhoneAngle => phone.localEulerAngles.z;
        public static float FitScale(Vector2 viewport) => Mathf.Max(0f, Mathf.Min(viewport.x / 349f, viewport.y / 765f));

        public void Build()
        {
            emptyCell = Resources.Load<Sprite>(ArtPath + "CellEmpty");
            fullCell = Resources.Load<Sprite>(ArtPath + "CellFull");
            layout = NewRect("Figma 349x765", transform);
            layout.anchorMin = layout.anchorMax = new Vector2(.5f, .5f);
            layout.sizeDelta = new Vector2(349f, 765f);
            title = Picture("Title", layout, 25f, 225f, 299f, 30f);
            guide = Picture("Guide", layout, 20f, 257f, 309f, 65f);
            phone = Picture("Phone", layout, 127.5f, 334.5f, 94f, 137f).rectTransform;
            track = Picture("GaugeTrack", layout, 65.5f, 484f, 218f, 22f).rectTransform;
            countLabel = NewRect("Count", layout).gameObject.AddComponent<Text>();
            Place(countLabel.rectTransform, 65.5f, 516f, 218f, 45f);
            countLabel.font = Resources.Load<Font>(ArtPath + "NanumBrushScript-Regular");
            countLabel.fontSize = 38;
            countLabel.alignment = TextAnchor.UpperCenter;
            countLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            countLabel.verticalOverflow = VerticalWrapMode.Overflow;
            // Figma 5orbscolor/MetalYellow/700.
            countLabel.color = new Color32(149, 109, 20, 255);
            countLabel.raycastTarget = false;
        }

        public void Present(int count, int target, float time, float wiggleHz)
        {
            if (layout == null) Build();
            var available = ((RectTransform)transform.parent).rect.size;
            layout.localScale = Vector3.one * FitScale(available);
            target = Mathf.Clamp(target, 2, 18);
            count = Mathf.Clamp(count, 0, target);
            title.gameObject.SetActive(count == 0);
            guide.gameObject.SetActive(count == 0);
            phone.localRotation = Quaternion.Euler(0f, 0f,
                12f * Mathf.Sin(time * wiggleHz * Mathf.PI * 2f));
            while (cells.Count < target)
            {
                var cell = Picture("CellEmpty", track, 0, 0, 18, 14);
                cell.name = "Cell " + (cells.Count + 1);
                cells.Add(cell);
            }
            float width = (206f - (target - 1) * 2.8f) / target;
            for (int i = 0; i < cells.Count; i++)
            {
                cells[i].gameObject.SetActive(i < target);
                if (i >= target) continue;
                var rect = cells[i].rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(width, 14f);
                rect.anchoredPosition = new Vector2(6f + width * .5f + i * (width + 2.8f), -11f);
                // PNG already includes the source cell's +0.5-degree rotation.
                rect.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 0f : 1.2f);
                cells[i].sprite = i < count ? fullCell : emptyCell;
            }
            FilledCells = count;
            countLabel.text = count + " / " + target;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x + width * .5f, -y - height * .5f);
        }
        private static Image Picture(string name, Transform parent, float x, float y, float width, float height)
        {
            var image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>(ArtPath + name);
            image.raycastTarget = false;
            Place(image.rectTransform, x, y, width, height);
            // Figma exports text at its tight glyph bounds, not the wider text layer bounds.
            if ((name == "Title" || name == "Guide") && image.sprite != null)
                image.rectTransform.sizeDelta = image.sprite.rect.size * .5f;
            return image;
        }
    }
}
