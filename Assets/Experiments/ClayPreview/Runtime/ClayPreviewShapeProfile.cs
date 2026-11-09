using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Hapio.Experiments
{
    public enum ClayPreviewShapeSource { Original = 0, Image = 1, Procedural = 2 }

    /// <summary>Explicit local comparison settings with a self-contained source PNG, never game settings.</summary>
    [Serializable]
    public sealed class ClayPreviewShapeProfile
    {
        public const string Format = "clay-preview-shape-profile-v1";
        public const int MaximumProfileBytes = 24 * 1024 * 1024;
        public string format = Format;
        public string bakeVersion = ClayPreviewShapeMask.BakeVersion;
        public ClayPreviewShapeSource source = ClayPreviewShapeSource.Original;
        public float core = .1625f, length = .21f, width = .048f;
        public float roundness = .85f, irregularity = 1, asymmetry = 1;
        public int spikes = 9, seed = 572, droplets = 4;
        public float dropletSize = .04f, dropletDistance = .1438f;
        public string imagePngBase64, imageSha256, imageFileName;

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "HapioClayShape", "startup-profile.json");

        public void SetImage(ClayPreviewShapeMask.Result image)
        {
            if (image == null || !image.Succeeded || image.SourceBytes == null)
                throw new ArgumentException("Use a successfully prepared PNG outline.", nameof(image));
            imagePngBase64 = Convert.ToBase64String(image.SourceBytes);
            imageSha256 = image.SourceSha256;
            imageFileName = image.FileName;
        }

        public bool Validate(out string error)
        {
            error = null;
            if (format != Format || bakeVersion != ClayPreviewShapeMask.BakeVersion)
            { error = "The shape profile format or outline bake version is incompatible."; return false; }
            if (!Enum.IsDefined(typeof(ClayPreviewShapeSource), source))
            { error = "The shape source is unknown."; return false; }
            if (!Within(core, .12f, .3f) || !Within(length, .05f, .3f) || !Within(width, .015f, .08f)
                || !Within(roundness, 0, 1) || !Within(irregularity, 0, 1) || !Within(asymmetry, 0, 1)
                || !Within(dropletSize, .005f, .04f) || !Within(dropletDistance, 0, .15f)
                || spikes < 5 || spikes > 20 || droplets < 0 || droplets > 12)
            { error = "A shape setting is non-finite or outside its supported range."; return false; }
            bool hasImage = !string.IsNullOrEmpty(imagePngBase64);
            if (source == ClayPreviewShapeSource.Image && !hasImage)
            { error = "The image shape profile has no embedded source PNG."; return false; }
            if (!hasImage)
            {
                if (!string.IsNullOrEmpty(imageSha256) || !string.IsNullOrEmpty(imageFileName))
                { error = "The shape profile has incomplete image metadata."; return false; }
                return true;
            }
            return TryGetImageBytes(out _, out error);
        }

        public bool TryGetImageBytes(out byte[] bytes, out string error)
        {
            bytes = null; error = null;
            try
            {
                const int maximumBase64Characters = (ClayPreviewImageImport.MaximumFileBytes + 2) / 3 * 4;
                if (string.IsNullOrEmpty(imagePngBase64) || imagePngBase64.Length > maximumBase64Characters)
                { error = "The embedded outline PNG is missing or exceeds 16 MiB."; return false; }
                if (!IsHash(imageSha256) || string.IsNullOrEmpty(imageFileName) || imageFileName.Length > 255
                    || imageFileName != Path.GetFileName(imageFileName))
                { error = "The embedded outline image metadata is invalid."; return false; }
                byte[] decoded = Convert.FromBase64String(imagePngBase64);
                if (decoded.Length == 0 || decoded.Length > ClayPreviewImageImport.MaximumFileBytes
                    || !Png(decoded) || !ClayPreviewImageImport.TryGetDimensions(decoded, out _, out _, out error))
                { error = error ?? "The embedded outline must be a valid bounded PNG."; return false; }
                if (!string.Equals(ClayPreviewShapeMask.Hash(decoded), imageSha256, StringComparison.OrdinalIgnoreCase))
                { error = "The embedded outline PNG checksum differs from the saved profile."; return false; }
                bytes = decoded; return true;
            }
            catch (Exception exception)
            { error = "The embedded outline could not be read (" + exception.GetType().Name + ")."; return false; }
        }

        public static bool TrySave(string path, ClayPreviewShapeProfile profile, out string error)
        {
            error = null; string temporary = null;
            try
            {
                if (profile == null) { error = "There is no shape profile to save."; return false; }
                if (!profile.Validate(out error)) return false;
                byte[] bytes = new UTF8Encoding(false, true).GetBytes(JsonUtility.ToJson(profile, true));
                if (bytes.Length > MaximumProfileBytes) { error = "The shape profile exceeds 24 MiB."; return false; }
                string absolute = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(absolute));
                temporary = absolute + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                // The temporary file shares a filesystem with the destination. A failed replacement
                // must leave the last valid profile in place, rather than deleting it first.
                if (File.Exists(absolute)) File.Replace(temporary, absolute, null);
                else File.Move(temporary, absolute);
                temporary = null; return true;
            }
            catch (Exception exception)
            { error = "The shape profile could not be saved (" + exception.GetType().Name + "). Previous profile kept."; return false; }
            finally
            {
                if (temporary != null)
                    try { if (File.Exists(temporary)) File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
            }
        }

        public static bool TryLoad(string path, out ClayPreviewShapeProfile profile, out string error)
        {
            profile = null; error = null;
            try
            {
                byte[] bytes;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length == 0 || stream.Length > MaximumProfileBytes)
                    { error = "Choose a nonempty shape profile of 24 MiB or less."; return false; }
                    bytes = new byte[(int)stream.Length]; int read = 0;
                    while (read < bytes.Length)
                    {
                        int count = stream.Read(bytes, read, bytes.Length - read);
                        if (count == 0) { error = "The shape profile changed or was truncated while reading."; return false; }
                        read += count;
                    }
                    if (stream.ReadByte() != -1) { error = "The shape profile changed while reading."; return false; }
                }
                var loaded = JsonUtility.FromJson<ClayPreviewShapeProfile>(new UTF8Encoding(false, true).GetString(bytes));
                if (loaded == null) { error = "The shape profile is empty."; return false; }
                if (!loaded.Validate(out error)) return false;
                profile = loaded; return true;
            }
            catch (Exception exception)
            { error = "The shape profile could not be loaded (" + exception.GetType().Name + "). Previous settings kept."; return false; }
        }

        static bool Within(float value, float minimum, float maximum) => !float.IsNaN(value) && !float.IsInfinity(value)
            && value >= minimum && value <= maximum;
        static bool IsHash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value)
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')) return false;
            return true;
        }
        static bool Png(byte[] b) => b.Length >= 8 && b[0] == 137 && b[1] == 80 && b[2] == 78 && b[3] == 71
            && b[4] == 13 && b[5] == 10 && b[6] == 26 && b[7] == 10;
    }
}
