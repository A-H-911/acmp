using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.CreateDelegation;

// Chairman/Secretary delegates a capability (policy) to another member for a bounded window
// (docs/domain/permission-role-matrix.md §E.3, row 28 Auth.Delegate). The delegate exercises the delegated policy only within
// the window; delegations auto-expire and are audited.
public sealed record CreateDelegationCommand(
    Guid DelegateMemberPublicId, string Capability, DateTimeOffset ValidFrom, DateTimeOffset ValidTo)
    : IRequest<Guid>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { nameof(CommitteeRole.Chairman), nameof(CommitteeRole.Secretary) };
}

public sealed class CreateDelegationValidator : AbstractValidator<CreateDelegationCommand>
{
    public CreateDelegationValidator()
    {
        RuleFor(x => x.DelegateMemberPublicId).NotEmpty();
        RuleFor(x => x.Capability).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ValidTo).GreaterThan(x => x.ValidFrom);
    }
}

public sealed class CreateDelegationHandler : IRequestHandler<CreateDelegationCommand, Guid>
{
    private readonly IMembershipDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;

    public CreateDelegationHandler(IMembershipDbContext db, ICurrentUser user, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _audit = audit;
    }

    public async Task<Guid> Handle(CreateDelegationCommand request, CancellationToken ct)
    {
        var delegator = await _db.Members.FirstOrDefaultAsync(m => m.KeycloakUserId == _user.UserId, ct)
            ?? throw new ForbiddenAccessException("Delegator is not a provisioned committee member.");

        var target = await _db.Members.FirstOrDefaultAsync(m => m.PublicId == request.DelegateMemberPublicId, ct)
            ?? throw new KeyNotFoundException("Delegate member not found.");

        var delegation = Delegation.Create(
            delegator.Id, target.Id, request.Capability.Trim(), request.ValidFrom, request.ValidTo);
        _db.Delegations.Add(delegation);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Membership.DelegationCreated", _user.UserId,
            new { delegation.PublicId, request.DelegateMemberPublicId, request.Capability, request.ValidFrom, request.ValidTo }, ct);

        return delegation.PublicId;
    }
}
