using Acmp.Modules.Risks.Application.Contracts;
using Acmp.Modules.Risks.Domain;

namespace Acmp.Modules.Risks.Application.Internal;

// Aggregate → read-model projection, shared by the queries and the command return values. Severity + the
// Exposure band are derived here (RiskExposureScale), never stored columns.
internal static class RiskMapping
{
    public static RiskSummaryDto ToSummary(Risk r) => new(
        r.PublicId, r.Key, r.Title, r.Status.ToString(), r.Likelihood.ToString(), r.Impact.ToString(),
        r.Severity(), r.Exposure().ToString(), r.OwnerUserId, r.OwnerName,
        r.SubjectType.ToString(), r.SubjectId, r.SubjectKey);

    public static RiskDetailDto ToDetail(Risk r) => new(
        r.PublicId, r.Key, r.Title, r.Description, r.Status.ToString(), r.Likelihood.ToString(), r.Impact.ToString(),
        r.Severity(), r.Exposure().ToString(), r.OwnerUserId, r.OwnerName,
        r.SubjectType.ToString(), r.SubjectId, r.SubjectKey,
        r.Mitigations.Select(ToMitigation).ToList(),
        r.ClosureNote, r.AcceptanceRationale, r.AcceptingAuthority, r.EscalationReason, r.EscalationTarget,
        r.ClosedAt, r.CreatedAt);

    private static MitigationDto ToMitigation(Mitigation m) => new(
        m.PublicId, m.Description, m.Type.ToString(), m.Status.ToString(), m.OwnerUserId, m.LinkedActionId, m.DueDate);
}
