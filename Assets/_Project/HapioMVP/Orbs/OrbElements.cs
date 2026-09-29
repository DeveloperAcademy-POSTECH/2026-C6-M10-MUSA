using System;
using System.Collections.Generic;
using C6.Prototype.Presentation;

namespace C6.Prototype.Orbs
{
    /// <summary>
    /// Element rule (오행 v1). An orb's element comes from its Host-issued ID, so every device
    /// agrees without any wire or snapshot change.
    /// - Raw: one element picked from the team list by hashing the ID.
    /// - Combined: the Host writes the pair into the LAST TWO characters of the 32-hex ID
    ///   ('0'..'4' = Fire, Water, Wood, Metal, Earth). Decoding therefore never depends on the
    ///   team list, its order, or any hash agreement between devices, and the ID stays a valid
    ///   Guid "N" string. Older hash-encoded IDs still decode through the legacy fallback.
    /// 오행 v2: 같은 속성의 음 + 양만 결합한다(예: 불 음 + 불 양). 다른 속성끼리는 결합 불가.
    /// 결합 ID는 여전히 두 칸을 쓰지만 두 칸이 항상 같은 속성이다.
    /// 속성은 씬이 Configure를 호출해야 켜진다. 호출 전(기본값)에는 모든 구슬이 None이라
    /// 속성 규칙이 적용되지 않는다. 옛 T05/T08 씬과 EditMode 테스트가 여기에 해당한다.
    /// </summary>
    public static class OrbElements
    {
        public static readonly OrbElement[] AllElements =
            { OrbElement.Fire, OrbElement.Water, OrbElement.Wood, OrbElement.Metal, OrbElement.Earth };
        private static OrbElement[] team = Array.Empty<OrbElement>();
        private const uint RawSalt = 0x1F3A5C7Bu, CombinedSalt = 0x51E2D3C4u;

        // Combined IDs carry the pair in their last two characters. '0' maps to FirstCode and the
        // codes stay inside the hex alphabet so the ID remains parseable as a Guid.
        private const OrbElement FirstCode = OrbElement.Fire, LastCode = OrbElement.Earth;
        private const int CodeLength = 2;

        public static IReadOnlyList<OrbElement> Team => team;
        public static bool Enabled => team.Length > 0;

        /// <summary>Distinct, valid elements in the given order. Null or empty disables elements (all None).</summary>
        public static void Configure(IEnumerable<OrbElement> elements)
        {
            var list = new List<OrbElement>();
            if (elements != null)
                foreach (var element in elements)
                    if (element != OrbElement.None && Enum.IsDefined(typeof(OrbElement), element) && !list.Contains(element))
                        list.Add(element);
            team = list.ToArray();
        }

        public static OrbElement RawElement(string orbId) => Pick(orbId, RawSalt);

        /// <summary>오행 v2: 두 Raw 구슬의 속성이 같을 때만 결합할 수 있다. 속성이 꺼져 있으면 둘 다 None이라 항상 true.</summary>
        public static bool SameElement(string firstOrbId, string secondOrbId)
            => RawElement(firstOrbId) == RawElement(secondOrbId);

        /// <summary>
        /// The pair carried by a combined orb ID. An ID without an encoded pair (DEV fixtures,
        /// debug Combined) picks one element by hash and uses it for both slots, so it always
        /// shows one of the five same-element artworks.
        /// </summary>
        public static void CombinedElements(string orbId, out OrbElement yin, out OrbElement yang)
        {
            if (TryDecodeCombinedId(orbId, out yin, out yang)) return;
            yin = Pick(orbId, CombinedSalt);
            yang = yin;
        }

        /// <summary>Host only: a fresh combined ID for a same-element pair (예: 불 + 불).</summary>
        public static string NewCombinedId(OrbElement element) => NewCombinedId(element, element);

        /// <summary>Host only: a fresh 32-hex ID whose last two characters carry the given pair.</summary>
        public static string NewCombinedId(OrbElement yin, OrbElement yang)
        {
            string id = Guid.NewGuid().ToString("N");
            if (!IsEncodable(yin) || !IsEncodable(yang) || id.Length < CodeLength) return id;
            return id.Substring(0, id.Length - CodeLength) + Code(yin) + Code(yang);
        }

        /// <summary>True when the ID carries an encoded pair. Diagnostics and tests use this.</summary>
        public static bool TryDecodeCombinedId(string orbId, out OrbElement yin, out OrbElement yang)
        {
            yin = OrbElement.None; yang = OrbElement.None;
            if (string.IsNullOrEmpty(orbId) || orbId.Length < CodeLength) return false;
            if (TryCode(orbId[orbId.Length - 2], out yin) && TryCode(orbId[orbId.Length - 1], out yang)) return true;
            yin = OrbElement.None; yang = OrbElement.None;
            return false;
        }

        private static bool IsEncodable(OrbElement element) => element >= FirstCode && element <= LastCode;

        private static char Code(OrbElement element) => (char)('0' + (int)element - (int)FirstCode);

        private static bool TryCode(char character, out OrbElement element)
        {
            int index = character - '0';
            element = OrbElement.None;
            if (index < 0 || index > (int)LastCode - (int)FirstCode) return false;
            element = (OrbElement)((int)FirstCode + index);
            return true;
        }

        private static OrbElement Pick(string orbId, uint salt)
        {
            if (team.Length == 0 || string.IsNullOrEmpty(orbId)) return OrbElement.None;
            unchecked
            {
                uint hash = 2166136261u ^ salt;
                foreach (char c in orbId) { hash ^= c; hash *= 16777619u; }
                hash ^= hash >> 15; hash *= 0x2C1B3C6Du; hash ^= hash >> 12; hash *= 0x297A2D39u; hash ^= hash >> 15;
                return team[(int)(hash % (uint)team.Length)];
            }
        }
    }
}
