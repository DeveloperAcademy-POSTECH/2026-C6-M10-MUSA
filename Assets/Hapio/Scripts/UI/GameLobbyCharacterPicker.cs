using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Story character selection presentation. Seat and character approval remain with the Host.</summary>
    public sealed class GameLobbyCharacterPicker : MonoBehaviour
    {
        public const string PortraitPath = "Hapio/UI/BattleCharacters/Portrait";
        private const string CheckPath = "Yaguang/Combo/Check";
        private const float TakenAlpha = .35f;

        private sealed class Layout
        {
            public GameObject root;
            public UnityEngine.UI.Text title;
            public UnityEngine.UI.Button release;
            public bool originalThreePlayerArt;
            public readonly UnityEngine.UI.Button[] choices = new UnityEngine.UI.Button[LabPlayerSlots.CharacterCount];
            public readonly UnityEngine.UI.Image[] portraits = new UnityEngine.UI.Image[LabPlayerSlots.CharacterCount];
            public readonly UnityEngine.UI.Image[] checks = new UnityEngine.UI.Image[LabPlayerSlots.CharacterCount];
            public readonly UnityEngine.UI.Image[] selectedChecks = new UnityEngine.UI.Image[LabPlayerSlots.CharacterCount];
            public readonly UnityEngine.UI.Text[] names = new UnityEngine.UI.Text[LabPlayerSlots.CharacterCount];
        }

        private LabNetwork network;
        private Action<int, int> choose;
        private Font font;
        private Sprite paper;
        private Layout current, legacy, threePlayer;

        public int Seat { get; private set; }
        public bool IsOpen => Seat > 0 && gameObject.activeSelf;
        public bool UsesThreePlayerArt => current != null && current.originalThreePlayerArt;
        public static int CharacterAt(int position) => LabPlayerSlots.CharacterOrder[position];
        public UnityEngine.UI.Button Choice(int position) => current.choices[position];
        public bool Checked(int position) => current != null && current.checks[position] != null
            && current.checks[position].gameObject.activeSelf;

        public static Sprite Portrait(int character) =>
            character < 1 || character > LabPlayerSlots.CharacterCount ? null : Resources.Load<Sprite>(PortraitPath + character);

        public static string NameOf(int character) =>
            character < 1 || character > GameBattleUiPresentation.CharacterNames.Length
                ? null : GameBattleUiPresentation.CharacterNames[character - 1];

        public static GameLobbyCharacterPicker Create(RectTransform parent, Font font, Sprite paper, LabNetwork network,
            Action<int, int> choose)
        {
            var root = new GameObject("Character Picker", typeof(RectTransform), typeof(UnityEngine.UI.Image))
                .GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var picker = root.gameObject.AddComponent<GameLobbyCharacterPicker>();
            picker.network = network; picker.choose = choose; picker.font = font; picker.paper = paper;
            // TextColor/PrimaryBrown/800 at half strength, as in the existing lobby modal.
            Color dim = GameLobbySeatColors.SeatInk; dim.a = .55f;
            root.GetComponent<UnityEngine.UI.Image>().color = dim;
            var close = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            close.transition = UnityEngine.UI.Selectable.Transition.None;
            close.onClick.AddListener(picker.Close);
            picker.EnsureLayout();
            root.gameObject.SetActive(false);
            return picker;
        }

        private void EnsureLayout()
        {
            bool useThree = GameThreePlayerLobbyArt.Applies(network);
            Layout next = useThree ? threePlayer ?? (threePlayer = CreateThreePlayerLayout())
                : legacy ?? (legacy = CreateLegacyLayout());
            if (current != next)
            {
                if (current != null) current.root.SetActive(false);
                current = next;
            }
            current.root.SetActive(true);
            bool mine = Seat > 0 && network != null && network.LocalSlotNumber == Seat;
            current.title.text = mine ? "캐릭터를 다시 고르시오." : "함께할 캐릭터를 고르시오.";
            if (current.release != null) current.release.gameObject.SetActive(mine
                && (!current.originalThreePlayerArt || network.LocalCharacter == 0));
        }

        private Layout CreateThreePlayerLayout()
        {
            var frame = Child(transform, "Figma Three Player Layout", 0, 279.5f, 402, 315);
            var layout = new Layout { root = frame.gameObject, originalThreePlayerArt = true };
            var panel = Picture(frame, "Panel", GameThreePlayerLobbyArt.PopupPanel, 27, 37, 348, 260);
            panel.raycastTarget = true;
            // Swallow panel presses; only the veil outside it closes the picker.
            panel.gameObject.AddComponent<UnityEngine.UI.Button>().transition = UnityEngine.UI.Selectable.Transition.None;
            layout.title = Label(frame, "Title", font, 27, GameLobbySeatColors.Ink, 42, 88, 317, 32);
            Label(frame, "Hint", font, 17, GameLobbySeatColors.Host, 42, 132, 317, 20).text =
                "다른 견습생이 고른 캐릭터는 고를 수 없소.";
            for (int i = 0; i < layout.choices.Length; i++)
            {
                int character = CharacterAt(i);
                RectTransform cell = Child(frame, "Character " + character, 48 + i * 63, 164, 53, 87);
                var hit = cell.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color = Color.clear;
                var button = cell.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = hit;
                button.transition = UnityEngine.UI.Selectable.Transition.None;
                button.onClick.AddListener(() => Pick(character));
                layout.choices[i] = button;
                layout.portraits[i] = Picture(cell, "Portrait", GameThreePlayerLobbyArt.Choice(character), 0, 0, 53, 53);
                layout.checks[i] = Picture(cell, "Taken", GameThreePlayerLobbyArt.Taken, 5, 11, 43.4184f, 30.9496f);
                layout.checks[i].gameObject.SetActive(false);
                layout.selectedChecks[i] = Picture(cell, "Current Choice", GameThreePlayerLobbyArt.ChoiceCheck, 9, 12, 36, 30);
                layout.selectedChecks[i].gameObject.SetActive(false);
                layout.names[i] = Label(cell, "Name", font, 24, GameLobbySeatColors.Host, 0, 59, 53, 28);
                layout.names[i].text = NameOf(character);
            }
            var close = Picture(frame, "Close", GameThreePlayerLobbyArt.Close, 322, 51, 37, 37);
            close.raycastTarget = true;
            var closeButton = close.gameObject.AddComponent<UnityEngine.UI.Button>();
            closeButton.transition = UnityEngine.UI.Selectable.Transition.None;
            closeButton.onClick.AddListener(Close);
            // Recovery only: a returning player can give up a restored seat whose character was taken.
            // Normal character selection keeps the original Figma popup without an extra control.
            var release = Label(frame, "Release Seat", font, 17, GameLobbySeatColors.Host, 42, 262, 317, 24);
            release.text = "자리 비우기"; release.raycastTarget = true;
            layout.release = release.gameObject.AddComponent<UnityEngine.UI.Button>();
            layout.release.transition = UnityEngine.UI.Selectable.Transition.None;
            layout.release.onClick.AddListener(() => { Close(); choose?.Invoke(0, 0); });
            release.gameObject.SetActive(false);
            return layout;
        }

        private Layout CreateLegacyLayout()
        {
            var root = new GameObject("Legacy Layout", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var layout = new Layout { root = root.gameObject };
            var sheet = Child(root, "Sheet", 32.5f, 322, 337, 230).gameObject.AddComponent<UnityEngine.UI.Image>();
            sheet.color = GameLobbySeatColors.LightText; sheet.raycastTarget = false;
            var card = Child(root, "Card", 28.5f, 318, 345, 238).gameObject.AddComponent<UnityEngine.UI.Image>();
            card.sprite = paper; card.type = UnityEngine.UI.Image.Type.Simple;
            if (paper == null) card.color = GameLobbySeatColors.LightText;
            card.gameObject.AddComponent<UnityEngine.UI.Button>().transition = UnityEngine.UI.Selectable.Transition.None;
            layout.title = Label(card.rectTransform, "Title", font, 26, GameLobbySeatColors.Ink, 0, 24, 345, 36);
            Label(card.rectTransform, "Hint", font, 16, GameLobbySeatColors.Muted, 0, 62, 345, 24).text =
                "다른 견습생이 고른 캐릭터는 고를 수 없소.";
            Sprite check = Resources.Load<Sprite>(CheckPath);
            for (int i = 0; i < layout.choices.Length; i++)
            {
                int character = CharacterAt(i);
                RectTransform cell = Child(card.rectTransform, "Character " + character, 14.5f + i * 64f, 100, 60, 96);
                var hit = cell.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color = Color.clear;
                var button = cell.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = hit;
                button.transition = UnityEngine.UI.Selectable.Transition.None;
                button.onClick.AddListener(() => Pick(character));
                layout.choices[i] = button;
                var portrait = Picture(cell, "Portrait", Portrait(character), 2, 0, 56, 56);
                portrait.preserveAspect = true; layout.portraits[i] = portrait;
                var mark = Picture(cell, "Taken", check, 16, 14, 28, 28);
                mark.preserveAspect = true; layout.checks[i] = mark;
                layout.names[i] = Label(cell, "Name", font, 18, GameLobbySeatColors.Ink, -6, 62, 72, 26);
                layout.names[i].text = NameOf(character);
            }
            var cancel = Label(card.rectTransform, "Close", font, 18, GameLobbySeatColors.Host, 0, 202, 345, 26);
            cancel.text = "닫기"; cancel.raycastTarget = true;
            cancel.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(Close);
            var leave = Label(card.rectTransform, "Release Seat", font, 18, GameLobbySeatColors.Versus, 200, 202, 130, 26);
            leave.text = "자리 비우기"; leave.raycastTarget = true;
            layout.release = leave.gameObject.AddComponent<UnityEngine.UI.Button>();
            layout.release.onClick.AddListener(() => { Close(); choose?.Invoke(0, 0); });
            return layout;
        }

        public void Open(int seat)
        {
            Seat = seat;
            EnsureLayout();
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
            ulong seatOwner = network.GetSlotOwner(Seat);
            if (!network.CanSelectPlayerSlot || seatOwner != ulong.MaxValue && seatOwner != network.LocalPlayerId) { Close(); return; }
            EnsureLayout();
            for (int i = 0; i < current.choices.Length; i++)
            {
                int character = CharacterAt(i);
                bool free = Free(character);
                current.choices[i].interactable = free;
                if (current.originalThreePlayerArt)
                {
                    ulong owner = network.GetCharacterOwner(character);
                    var state = owner == ulong.MaxValue ? GameThreePlayerLobbyArt.ChoiceState.Available
                        : owner == network.LocalPlayerId ? GameThreePlayerLobbyArt.ChoiceState.Selected
                        : GameThreePlayerLobbyArt.ChoiceState.Taken;
                    current.portraits[i].sprite = GameThreePlayerLobbyArt.Choice(character, state);
                    // The original variants contain their own dashed/thin/thick outline and portrait opacity.
                    // A global fade would also dim the outline and apply the taken portrait's 25% twice.
                    current.portraits[i].color = Color.white;
                    Color nameColor = state == GameThreePlayerLobbyArt.ChoiceState.Selected
                        ? GameLobbySeatColors.Ink : GameLobbySeatColors.Host;
                    nameColor.a = state == GameThreePlayerLobbyArt.ChoiceState.Taken ? .56f : 1f;
                    current.names[i].color = nameColor;
                    current.selectedChecks[i].gameObject.SetActive(state == GameThreePlayerLobbyArt.ChoiceState.Selected);
                }
                else
                {
                    current.portraits[i].color = new Color(1f, 1f, 1f, free ? 1f : TakenAlpha);
                    current.names[i].color = free ? GameLobbySeatColors.Ink : GameLobbySeatColors.Disabled;
                }
                current.checks[i].gameObject.SetActive(!free);
            }
        }

        private static RectTransform Child(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            LabLobbyView.Place(rect, x, y, width, height);
            return rect;
        }

        private static UnityEngine.UI.Image Picture(Transform parent, string name, Sprite sprite, float x, float y,
            float width, float height)
        {
            var image = Child(parent, name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite; image.color = Color.white; image.raycastTarget = false;
            return image;
        }

        private static UnityEngine.UI.Text Label(Transform parent, string name, Font font, int size, Color color, float x, float y,
            float width, float height)
        {
            var text = Child(parent, name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font; text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
