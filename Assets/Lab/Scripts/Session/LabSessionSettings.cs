using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Deterministic fingerprint and bounded snapshot fragments for one Lab protocol version.</summary>
    public static class LabSessionSettings
    {
        public const int MaximumSnapshotCharacters = 512 * 1024;
        public const int MaximumSnapshotBytes = 512 * 1024;
        // A JSON character can expand to a six-byte escape in the outer packet. 4096 keeps
        // that worst case plus packet metadata below LabNetwork's 32 KiB receive bound.
        public const int SnapshotChunkCharacters = 512;

        /// <summary>Which room this is follows from ValidRoomSize before here; this only bounds the count (3:3 has six, #394).</summary>
        public static string Fingerprint(float[] values, int participants, ushort protocol)
        {
            if (values == null || values.Length != LabConfig.DeveloperFields.Count || participants < LabModel.MinimumParticipants
                || participants > LabModel.TerritoryMaximumParticipants) throw new ArgumentException("Invalid Lab session settings.");
            var text = new StringBuilder(512).Append("C6Lab/").Append(protocol).Append('/')
                .Append(participants).Append('/').Append(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                    throw new ArgumentException("Settings must be finite.");
                text.Append('|').Append(values[i].ToString("R", CultureInfo.InvariantCulture));
            }
            using (var hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public static string[] ChunkSnapshot(LabSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string json = JsonUtility.ToJson(snapshot);
            if (json.Length < 1 || json.Length > MaximumSnapshotCharacters
                || Encoding.UTF8.GetByteCount(json) > MaximumSnapshotBytes)
                throw new ArgumentException("Snapshot exceeds the bounded Lab transport size.", nameof(snapshot));
            var pieces = new System.Collections.Generic.List<string>();
            for (int offset = 0; offset < json.Length;)
            {
                int length = Math.Min(SnapshotChunkCharacters, json.Length - offset);
                if (offset + length < json.Length && char.IsHighSurrogate(json[offset + length - 1])) length--;
                pieces.Add(json.Substring(offset, length));
                offset += length;
            }
            return pieces.ToArray();
        }
    }
}
