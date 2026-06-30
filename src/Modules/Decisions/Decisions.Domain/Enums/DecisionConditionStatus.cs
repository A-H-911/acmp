namespace Acmp.Modules.Decisions.Domain.Enums;

// Status of a single condition attached to a ConditionallyApproved decision. Open → Met (satisfied) or
// Waived (chair released it). The linked Action that discharges a condition lands in P8 (LinkedActionId).
public enum DecisionConditionStatus
{
    Open = 0,
    Met = 1,
    Waived = 2,
}
