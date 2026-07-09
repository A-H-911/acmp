namespace Acmp.Modules.Meetings.Application;

// Recording-upload constraints (FR-056). Configurable via "Meetings:Recording". Mirrors the topic-attachment
// convention (AC-049) but recordings are video and far larger than documents, so the default cap is higher;
// the operator tunes MaxSizeBytes. Content types default to the browser-recordable video formats.
public sealed class MeetingRecordingOptions
{
    public const string SectionName = "Meetings:Recording";

    public long MaxSizeBytes { get; set; } = 2L * 1024 * 1024 * 1024; // 2 GB default (configurable)

    public IReadOnlyCollection<string> AllowedContentTypes { get; set; } = new[]
    {
        "video/mp4",
        "video/webm",
        "video/quicktime", // .mov
    };
}
