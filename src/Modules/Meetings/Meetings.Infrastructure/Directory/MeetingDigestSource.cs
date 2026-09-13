using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Shared.Contracts.Meetings;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Infrastructure.Directory;

// Meetings-owned implementation of IMeetingDigestSource (ADR-0001) for the digest (AC-161): Scheduled
// meetings whose start falls in [from, to).
public sealed class MeetingDigestSource : IMeetingDigestSource
{
    private readonly IMeetingsDbContext _db;

    public MeetingDigestSource(IMeetingsDbContext db) => _db = db;

    public Task<int> CountStartingAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
        _db.Meetings.AsNoTracking()
            .CountAsync(m => m.Status == MeetingStatus.Scheduled && m.ScheduledStart >= from && m.ScheduledStart < to, ct);
}
