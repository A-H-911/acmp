using Acmp.Modules.Dependencies.Application.Abstractions;
using Acmp.Modules.Dependencies.Application.Contracts;
using Acmp.Modules.Dependencies.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Dependencies.Application.Features.GetDependencyByKey;

// Dependency detail by display key (DPN-YYYY-###): both endpoints, kind, status, note, and the derived
// blocker flag. Readable by any authenticated committee member (read-all). Null → the endpoint maps to 404.
public sealed record GetDependencyByKeyQuery(string Key) : IRequest<DependencyDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetDependencyByKeyHandler : IRequestHandler<GetDependencyByKeyQuery, DependencyDto?>
{
    private readonly IDependenciesDbContext _db;

    public GetDependencyByKeyHandler(IDependenciesDbContext db) => _db = db;

    public async Task<DependencyDto?> Handle(GetDependencyByKeyQuery request, CancellationToken ct)
    {
        var edge = await _db.Dependencies.AsNoTracking().FirstOrDefaultAsync(d => d.Key == request.Key, ct);
        return edge is null ? null : DependencyMapping.ToDetail(edge);
    }
}
