namespace Acmp.Modules.Topics.Application;

// File-upload constraints (AC-049). Configurable via "Topics:Attachments"; defaults to the AC-049
// default of 50 MB and the document/diagram types the design's upload hint lists (PDF/PNG/SVG/DOCX)
// plus common image types. The design's "up to 25 MB" hint is display copy; the AC default is 50 MB.
public sealed class TopicAttachmentOptions
{
    public const string SectionName = "Topics:Attachments";

    public long MaxSizeBytes { get; set; } = 50L * 1024 * 1024;

    public IReadOnlyCollection<string> AllowedContentTypes { get; set; } = new[]
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "image/svg+xml",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // .docx
    };
}
