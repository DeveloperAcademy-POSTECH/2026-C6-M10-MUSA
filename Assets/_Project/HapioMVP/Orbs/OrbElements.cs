using System;
using System.Collections.Generic;
using C6.Prototype.Presentation;

namespace C6.Prototype.Orbs
{
    /// <summary>
    /// Element identity. Normal lobby-selected Raw orbs carry an explicit Host-approved element.
    /// Legacy scenes and fixtures without that field may still derive it from the Host-issued ID.
    /// - Raw: explicit element first; ID hash is used only by the separate legacy path.
    /// - Combined: the Host writes the pair into the LAST TWO characters of the 32-hex ID
    ///   ('0'..'4' = Fire, Water, Wood, Metal, Earth). Decoding therefore never depends on the
    ///   team list, its order, or any hash agreement between devices, and the ID stays a valid
    ///   Guid "N" string. Older hash-encoded IDs still decode through the legacy fallback.
    /// 오행 v2: 같은 속성의 음 + 양만 결합한다(예: 불 음 + 불 양). 다른 속성끼리는 결합 불가.
    /// 결합 ID는 여전히 두 칸을 쓰지만 두 칸이 항상 같은 속성이다.
    /// Configure는 legacy ID hash의 공통 목록만 설정한다. 명시 Raw 원소는 목록이 비어도 유지된다.
    /// 원소 없는 옛 T05/T08 씬과 fixture에서는 ID hash가 None으로 해석되는 기존 경로를 유지한다.
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

        public static bool IsValidRawElement(OrbElement element) => element >= OrbElement.Fire && element <= OrbElement.Earth;

        /// <summary>Attack orbs have no duplicate Raw field. Missing Raw is valid only in legacy contracts.</summary>
        public static bool ValidElementData(OrbKind kind, OrbElement rawElement, bool requireExplicitRaw = false)
            => kind == OrbKind.Combined || kind == OrbKind.FeverAttack ? rawElement == OrbElement.None
                : kind == OrbKind.Raw && (IsValidRawElement(rawElement) || !requireExplicitRaw && rawElement == OrbElement.None);

        /// <summary>The received owner and the global team list never override an explicit Raw element.</summary>
        public static OrbElement RawElement(OrbRecord orb)
        {
            if (orb == null || orb.Kind != OrbKind.Raw) return OrbElement.None;
            if (IsValidRawElement(orb.RawElement)) return orb.RawElement;
            return orb.RawElement == OrbElement.None ? RawElement(orb.OrbId) : OrbElement.None;
        }

        public static bool SameElement(OrbRecord first, OrbRecord second)
            => first != null && second != null && first.Kind == OrbKind.Raw && second.Kind == OrbKind.Raw
                && ValidElementData(first.Kind, first.RawElement) && ValidElementData(second.Kind, second.RawElement)
                && RawElement(first) == RawElement(second);

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
            // Explicit sandbox identities are presentation-only. Never accept their prefix in a normal Host request.
            const string addedPrefix = "sandbox-added-", combinedPrefix = "sandbox-combined-";
            string sandboxId = orbId != null && orbId.StartsWith(addedPrefix, StringComparison.Ordinal)
                ? orbId.Substring(addedPrefix.Length)
                : orbId != null && orbId.StartsWith(combinedPrefix, StringComparison.Ordinal)
                    ? orbId.Substring(combinedPrefix.Length) : null;
            if (sandboxId != null && TryDecodeCombinedId(sandboxId, out yin, out yang)) return;
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
            if (string.IsNullOrEmpty(orbId) || orbId.Length != 32 || !Guid.TryParseExact(orbId, "N", out _)) return false;
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
