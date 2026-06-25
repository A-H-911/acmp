using Acmp.Modules.Membership.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.GetMembers;

public sealed class GetMembersHandler : IRequestHandler<GetMembersQuery, IReadOnlyList<MemberDto>>
{
    private readonly IMembershipDbContext _db;

    public GetMembersHandler(IMembershipDbContext db) => _db = db;

    public async Task<IReadOnlyList<MemberDto>> Handle(GetMembersQuery request, CancellationToken ct)
    {
        // Few streams for a single committee — load once and map in memory (no per-row join).
        var streams = await _db.Streams.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => new StreamRefDto(s.PublicId, s.Code, s.Name.En, s.Name.Ar), ct);

        var query = _db.Members.AsNoTracking();
        if (!request.IncludeInactive)
            query = query.Where(m => m.Status == Domain.Enums.MembershipStatus.Active);

        var rows = await query
            .OrderBy(m => m.FullName)
            .Select(m => new
            {
                m.PublicId,
                m.FullName,
                m.Email,
                m.Role,
                m.Status,
                m.IsVotingEligible,
                StreamIds = m.Streams.Select(s => s.StreamId).ToList(),
            })
            .ToListAsync(ct);

        return rows.Select(m => new MemberDto(
            m.PublicId, m.FullName, m.Email, m.Role.ToString(), m.Status.ToString(),
            m.Status == Domain.Enums.MembershipStatus.Active, m.IsVotingEligible,
            m.StreamIds.Where(streams.ContainsKey).Select(id => streams[id]).ToList())).ToList();
    }
}
