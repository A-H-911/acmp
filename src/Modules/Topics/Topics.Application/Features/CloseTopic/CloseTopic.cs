using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.CloseTopic;

// FR-160 / AC-109 — close a topic once its decision has been issued, so concluded work leaves the
// active backlog.
//
// THE GAP THIS FILLS: Topic.Decide IS called (TopicDecisionRecorder), so topics reach Decided — and
// before this, nothing called Topic.Close, so Decided was a permanent resting state and the
// committee's open list grew without bound. Found by the DW-026 reachability check as part of
// DEF-084.
//
// Policies.TopicTriage rather than a new policy: this is a Secretary action on a topic, the same
// authority that accepts, rejects and defers one. A new IAuthorizationRequirement would have to be
// registered into a policy to have any effect at all — an unregistered one FAILS OPEN, which is
// what DEF-057 was.
public sealed record CloseTopicCommand(Guid TopicId) : IRequest;

public sealed class CloseTopicValidator : AbstractValidator<CloseTopicCommand>
{
    public CloseTopicValidator() => RuleFor(x => x.TopicId).NotEmpty();
}

public sealed class CloseTopicHandler : IRequestHandler<CloseTopicCommand>
{
    private readonly ITopicsDbContext _db;
    private readonly IResourceAuthorizer _authz;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public CloseTopicHandler(ITopicsDbContext db, IResourceAuthorizer authz, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _authz = authz;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(CloseTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        await _authz.EnsureAsync(topic, Policies.TopicTriage, ct);

        // The status guard lives in the aggregate (RequireStatus(Decided)) and is asserted by
        // AC-109's second clause rather than trusted — a transition guard nothing exercises is
        // indistinguishable from an absent one.
        var (sub, name) = CurrentActor.Of(_user);
        topic.Close(sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Topics.TopicClosed", nameof(Topic), topic.PublicId.ToString(), ct: ct);
    }
}
