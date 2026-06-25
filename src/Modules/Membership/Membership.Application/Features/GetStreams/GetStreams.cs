using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Features.GetMembers;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.GetStreams;

// The committee's streams — for the directory filter and stream-assignment UI. Any authenticated
// user (read is committee-wide, README §C).
public sealed record GetStreamsQuery : IRequest<IReadOnlyList<StreamRefDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetStreamsHandler : IRequestHandler<GetStreamsQuery, IReadOnlyList<StreamRefDto>>
{
    private readonly IMembershipDbContext _db;

    public GetStreamsHandler(IMembershipDbContext db) => _db = db;

    public async Task<IReadOnlyList<StreamRefDto>> Handle(GetStreamsQuery request, CancellationToken ct) =>
        await _db.Streams.AsNoTracking()
            .OrderBy(s => s.Code)
            .Select(s => new StreamRefDto(s.PublicId, s.Code, s.Name.En, s.Name.Ar))
            .ToListAsync(ct);
}
