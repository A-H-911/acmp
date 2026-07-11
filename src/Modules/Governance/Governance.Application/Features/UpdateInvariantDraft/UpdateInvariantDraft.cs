using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Modules.Governance.Domain;
using Acmp.Modules.Governance.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.UpdateInvariantDraft;

// Revise a Draft invariant (the request-changes loop returns Proposed → Draft, then the owner edits here).
// The domain refuses the edit once the invariant is Active (immutability). RBAC = Invariant.Create. Content
// is mirrored to both columns (FTS), so EN+AR are required on the required sections.
public sealed record UpdateInvariantDraftCommand(
    Guid InvariantId,
    InvariantCategory Category,
    InvariantScope Scope,
    LocalizedString Statement,
    LocalizedString Rationale,
    LocalizedString? ExceptionsPolicy,
    string OwnerUserId,
    string OwnerName) : IRequest<InvariantSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = InvariantRoles.Author;
}

public sealed class UpdateInvariantDraftValidator : AbstractValidator<UpdateInvariantDraftCommand>
{
    public UpdateInvariantDraftValidator()
    {
        RuleFor(x => x.InvariantId).NotEmpty();
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x.OwnerUserId).NotEmpty().WithMessage("An owner is required.");
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(256).WithMessage("An owner name is required (max 256).");
        RuleFor(x => x.Statement).NotNull().WithMessage("A statement is required.");
        RuleFor(x => x.Statement!.En).NotEmpty().When(x => x.Statement is not null).WithMessage("Statement (EN) is required.");
        RuleFor(x => x.Statement!.Ar).NotEmpty().When(x => x.Statement is not null).WithMessage("Statement (AR) is required.");
        RuleFor(x => x.Rationale).NotNull().WithMessage("A rationale is required.");
        RuleFor(x => x.Rationale!.En).NotEmpty().When(x => x.Rationale is not null).WithMessage("Rationale (EN) is required.");
        RuleFor(x => x.Rationale!.Ar).NotEmpty().When(x => x.Rationale is not null).WithMessage("Rationale (AR) is required.");
    }
}

public sealed class UpdateInvariantDraftHandler : IRequestHandler<UpdateInvariantDraftCommand, InvariantSummaryDto>
{
    private readonly IGovernanceDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;

    public UpdateInvariantDraftHandler(IGovernanceDbContext db, ICurrentUser user, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _audit = audit;
    }

    public async Task<InvariantSummaryDto> Handle(UpdateInvariantDraftCommand request, CancellationToken ct)
    {
        var inv = await _db.Invariants.FirstOrDefaultAsync(a => a.PublicId == request.InvariantId, ct)
            ?? throw new KeyNotFoundException("Invariant not found.");

        inv.UpdateDraft(request.Category, request.Scope, request.Statement, request.Rationale,
            request.ExceptionsPolicy, request.OwnerUserId, request.OwnerName);
        await _db.SaveChangesAsync(ct);

        var (sub, _) = CurrentActor.Of(_user);
        await _audit.EmitEnrichedAsync("Governance.InvariantDraftUpdated", nameof(Invariant), inv.PublicId.ToString(), ct: ct);

        return InvariantMapping.ToSummary(inv);
    }
}
