using Acmp.Modules.Risks.Domain.Enums;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Risks.Domain;

// A planned/implemented response reducing a Risk (docs/domain/domain-model.md §Mitigation). Owned child of the Risk aggregate
// (reached only through it) — the exact shape of DecisionCondition: BaseEntity identity, a bilingual text,
// its own Planned→InProgress→Done status, and an optional LinkedActionId (the Action fulfilling it, a soft
// value ref, no FK — ADR-0001). Mutation is driven by the Risk aggregate so its invariants hold.
public sealed class Mitigation : BaseEntity
{
    private Mitigation() { }

    public LocalizedString Description { get; private set; } = null!;
    public MitigationType Type { get; private set; }
    public MitigationStatus Status { get; private set; }
    public string? OwnerUserId { get; private set; }
    public Guid? LinkedActionId { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }

    internal static Mitigation Create(LocalizedString description, MitigationType type,
        string? ownerUserId, Guid? linkedActionId, DateTimeOffset? dueDate)
    {
        if (description is null) throw new InvalidOperationException("A mitigation description is required.");
        return new Mitigation
        {
            Description = description,
            Type = type,
            Status = MitigationStatus.Planned,
            OwnerUserId = string.IsNullOrWhiteSpace(ownerUserId) ? null : ownerUserId,
            LinkedActionId = linkedActionId,
            DueDate = dueDate,
        };
    }

    // Forward-only (Planned → InProgress → Done); a mitigation never regresses (docs/domain/entity-lifecycles.md §10).
    internal void SetStatus(MitigationStatus status)
    {
        if (status < Status)
            throw new InvalidOperationException($"A mitigation cannot move back from {Status} to {status}.");
        Status = status;
    }

    public bool IsDone => Status == MitigationStatus.Done;
}
