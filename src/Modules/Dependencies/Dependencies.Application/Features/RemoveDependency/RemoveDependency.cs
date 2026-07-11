using Acmp.Modules.Dependencies.Application.Abstractions;
using Acmp.Modules.Dependencies.Application.Internal;
using Acmp.Modules.Dependencies.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Dependencies.Application.Features.RemoveDependency;

// Retract a dependency created in error (Open → Removed, a soft-delete state — the row is kept for the audit
// trail). RBAC = Dependency.Create (same authority that creates). Audited (Dependency.Removed). Unknown id →
// 404 (KeyNotFound); a non-Open edge → 409 (the domain guard).
public sealed record RemoveDependencyCommand(Guid Id) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class RemoveDependencyHandler : IRequestHandler<RemoveDependencyCommand>
{
    private readonly IDependenciesDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;

    public RemoveDependencyHandler(IDependenciesDbContext db, ICurrentUser user, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(RemoveDependencyCommand request, CancellationToken ct)
    {
        var edge = await _db.Dependencies.FirstOrDefaultAsync(d => d.PublicId == request.Id, ct)
            ?? throw new KeyNotFoundException("Dependency not found.");

        var (sub, _) = CurrentActor.Of(_user);
        edge.Remove();
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Dependency.Removed", nameof(Dependency), edge.PublicId.ToString(), ct: ct);
    }
}
