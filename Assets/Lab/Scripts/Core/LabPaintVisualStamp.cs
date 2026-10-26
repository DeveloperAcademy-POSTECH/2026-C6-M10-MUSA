using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Bounded, Host-authored rendering hints. These never decide gameplay coverage or ownership.
    /// The current committed RGBA/P maps remain authoritative for erosion and overwriting.
    /// </summary>
    [Serializable]
    public struct LabPaintVisualStamp
    {
        public const int MaximumCount = 128;
        public const int ByteSize = 44;
        public const int MaximumBytes = MaximumCount * ByteSize;
        public const float MaximumRadius = 64f;
        public uint sequence;
        public Vector3 center, right;
        public float radius;
        public Color32 color;
        public byte painterSlot;
        public LabPaintPattern pattern;
        public uint seed;
        public Vector3 Up => Vector3.Cross(center, right);

        public static bool Valid(LabPaintVisualStamp[] stamps)
        {
            if (stamps == null || stamps.Length > MaximumCount) return false;
            uint previous = 0;
            foreach (var stamp in stamps)
            {
                if (stamp.sequence == 0 || stamp.sequence <= previous
                    || !Finite(stamp.center) || !Finite(stamp.right)
                    || Mathf.Abs(stamp.center.sqrMagnitude - 1f) > .002f
                    || Mathf.Abs(stamp.right.sqrMagnitude - 1f) > .002f
                    || Mathf.Abs(Vector3.Dot(stamp.center, stamp.right)) > .002f
                    || !float.IsFinite(stamp.radius) || stamp.radius <= 0f || stamp.radius > MaximumRadius
                    || stamp.color.a != 255 || stamp.painterSlot > 5
                    || stamp.pattern < LabPaintPattern.Legacy || stamp.pattern > LabPaintPattern.Flow) return false;
                previous = stamp.sequence;
            }
            return true;
        }

        public static byte[] Encode(LabPaintVisualStamp[] stamps)
        {
            if (!Valid(stamps)) throw new ArgumentException("Invalid visual paint descriptors.", nameof(stamps));
            if (stamps.Length == 0) return Array.Empty<byte>();
            using (var stream = new MemoryStream(stamps.Length * ByteSize))
            using (var writer = new BinaryWriter(stream))
            {
                foreach (var s in stamps)
                {
                    writer.Write(s.sequence);
                    writer.Write(s.center.x); writer.Write(s.center.y); writer.Write(s.center.z);
                    writer.Write(s.right.x); writer.Write(s.right.y); writer.Write(s.right.z);
                    writer.Write(s.radius);
                    writer.Write(s.color.r); writer.Write(s.color.g); writer.Write(s.color.b); writer.Write(s.color.a);
                    writer.Write(s.painterSlot); writer.Write((byte)s.pattern); writer.Write((ushort)0);
                    writer.Write(s.seed);
                }
                return stream.ToArray();
            }
        }

        public static bool TryDecode(byte[] bytes, out LabPaintVisualStamp[] stamps)
        {
            stamps = null;
            if (bytes == null || bytes.Length > MaximumBytes || bytes.Length % ByteSize != 0) return false;
            var result = new LabPaintVisualStamp[bytes.Length / ByteSize];
            using (var reader = new BinaryReader(new MemoryStream(bytes, false)))
            {
                for (int i = 0; i < result.Length; i++)
                {
                    var s = new LabPaintVisualStamp { sequence = reader.ReadUInt32() };
                    s.center = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    s.right = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    s.radius = reader.ReadSingle();
                    s.color = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                    s.painterSlot = reader.ReadByte(); s.pattern = (LabPaintPattern)reader.ReadByte();
                    if (reader.ReadUInt16() != 0) return false;
                    s.seed = reader.ReadUInt32(); result[i] = s;
                }
            }
            if (!Valid(result)) return false;
            stamps = result;
            return true;
        }

        public static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
    }
}
