using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Figma seat presentation only. Receives the existing lobby's decisions; owns no session rules.</summary>
    public sealed class GameLobbySeatDesign : MonoBehaviour
    {
        public Image paper;
        public Sprite storyPaper, teamPaper, sixPlayerPaper;
        public GameObject[] teamColumns;

        public void Present(GameLobbyView view, int players, bool team, bool waiting, bool allChosen, bool start)
        {
            int size = team ? players / 2 : 0;
            for (int i = 0; i < teamColumns.Length; i++) teamColumns[i].SetActive(!waiting && size == i + 1);
            float cardTop = team && size == 3 ? 268 : 283;
            Sprite card = team ? size == 3 ? sixPlayerPaper : teamPaper : storyPaper;
            paper.sprite = card;
            // Original SVG bounds include the hand-drawn stroke outside the logical 345-wide card.
            Place(paper.transform, 24.58f, cardTop - 2.958f, card.rect.width / 4f, card.rect.height / 4f);
            Place(view.roomName.transform, 55, cardTop + 19, 280, 38);
            Place(view.joinedCount.transform, 55, cardTop + 58, 145, 27);
            Place(view.selectedCount.transform, 205, cardTop + 58, 143, 27);
            view.heading.fontSize = 36;
            view.heading.color = GameLobbySeatColors.Ink;
            view.heading.text = waiting ? view.heading.text : allChosen ? team ? "준비 완료!" : "요괴 봉인 준비 완료!" : "빈 자리를 고르시오.";
            view.subheading.color = GameLobbySeatColors.Muted;
            if (allChosen && team && string.IsNullOrEmpty(view.subheading.text)) view.subheading.text = "봉인을 시작해보시오!";
            else if (!allChosen && team && !waiting && view.subheading.text == GameLobbyView.TeamSeatHint(players))
                view.subheading.text = size == 1 ? "원하는 자리를 선택 후, 준비하시오." : size + "명씩 함께 앉아, 준비하시오.";
            Place(view.heading.transform, 27, 162, 349, 45);
            Place(view.subheading.transform, 27, 211, 349, 32);
            view.clearSlotButton.gameObject.SetActive(false); // The existing own-seat click still clears the choice.
            for (int i = 0; i < view.slotImages.Length; i++)
            {
                var image = view.slotImages[i];
                if (!view.slotButtons[i].gameObject.activeSelf) continue;
                bool filled = team ? image.sprite == view.blueTeamFilledSlot || image.sprite == view.redTeamFilledSlot
                    : image.sprite == view.filledSlots[i];
                Vector2 bounds = image.sprite.rect.size / 4f;
                Place(image.transform, (63 - bounds.x) * .5f, (63 - bounds.y) * .5f, bounds.x, bounds.y);
                // P6's component is unrotated; every other marker component uses the same 180-degree orientation.
                image.rectTransform.localEulerAngles = new Vector3(0, 0, i == 5 && filled ? 0 : 180);
                view.slotLabels[i].fontSize = 24;
                view.slotLabels[i].color = filled && (team || i != 3) ? GameLobbySeatColors.LightText : GameLobbySeatColors.SeatInk;
                Place(view.slotLabels[i].transform, 0, 0, 63, 63);
                var label = view.localLabels[i];
                bool host = label.text.Contains("방장");
                label.text = host ? "방장" : string.Empty;
                label.fontSize = 20; label.color = GameLobbySeatColors.Host;
                Vector2 center = ((RectTransform)view.slotButtons[i].transform).anchoredPosition;
                Place(label.transform, center.x - 60, -center.y + (team ? -67 : 43), 120, 26);
            }
            Place(view.sealButton.transform, 27, start ? 766 : 768, 349, start ? 58 : 56);
            view.sealButton.targetGraphic.color = start ? GameLobbySeatColors.Ready : GameLobbySeatColors.Disabled;
            var text = view.sealButton.GetComponentInChildren<Text>();
            text.fontSize = 20; text.color = GameLobbySeatColors.LightText;
            if (start) text.text = team ? "땅따먹기 시작" : "요괴 봉인 시작";
            else if (!allChosen) text.text = team ? "땅따먹기 대기 중" : "요괴 봉인 대기";
        }

        private static void Place(Transform target, float x, float y, float width, float height)
            => LabLobbyView.Place((RectTransform)target, x, y, width, height);
    }
}
