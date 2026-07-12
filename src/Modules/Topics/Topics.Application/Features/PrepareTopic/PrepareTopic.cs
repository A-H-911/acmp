using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.PrepareTopic;

// W4: mark an accepted topic Prepared (AC-035). ABAC-gated by Policies.TopicEdit — Owner (AiO) or
// Secretary/Chairman; the owner check resolves via the grant created on accept.
public sealed record PrepareTopicCommand(Guid TopicId) : IRequest;

public sealed class PrepareTopicValidator : AbstractValidator<PrepareTopicCommand>
{
    public PrepareTopicValidator() => RuleFor(x => x.TopicId).NotEmpty();
}

public sealed class PrepareTopicHandler : IRequestHandler<PrepareTopicCommand>
{
    private readonly ITopicsDbContext _db;
    private readonly IResourceAuthorizer _authz;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICommitteeDirectory _directory;
    private readonly INotificationChannel _notifications;

    public PrepareTopicHandler(ITopicsDbContext db, IResourceAuthorizer authz, ICurrentUser user, IClock clock,
        IAuditSink audit, ICommitteeDirectory directory, INotificationChannel notifications)
    {
        _db = db;
        _authz = authz;
        _user = user;
        _clock = clock;
        _audit = audit;
        _directory = directory;
        _notifications = notifications;
    }

    public async Task Handle(PrepareTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        await _authz.EnsureAsync(topic, Policies.TopicEdit, ct);

        var (sub, name) = CurrentActor.Of(_user);
        topic.MarkPrepared(sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Topics.TopicPrepared", nameof(Topic), topic.PublicId.ToString(), ct: ct);

        // W4: tell the Secretary roster an item is ready to schedule (skip the actor if a Secretary
        // prepared it themselves — no self-noise, mirrors CreateAction). Empty roster → no recipients.
        var secretaries = await _directory.GetActiveMembersInRoleAsync(AcmpRoles.Secretary, ct);
        var build = TopicNotifications.TopicPrepared(topic.Key);
        foreach (var secretary in secretaries)
            if (!string.Equals(secretary.UserId, sub, StringComparison.Ordinal))
                await _notifications.PublishAsync(build(secretary.UserId), ct);
    }
}
