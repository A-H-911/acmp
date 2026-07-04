using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Modules.Governance.Domain;
using Acmp.Modules.Governance.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Governance.Application.Features.CreateInvariant;

// W18: author a new Architecture Invariant in Draft. RBAC = Invariant.Create (Chairman/Secretary; Member/
// Reviewer allow-if-owner). Content is entered in one UI language and MIRRORED to both LocalizedString
// columns (the locked FTS pattern), so both EN+AR are required. Category/Scope/Statement/Rationale/Owner are
// all required at create (the design's single-step create dialog); ExceptionsPolicy is optional (docs/22 §A.5).
public sealed record CreateInvariantCommand(
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

public sealed class CreateInvariantValidator : AbstractValidator<CreateInvariantCommand>
{
    public CreateInvariantValidator()
    {
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

public sealed class CreateInvariantHandler : IRequestHandler<CreateInvariantCommand, InvariantSummaryDto>
{
    private readonly IGovernanceDbContext _db;
    private readonly IInvariantKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public CreateInvariantHandler(IGovernanceDbContext db, IInvariantKeyGenerator keys, ICurrentUser user,
        IClock clock, IAuditSink audit)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<InvariantSummaryDto> Handle(CreateInvariantCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, _) = CurrentActor.Of(_user);

        var key = await _keys.NextInvariantKeyAsync(now.Year, ct);
        var inv = Invariant.Draft(key, request.Category, request.Scope, request.Statement, request.Rationale,
            request.ExceptionsPolicy, request.OwnerUserId, request.OwnerName, now);

        _db.Invariants.Add(inv);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Governance.InvariantDrafted", sub, new { inv.PublicId, inv.Key, inv.OwnerUserId }, ct);

        return InvariantMapping.ToSummary(inv);
    }
}
