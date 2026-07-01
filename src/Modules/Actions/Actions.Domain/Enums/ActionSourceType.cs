namespace Acmp.Modules.Actions.Domain.Enums;

// The kind of governance artifact an action was raised from (W13, docs/11). A soft reference: the
// (SourceType, SourceId) pair points at another module's aggregate by PublicId, never an EF FK (ADR-0001).
// Risk lands in P10; the value is defined now so the enum contract is stable.
public enum ActionSourceType
{
    Topic = 0,
    Meeting = 1,
    Decision = 2,
    Condition = 3,
    Risk = 4,
}
