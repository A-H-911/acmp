using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Membership.Domain;

// A bounded-window delegation of a capability (policy) from one member to another (docs/10 §E.3).
// Delegations are first-class, audited, and auto-expire; they cannot transfer a capability the
// delegator lacks, and never bypass immutability (none exists to bypass).
public sealed class Delegation : AuditableEntity
{
    private Delegation() { }

    public long DelegatorMemberId { get; private set; }
    public long DelegateMemberId { get; private set; }
    public string Capability { get; private set; } = string.Empty;
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset ValidTo { get; private set; }

    public static Delegation Create(long delegatorId, long delegateId, string capability, DateTimeOffset from, DateTimeOffset to) =>
        new()
        {
            DelegatorMemberId = delegatorId,
            DelegateMemberId = delegateId,
            Capability = capability,
            ValidFrom = from,
            ValidTo = to,
        };

    public bool IsActiveAt(DateTimeOffset now) => ValidFrom <= now && now <= ValidTo;
}
