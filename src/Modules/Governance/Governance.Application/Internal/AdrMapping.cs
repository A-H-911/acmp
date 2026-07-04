using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Domain;
using Acmp.Modules.Governance.Domain.Enums;

namespace Acmp.Modules.Governance.Application.Internal;

// Aggregate → read-model projection, shared by the queries and the command return values. The supersession
// peer keys (SupersededByAdrKey / SupersedesAdrKey) are resolved by the caller and passed in — they are
// in-module lookups (governance schema only), not on the aggregate.
internal static class AdrMapping
{
    public static AdrSummaryDto ToSummary(Adr a) => new(
        a.PublicId, a.Key, a.Title, a.Status.ToString(), a.AuthorName, a.ApprovedAt, a.CreatedAt,
        a.Status == AdrStatus.Superseded);

    public static AdrDetailDto ToDetail(Adr a, string? supersededByKey, string? supersedesKey) => new(
        a.PublicId, a.Key, a.Title, a.Status.ToString(),
        a.Context, a.DecisionDrivers, a.DecisionText, a.ConsequencesPositive, a.ConsequencesNegative,
        a.Options.Select(ToOption).ToList(),
        a.AuthorUserId, a.AuthorName, a.SourceDecisionId,
        a.ApprovedAt, a.ApprovedByName,
        a.SupersededByAdrId, supersededByKey, a.SupersessionReason,
        a.SupersedesAdrId, supersedesKey,
        a.DeprecationReason, a.CreatedAt);

    private static AdrOptionDto ToOption(AdrOption o) => new(o.PublicId, o.Name, o.Body, o.IsChosen);
}
