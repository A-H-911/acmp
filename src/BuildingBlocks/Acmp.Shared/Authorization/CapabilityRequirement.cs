using Microsoft.AspNetCore.Authorization;

namespace Acmp.Shared.Authorization;

// One row of the docs/10 §C capability matrix as an authorization requirement.
// AllowRoles  = matrix "A"   (role grants the action outright).
// OwnerRoles  = matrix "AiO" (role grants it only with a per-topic relationship on the target).
// A Deny is the absence of both. SoD-5 is encoded by simply never listing Administrator in a
// committee-content row's AllowRoles/OwnerRoles.
public sealed class CapabilityRequirement : IAuthorizationRequirement
{
    public CapabilityRequirement(string policyName, IReadOnlyCollection<string> allowRoles, IReadOnlyCollection<string> ownerRoles)
    {
        PolicyName = policyName;
        AllowRoles = allowRoles;
        OwnerRoles = ownerRoles;
    }

    public string PolicyName { get; }
    public IReadOnlyCollection<string> AllowRoles { get; }
    public IReadOnlyCollection<string> OwnerRoles { get; }
}
