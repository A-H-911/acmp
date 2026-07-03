using Acmp.Shared.Application.Abstractions;

namespace Acmp.Modules.Traceability.Application.Internal;

// The acting principal's stable subject + display-name snapshot, for attribution on edge create/deactivate
// and audit. Subject matches ICurrentUser.UserId (the Keycloak sub) used everywhere else.
internal static class CurrentActor
{
    public static (string Sub, string Name) Of(ICurrentUser user) =>
        (user.UserId ?? "system", user.DisplayName ?? user.UserName ?? user.Email ?? "Unknown");
}
