using Acmp.Modules.Risks.Application.Abstractions;
using Acmp.Modules.Risks.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Risks.Application.Features.AcceptRisk;

// W15 (accept): a governing authority consciously accepts a risk rather than mitigating it (Open/Mitigating
// → Accepted, terminal). This is NARROWER than the other transitions (docs/domain/entity-lifecycles.md §10 line 217): Chairman or
// Secretary only, NO allow-if-owner — enforced by the dedicated Risk.Accept policy at the endpoint and
// re-checked here. The rationale + accepting authority are recorded on the aggregate and the act is
// high-importance audited (guardrail 5).
public sealed record AcceptRiskCommand(Guid RiskId, LocalizedString Rationale, string Authority)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class AcceptRiskValidator : AbstractValidator<AcceptRiskCommand>
{
    public AcceptRiskValidator()
    {
        RuleFor(x => x.Rationale).NotNull().WithMessage("An acceptance rationale is required.");
        RuleFor(x => x.Rationale!.En).NotEmpty().When(x => x.Rationale is not null).WithMessage("Acceptance rationale (EN) is required.");
        RuleFor(x => x.Rationale!.Ar).NotEmpty().When(x => x.Rationale is not null).WithMessage("Acceptance rationale (AR) is required.");
        RuleFor(x => x.Authority).NotEmpty().WithMessage("An accepting authority is required.");
    }
}

public sealed class AcceptRiskHandler : IRequestHandler<AcceptRiskCommand>
{
    private readonly IRisksDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public AcceptRiskHandler(IRisksDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public async Task Handle(AcceptRiskCommand request, CancellationToken ct)
    {
        var risk = await _db.Risks.FirstOrDefaultAsync(r => r.PublicId == request.RiskId, ct)
            ?? throw new KeyNotFoundException("Risk not found.");

        var (sub, _) = CurrentActor.Of(_user);
        risk.Accept(request.Rationale, request.Authority, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Risks.RiskAccepted", sub,
            new { risk.PublicId, risk.Key, Authority = risk.AcceptingAuthority }, ct);
    }
}
