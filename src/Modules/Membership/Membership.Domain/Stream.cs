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

    public static Stream Create(string code, LocalizedString name) =>
        new() { Code = code.Trim().ToLowerInvariant(), Name = name };
}
