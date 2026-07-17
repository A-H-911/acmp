using System.Text;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.FileStorage;
using FluentAssertions;

namespace Acmp.Application.Tests.Shared;

// C-FILE-01: the magic-byte inspector accepts content whose real bytes match the declared type and rejects
// mislabelled payloads. Text formats (svg/json) fall back to a structural head-check.
public class MimeFileContentInspectorTests
{
    private static readonly IFileContentInspector Inspector = new MimeFileContentInspector();

    // Real leading signatures for the allow-listed types.
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%\xE2\xE3\xCF\xD3");
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
    private static readonly byte[] Mp4 =
    {
        0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D, // ....ftypisom
        0x00, 0x00, 0x02, 0x00, 0x69, 0x73, 0x6F, 0x6D, 0x69, 0x73, 0x6F, 0x32, // ....isomiso2
        0x61, 0x76, 0x63, 0x31, 0x6D, 0x70, 0x34, 0x31,                         // avc1mp41
    };
    private static readonly byte[] Webm =
    {
        0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01, 0x42, 0xF7, 0x81, 0x01, 0x42,
        0xF2, 0x81, 0x04, 0x42, 0xF3, 0x81, 0x08, 0x42, 0x82, 0x84, 0x77, 0x65, 0x62, 0x6D, // ..webm
    };

    private static Stream S(byte[] bytes) => new MemoryStream(bytes);

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("video/mp4")]
    [InlineData("video/webm")]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    public void Matching_binary_content_is_accepted(string declared)
    {
        var bytes = declared switch
        {
            "application/pdf" => Pdf,
            "video/mp4" => Mp4,
            "video/webm" => Webm,
            "image/png" => Png,
            _ => Jpeg,
        };
        Inspector.ContentMatchesDeclared(S(bytes), declared).Should().BeTrue();
    }

    [Fact]
    public void Mislabelled_binary_content_is_rejected()
    {
        // A PNG declared as a PDF (the classic content-type spoof) is caught.
        Inspector.ContentMatchesDeclared(S(Png), "application/pdf").Should().BeFalse();
        Inspector.ContentMatchesDeclared(S(Pdf), "image/png").Should().BeFalse();
    }

    [Fact]
    public void Svg_and_json_use_a_structural_head_check()
    {
        Inspector.ContentMatchesDeclared(S(Encoding.UTF8.GetBytes("<svg xmlns=\"...\"></svg>")), "image/svg+xml").Should().BeTrue();
        Inspector.ContentMatchesDeclared(S(Encoding.UTF8.GetBytes("  \n  {\"a\":1}")), "application/json").Should().BeTrue();
        Inspector.ContentMatchesDeclared(S(Encoding.UTF8.GetBytes("[1,2,3]")), "application/json").Should().BeTrue();
        // Binary bytes labelled as text are rejected.
        Inspector.ContentMatchesDeclared(S(Png), "image/svg+xml").Should().BeFalse();
        Inspector.ContentMatchesDeclared(S(Png), "application/json").Should().BeFalse();
    }

    [Fact]
    public void Unrecognised_declared_type_fails_closed()
    {
        Inspector.ContentMatchesDeclared(S(Pdf), "application/x-msdownload").Should().BeFalse();
    }

    [Fact]
    public void Empty_or_non_seekable_content_fails_closed()
    {
        Inspector.ContentMatchesDeclared(S(Array.Empty<byte>()), "application/pdf").Should().BeFalse();
        Inspector.ContentMatchesDeclared(new NonSeekableStream(Pdf), "application/pdf").Should().BeFalse();
    }

    // A read-only forward stream (CanSeek == false) — the inspector cannot rewind after a peek, so it must
    // fail closed rather than consume the head and corrupt a would-be upload.
    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}
