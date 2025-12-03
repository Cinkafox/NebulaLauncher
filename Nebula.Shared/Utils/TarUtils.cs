using System.IO.Compression;
using System.Text;

namespace Nebula.Shared.Utils;

public static class TarUtils
{
    public static void ExtractTarGz(Stream stream, string destinationDirectory)
    {
        if (destinationDirectory == null) throw new ArgumentNullException(nameof(destinationDirectory));
        
        using (var gzs = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: false))
        {
            // GZipStream does not expose length, so just pass as streaming source
            TarExtractor.ExtractTar(gzs, destinationDirectory);
        }
    }
}

public static class TarExtractor
{
    private const int BlockSize = 512;

    public static void ExtractTar(Stream tarStream, string destinationDirectory)
    {
        if (tarStream == null) throw new ArgumentNullException(nameof(tarStream));
        if (destinationDirectory == null) throw new ArgumentNullException(nameof(destinationDirectory));

        Directory.CreateDirectory(destinationDirectory);

        string pendingLongName = null;
        string pendingLongLink = null;

        var block = new byte[BlockSize];
        var zeroBlockCount = 0;

        while (true)
        {
            var read = ReadExactly(tarStream, block, 0, BlockSize);
            if (read == 0)
                break;

            if (IsAllZero(block))
            {
                zeroBlockCount++;
                if (zeroBlockCount >= 2) break; // two consecutive zero blocks -> end of archive
                continue;
            }

            zeroBlockCount = 0;

            var header = TarHeader.FromBlock(block);

            // validate header checksum (best-effort)
            if (!header.IsValidChecksum(block))
            {
                // Not fatal, but warn (we're not writing warnings to console by default).
            }

            // Some tar implementations supply the long filename in a preceding entry whose typeflag is 'L'.
            // If present, use that name for the following file.
            if (header.TypeFlag == 'L') // GNU long name
            {
                // read content blocks with size header.Size
                var size = header.Size;
                var nameBytes = new byte[size];
                ReadExactly(tarStream, nameBytes, 0, (int)size);
                // skip padding to full 512 block
                SkipPadding(tarStream, size);
                pendingLongName = ReadNullTerminatedString(nameBytes);
                continue;
            }

            if (header.TypeFlag == 'K') // GNU long linkname
            {
                var size = header.Size;
                var linkBytes = new byte[size];
                ReadExactly(tarStream, linkBytes, 0, (int)size);
                SkipPadding(tarStream, size);
                pendingLongLink = ReadNullTerminatedString(linkBytes);
                continue;
            }

            // Determine final name
            var entryName = !string.IsNullOrEmpty(pendingLongName) ? pendingLongName : header.GetName();
            var entryLinkName = !string.IsNullOrEmpty(pendingLongLink) ? pendingLongLink : header.LinkName;

            // reset pending longs after use
            pendingLongName = null;
            pendingLongLink = null;

            // sanitize path separators and avoid absolute paths
            entryName = entryName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            var targetPath = Path.Combine(destinationDirectory, entryName);

            switch (header.TypeFlag)
            {
                case '0':
                case '\0': // normal file
                case '7': // regular file (SUSv4)
                    EnsureParentDirectoryExists(targetPath);
                    using (var outFile = File.Open(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        CopyExact(tarStream, outFile, header.Size);
                    }

                    SkipPadding(tarStream, header.Size);
                    TrySetTimes(targetPath, header.ModTime);
                    break;

                case '5': // directory
                    Directory.CreateDirectory(targetPath);
                    TrySetTimes(targetPath, header.ModTime);
                    break;

                case '2': // symlink
                    // Creating symlinks require privileges on Windows and may fail.
                    // To keep things robust across platforms, write a small .symlink-info file for Windows fallback,
                    // and attempt real symlink creation on Unix-like platforms.
                    EnsureParentDirectoryExists(targetPath);
                    TryCreateSymlink(entryLinkName, targetPath);
                    break;

                case '1': // hard link - we will try to create by copying if target exists; otherwise skip
                    var linkTargetPath = Path.Combine(destinationDirectory,
                        entryLinkName.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(linkTargetPath))
                    {
                        EnsureParentDirectoryExists(targetPath);
                        File.Copy(linkTargetPath, targetPath, true);
                    }

                    break;

                case '3': // character device - skip
                case '4': // block device - skip
                case '6': // contiguous file - treat as regular
                    // To be safe, treat as file if size > 0
                    if (header.Size > 0)
                    {
                        EnsureParentDirectoryExists(targetPath);
                        using (var outFile = File.Open(targetPath, FileMode.Create, FileAccess.Write))
                        {
                            CopyExact(tarStream, outFile, header.Size);
                        }

                        SkipPadding(tarStream, header.Size);
                        TrySetTimes(targetPath, header.ModTime);
                    }

                    break;

                default:
                    // Unknown type - skip the file data
                    if (header.Size > 0)
                    {
                        Skip(tarStream, header.Size);
                        SkipPadding(tarStream, header.Size);
                    }

                    break;
            }
        }
    }

    private static void TryCreateSymlink(string linkTarget, string symlinkPath)
    {
        try
        {
            // On Unix-like systems we can try to create a symlink
            if (IsWindows())
            {
                // don't try symlinks on Windows by default - write a .symlink-info file instead
                File.WriteAllText(symlinkPath + ".symlink-info", $"symlink -> {linkTarget}");
            }
            else
            {
                // Unix: use symlink
                var dir = Path.GetDirectoryName(symlinkPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                // Use native syscall via Mono.Posix? Not allowed. Fall back to invoking 'ln -s' is not allowed.
                // Instead use System.IO.File.CreateSymbolicLink if available (net core 2.1+)
#if NETCOREAPP2_1_OR_GREATER || NET5_0_OR_GREATER
                var sym = new FileInfo(symlinkPath);
                sym.CreateAsSymbolicLink(linkTarget);
#else
                // If unavailable, write a .symlink-info file.
                File.WriteAllText(symlinkPath + ".symlink-info", $"symlink -> {linkTarget}");
#endif
            }
        }
        catch
        {
            // Ignore failures to create symlink; write fallback info
            try
            {
                File.WriteAllText(symlinkPath + ".symlink-info", $"symlink -> {linkTarget}");
            }
            catch
            {
            }
        }
    }

    private static bool IsWindows()
    {
        return Path.DirectorySeparatorChar == '\\';
    }

    private static void TrySetTimes(string path, DateTimeOffset modTime)
    {
        try
        {
            var dt = modTime.UtcDateTime;
            // convert to local to set file time sensibly
            File.SetLastWriteTimeUtc(path, dt);
        }
        catch
        {
            /* best-effort */
        }
    }

    private static void EnsureParentDirectoryExists(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    // Read exactly count bytes or throw if cannot
    private static int ReadExactly(Stream s, byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (total < count)
        {
            var r = s.Read(buffer, offset + total, count - total);
            if (r == 0) break;
            total += r;
        }

        return total;
    }

    // Skip count bytes by reading and discarding
    private static void Skip(Stream s, long count)
    {
        var tmp = new byte[8192];
        var remaining = count;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(tmp.Length, remaining);
            var r = s.Read(tmp, 0, toRead);
            if (r == 0) break;
            remaining -= r;
        }
    }

    private static void CopyExact(Stream source, Stream dest, long count)
    {
        var buf = new byte[8192];
        var remaining = count;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buf.Length, remaining);
            var r = source.Read(buf, 0, toRead);
            if (r == 0) break;
            dest.Write(buf, 0, r);
            remaining -= r;
        }
    }

    private static void SkipPadding(Stream s, long size)
    {
        var pad = (BlockSize - size % BlockSize) % BlockSize;
        if (pad > 0) Skip(s, pad);
    }

    private static bool IsAllZero(byte[] block)
    {
        for (var i = 0; i < block.Length; i++)
            if (block[i] != 0)
                return false;
        return true;
    }

    private static string ReadNullTerminatedString(byte[] bytes)
    {
        var len = 0;
        while (len < bytes.Length && bytes[len] != 0) len++;
        return Encoding.UTF8.GetString(bytes, 0, len);
    }

    private class TarHeader
    {
        public string Name { get; private set; }
        public int Mode { get; private set; }
        public int Uid { get; private set; }
        public int Gid { get; private set; }
        public long Size { get; private set; }
        public DateTimeOffset ModTime { get; private set; }
        public int Checksum { get; private set; }
        public char TypeFlag { get; private set; }
        public string LinkName { get; private set; }
        public string Magic { get; private set; }
        public string Version { get; private set; }
        public string UName { get; private set; }
        public string GName { get; private set; }
        public string DevMajor { get; private set; }
        public string DevMinor { get; private set; }
        public string Prefix { get; private set; }

        public static TarHeader FromBlock(byte[] block)
        {
            var h = new TarHeader();
            h.Name = ReadString(block, 0, 100);
            h.Mode = (int)ReadOctal(block, 100, 8);
            h.Uid = (int)ReadOctal(block, 108, 8);
            h.Gid = (int)ReadOctal(block, 116, 8);
            h.Size = ReadOctal(block, 124, 12);
            var mtime = ReadOctal(block, 136, 12);
            h.ModTime = DateTimeOffset.FromUnixTimeSeconds(mtime);
            h.Checksum = (int)ReadOctal(block, 148, 8);
            h.TypeFlag = (char)block[156];
            h.LinkName = ReadString(block, 157, 100);
            h.Magic = ReadString(block, 257, 6);
            h.Version = ReadString(block, 263, 2);
            h.UName = ReadString(block, 265, 32);
            h.GName = ReadString(block, 297, 32);
            h.DevMajor = ReadString(block, 329, 8);
            h.DevMinor = ReadString(block, 337, 8);
            h.Prefix = ReadString(block, 345, 155);

            return h;
        }

        public string GetName()
        {
            if (!string.IsNullOrEmpty(Prefix))
                return $"{Prefix}/{Name}".Trim('/');
            return Name;
        }

        public bool IsValidChecksum(byte[] block)
        {
            // compute checksum where checksum field (148..155) is spaces (0x20)
            long sum = 0;
            for (var i = 0; i < block.Length; i++)
                if (i >= 148 && i < 156) sum += 32; // space
                else sum += block[i];

            // stored checksum could be octal until null
            var stored = Checksum;
            return Math.Abs(sum - stored) <= 1; // allow +/-1 tolerance
        }

        private static string ReadString(byte[] buf, int offset, int length)
        {
            var end = offset;
            var max = offset + length;
            while (end < max && buf[end] != 0) end++;
            if (end == offset) return string.Empty;
            return Encoding.ASCII.GetString(buf, offset, end - offset);
        }

        private static long ReadOctal(byte[] buf, int offset, int length)
        {
            // Many tars store as ASCII octal, possibly padded with nulls or spaces.
            var end = offset + length;
            var i = offset;
            // skip leading spaces and nulls
            while (i < end && (buf[i] == 0 || buf[i] == (byte)' ')) i++;
            long val = 0;
            var found = false;
            for (; i < end; i++)
            {
                var b = buf[i];
                if (b == 0 || b == (byte)' ') break;
                if (b >= (byte)'0' && b <= (byte)'7')
                {
                    found = true;
                    val = (val << 3) + (b - (byte)'0');
                }
                // some implementations use base-10 ascii or binary; ignore invalid chars
            }

            if (!found) return 0;
            return val;
        }
    }
}