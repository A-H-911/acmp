using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.ReopenTopic;

// FR-045 / AC-112 — reopen a Closed or Rejected topic with a recorded justification; the topic
// re-enters the triage workflow.
//
// ⚠ FR-045 WAS APPROVED AND TRACED (WBS-5.7, TEST-018) SINCE THE ORIGINAL PLAN AND SIMPLY NEVER
// BUILT — Topic.Reopen has sat on the aggregate with no caller, which is how the DW-026 reachability
// check surfaced it as part of DEF-084. This is execution of approved work, not new scope; the AC is
// new because FR-045 had none, and per DW-029 a requirement with no AC can never advance past
// Approved however well it is built.
//
// The justification is mandatory, matching the rejection and deferral rule (FR-044). The aggregate
// enforces it too (RequireReason) — the validator makes it a 400 instead of a 500.
public sealed record ReopenTopicCommand(Guid TopicId, string Justification) : IRequest;

public sealed class ReopenTopicValidator : AbstractValidator<ReopenTopicCommand>
{
    public ReopenTopicValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Justification).NotEmpty().WithMessage("A reopen justification is required.");
    }
}

public sealed class ReopenTopicHandler : IRequestHandler<ReopenTopicCommand>
{
    private readonly ITopicsDbContext _db;
    private readonly IResourceAuthorizer _authz;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public ReopenTopicHandler(ITopicsDbContext db, IResourceAuthorizer authz, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _authz = authz;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(ReopenTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        await _authz.EnsureAsync(topic, Policies.TopicTriage, ct);

        var (sub, name) = CurrentActor.Of(_user);
        topic.Reopen(request.Justification, sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Topics.TopicReopened", nameof(Topic), topic.PublicId.ToString(), ct: ct);
    }
}
