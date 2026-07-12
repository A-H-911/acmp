using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Meetings;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Infrastructure.Directory;

// Meetings-side implementation of the Webex write seam (ADR-0021). The Webex integration calls this to store
// the meeting correlation id and, later, the recording reference — every write emits an AuditEvent (INV-005).
public sealed class MeetingWebexWriter : IMeetingWebexWriter
{
    private readonly IMeetingsDbContext _db;
    private readonly IAuditSink _audit;

    public MeetingWebexWriter(IMeetingsDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task SetWebexMeetingAsync(Guid meetingPublicId, string webexMeetingId, string? joinUrl, CancellationToken ct = default)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.PublicId == meetingPublicId, ct)
            ?? throw new KeyNotFoundException($"Meeting {meetingPublicId} was not found.");

        meeting.SetWebexMeeting(webexMeetingId, joinUrl);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Meetings.WebexMeetingLinked", nameof(Meeting), meeting.PublicId.ToString(), ct: ct);
    }

    public async Task<bool> AttachRecordingAsync(string webexMeetingId, RecordingReference recording, CancellationToken ct = default)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.WebexMeetingId == webexMeetingId, ct);
        if (meeting is null)
            return false;

        meeting.AttachRecording(recording.PlaybackUrl, recording.DownloadUrl, recording.DurationSeconds);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Meetings.RecordingAttached", nameof(Meeting), meeting.PublicId.ToString(), ct: ct);
        return true;
    }
}
