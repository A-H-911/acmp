using System.Security.Claims;
using Acmp.Shared.Authorization.Abac;
using Microsoft.AspNetCore.Authorization;

namespace Acmp.Shared.Authorization;

// Evaluates a CapabilityRequirement: RBAC grant (A) ⟶ Allow-if-owner relationship (AiO) ⟶
// delegation widening (docs/10 §E.3). Deny-by-default: the requirement is only satisfied if one
// path calls Succeed. Overlay precedence and SoD/immutability are out of band — a relationship
// never overrides a Deny (a role absent from both lists is simply never satisfied here).
public sealed class CapabilityHandler : AuthorizationHandler<CapabilityRequirement>
{
    private readonly ITopicCapabilityResolver _capabilities;
    private readonly IDelegationResolver _delegations;

    public CapabilityHandler(ITopicCapabilityResolver capabilities, IDelegationResolver delegations)
    {
        _capabilities = capabilities;
        _delegations = delegations;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CapabilityRequirement requirement)
    {
        var user = context.User;

        // Matrix "A": the role grants the action outright.
        if (requirement.AllowRoles.Any(user.IsInRole))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        if (userId is null)
            return;

        // Matrix "AiO": the role grants it only with a per-topic relationship on the target topic.
        // ponytail: any relationship counts in P4 (no aggregate yet); per-capability gating (Owner
        // edits vs Presenter read-only, docs/10 §D) is refined when Topics ship (P5).
        if (requirement.OwnerRoles.Any(user.IsInRole) && context.Resource is ITopicScopedResource scoped)
        {
            var caps = await _capabilities.GetCapabilitiesAsync(userId, scoped.TopicId);
            if (caps.Count > 0)
            {
                context.Succeed(requirement);
                return;
            }
        }

        // Delegation widening: an active, in-window delegation for this policy grants it (docs/10 §E.3).
        if (await _delegations.HasActiveDelegationAsync(userId, requirement.PolicyName))
            context.Succeed(requirement);
    }
}
