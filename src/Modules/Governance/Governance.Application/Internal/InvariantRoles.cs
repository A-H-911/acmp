using Acmp.Shared.Authorization;

namespace Acmp.Modules.Governance.Application.Internal;

// The MediatR-boundary role re-check (defence in depth, guardrail 4) for the Invariant commands, matching the
// docs/domain/permission-role-matrix.md policy cells (rows 21/22) enforced at the endpoints. Author = Invariant.Create (full Chairman/
// Secretary + allow-if-owner Member/Reviewer); Approver = Invariant.Approve (Chairman/Secretary). The ABAC
// allow-if-owner nuance is resolved by the endpoint policy; the flat list here is the coarse backstop.
// (Same role sets as AdrRoles — kept separate so each aggregate's authz reads on its own.)
internal static class InvariantRoles
{
    public static readonly string[] Author =
        { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Member, AcmpRoles.Reviewer };

    public static readonly string[] Approver =
        { AcmpRoles.Chairman, AcmpRoles.Secretary };
}
