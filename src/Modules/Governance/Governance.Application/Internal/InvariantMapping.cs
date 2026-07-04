using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Domain;
using Acmp.Modules.Governance.Domain.Enums;

namespace Acmp.Modules.Governance.Application.Internal;

// Aggregate → read-model projection, shared by the queries and the command return values. The supersession
// peer keys (SupersededByInvariantKey / SupersedesInvariantKey) are resolved by the caller and passed in —
// they are in-module lookups (governance schema only), not on the aggregate.
internal static class InvariantMapping
{
    public static InvariantSummaryDto ToSummary(Invariant i) => new(
        i.PublicId, i.Key, i.Statement, i.Status.ToString(), i.Category.ToString(), i.Scope.ToString(),
        i.OwnerName, i.ActivatedAt, i.CreatedAt, i.Status == InvariantStatus.Superseded);

    public static InvariantDetailDto ToDetail(Invariant i, string? supersededByKey, string? supersedesKey) => new(
        i.PublicId, i.Key, i.Status.ToString(), i.Category.ToString(), i.Scope.ToString(),
        i.Statement, i.Rationale, i.ExceptionsPolicy,
        i.OwnerUserId, i.OwnerName,
        i.ActivatedAt, i.ActivatedByName,
        i.SupersededByInvariantId, supersededByKey, i.SupersessionReason,
        i.SupersedesInvariantId, supersedesKey,
        i.RetirementReason, i.CreatedAt);
}
