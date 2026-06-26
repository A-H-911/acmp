using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
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

    public RejectTopicHandler(ITopicsDbContext db, IResourceAuthorizer authz, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _authz = authz;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(RejectTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        await _authz.EnsureAsync(topic, Policies.TopicTriage, ct);

        var (sub, name) = CurrentActor.Of(_user);
        topic.Reject(request.Reason, sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Topics.TopicRejected", sub, new { topic.PublicId, topic.Key, request.Reason }, ct);
    }
}
