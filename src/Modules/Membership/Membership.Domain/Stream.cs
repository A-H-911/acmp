using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Membership.Domain;

// A delivery/architecture stream that topics are scoped to (BL-024). Code is the stable lowercase
// scope key used in ABAC stream checks (docs/domain/permission-role-matrix.md §E.1); Name is bilingual for display (guardrail 9).
public sealed class Stream : AuditableEntity
{
    private Stream() { }

    public string Code { get; private set; } = string.Empty;
    public LocalizedString Name { get; private set; } = null!;

    // ADR-0042 (3): the "unrestricted" marker. A member assigned this stream is not stream-bounded.
    // It is a COLUMN and never a magic code — matching on Code would break silently when the row is
    // renamed or re-coded, or hand universal access to a future stream that collided with the string.
    // Set only by the Membership_StreamTaxonomy_ADR0042 seed: the wildcard is a seeded singleton (a
    // filtered unique index enforces "at most one"), so there is deliberately no runtime factory for
    // it. ⚠ Nothing READS this yet — the reader is UserStreamProvider, in ADR-0042 step 7 (DW-026).
    public bool IsWildcard { get; private set; }

    public static Stream Create(string code, LocalizedString name) =>
        new() { Code = code.Trim().ToLowerInvariant(), Name = name };
}
