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
        var query = _db.Members.AsNoTracking();
        if (!request.IncludeInactive)
            query = query.Where(m => m.IsActive);

        return await query
            .OrderBy(m => m.FullName)
            .Select(m => new MemberDto(m.PublicId, m.FullName, m.Email, m.Role.ToString(), m.IsActive))
            .ToListAsync(ct);
    }
}
