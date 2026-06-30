using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Decisions.Domain;

// A single condition attached to a ConditionallyApproved decision (docs/11 §Decisions). Owned by the
// Decision root, mutated only through it. Bilingual text (guardrail 9). LinkedActionId is a forward
// reference to the Action that discharges the condition (P8) — a plain value, never a cross-module FK.
public sealed class DecisionCondition : BaseEntity
{
    private DecisionCondition() { }

    public LocalizedString Text { get; private set; } = null!;
    public DecisionConditionStatus Status { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public Guid? LinkedActionId { get; private set; }   // Action.PublicId (P8) — value reference, no FK

    internal DecisionCondition(LocalizedString text, DateTimeOffset? dueDate)
    {
        Text = text ?? throw new InvalidOperationException("A condition requires bilingual text.");
        DueDate = dueDate;
        Status = DecisionConditionStatus.Open;
    }
}
