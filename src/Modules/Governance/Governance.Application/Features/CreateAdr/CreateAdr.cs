using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Modules.Governance.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Governance.Application.Features.CreateAdr;

// W17: author a new ADR in Draft (the manual path; promotion from a Decision is the P11e slice, which sets
// SourceDecisionId). RBAC = Adr.Create (Chairman/Secretary; Member/Reviewer allow-if-owner). Content is
// entered in one UI language and MIRRORED to both LocalizedString columns (the locked FTS pattern), so both
// EN+AR are required. Title + Context + Decision are the required MADR-lite sections; drivers/consequences/
// options are optional at draft time.
public sealed record AdrOptionRequest(LocalizedString Name, LocalizedString? Body, bool IsChosen);

public sealed record CreateAdrCommand(
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

public sealed class CreateAdrValidator : AbstractValidator<CreateAdrCommand>
{
    public CreateAdrValidator()
    {
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

public sealed class CreateAdrHandler : IRequestHandler<CreateAdrCommand, AdrSummaryDto>
{
    private readonly IGovernanceDbContext _db;
    private readonly IAdrKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public CreateAdrHandler(IGovernanceDbContext db, IAdrKeyGenerator keys, ICurrentUser user,
        IClock clock, IAuditSink audit)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<AdrSummaryDto> Handle(CreateAdrCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, name) = CurrentActor.Of(_user);

        var key = await _keys.NextAdrKeyAsync(now.Year, ct);
        var options = (request.Options ?? Array.Empty<AdrOptionRequest>())
            .Select(o => new AdrOptionInput(o.Name, o.Body, o.IsChosen));

        var adr = Adr.Draft(key, request.Title, request.Context, request.DecisionDrivers, request.DecisionText,
            request.ConsequencesPositive, request.ConsequencesNegative, options, sub, name, sourceDecisionId: null, now);

        _db.Adrs.Add(adr);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Governance.AdrDrafted", nameof(Adr), adr.PublicId.ToString(), ct: ct);

        return AdrMapping.ToSummary(adr);
    }
}
