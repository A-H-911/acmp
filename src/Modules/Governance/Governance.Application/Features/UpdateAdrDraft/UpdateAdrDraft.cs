using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Features.CreateAdr;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Modules.Governance.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.UpdateAdrDraft;

// Revise a Draft ADR (the request-changes loop returns Proposed → Draft, then the author edits here). The
// domain refuses the edit once the ADR is Approved (FR-101 immutability). RBAC = Adr.Create. Content is
// mirrored to both columns (FTS), so EN+AR are required on the required sections.
public sealed record UpdateAdrDraftCommand(
    Guid AdrId,
    LocalizedString Title,
    LocalizedString Context,
    LocalizedString? DecisionDrivers,
    LocalizedString DecisionText,
    LocalizedString? ConsequencesPositive,
    LocalizedString? ConsequencesNegative,
    IReadOnlyList<AdrOptionRequest>? Options) : IRequest<AdrSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = AdrRoles.Author;
}

public sealed class UpdateAdrDraftValidator : AbstractValidator<UpdateAdrDraftCommand>
{
    public UpdateAdrDraftValidator()
    {
        RuleFor(x => x.AdrId).NotEmpty();
        RuleFor(x => x.Title).NotNull().WithMessage("A title is required.");
        RuleFor(x => x.Title!.En).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (EN) is required (max 512).");
        RuleFor(x => x.Title!.Ar).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (AR) is required (max 512).");
        RuleFor(x => x.Context).NotNull().WithMessage("A context is required.");
        RuleFor(x => x.Context!.En).NotEmpty().When(x => x.Context is not null).WithMessage("Context (EN) is required.");
        RuleFor(x => x.Context!.Ar).NotEmpty().When(x => x.Context is not null).WithMessage("Context (AR) is required.");
        RuleFor(x => x.DecisionText).NotNull().WithMessage("A decision is required.");
        RuleFor(x => x.DecisionText!.En).NotEmpty().When(x => x.DecisionText is not null).WithMessage("Decision (EN) is required.");
        RuleFor(x => x.DecisionText!.Ar).NotEmpty().When(x => x.DecisionText is not null).WithMessage("Decision (AR) is required.");
        RuleForEach(x => x.Options).ChildRules(o =>
        {
            o.RuleFor(r => r.Name).NotNull().WithMessage("An option requires a name.");
            o.RuleFor(r => r.Name!.En).NotEmpty().When(r => r.Name is not null).WithMessage("Option name (EN) is required.");
            o.RuleFor(r => r.Name!.Ar).NotEmpty().When(r => r.Name is not null).WithMessage("Option name (AR) is required.");
        });
    }
}

public sealed class UpdateAdrDraftHandler : IRequestHandler<UpdateAdrDraftCommand, AdrSummaryDto>
{
    private readonly IGovernanceDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;

    public UpdateAdrDraftHandler(IGovernanceDbContext db, ICurrentUser user, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _audit = audit;
    }

    public async Task<AdrSummaryDto> Handle(UpdateAdrDraftCommand request, CancellationToken ct)
    {
        // Tracking query so EF loads + replaces the owned Options collection.
        var adr = await _db.Adrs.FirstOrDefaultAsync(a => a.PublicId == request.AdrId, ct)
            ?? throw new KeyNotFoundException("ADR not found.");

        var options = (request.Options ?? Array.Empty<AdrOptionRequest>())
            .Select(o => new AdrOptionInput(o.Name, o.Body, o.IsChosen));
        adr.UpdateDraft(request.Title, request.Context, request.DecisionDrivers, request.DecisionText,
            request.ConsequencesPositive, request.ConsequencesNegative, options);
        await _db.SaveChangesAsync(ct);

        var (sub, _) = CurrentActor.Of(_user);
        await _audit.EmitEnrichedAsync("Governance.AdrDraftUpdated", nameof(Adr), adr.PublicId.ToString(), ct: ct);

        return AdrMapping.ToSummary(adr);
    }
}
