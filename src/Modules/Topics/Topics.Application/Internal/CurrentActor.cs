using Acmp.Shared.Application.Abstractions;

namespace Acmp.Modules.Topics.Application.Internal;

// The acting principal's stable subject + display-name snapshot, for attribution on transitions and
// audit. Subject matches ICurrentUser.UserId (the Keycloak sub) used everywhere else.
internal static class CurrentActor
{
    public static (string Sub, string Name) Of(ICurrentUser user) =>
        (user.UserId ?? "system", user.DisplayName ?? user.UserName ?? user.Email ?? "Unknown");
}
