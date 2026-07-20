using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.RejectTopic;

// W20: reject a submitted/triaged topic with a mandatory rationale (AC-031). The rejection becomes an
// immutable history record (AC-032/033). ABAC-gated by Policies.TopicTriage.
public sealed record RejectTopicCommand(Guid TopicId, string Reason) : IRequest;

public sealed class RejectTopicValidator : AbstractValidator<RejectTopicCommand>
{
    public RejectTopicValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Rejection reason is required.");
    }
}

public sealed class RejectTopicHandler : IRequestHandler<RejectTopicCommand>
{
    private readonly ITopicsDbContext _db;
    private readonly IResourceAuthorizer _authz;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly INotificationChannel _notifications;

    public RejectTopicHandler(ITopicsDbContext db, IResourceAuthorizer authz, ICurrentUser user, IClock clock,
        IAuditSink audit, INotificationChannel notifications)
    {
        _db = db;
        _authz = authz;
        _user = user;
        _clock = clock;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task Handle(RejectTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        await _authz.EnsureAsync(topic, Policies.TopicTriage, ct);

        var (sub, name) = CurrentActor.Of(_user);
        topic.Reject(request.Reason, sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Topics.TopicRejected", nameof(Topic), topic.PublicId.ToString(), ct: ct);

        // AC-032: notify the submitter their topic was rejected (skip if they rejected it themselves — no self-noise).
        if (!string.Equals(topic.SubmittedBySub, sub, StringComparison.Ordinal))
            await _notifications.PublishAsync(TopicNotifications.TopicRejected(topic.Key, request.Reason)(topic.SubmittedBySub), ct);
    }
}
