using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.MoveTopicPriority;

// AC-043 / FR-034: the keyboard move-up/down alternative to drag-and-drop for backlog priority. A single ±1
// delta swaps the topic with its neighbour WITHIN its kanban bucket (the visual column). Topic priorities are
// not contiguous — every topic defaults to 0 — so a naive int-swap is a no-op; instead the column is
// materialized, ordered deterministically (Priority, then CreatedAt, then Key — a stable tiebreak GetBacklog
// shares), renumbered 1..N, then swapped. Mirrors Agenda.MoveItem (AC-044). ABAC-gated by BacklogPrioritize.
public sealed record MoveTopicPriorityCommand(Guid TopicId, int Delta) : IRequest;

public sealed class MoveTopicPriorityValidator : AbstractValidator<MoveTopicPriorityCommand>
{
    public MoveTopicPriorityValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Delta).Must(d => d is 1 or -1).WithMessage("Delta must be +1 (down) or -1 (up).");
    }
}

public sealed class MoveTopicPriorityHandler : IRequestHandler<MoveTopicPriorityCommand>
{
    private readonly ITopicsDbContext _db;
    private readonly IResourceAuthorizer _authz;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public MoveTopicPriorityHandler(ITopicsDbContext db, IResourceAuthorizer authz, IClock clock, IAuditSink audit)
    {
        _db = db;
        _authz = authz;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(MoveTopicPriorityCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        await _authz.EnsureAsync(topic, Policies.BacklogPrioritize, ct);

        // Decided/Closed/Converted topics have left the backlog and are immutable — not reorderable (AC-034).
        if (topic.Status is TopicStatus.Decided or TopicStatus.Closed or TopicStatus.Converted)
            throw new InvalidOperationException("A decided topic cannot be reordered.");

        var bucket = TopicBuckets.BucketOf(topic.Status);
        var column = (await _db.Topics.ToListAsync(ct))
            .Where(t => TopicBuckets.BucketOf(t.Status) == bucket)
            .OrderBy(t => t.Priority).ThenBy(t => t.CreatedAt).ThenBy(t => t.Key)
            .ToList();

        var index = column.FindIndex(t => t.PublicId == topic.PublicId);
        var target = index + request.Delta;
        if (target < 0 || target >= column.Count)
            return;   // already at the top/bottom of its column — nothing to do

        (column[index], column[target]) = (column[target], column[index]);
        var now = _clock.UtcNow;
        for (var i = 0; i < column.Count; i++)
            column[i].SetPriority(i + 1, now);   // contiguous 1..N in the new order (persistence matches display)

        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Topics.TopicReordered", nameof(Topic), topic.PublicId.ToString(), ct: ct);
    }
}
