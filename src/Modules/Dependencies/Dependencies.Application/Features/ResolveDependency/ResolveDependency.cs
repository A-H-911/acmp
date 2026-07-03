using Acmp.Modules.Dependencies.Application.Abstractions;
using Acmp.Modules.Dependencies.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Dependencies.Application.Features.ResolveDependency;

// Mark a dependency satisfied (Open → Resolved). RBAC = Dependency.Create (same authority that creates).
// Audited (Dependency.Resolved). Unknown id → 404 (KeyNotFound); a non-Open edge → 409 (the domain guard).
public sealed record ResolveDependencyCommand(Guid Id) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class ResolveDependencyHandler : IRequestHandler<ResolveDependencyCommand>
{
    private readonly IDependenciesDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;

    public ResolveDependencyHandler(IDependenciesDbContext db, ICurrentUser user, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(ResolveDependencyCommand request, CancellationToken ct)
    {
        var edge = await _db.Dependencies.FirstOrDefaultAsync(d => d.PublicId == request.Id, ct)
            ?? throw new KeyNotFoundException("Dependency not found.");

        var (sub, _) = CurrentActor.Of(_user);
        edge.Resolve();
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Dependency.Resolved", sub, new { edge.PublicId, edge.Key }, ct);
    }
}
