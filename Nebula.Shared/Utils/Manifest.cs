using System.Diagnostics.CodeAnalysis;
using System.Text;
using Nebula.Shared.Models;

namespace Nebula.Shared.Utils;

public class ManifestReader : StreamReader
{
    public const int BufferSize = 128;

    public ManifestReader(Stream stream) : base(stream)
    {
        ReadManifestVersion();
    }

    public ManifestReader(Stream stream, bool detectEncodingFromByteOrderMarks) : base(stream,
        detectEncodingFromByteOrderMarks)
    {
        ReadManifestVersion();
    }

    public ManifestReader(Stream stream, Encoding encoding) : base(stream, encoding)
    {
        ReadManifestVersion();
    }

    public ManifestReader(Stream stream, Encoding encoding, bool detectEncodingFromByteOrderMarks) : base(stream,
        encoding, detectEncodingFromByteOrderMarks)
    {
        ReadManifestVersion();
    }

    public ManifestReader(Stream stream, Encoding encoding, bool detectEncodingFromByteOrderMarks, int bufferSize) :
        base(stream, encoding, detectEncodingFromByteOrderMarks, bufferSize)
    {
        ReadManifestVersion();
    }

    public ManifestReader(Stream stream, Encoding? encoding = null, bool detectEncodingFromByteOrderMarks = true,
        int bufferSize = -1, bool leaveOpen = false) : base(stream, encoding, detectEncodingFromByteOrderMarks,
        bufferSize, leaveOpen)
    {
        ReadManifestVersion();
    }

    public ManifestReader(string path) : base(path)
    {
        ReadManifestVersion();
    }

    public ManifestReader(string path, bool detectEncodingFromByteOrderMarks) : base(path,
        detectEncodingFromByteOrderMarks)
    {
        ReadManifestVersion();
    }

    public ManifestReader(string path, FileStreamOptions options) : base(path, options)
    {
        ReadManifestVersion();
    }

    public ManifestReader(string path, Encoding encoding) : base(path, encoding)
    {
        ReadManifestVersion();
    }

    public ManifestReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks) : base(path, encoding,
        detectEncodingFromByteOrderMarks)
    {
        ReadManifestVersion();
    }

    public ManifestReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks, int bufferSize) : base(
        path, encoding, detectEncodingFromByteOrderMarks, bufferSize)
    {
        ReadManifestVersion();
    }

    public ManifestReader(string path, Encoding encoding, bool detectEncodingFromByteOrderMarks,
        FileStreamOptions options) : base(path, encoding, detectEncodingFromByteOrderMarks, options)
    {
        ReadManifestVersion();
    }

    public string ManifestVersion { get; private set; } = "";
    public int CurrentId { get; private set; }

    private void ReadManifestVersion()
    {
        ManifestVersion = ReadLine() ?? throw new InvalidOperationException("File is empty!");
    }

    public RobustManifestItem? ReadItem()
    {
        var line = ReadLine();
        if (line == null) return null;

        var firstSpaceIndex = line.IndexOf(' ');
        if (firstSpaceIndex == -1)
            return new RobustManifestItem(line, string.Empty, CurrentId++);

        var span = line.AsSpan();
        var firstPart = span.Slice(0, firstSpaceIndex);
        var secondPart = span.Slice(firstSpaceIndex + 1);

        return new RobustManifestItem(firstPart.ToString(), secondPart.ToString(), CurrentId++);
    }

    public bool TryReadItem([NotNullWhen(true)] out RobustManifestItem? item)
    {
        item = ReadItem();
        return item != null;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        CurrentId = 0;
    }

    public new void DiscardBufferedData()
    {
        base.DiscardBufferedData();
        CurrentId = 0;
    }
}