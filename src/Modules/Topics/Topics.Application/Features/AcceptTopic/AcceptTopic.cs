using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Authorization.Abac;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.AcceptTopic;

// W2: accept a submitted topic into the backlog and assign an Owner. ABAC-gated by Policies.TopicTriage
// (Chairman/Secretary). On accept, the owner's per-topic Owner capability is granted in Membership
// ("grant-on-accept") so the ABAC owner check resolves for later edits (AC-009).
public sealed record AcceptTopicCommand(Guid TopicId, Guid OwnerId, string OwnerName) : IRequest;

public sealed class AcceptTopicValidator : AbstractValidator<AcceptTopicCommand>
{
    public AcceptTopicValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.OwnerId).NotEmpty().WithMessage("An owner must be assigned on accept.");
    }
}

public sealed class AcceptTopicHandler : IRequestHandler<AcceptTopicCommand>
{
    private readonly ITopicsDbContext _db;
    private readonly IResourceAuthorizer _authz;
    private readonly ITopicCapabilityWriter _capabilities;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public AcceptTopicHandler(ITopicsDbContext db, IResourceAuthorizer authz, ITopicCapabilityWriter capabilities,
        ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _authz = authz;
        _capabilities = capabilities;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(AcceptTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        await _authz.EnsureAsync(topic, Policies.TopicTriage, ct);

        var now = _clock.UtcNow;
        var (sub, name) = CurrentActor.Of(_user);
        if (topic.Status == Domain.Enums.TopicStatus.Submitted)
            topic.BeginTriage(sub, name, now);
        topic.Accept(request.OwnerId, request.OwnerName, sub, name, now);
        await _db.SaveChangesAsync(ct);

        await _capabilities.GrantAsync(topic.PublicId, request.OwnerId, TopicCapabilityType.Owner, ct);
        await _audit.EmitAsync("Topics.TopicAccepted", sub, new { topic.PublicId, topic.Key, request.OwnerId }, ct);
    }
}
