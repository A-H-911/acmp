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
        {
            // FR-158 / DEF-038 — INVITED IS NOT "INACTIVE". It means pre-registered and not yet
            // signed in, and hiding it is precisely the defect: the roster showed 1 of 26 real
            // committee members and read as though the committee were nearly empty, leaving an
            // administrator unable to name the people who still had to be chased.
            //
            // Disabled remains hidden by default — that IS the "inactive" the flag is about, and
            // AC-058 keeps those records for historical attribution rather than for the directory.
            query = query.Where(m => m.Status == Domain.Enums.MembershipStatus.Active
                                     || m.Status == Domain.Enums.MembershipStatus.Invited);
        }

        var rows = await query
            .OrderBy(m => m.FullName)
            .Select(m => new
            {
                m.PublicId,
                m.KeycloakUserId,
                m.FullName,
                m.Email,
                m.Role,
                m.Status,
                m.IsVotingEligible,
                StreamIds = m.Streams.Select(s => s.StreamId).ToList(),
            })
            .ToListAsync(ct);

        return rows.Select(m => new MemberDto(
            m.PublicId, m.KeycloakUserId, m.FullName, m.Email, m.Role.ToString(), m.Status.ToString(),
            m.Status == Domain.Enums.MembershipStatus.Active, m.IsVotingEligible,
            m.StreamIds.Where(streams.ContainsKey).Select(id => streams[id]).ToList())).ToList();
    }
}
