namespace Acmp.Modules.Dependencies.Domain.Enums;

// The nature of the directed dependency edge (From --Kind--> To). BlockedBy/Blocks are the "blocker" kinds
// that drive the register's blocked filter (see IsBlocker in the mapping). Explicit int values start at 1 so
// a default(0) can never silently pass IsInEnum. Serialized on the wire as the string name.
public enum DependencyKind
{
    DependsOn = 1,
    BlockedBy = 2,
    Blocks = 3,
    RelatesTo = 4,
}
