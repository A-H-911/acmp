using Acmp.Modules.Dependencies.Application.Abstractions;
using Acmp.Modules.Dependencies.Application.Contracts;
using Acmp.Modules.Dependencies.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Topics;
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
    private readonly ITopicConfidentiality _confidentiality;

    public GetDependencyByKeyHandler(IDependenciesDbContext db, ITopicConfidentiality confidentiality)
    {
        _db = db;
        _confidentiality = confidentiality;
    }

    public async Task<DependencyDto?> Handle(GetDependencyByKeyQuery request, CancellationToken ct)
    {
        // FR-163 / AC-114 — THE DIRECT-BY-KEY PATH IS THE IDOR PATH, exactly as GetTopicDetail argues for
        // topics themselves: filtering the register while leaving this open would hide the edge from the
        // list and hand its Restricted endpoint's title to anyone who guesses or is sent the key.
        //
        // ⚠ FILTERED IN THE QUERY, SO THE REFUSAL IS null → 404 AND NOT 403. A 403 would confirm that a
        // dependency with this key exists, and its existence is part of what the classification protects.
        var edge = await _db.Dependencies.AsNoTracking()
            .WithoutHiddenTopics(await _confidentiality.GetHiddenTopicIdsAsync(ct))
            .FirstOrDefaultAsync(d => d.Key == request.Key, ct);
        return edge is null ? null : DependencyMapping.ToDetail(edge);
    }
}
