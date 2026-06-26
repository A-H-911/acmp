using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
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

    public PrepareTopicHandler(ITopicsDbContext db, IResourceAuthorizer authz, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _authz = authz;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(PrepareTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        await _authz.EnsureAsync(topic, Policies.TopicEdit, ct);

        var (sub, name) = CurrentActor.Of(_user);
        topic.MarkPrepared(sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Topics.TopicPrepared", sub, new { topic.PublicId, topic.Key }, ct);
    }
}
