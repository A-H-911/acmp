namespace Acmp.Modules.Actions.Domain.Enums;

// Action handling priority (docs/11 §Action). An attribute, not a lifecycle state.
public enum ActionPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
}
