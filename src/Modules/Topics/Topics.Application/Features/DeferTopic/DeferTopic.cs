using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.DeferTopic;

// W20: defer a topic with a mandatory reason and an optional revisit date. ABAC-gated by
// Policies.TopicTriage.
public sealed record DeferTopicCommand(Guid TopicId, string Reason, DateTimeOffset? RevisitOn) : IRequest;

public sealed class DeferTopicValidator : AbstractValidator<DeferTopicCommand>
{
    public DeferTopicValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A defer reason is required.");
    }
}

public sealed class DeferTopicHandler : IRequestHandler<DeferTopicCommand>
{
    private readonly ITopicsDbContext _db;
    private readonly IResourceAuthorizer _authz;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public DeferTopicHandler(ITopicsDbContext db, IResourceAuthorizer authz, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _authz = authz;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(DeferTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        await _authz.EnsureAsync(topic, Policies.TopicTriage, ct);

        var (sub, name) = CurrentActor.Of(_user);
        topic.Defer(request.Reason, request.RevisitOn, sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Topics.TopicDeferred", sub, new { topic.PublicId, topic.Key, request.Reason }, ct);
    }
}
