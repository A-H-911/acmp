using Acmp.Shared.Authorization;

namespace Acmp.Modules.Governance.Application.Internal;

// The MediatR-boundary role re-check (defence in depth, guardrail 4) for the ADR commands, matching the
// docs/domain/permission-role-matrix.md policy cells enforced at the endpoints. Author = Adr.Create (full Chairman/Secretary + allow-if-
// owner Member/Reviewer); Approver = Adr.Approve / Adr.Supersede (Chairman/Secretary). The ABAC allow-if-
// owner nuance is resolved by the endpoint policy; the flat list here is the coarse backstop.
internal static class AdrRoles
{
    public static readonly string[] Author =
        { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Member, AcmpRoles.Reviewer };

    public static readonly string[] Approver =
        { AcmpRoles.Chairman, AcmpRoles.Secretary };
}
