using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Shared.Contracts.Topics;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Infrastructure.Persistence;

// Topics-side implementation of the cross-module ITopicConfidentiality seam (ADR-0001, SL-030 / FR-163 /
// C-AUTHZ-04): Meetings, Traceability and Dependencies each froze a topic's key+title into their own schema
// at create time (ADR-0019), so the read predicate that guards Topics' own surfaces cannot reach those
// copies. They ask here which topics to redact, and redact at projection (mirrors TopicStreamReader).
//
// ⚠ THIS IS NOT A THIRD EXPRESSION OF THE VISIBILITY RULE. TopicVisibilityQuery's remarks already warn that
// VisibleTo and TopicVisibilityScope.CanSee must stay equivalent; a third hand-written predicate here would
// be the drift they warn about. So the hidden set is DERIVED from VisibleTo by subtraction rather than
// written out: restricted topics, minus the restricted topics VisibleTo lets through. Change the rule in one
// place and this follows for free.
public sealed class TopicConfidentialityReader : ITopicConfidentiality
{
    private readonly TopicsDbContext _db;
    private readonly ITopicVisibility _visibility;

    public TopicConfidentialityReader(TopicsDbContext db, ITopicVisibility visibility)
    {
        _db = db;
        _visibility = visibility;
    }

    public async Task<Guid[]> GetHiddenTopicIdsAsync(CancellationToken ct = default)
    {
        var scope = await _visibility.ResolveAsync(ct);

        // The overwhelmingly common privileged case costs zero round trips: a committee-wide reader hides
        // from nothing, so there is no set to compute (DEC-063 d1).
        if (scope.SeesAllRestricted)
            return Array.Empty<Guid>();

        // Only a RESTRICTED topic can ever be hidden, so the unrestricted majority never enters either query.
        var restricted = _db.Topics.AsNoTracking().Where(t => t.IsRestricted);

        var all = await restricted.Select(t => t.PublicId).ToListAsync(ct);
        if (all.Count == 0)
            return Array.Empty<Guid>();

        var visible = await restricted.VisibleTo(scope).Select(t => t.PublicId).ToListAsync(ct);

        return all.Except(visible).ToArray();
    }
}
