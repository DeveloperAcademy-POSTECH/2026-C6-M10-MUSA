using System;
using C6Lab;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// Story lobby: after an empty seat is pressed, the five battle characters to take it with. A character another player
    /// holds is checked and cannot be pressed. Presentation only: the Host approves the seat and the character together
    /// (<see cref="LabNetwork.RequestPlayerSlot"/>). Built at run time over the saved lobby; no Figma frame exists yet, so
    /// it reuses the lobby's paper, fonts and Hapio.tokens.json colours.
    /// </summary>
    public sealed class GameLobbyCharacterPicker : MonoBehaviour
    {
        public const string PortraitPath = "Hapio/UI/BattleCharacters/Portrait";
        private const string CheckPath = "Yaguang/Combo/Check";
        private const float TakenAlpha = .35f;

        private readonly Button[] choices = new Button[LabPlayerSlots.CharacterCount];
        private readonly Image[] portraits = new Image[LabPlayerSlots.CharacterCount];
        private readonly Image[] checks = new Image[LabPlayerSlots.CharacterCount];
        private readonly Text[] names = new Text[LabPlayerSlots.CharacterCount];
        private LabNetwork network;
        private Action<int, int> choose;
        private Text title;

        /// <summary>The seat (P number) being taken; 0 while closed.</summary>
        public int Seat { get; private set; }
        public bool IsOpen => Seat > 0 && gameObject.activeSelf;
        /// <summary>The character number (1..5, artwork order) shown at this position of the row.</summary>
        public static int CharacterAt(int position) => LabPlayerSlots.CharacterOrder[position];
        public Button Choice(int position) => choices[position];
        public bool Checked(int position) => checks[position] != null && checks[position].gameObject.activeSelf;

        public static Sprite Portrait(int character) =>
            character < 1 || character > LabPlayerSlots.CharacterCount ? null : Resources.Load<Sprite>(PortraitPath + character);

        public static string NameOf(int character) =>
            character < 1 || character > GameBattleUiPresentation.CharacterNames.Length
                ? null : GameBattleUiPresentation.CharacterNames[character - 1];

        public static GameLobbyCharacterPicker Create(RectTransform parent, Font font, Sprite paper, LabNetwork network,
            Action<int, int> choose)
        {
            var root = new GameObject("Character Picker", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var picker = root.gameObject.AddComponent<GameLobbyCharacterPicker>();
            picker.network = network; picker.choose = choose;
            // TextColor/PrimaryBrown/800 at half strength: the lobby stays visible but cannot be pressed.
            Color dim = GameLobbySeatColors.SeatInk; dim.a = .55f;
            var veil = root.GetComponent<Image>(); veil.color = dim;
            var close = root.gameObject.AddComponent<Button>(); close.transition = Selectable.Transition.None;
            close.onClick.AddListener(picker.Close);

            var card = Child(root, "Card", 28.5f, 318, 345, 238).gameObject.AddComponent<Image>();
            card.sprite = paper; card.type = Image.Type.Simple;
            if (paper == null) card.color = GameLobbySeatColors.LightText;
            // The card swallows presses so only the veil closes the picker.
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            picker.title = Label(card.rectTransform, "Title", font, 26, GameLobbySeatColors.Ink, 0, 24, 345, 36);
            Label(card.rectTransform, "Hint", font, 16, GameLobbySeatColors.Muted, 0, 62, 345, 24).text =
                "다른 견습생이 고른 캐릭터는 고를 수 없소.";
            Sprite check = Resources.Load<Sprite>(CheckPath);
            for (int i = 0; i < picker.choices.Length; i++)
            {
                int character = CharacterAt(i);
                RectTransform cell = Child(card.rectTransform, "Character " + character, 14.5f + i * 64f, 100, 60, 96);
                var hit = cell.gameObject.AddComponent<Image>(); hit.color = Color.clear;
                var button = cell.gameObject.AddComponent<Button>(); button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => picker.Pick(character));
                picker.choices[i] = button;
                var portrait = Child(cell, "Portrait", 2, 0, 56, 56).gameObject.AddComponent<Image>();
                portrait.sprite = Portrait(character); portrait.preserveAspect = true; portrait.raycastTarget = false;
                picker.portraits[i] = portrait;
                var mark = Child(cell, "Taken", 16, 14, 28, 28).gameObject.AddComponent<Image>();
                mark.sprite = check; mark.preserveAspect = true; mark.raycastTarget = false;
                picker.checks[i] = mark;
                picker.names[i] = Label(cell, "Name", font, 18, GameLobbySeatColors.Ink, -6, 62, 72, 26);
                picker.names[i].text = NameOf(character);
            }
            var cancel = Label(card.rectTransform, "Close", font, 18, GameLobbySeatColors.Host, 0, 202, 345, 26);
            cancel.text = "닫기"; cancel.raycastTarget = true;
            cancel.gameObject.AddComponent<Button>().onClick.AddListener(picker.Close);
            root.gameObject.SetActive(false);
            return picker;
        }

        public void Open(int seat)
        {
            Seat = seat;
            title.text = "함께할 캐릭터를 고르시오.";
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            Seat = 0;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void Pick(int character)
        {
            int seat = Seat;
            if (seat <= 0 || !Free(character)) return;
            Close();
            choose?.Invoke(seat, character);
        }

        private bool Free(int character)
        {
            ulong owner = network != null ? network.GetCharacterOwner(character) : ulong.MaxValue;
            return owner == ulong.MaxValue || owner == network.LocalPlayerId;
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (Seat <= 0 || network == null) return;
            // The seat went to someone else, or choosing closed (start, leaving): nothing left to pick here.
            ulong seatOwner = network.GetSlotOwner(Seat);
            if (!network.CanSelectPlayerSlot || seatOwner != ulong.MaxValue && seatOwner != network.LocalPlayerId) { Close(); return; }
            for (int i = 0; i < choices.Length; i++)
            {
                bool free = Free(CharacterAt(i));
                choices[i].interactable = free;
                portraits[i].color = new Color(1f, 1f, 1f, free ? 1f : TakenAlpha);
                names[i].color = free ? GameLobbySeatColors.Ink : GameLobbySeatColors.Disabled;
                checks[i].gameObject.SetActive(!free);
            }
        }

        private static RectTransform Child(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            LabLobbyView.Place(rect, x, y, width, height);
            return rect;
        }

        private static Text Label(Transform parent, string name, Font font, int size, Color color, float x, float y,
            float width, float height)
        {
            var text = Child(parent, name, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
