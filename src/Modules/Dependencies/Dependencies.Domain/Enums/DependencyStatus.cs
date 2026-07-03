namespace Acmp.Modules.Dependencies.Domain.Enums;

// Lifecycle state of a dependency edge. Open is the live state; Resolved records that the dependency was
// satisfied; Removed is the soft-delete/retract state (there is no separate IsActive flag — Status carries
// it). Explicit int values start at 1 so a default(0) can never silently pass IsInEnum.
public enum DependencyStatus
{
    Open = 1,
    Resolved = 2,
    Removed = 3,
}
