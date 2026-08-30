using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Shared.Contracts.Meetings;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Infrastructure.Directory;

// Meetings-owned implementation of the shared IAgendaPresenterReader port (ADR-0001): answers the Decisions
// module's SoD-4 warn-and-audit check without exposing Meetings' tables. Mirrors MeetingQuorumSource, which
// exists for the same reason on the Vote quorum gate.
//
// ⚠ THE QUERY PROJECTS TO Guid? AND THEN TAKES THE FIRST — it does not filter on the presenter being set.
// Selecting `i.PresenterUserId` and letting FirstOrDefaultAsync return null covers all three "no answer"
// cases identically (unknown meeting, no agenda item for that topic, item with no presenter assigned),
// which is what the port's contract promises. Filtering `PresenterUserId != null` would look equivalent and
// is not: it would make an unassigned presenter indistinguishable from a missing row only by accident,
// rather than by a stated contract.
public sealed class AgendaPresenterReader : IAgendaPresenterReader
{
    private readonly IMeetingsDbContext _db;

    public AgendaPresenterReader(IMeetingsDbContext db) => _db = db;

    public Task<Guid?> GetPresenterUserIdAsync(Guid meetingId, Guid topicId, CancellationToken ct = default) =>
        _db.Agendas.AsNoTracking()
            .Where(a => a.MeetingId == meetingId)
            .SelectMany(a => a.Items.Where(i => i.TopicId == topicId).Select(i => i.PresenterUserId))
            .FirstOrDefaultAsync(ct);
}
