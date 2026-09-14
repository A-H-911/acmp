namespace Acmp.Modules.Topics.Application;

// File-upload constraints (AC-162, which replaced AC-105/AC-049). Configurable via "Topics:Attachments";
// the default is NFR-011's 100 MB (DEC-190 a2 - the operator read NFR-011 as a grant, DEF-154) and the
// document/diagram types the design's upload hint lists (PDF/PNG/SVG/DOCX) plus common image types. The
// upload endpoint's Kestrel body limit is derived from MaxSizeBytes (TopicEndpoints), so raising this is
// enough; the SPA mirrors the default in SubmitTopic.tsx / TopicDetail.tsx for its pre-upload check.
public sealed class TopicAttachmentOptions
{
    public const string SectionName = "Topics:Attachments";

    public long MaxSizeBytes { get; set; } = 100L * 1024 * 1024;

    public IReadOnlyCollection<string> AllowedContentTypes { get; set; } = new[]
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "image/svg+xml",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // .docx
    };
}
