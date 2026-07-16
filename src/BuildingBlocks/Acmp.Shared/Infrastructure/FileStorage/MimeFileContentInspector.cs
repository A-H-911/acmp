using Acmp.Shared.Application.Abstractions;
using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Engine;

namespace Acmp.Shared.Infrastructure.FileStorage;

// C-FILE-01: magic-byte content inspection backed by Mime-Detective (14k+ signatures). Only the leading bytes
// are read + inspected — enough for the signature, cheap for a 2 GB recording. The stream position is restored
// so the handler can stream the full body to MinIO afterwards. Text formats (SVG/JSON) carry no binary magic,
// so they get a light structural head-check instead (OQ-026 default = MIME/extension whitelist v1; a deep
// scanner / ClamAV stays operator-opt-in).
public sealed class MimeFileContentInspector : IFileContentInspector
{
    private const int HeadBytes = 512;

    // Building the inspector compiles the signature trie — do it once (stateless + thread-safe to reuse).
    private static readonly IContentInspector Inspector =
        new ContentInspectorBuilder { Definitions = DefaultDefinitions.All() }.Build();

    // Declared content type -> the file extensions Mime-Detective may legitimately report for it. Mime-Detective
    // handles these well; the video containers below are checked by raw magic instead (its default set is
    // unreliable for the fragment/box formats). docx is a zip container (detected as docx or a bare zip).
    private static readonly IReadOnlyDictionary<string, string[]> BinaryExpectations =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = new[] { "png" },
            ["image/jpeg"] = new[] { "jpg", "jpeg" },
            ["application/pdf"] = new[] { "pdf" },
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = new[] { "docx", "zip" },
        };

    private static readonly byte[] Ftyp = { 0x66, 0x74, 0x79, 0x70 };       // "ftyp" (ISO-BMFF box, offset 4)
    private static readonly byte[] Ebml = { 0x1A, 0x45, 0xDF, 0xA3 };       // EBML header (WebM / Matroska)

    public bool ContentMatchesDeclared(Stream content, string declaredContentType)
    {
        var head = ReadHead(content);
        if (head.Length == 0)
            return false;

        return declaredContentType?.ToLowerInvariant() switch
        {
            "image/svg+xml" => StartsWith(head, (byte)'<'),
            "application/json" => StartsWith(head, (byte)'{') || StartsWith(head, (byte)'['),
            // ISO-BMFF (mp4/mov/quicktime) carries the "ftyp" box at offset 4; WebM/Matroska starts with EBML.
            // Raw magic here is more reliable than Mime-Detective for these container formats.
            "video/mp4" or "video/quicktime" => HasBytesAt(head, Ftyp, 4),
            "video/webm" => HasBytesAt(head, Ebml, 0),
            _ when BinaryExpectations.TryGetValue(declaredContentType!, out var expected) => MatchesAnyExtension(head, expected),
            _ => false,
        };
    }

    private static bool HasBytesAt(byte[] head, byte[] signature, int offset)
    {
        if (head.Length < offset + signature.Length)
            return false;
        for (var i = 0; i < signature.Length; i++)
            if (head[offset + i] != signature[i])
                return false;
        return true;
    }

    private static bool MatchesAnyExtension(byte[] head, string[] expected)
    {
        var sniffed = Inspector.Inspect(head).ByFileExtension().Select(x => x.Extension);
        return expected.Any(e => sniffed.Contains(e, StringComparer.OrdinalIgnoreCase));
    }

    // Reads up to HeadBytes and restores the position. Non-seekable streams cannot be rewound after a peek, so
    // fail closed rather than silently corrupt the upload (form-upload streams are always seekable).
    private static byte[] ReadHead(Stream content)
    {
        if (!content.CanSeek)
            return Array.Empty<byte>();

        var origin = content.Position;
        var buffer = new byte[HeadBytes];
        var read = 0;
        int n;
        while (read < buffer.Length && (n = content.Read(buffer, read, buffer.Length - read)) > 0)
            read += n;
        content.Position = origin;
        return read == buffer.Length ? buffer : buffer[..read];
    }

    private static bool StartsWith(byte[] head, byte token)
    {
        foreach (var b in head)
        {
            // Skip leading whitespace + a UTF-8 BOM before the first meaningful character.
            if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0xEF or 0xBB or 0xBF)
                continue;
            return b == token;
        }
        return false;
    }
}
