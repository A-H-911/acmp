using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.AssignStreams;

// Administrator sets a member's stream assignments (BL-024). Streams scope WRITE access (docs/domain/permission-role-matrix.md
// §E.1); read stays committee-wide. Unknown stream ids are ignored — only resolved streams assign.
public sealed record AssignStreamsCommand(Guid MemberPublicId, IReadOnlyList<Guid> StreamPublicIds)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { nameof(CommitteeRole.Administrator) };
}

public sealed class AssignStreamsValidator : AbstractValidator<AssignStreamsCommand>
{
    public AssignStreamsValidator() => RuleFor(x => x.MemberPublicId).NotEmpty();
}

public sealed class AssignStreamsHandler : IRequestHandler<AssignStreamsCommand>
{
    private readonly IMembershipDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;

    public AssignStreamsHandler(IMembershipDbContext db, ICurrentUser user, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(AssignStreamsCommand request, CancellationToken ct)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.PublicId == request.MemberPublicId, ct)
            ?? throw new KeyNotFoundException("Committee member not found.");

        var streamIds = await _db.Streams
            .Where(s => request.StreamPublicIds.Contains(s.PublicId))
            .Select(s => s.Id)
            .ToListAsync(ct);

        member.AssignStreams(streamIds);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Membership.StreamsAssigned", nameof(CommitteeMember),
            request.MemberPublicId.ToString(), ct: ct);
    }
}
