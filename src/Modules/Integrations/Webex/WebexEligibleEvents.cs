namespace Acmp.Modules.Integrations.Webex;

// Which notification categories are mirrored to the shared committee Webex space. Deliberately narrowed to
// genuinely COMMITTEE-WIDE events: a shared space would over-broadcast subset-targeted events (VoteOpened →
// eligible voters only, RiskEscalated → Chair+Secretary), so those stay in-app until per-user DM (email)
// exists (D-14). Cards carry no sensitive content regardless (title + deep link + urgency only). The
// category strings match the per-module NotificationMessage.Category constants (notification-strategy.md §3).
public static class WebexEligibleEvents
{
    // Exact matches for the per-module NotificationMessage.Category constants (verified against source):
    // MeetingNotifications, MinutesNotifications, DecisionNotifications (CategoryDecisionIssued).
    public static readonly IReadOnlySet<string> Categories = new HashSet<string>(StringComparer.Ordinal)
    {
        "MeetingScheduled",
        "AgendaPublished",
        "MinutesPublished",
        "DecisionIssued",
    };

    public static bool Includes(string category) => Categories.Contains(category);
}
