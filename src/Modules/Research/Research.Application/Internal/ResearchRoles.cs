using Acmp.Shared.Authorization;

namespace Acmp.Modules.Research.Application.Internal;

// The MediatR-boundary role re-check (defence in depth, guardrail 4) for the Research commands, matching the
// docs/domain/permission-role-matrix.md Research.Manage cell (#26): Chairman/Secretary full-allow + Member/
// Reviewer allow-if-owner. The ABAC allow-if-owner nuance is resolved by the endpoint ResearchManage policy;
// the flat list here is the coarse backstop. (A ResearchMission is not topic-scoped, so — exactly like ADRs —
// the AiO has no ownership relationship to resolve at the endpoint and Member/Reviewer are denied there; this
// coarse backstop still lists them because it is the last-line role gate, not the ownership gate.)
internal static class ResearchRoles
{
    public static readonly string[] Manage =
        { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Member, AcmpRoles.Reviewer };
}
