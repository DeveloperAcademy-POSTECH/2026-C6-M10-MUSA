using System;
using System.Globalization;

namespace C6Lab
{
    /// <summary>Bonjour arbitrates the shared room number, including simultaneous creation.</summary>
    public static class LabRoomNames
    {
        public const string ServiceBase = "C6Lab-Room";

        public static bool TryNumber(string name, out int number, string serviceBase = ServiceBase)
        {
            number = 0;
            if (name == serviceBase) { number = 1; return true; }
            string prefix = serviceBase + " (";
            if (name == null || !name.StartsWith(prefix, StringComparison.Ordinal)
                || !name.EndsWith(")", StringComparison.Ordinal)) return false;
            string suffix = name.Substring(prefix.Length, name.Length - prefix.Length - 1);
            if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 2)
                return false;
            number = value;
            return true;
        }

        public static int SortNumber(string name) => TryNumber(name, out int number) ? number : int.MaxValue;

        public static string Display(string name)
        {
            if (TryNumber(name, out int number)) return "수장고-" + number.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(name)) return "방 이름 확인 중";
            // Preserve compatibility with already-running versions on the same LAN.
            if (name.StartsWith("C6Lab-", StringComparison.Ordinal)) return "수장고-" + name.Substring(6);
            return name;
        }
    }
}
