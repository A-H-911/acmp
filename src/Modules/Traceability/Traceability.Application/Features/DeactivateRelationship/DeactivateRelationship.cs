using Acmp.Modules.Traceability.Application.Abstractions;
using Acmp.Modules.Traceability.Application.Internal;
using Acmp.Modules.Traceability.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Traceability.Application.Features.DeactivateRelationship;

// Soft-delete an edge that was created in error (docs/domain/search-and-traceability.md §5): the row is kept with IsActive=0 for the audit
// trail — hard deletes are not permitted (ADR-0009). RBAC = Traceability.Link (same authority that creates).
// Audited (Relationship.Deactivated, guardrail #5). Unknown id → 404; already-inactive → no-op, still audited.
public sealed record DeactivateRelationshipCommand(Guid Id) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class DeactivateRelationshipHandler : IRequestHandler<DeactivateRelationshipCommand>
{
    private readonly ITraceabilityDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public DeactivateRelationshipHandler(ITraceabilityDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(DeactivateRelationshipCommand request, CancellationToken ct)
    {
        var edge = await _db.Relationships.FirstOrDefaultAsync(r => r.PublicId == request.Id, ct)
            ?? throw new KeyNotFoundException("Relationship not found.");

        var (sub, _) = CurrentActor.Of(_user);
        edge.Deactivate(sub, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Relationship.Deactivated", nameof(Relationship), edge.PublicId.ToString(), ct: ct);
    }
}
