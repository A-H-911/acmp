using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.PrioritizeTopic;

// W3: set a topic's backlog priority ordinal (persists drag-and-drop or keyboard reorder, AC-043).
// ABAC-gated by Policies.BacklogPrioritize (Chairman/Secretary).
public sealed record PrioritizeTopicCommand(Guid TopicId, int Priority) : IRequest;

public sealed class PrioritizeTopicValidator : AbstractValidator<PrioritizeTopicCommand>
{
    public PrioritizeTopicValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
    }
}

public sealed class PrioritizeTopicHandler : IRequestHandler<PrioritizeTopicCommand>
{
    private readonly ITopicsDbContext _db;
    private readonly IResourceAuthorizer _authz;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public PrioritizeTopicHandler(ITopicsDbContext db, IResourceAuthorizer authz, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _authz = authz;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(PrioritizeTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        await _authz.EnsureAsync(topic, Policies.BacklogPrioritize, ct);

        topic.SetPriority(request.Priority, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        var (sub, _) = CurrentActor.Of(_user);
        await _audit.EmitEnrichedAsync("Topics.TopicPrioritized", nameof(Topic), topic.PublicId.ToString(), ct: ct);
    }
}
