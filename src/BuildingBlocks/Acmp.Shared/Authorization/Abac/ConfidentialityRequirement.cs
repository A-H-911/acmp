using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Acmp.Shared.Authorization.Abac;

// C-AUTHZ-04 (SEC-304) / FR-163, reader set settled by DEC-063 d1.
//
// ⚠ THIS IS HALF OF THE CONTROL, NOT THE WHOLE OF IT. An IAuthorizationRequirement is only ever
// evaluated where IAuthorizationService is called, and NO topic READ path calls it — reads are a bare
// RequireAuthorization() group plus an empty AllowedRoles, which GuestSurfaceMiddleware's own header
// records as the reason it had to exist. So this requirement narrows the paths that DO authorize
// against a resource, and list/detail/search exclusion is enforced separately by a query predicate.
// Anyone changing one must change the other; they are one control expressed twice.
public sealed class ConfidentialityRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Narrows access to a Restricted artifact to the committee-wide readers (Chairman, Secretary,
/// Auditor), plus holders of an explicit per-topic capability grant — which includes the Owner,
/// because grant-on-accept records ownership as <see cref="TopicCapabilityType.Owner"/>.
/// </summary>
/// <remarks>
/// ⚠ CONFIDENTIALITY NARROWS, NEVER WIDENS (SEC-304). An unrestricted resource succeeds immediately
/// and the principal's other requirements still decide the outcome — this handler must never be the
/// reason something otherwise refused becomes allowed.
/// <para>
/// ⚠ DEF-068: this is a two-parameter handler, so ASP.NET never invokes it when the resource is absent
/// or is not an <see cref="IConfidentialResource"/> — the requirement then goes unsatisfied and the
/// policy refuses EVERYONE, the Chairman included. A policy may therefore join
/// <c>AuthorizationRegistration.ConfidentialityScoped</c> only if EVERY call site passes a
/// confidential aggregate. That is why <c>Policies.TopicTriage</c> is NOT in that set: it is applied
/// at endpoint level on /close and /reopen, where there is no resource at all.
/// </para>
/// </remarks>
public sealed class ConfidentialityHandler : AuthorizationHandler<ConfidentialityRequirement, IConfidentialResource>
{
    // ⚠ STATED POSITIVELY, for the reason DEF-060 records about the stream-bounded list: the
    // complement of a "restricted-from" list is an inference, and a role added later would fall into
    // whichever side the author never thought about. These three are the committee-wide READERS
    // C-AUTHZ-04 names, read through DEC-063 d1 — "Chair/Coord" is Chairman + Secretary, because no
    // Coordinator role exists, and Auditor the control names separately.
    //
    // ⚠ AUDITOR IS A ROLE BYPASS, NOT A CAPABILITY, AND IT HAS TO BE. Auditor appears in exactly two
    // policy rows (AuditRead, ReportExport) and holds no per-topic capability at all, so there is no
    // grant for it to arrive through — naming the role here is the only hook that exists.
    // ⚠ Administrator is deliberately ABSENT: it is not in the written control, and widening the
    // reader set beyond what is written is the one direction this control must never drift.
    private static readonly string[] CommitteeWideReaders =
        { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Auditor };

    private readonly ITopicCapabilityResolver _capabilities;

    public ConfidentialityHandler(ITopicCapabilityResolver capabilities) => _capabilities = capabilities;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ConfidentialityRequirement requirement, IConfidentialResource resource)
    {
        // Not classified: this control has nothing to say, and saying nothing means "let the other
        // requirements decide" rather than "allow".
        if (!resource.IsRestricted)
        {
            context.Succeed(requirement);
            return;
        }

        if (CommitteeWideReaders.Any(context.User.IsInRole))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? context.User.FindFirst("sub")?.Value;
        if (userId is null)
            return;

        // The grantee leg. A resource that carries no topic identity cannot have grants resolved
        // against it, so it stays refused — fail closed, because the alternative on a control whose
        // whole job is to withhold would be to leak on a shape nobody anticipated.
        if (resource is not ITopicScopedResource scoped)
            return;

        var capabilities = await _capabilities.GetCapabilitiesAsync(userId, scoped.TopicId);
        if (capabilities.Count > 0)
            context.Succeed(requirement);
    }
}
