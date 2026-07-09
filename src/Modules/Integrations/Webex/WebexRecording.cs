namespace Acmp.Modules.Integrations.Webex;

// The recording details ACMP needs from GET /recordings/{id}: the Webex meeting id it belongs to (the
// correlation key) plus the reference URLs and duration (webex-feasibility.md §3.1). The file itself is never
// downloaded — only the reference is stored (§5).
public sealed record WebexRecording(
    string Id,
    string? MeetingId,
    string? PlaybackUrl,
    string? DownloadUrl,
    int? DurationSeconds);
