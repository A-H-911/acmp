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

// AC-043 / FR-034 (keyboard move up/down) AND AC-141 / FR-037 (drag-and-drop): reorder a topic WITHIN its
// kanban bucket (the visual column) by a signed position delta. Topic priorities are not contiguous — every
// topic defaults to 0 — so a naive int-swap is a no-op; instead the column is materialized, ordered
// deterministically (Priority, then CreatedAt, then Key — a stable tiebreak GetBacklog shares), the topic is
// MOVED to its target index, and the column is renumbered 1..N. Mirrors Agenda.MoveItem (AC-044).
// ABAC-gated by BacklogPrioritize.
//
// ⚠ THE DELTA WAS ±1-ONLY AND THE OPERATION WAS A SWAP UNTIL DW-040. That pair is why drag-and-drop could
// not be expressed at all: dragging a card from index 5 to index 0 is a MOVE — 1..4 each shift down one —
// whereas a swap would exchange 5 and 0 and leave 1..4 sitting where they were, which is not what dragging
// means to anyone. Widening the delta without also replacing the swap would have shipped exactly that
// surprise, silently, because for |delta| = 1 the two operations are INDISTINGUISHABLE.
//
// That indistinguishability is what makes this change safe rather than merely small: for a ±1 delta,
// remove-then-insert produces a byte-identical column to the old swap, so AC-043's keyboard path and its
// tests are untouched by construction. MoveTopicPriorityMoveSemanticsTests proves both halves — that ±1
// agrees with a swap, and that |delta| > 1 deliberately does not.
// TWO WAYS TO SAY WHERE, AND THE SECOND EXISTS BECAUSE THE FIRST IS UNSOUND FOR DRAG.
//   Delta          — a signed offset from the topic's CURRENT position. The keyboard path (AC-043).
//   TargetTopicId  — "put this topic where THAT one is". The drag path (AC-141).
//
// ⚠⚠ WHY DRAG CANNOT USE Delta, found while building DW-040 and NOT anticipated by that row, which
// described the work as "only the gesture is missing". A delta is a POSITION ARITHMETIC, and the client
// cannot do that arithmetic correctly: the kanban renders `data.items`, which is the FILTERED, SORTED and
// PAGE-TRUNCATED backlog result. All three break the mapping between what the user sees and the canonical
// column this handler orders by (Priority, CreatedAt, Key) —
//   · the filter bar is rendered in kanban view, so hidden cards sit between visible ones;
//   · the kanban hides the pager but still sends pageSize, so it shows a PREFIX of the column;
//   · the sort chosen in table view persists into kanban, so the visual order may not be priority at all.
// A client that computes "3 positions up" from its own list therefore asks the server to move the topic 3
// places in a DIFFERENT sequence. Sending the target's IDENTITY instead is immune to all three, because the
// server resolves both endpoints inside the canonical column it already materializes.
public sealed record MoveTopicPriorityCommand(Guid TopicId, int Delta, Guid? TargetTopicId = null) : IRequest;

public sealed class MoveTopicPriorityValidator : AbstractValidator<MoveTopicPriorityCommand>
{
    public MoveTopicPriorityValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();

        // EXACTLY ONE addressing mode. Accepting both would leave the handler to pick a winner silently,
        // and accepting neither is a no-op request the caller should be told about rather than have ignored.
        RuleFor(x => x).Must(c => (c.Delta != 0) ^ (c.TargetTopicId is not null))
            .WithMessage("Specify exactly one of: a non-zero Delta, or a TargetTopicId to move before.");

        RuleFor(x => x.TargetTopicId).NotEqual(x => x.TopicId)
            .When(x => x.TargetTopicId is not null)
            .WithMessage("A topic cannot be moved to its own position.");
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

        // C-AUTHZ-04 / FR-163: this full-table load is deliberately NOT visibility-filtered, and the
        // reason is the gate above it. Policies.BacklogPrioritize allows Chairman and Secretary ONLY,
        // and both are committee-wide readers (DEC-063 d1) — so nobody who reaches this line could be
        // shielded from anything in the column. Filtering here would be worse than useless: it would
        // renumber the column as if the hidden topics were absent, corrupting the order for the very
        // people entitled to see them. If BacklogPrioritize is ever widened, this comment is wrong and
        // the filter becomes necessary.
        var bucket = TopicBuckets.BucketOf(topic.Status);
        var column = (await _db.Topics.ToListAsync(ct))
            .Where(t => TopicBuckets.BucketOf(t.Status) == bucket)
            .OrderBy(t => t.Priority).ThenBy(t => t.CreatedAt).ThenBy(t => t.Key)
            .ToList();

        var index = column.FindIndex(t => t.PublicId == topic.PublicId);

        // Resolve the destination INSIDE the canonical column, never from a client-supplied position.
        int target;
        if (request.TargetTopicId is { } targetId)
        {
            target = column.FindIndex(t => t.PublicId == targetId);
            if (target < 0)
                return;   // target is not in this topic's column (different bucket, or gone) — refuse silently
        }
        else
        {
            target = index + request.Delta;
        }

        if (target < 0 || target >= column.Count)
            return;   // already at the top/bottom of its column — nothing to do

        // MOVE, not swap: lift the topic out and re-insert it at the target index, so every topic between
        // the two positions shifts by one. For |delta| = 1 this is identical to the swap it replaced (the
        // element between them is the target itself), which is why AC-043 is unaffected.
        var moved = column[index];
        column.RemoveAt(index);
        column.Insert(target, moved);

        var now = _clock.UtcNow;
        for (var i = 0; i < column.Count; i++)
            column[i].SetPriority(i + 1, now);   // contiguous 1..N in the new order (persistence matches display)

        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Topics.TopicReordered", nameof(Topic), topic.PublicId.ToString(), ct: ct);
    }
}
