using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Contracts.Topics;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.IssueDecision;

// W12 (issue): the Chairman issues a drafted decision (Draft → Issued). RBAC = Decision.ChairApprove
// (Chairman only). On success: mark the topic Decided via the cross-module seam, fan out a DecisionIssued
// notification to the committee, and audit (payload carries the override flag, AC-016/SoD-3). A chair
// override (issuing against the vote) must carry a justification — enforced by the validator (400) and
// the domain guard (defence in depth). The SoD-3 co-attestation GATE itself is vote-coupled → P9.
public sealed record IssueDecisionCommand(
    Guid DecisionId,
    bool ChairOverride,
    LocalizedString? OverrideJustification) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman };
}

public sealed class IssueDecisionValidator : AbstractValidator<IssueDecisionCommand>
{
    public IssueDecisionValidator()
    {
        RuleFor(x => x.DecisionId).NotEmpty();

        // Override justification required (and bilingual) when overriding — clean 400, not the domain 500.
        RuleFor(x => x.OverrideJustification).NotNull().When(x => x.ChairOverride)
            .WithMessage("A chair override requires a justification.");
        RuleFor(x => x.OverrideJustification!.En).NotEmpty()
            .When(x => x.ChairOverride && x.OverrideJustification is not null)
            .WithMessage("Override justification (EN) is required.");
        RuleFor(x => x.OverrideJustification!.Ar).NotEmpty()
            .When(x => x.ChairOverride && x.OverrideJustification is not null)
            .WithMessage("Override justification (AR) is required.");
    }
}

public sealed class IssueDecisionHandler : IRequestHandler<IssueDecisionCommand>
{
    private readonly IDecisionsDbContext _db;
    private readonly ITopicDecisionRecorder _topics;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICommitteeDirectory _directory;
    private readonly INotificationChannel _notifications;

    public IssueDecisionHandler(IDecisionsDbContext db, ITopicDecisionRecorder topics,
        ICurrentUser user, IClock clock, IAuditSink audit,
        ICommitteeDirectory directory, INotificationChannel notifications)
    {
        _db = db;
        _topics = topics;
        _user = user;
        _clock = clock;
        _audit = audit;
        _directory = directory;
        _notifications = notifications;
    }

    public async Task Handle(IssueDecisionCommand request, CancellationToken ct)
    {
        var decision = await _db.Decisions.FirstOrDefaultAsync(d => d.PublicId == request.DecisionId, ct)
            ?? throw new KeyNotFoundException("Decision not found.");

        var (sub, name) = CurrentActor.Of(_user);
        decision.Issue(sub, name, request.ChairOverride, request.OverrideJustification, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await DecisionIssuance.ApplyAsync(_topics, _directory, _notifications, _audit,
            sub, decision.TopicId, decision.Key, request.ChairOverride, ct);
    }
}
