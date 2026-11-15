using System;
using System.Globalization;

namespace C6Lab
{
    /// <summary>Bonjour arbitrates the shared room number, including simultaneous creation.</summary>
    public static class LabRoomNames
    {
        public const string ServiceBase = "C6Lab-Room";
        public const string GameServiceBase = "Hapio-Room";
        /// <summary>Staff demo rooms advertise a fixed name (<see cref="LabPendingRoom.StaffStageName"/>) for collision checks.</summary>
        public const string StaffServicePrefix = "Hapio-Staff-";
        private static readonly string[] StaffStageYokai = { "구미호", "야광귀", "지귀" };

        public static string ServiceBaseFor(LabRoomFamily family) => family == LabRoomFamily.Lab ? ServiceBase
            : family == LabRoomFamily.Game ? GameServiceBase : throw new ArgumentOutOfRangeException(nameof(family));

        public static bool TryNumber(string name, out int number, string serviceBase = null)
        {
            if (serviceBase == null)
                return TryNumber(name, out number, ServiceBase) || TryNumber(name, out number, GameServiceBase);
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

        /// <summary>Parse current grouped and historical ungrouped names for display only; admission checks the live protocol.</summary>
        public static bool TryStaffStage(string name, out int stage) => TryStaffRoom(name, out stage, out _);

        public static bool TryStaffRoom(string name, out int stage, out int group)
        {
            stage = group = 0;
            if (name == null || !name.StartsWith(StaffServicePrefix, StringComparison.Ordinal)) return false;
            string[] parts = name.Substring(StaffServicePrefix.Length).Split('-');
            if ((parts.Length != 4 && parts.Length != 6) || parts[2] != "Stage" || !IsDigits(parts[0]) || !IsDigits(parts[1])
                || parts[3].Length != 1 || parts[3][0] < '1' || parts[3][0] > '3') return false;
            if (parts.Length == 6)
            {
                if (parts[4] != "Group" || parts[5].Length != 1 || parts[5][0] < '1' || parts[5][0] > '2') return false;
                group = parts[5][0] - '0';
            }
            stage = parts[3][0] - '0';
            return true;
        }

        private static bool IsDigits(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 9) return false;
            foreach (char c in value) if (c < '0' || c > '9') return false;
            return true;
        }

        public static int SortNumber(string name) => TryNumber(name, out int number) ? number : int.MaxValue;

        public static string Display(string name)
        {
            if (TryNumber(name, out int number)) return "수장고-" + number.ToString(CultureInfo.InvariantCulture);
            // The fixed staff name carries the demo revision and protocol only for Bonjour; players see the stage.
            if (TryStaffRoom(name, out int stage, out int group))
                return (group == 0 ? "수장고-" : (group == 1 ? "A그룹 · " : "B그룹 · "))
                    + stage.ToString(CultureInfo.InvariantCulture) + "스테이지 " + StaffStageYokai[stage - 1];
            if (string.IsNullOrWhiteSpace(name)) return "방 이름 확인 중";
            // Preserve compatibility with already-running versions on the same LAN.
            if (name.StartsWith("C6Lab-", StringComparison.Ordinal)) return "수장고-" + name.Substring(6);
            if (name.StartsWith("Hapio-", StringComparison.Ordinal)) return "수장고-" + name.Substring(6);
            return name;
        }
    }
}
