namespace Acmp.Modules.Governance.Domain.Enums;

// The Architecture Invariant category (FR-106; docs/domain/standards-and-best-practices.md §A.5 "category: data, integration, security…").
// Values are the OQ-036 default set (Security, Performance, Data, Interoperability, Compliance, Other) —
// OQ-036/OQ-007 remains OPEN: the committee must confirm the enum before it is treated as final (FR-106 lists
// it without Compliance; we take the fuller OQ-036 default). Serialized as the string name (localized in the SPA).
public enum InvariantCategory
{
    Security = 1,
    Performance = 2,
    Data = 3,
    Interoperability = 4,
    Compliance = 5,
    Other = 6,
}
