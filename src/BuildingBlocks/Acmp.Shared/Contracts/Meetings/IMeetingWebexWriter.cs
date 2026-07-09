namespace Acmp.Shared.Contracts.Meetings;

// A Webex recording reference stored on the meeting (the reference, never the file — webex-feasibility.md §5).
public sealed record RecordingReference(string? PlaybackUrl, string? DownloadUrl, int? DurationSeconds);

// Inbound cross-module WRITE seam (ADR-0021): the Webex integration writes back into the Meetings aggregate
// without touching its tables. Implemented in Meetings.Infrastructure; called by the meeting-create job
// (SetWebexMeetingAsync, WS3b) and the recording-ready webhook processor (AttachRecordingAsync, WS3).
public interface IMeetingWebexWriter
{
    // Store the Webex meeting id (+ refreshed join URL) on the ACMP meeting so the recording webhook can
    // later correlate. Throws KeyNotFoundException if the meeting does not exist.
    Task SetWebexMeetingAsync(Guid meetingPublicId, string webexMeetingId, string? joinUrl, CancellationToken ct = default);

    // Attach a recording to the meeting whose WebexMeetingId matches. Returns false when no meeting matches
    // (an uncorrelated recording) so the caller can log-and-drop. Idempotent.
    Task<bool> AttachRecordingAsync(string webexMeetingId, RecordingReference recording, CancellationToken ct = default);
}
