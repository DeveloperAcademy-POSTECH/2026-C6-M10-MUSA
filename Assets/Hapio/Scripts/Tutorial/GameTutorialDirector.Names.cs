using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 (#477): 세 캐릭터 얼굴 바로 아래 가운데에 이름(묵이·금옥·불이)을 쓴다. 얼굴이 옮겨지거나 커져도 매 프레임 따라가고,
    /// 화면 아래 끝을 넘지 않는다. 화면을 어둡게 하는 막보다 아래에 그려 얼굴과 함께 어두워지고, 구슬은 이름 위로 오지 않는다.
    /// </summary>
    public sealed partial class GameTutorialDirector
    {
        private readonly Text[] nameLabels = new Text[Seats];

        /// <summary>The name label under a seat's face (0 금옥이 P1, 1 불이 P2, 2 묵이 P3), or null.</summary>
        public Text NameLabelAt(int seat) => seat >= 0 && seat < Seats ? nameLabels[seat] : null;

        private void BuildNames()
        {
            Font font = art.nameFont != null ? art.nameFont : art.bannerFont;
            for (int seat = 0; seat < Seats; seat++)
            {
                Text label = AddText("Name " + (seat + 1), overlay, font, art.nameFontSize, art.nameColor);
                label.resizeTextForBestFit = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.text = GameTutorialScript.NameOf(GameTutorialScript.SpeakerAt(seat));
                RectTransform rect = label.rectTransform;
                Center(rect);
                rect.pivot = new Vector2(.5f, 1f);
                // As wide as the name itself, so the wall that keeps orbs off it is no wider.
                rect.sizeDelta = new Vector2(label.preferredWidth + 4f, art.nameFontSize * 1.3f);
                label.gameObject.SetActive(false);
                nameLabels[seat] = label;
            }
        }

        /// <summary>Each name just under its face's middle, kept inside the bottom of the screen.</summary>
        private void PlaceNames()
        {
            Rect bounds = overlay.rect;
            for (int seat = 0; seat < Seats; seat++)
            {
                Text label = nameLabels[seat];
                if (label == null) continue;
                Image face = portraits[seat];
                bool shown = face != null && TryOverlayRect(face.rectTransform, out Rect _);
                if (label.gameObject.activeSelf != shown) label.gameObject.SetActive(shown);
                if (!shown) continue;
                TryOverlayRect(face.rectTransform, out Rect area);
                RectTransform rect = label.rectTransform;
                float top = Mathf.Max(area.yMin - art.nameGap, bounds.yMin + art.nameScreenMargin + rect.sizeDelta.y);
                rect.anchoredPosition = new Vector2(area.center.x, top);
            }
        }
    }
}
