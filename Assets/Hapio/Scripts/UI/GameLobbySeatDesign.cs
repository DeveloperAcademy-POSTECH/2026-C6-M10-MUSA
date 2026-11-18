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
            // Five story seats: the two upper seats have lines running down from them, so their names go above the seat.
            float topRow = float.MinValue;
            if (!team && players == 5)
                for (int i = 0; i < view.slotButtons.Length; i++)
                    if (view.slotButtons[i].gameObject.activeSelf)
                        topRow = Mathf.Max(topRow, ((RectTransform)view.slotButtons[i].transform).anchoredPosition.y);
            for (int i = 0; i < view.slotImages.Length; i++)
            {
                var image = view.slotImages[i];
                image.enabled = true;
                var upperComponent = image.transform.Find("Upper Seat Component");
                if (upperComponent != null) upperComponent.gameObject.SetActive(false);
                if (!view.slotButtons[i].gameObject.activeSelf) continue;
                // Character components are shared by every story capacity, not only the three-seat layout.
                // Keep each layout's existing empty markers and anchors outside the three-player design.
                if (!team && (players == 3 || view.SeatCharacter(i) > 0))
                {
                    PresentStorySeat(view, i);
                    if (players == 5 && ((RectTransform)view.slotButtons[i].transform).anchoredPosition.y >= topRow - 1f)
                        PresentUpperStoryName(view, i);
                    continue;
                }
                // A story seat taken with a character shows its portrait upright, filling the 63-unit seat.
                bool portrait = view.SeatCharacter(i) > 0;
                bool filled = portrait || (team ? image.sprite == view.blueTeamFilledSlot || image.sprite == view.redTeamFilledSlot
                    : image.sprite == view.filledSlots[i]);
                Vector2 bounds = portrait ? new Vector2(63, 63) : image.sprite.rect.size / 4f;
                Place(image.transform, (63 - bounds.x) * .5f, (63 - bounds.y) * .5f, bounds.x, bounds.y);
                // P6's component is unrotated; every other marker component uses the same 180-degree orientation.
                image.rectTransform.localEulerAngles = new Vector3(0, 0, portrait || i == 5 && filled ? 0 : 180);
                view.slotLabels[i].fontSize = 24;
                view.slotLabels[i].color = filled && (team || i != 3) ? GameLobbySeatColors.LightText : GameLobbySeatColors.SeatInk;
                Place(view.slotLabels[i].transform, 0, 0, 63, 63);
                var label = view.localLabels[i];
                bool host = label.text.Contains("방장"), held = label.text == GameLobbyView.HeldSeatLabel;
                string who = portrait ? GameLobbyCharacterPicker.NameOf(view.SeatCharacter(i)) : null;
                bool pick = label.text.Contains("캐릭터 고르기");
                label.text = held ? GameLobbyView.HeldSeatLabel : who != null ? (host ? who + " · 방장" : who)
                    : pick ? "캐릭터 고르기" : host ? "방장" : string.Empty;
                label.fontSize = 20; label.color = GameLobbySeatColors.Host;
                Vector2 center = ((RectTransform)view.slotButtons[i].transform).anchoredPosition;
                bool above = team || center.y >= topRow - 1f;
                Place(label.transform, center.x - 60, -center.y + (above ? -67 : 43), 120, 26);
            }
            Place(view.sealButton.transform, 27, start ? 766 : 768, 349, start ? 58 : 56);
            view.sealButton.targetGraphic.color = start ? GameLobbySeatColors.Ready : GameLobbySeatColors.Disabled;
            var text = view.sealButton.GetComponentInChildren<Text>();
            text.fontSize = 20; text.color = GameLobbySeatColors.LightText;
            if (start) text.text = team ? "땅따먹기 시작" : "요괴 봉인 시작";
            else if (!allChosen) text.text = team ? "땅따먹기 대기 중" : "요괴 봉인 대기";
        }

        private static void PresentStorySeat(GameLobbyView view, int index)
        {
            int character = view.SeatCharacter(index);
            bool host = view.SeatIsHost(index), held = view.SeatIsHeld(index);
            bool local = view.SeatIsLocal(index) && !held;
            var image = view.slotImages[index];
            image.sprite = character > 0 ? GameThreePlayerLobbyArt.Seat(character, host, local)
                : GameThreePlayerLobbyArt.Empty(index + 1);
            Rect bounds = character > 0 ? GameThreePlayerLobbyArt.SeatBounds(character, host, local)
                : GameThreePlayerLobbyArt.EmptyBounds(index + 1);
            Place(image.transform, bounds.x, bounds.y, bounds.width, bounds.height);
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.rectTransform.localEulerAngles = Vector3.zero;
            // RefreshPositions already applied the held/disabled opacity; preserve it without tinting the source art.
            image.color = new Color(1f, 1f, 1f, image.color.a);
            view.slotLabels[index].text = string.Empty;

            // Selected source components already contain their name and, for the actual Host, its badge.
            var label = view.localLabels[index];
            label.gameObject.SetActive(character == 0 || held);
            if (held) label.text = GameLobbyView.HeldSeatLabel;
            label.fontSize = 20; label.color = GameLobbySeatColors.Host;
            Vector2 center = ((RectTransform)view.slotButtons[index].transform).anchoredPosition;
            Place(label.transform, center.x - 60, -center.y - 67, 120, 26);
        }

        private static void Place(Transform target, float x, float y, float width, float height)
            => LabLobbyView.Place((RectTransform)target, x, y, width, height);

        // Keep the original component pixels, but put its name above the two upper seats where the
        // pentagon lines run downwards. Rect masks separate the authored name without editing the PNG.
        private static void PresentUpperStoryName(GameLobbyView view, int index)
        {
            var source = view.slotImages[index];
            var root = source.transform.Find("Upper Seat Component");
            if (root == null)
            {
                root = new GameObject("Upper Seat Component", typeof(RectTransform)).transform;
                root.SetParent(source.transform, false);
                for (int i = 0; i < 2; i++)
                {
                    var mask = new GameObject(i == 0 ? "Portrait" : "Name", typeof(RectTransform), typeof(RectMask2D)).transform;
                    mask.SetParent(root, false);
                    var art = new GameObject("Original Component", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    art.transform.SetParent(mask, false); art.raycastTarget = false;
                }
            }
            root.gameObject.SetActive(true);
            float scale = 63f / 38.347824f;
            int character = view.SeatCharacter(index);
            bool held = view.SeatIsHeld(index);
            bool framed = view.SeatIsHost(index) || view.SeatIsLocal(index) && !held;
            var bounds = source.rectTransform;
            float shift = (63f - 42f * scale) * .5f;
            float sourceTop = -bounds.anchoredPosition.y - bounds.rect.height * .5f;
            float nameTop = framed ? (character == 5 ? 51 : 53) * scale
                : 46 * scale - (sourceTop - shift);
            Place(root, 0, 0, bounds.rect.width, bounds.rect.height);
            for (int i = 0; i < 2; i++)
            {
                var mask = root.GetChild(i);
                // The away label uses the upper name row. Keep the portrait, but hide duplicate name pixels
                // until the seat is reclaimed; the same original name layer is restored on the next refresh.
                mask.gameObject.SetActive(i == 0 || !held);
                Place(mask, 0, i == 0 ? 0 : -40 - sourceTop,
                    bounds.rect.width, i == 0 ? nameTop : bounds.rect.height - nameTop);
                var art = mask.GetChild(0).GetComponent<Image>();
                art.sprite = source.sprite; art.color = source.color;
                Place(art.transform, 0, i == 0 ? 0 : -nameTop, bounds.rect.width, bounds.rect.height);
            }
            source.enabled = false;
        }
    }
}
