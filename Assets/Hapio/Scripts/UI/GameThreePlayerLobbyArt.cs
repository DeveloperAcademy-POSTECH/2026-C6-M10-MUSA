using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Original Figma story seat components and three-player picker assets; owns no seat or character rules.</summary>
    public static class GameThreePlayerLobbyArt
    {
        public const string Root = "Hapio/UI/ThreePlayerLobby/";
        public enum ChoiceState { Available, Taken, Selected }
        private const float FrameScale = 63f / 38.347824f;
        // Render origins captured at 1.5x, indexed by character ID (1 불이, 2 솔이, 3 묵이, 4 금옥, 5 만복).
        private static readonly Vector2[] SeatOffsets = {
            new Vector2(-1.70453f, -1.87926f), new Vector2(-1.72650f, -1.87921f),
            new Vector2(-2.03870f, -1.88639f), new Vector2(-1.90297f, -1.87881f),
            new Vector2(-2.04936f, -1.88669f)
        };

        public static bool Applies(LabNetwork network) => network != null
            && network.TargetParticipantCount == 3 && network.LobbyUsesCharacters;

        public static Sprite Empty(int seat)
        {
            if (seat < 1 || seat > 3) throw new ArgumentOutOfRangeException(nameof(seat));
            return Load("Empty" + seat);
        }

        public static Sprite Seat(int character, bool host, bool local = false)
        {
            RequireCharacter(character);
            return Load((local ? host ? "SeatHostMe" : "SeatMe" : host ? "SeatHost" : "Seat") + character);
        }

        public static Rect EmptyBounds(int seat)
        {
            // The exported 41-unit canvas includes paint outside the 38.347824-unit logical circle.
            Vector2 size = Empty(seat).rect.size * (.25f * FrameScale);
            return new Rect((63f - size.x) * .5f, (63f - size.y) * .5f, size.x, size.y);
        }

        public static Rect SeatBounds(int character, bool host, bool local = false)
        {
            Vector2 size = Seat(character, host, local).rect.size * (.25f * FrameScale);
            // Preserve the reference frame's occupied/empty ratio: a 42-unit avatar becomes 69 units,
            // centred on the same 63-unit hit rectangle used by the empty logical circle.
            float shift = (63f - 42f * FrameScale) * .5f;
            Vector2 origin = host || local
                ? new Vector2(-8f * FrameScale + shift, -(character == 5 ? 5f : 7f) * FrameScale + shift)
                : SeatOffsets[character - 1] * (FrameScale / 1.5f) + Vector2.one * shift;
            return new Rect(origin, size);
        }

        public static Sprite Choice(int character) => Choice(character, ChoiceState.Available);

        public static Sprite Choice(int character, ChoiceState state)
        {
            RequireCharacter(character);
            string prefix;
            switch (state)
            {
                case ChoiceState.Available: prefix = "Choice"; break;
                case ChoiceState.Taken: prefix = "ChoiceTaken"; break;
                case ChoiceState.Selected: prefix = "ChoiceSelected"; break;
                default: throw new ArgumentOutOfRangeException(nameof(state));
            }
            return Load(prefix + character);
        }

        public static Sprite Taken => Load("Taken");
        public static Sprite ChoiceCheck => Load("ChoiceCheck");
        public static Sprite Close => Load("Close");
        public static Sprite PopupPanel => Load("PopupPanel");

        private static Sprite Load(string name) => Resources.Load<Sprite>(Root + name)
            ?? throw new InvalidOperationException("Missing original three-player Figma lobby asset: " + Root + name);

        private static void RequireCharacter(int character)
        {
            if (character < 1 || character > LabPlayerSlots.CharacterCount)
                throw new ArgumentOutOfRangeException(nameof(character));
        }
    }
}
