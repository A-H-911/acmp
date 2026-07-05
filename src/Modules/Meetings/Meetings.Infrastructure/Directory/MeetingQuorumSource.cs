using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Shared.Contracts.Meetings;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Infrastructure.Directory;

// Meetings-owned implementation of the shared IMeetingQuorumSource port (ADR-0001): answers the Decisions
// module's Vote present-quorum gate without exposing Meetings' tables. "Present-eligible" = an owned
// Attendance row with IsVotingEligible AND Status ∈ {Present, Late} (docs/domain/domain-model.md §Attendance). Unknown meeting =
// 0 (the handler treats "no linked meeting" separately).
public sealed class MeetingQuorumSource : IMeetingQuorumSource
{
    private readonly IMeetingsDbContext _db;

    public MeetingQuorumSource(IMeetingsDbContext db) => _db = db;

    public Task<int> GetPresentEligibleCountAsync(Guid meetingId, CancellationToken ct = default) =>
        _db.Meetings.AsNoTracking()
            .Where(m => m.PublicId == meetingId)
            .Select(m => m.Attendees.Count(a => a.IsVotingEligible &&
                (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)))
            .FirstOrDefaultAsync(ct);
}
