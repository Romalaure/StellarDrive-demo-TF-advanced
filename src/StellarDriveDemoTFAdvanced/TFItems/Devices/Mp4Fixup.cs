using System;
using System.Collections.Generic;
using System.IO;

namespace StellarDriveDemoTFAdvanced.TFItems.Devices
{
    /// <summary>
    /// YouTube serves audio as fragmented MP4. When its track carries an edit list ("edts" box),
    /// Windows Media Foundation reports the end of the stream before the first sample. Dropping
    /// the edit list (it only trims a few milliseconds of encoder delay) makes it decodable.
    /// Sample data is addressed relative to the fragments, so removing bytes from the header
    /// moves nothing that is referenced.
    /// </summary>
    internal static class Mp4Fixup
    {
        private static readonly HashSet<string> Containers = new HashSet<string> { "moov", "trak" };

        /// <summary>Rewrites the file without edit lists; returns whether anything was removed.</summary>
        public static bool StripEditLists(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            var output = new MemoryStream(data.Length);
            bool changed = Copy(data, 0, data.Length, output, out _);
            if (changed)
                File.WriteAllBytes(path, output.ToArray());
            return changed;
        }

        // Copies the boxes in [start, end), dropping "edts" inside moov/trak and fixing container sizes
        private static bool Copy(byte[] data, int start, int end, Stream output, out long written)
        {
            bool changed = false;
            written = 0;
            int i = start;
            while (i + 8 <= end)
            {
                long size = ReadUInt32(data, i);
                string type = System.Text.Encoding.ASCII.GetString(data, i + 4, 4);
                int header = 8;
                if (size == 1 && i + 16 <= end)
                {
                    size = (long)ReadUInt64(data, i + 8);
                    header = 16;
                }
                else if (size == 0)
                {
                    size = end - i;
                }
                if (size < header || i + size > end)
                {
                    // Malformed or truncated: copy the rest as is
                    output.Write(data, i, end - i);
                    written += end - i;
                    return changed;
                }

                if (type == "edts" && start > 0)
                {
                    changed = true;
                }
                else if (Containers.Contains(type) && header == 8)
                {
                    var inner = new MemoryStream();
                    changed |= Copy(data, i + 8, (int)(i + size), inner, out long innerSize);
                    WriteUInt32(output, (uint)(innerSize + 8));
                    output.Write(data, i + 4, 4);
                    inner.Position = 0;
                    inner.CopyTo(output);
                    written += innerSize + 8;
                }
                else
                {
                    output.Write(data, i, (int)size);
                    written += size;
                }
                i += (int)size;
            }
            if (i < end)
            {
                output.Write(data, i, end - i);
                written += end - i;
            }
            return changed;
        }

        private static uint ReadUInt32(byte[] data, int at) =>
            (uint)(data[at] << 24 | data[at + 1] << 16 | data[at + 2] << 8 | data[at + 3]);

        private static ulong ReadUInt64(byte[] data, int at) => (ulong)ReadUInt32(data, at) << 32 | ReadUInt32(data, at + 4);

        private static void WriteUInt32(Stream output, uint value)
        {
            output.WriteByte((byte)(value >> 24));
            output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }
    }
}
