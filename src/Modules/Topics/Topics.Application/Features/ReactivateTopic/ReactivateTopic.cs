using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.ReactivateTopic;

// FR-161 / AC-110 — return a deferred topic to triage, so deferral is a pause rather than a
// permanent exit.
//
// ⚠ THE VISIBLE HALF IS WHY THIS MATTERS. Topic.Defer IS called (DeferTopic) and writes RevisitOn,
// and GetTopicDetail projects RevisitOn into the detail DTO — so before this the product DISPLAYED a
// revisit date and offered no way to act on it, while Topic.Reactivate sat on the aggregate with no
// caller. That is DW-015's exact shape: a transition built backend-only left the user-facing loop
// broken with every backend test passing. AC-110 therefore carries a UI clause, and it is not
// satisfiable by this handler alone.
public sealed record ReactivateTopicCommand(Guid TopicId) : IRequest;

public sealed class ReactivateTopicValidator : AbstractValidator<ReactivateTopicCommand>
{
    public ReactivateTopicValidator() => RuleFor(x => x.TopicId).NotEmpty();
}

public sealed class ReactivateTopicHandler : IRequestHandler<ReactivateTopicCommand>
{
    private readonly ITopicsDbContext _db;
    private readonly IResourceAuthorizer _authz;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public ReactivateTopicHandler(ITopicsDbContext db, IResourceAuthorizer authz, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _authz = authz;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(ReactivateTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        await _authz.EnsureAsync(topic, Policies.TopicTriage, ct);

        // RevisitOn is deliberately LEFT AS IT WAS. It is the record of what the committee agreed
        // when they deferred, and the topic's history is immutable (FR-044) — clearing it here would
        // erase that agreement on the way back in, and nothing needs it cleared: the status is what
        // decides whether the topic is deferred.
        var (sub, name) = CurrentActor.Of(_user);
        topic.Reactivate(sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Topics.TopicReactivated", nameof(Topic), topic.PublicId.ToString(), ct: ct);
    }
}
