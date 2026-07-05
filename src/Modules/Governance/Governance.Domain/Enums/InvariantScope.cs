namespace Acmp.Modules.Governance.Domain.Enums;

// The reach of an Architecture Invariant (FR-106: single-stream / multi-stream / platform / org-wide;
// docs/domain/standards-and-best-practices.md §A.5 "scope: platform-wide, module-specific, service-specific"). An enum, not a link to a specific
// stream — the invariant states a class of scope, it does not target one committee stream. Serialized as the
// string name (localized in the SPA).
public enum InvariantScope
{
    SingleStream = 1,
    MultiStream = 2,
    Platform = 3,
    OrgWide = 4,
}
