using Acmp.Modules.Actions.Application.Abstractions;
using Acmp.Modules.Actions.Domain.Enums;
using Acmp.Shared.Contracts.Actions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Actions.Infrastructure.Directory;

// Actions-owned implementation of the shared IActionLinkDirectory port (ADR-0001): answers the Decisions
// module's AC-029 gate without exposing Actions' tables. "Downstream link" is the (SourceType=Decision,
// SourceId) soft reference an ActionItem already carries (W13) — no FK, no cross-module read from the caller.
public sealed class ActionLinkDirectory : IActionLinkDirectory
{
    private readonly IActionsDbContext _db;

    public ActionLinkDirectory(IActionsDbContext db) => _db = db;

    public Task<bool> DecisionHasLinkedActionAsync(Guid decisionId, CancellationToken ct = default) =>
        _db.Actions.AsNoTracking()
            .AnyAsync(a => a.SourceType == ActionSourceType.Decision && a.SourceId == decisionId, ct);
}
