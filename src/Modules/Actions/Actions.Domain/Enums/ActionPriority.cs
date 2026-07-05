namespace Acmp.Modules.Actions.Domain.Enums;

// Action handling priority (docs/domain/domain-model.md §Action). An attribute, not a lifecycle state.
public enum ActionPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
}
