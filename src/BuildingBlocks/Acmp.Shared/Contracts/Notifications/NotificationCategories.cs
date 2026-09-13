namespace Acmp.Shared.Contracts.Notifications;

// The ONE catalog of notification event types (FR-133 / AC-160, DEC-186). Every category a module
// publishes is declared here and the per-module *Notifications classes alias these constants;
// NotificationCatalogTests fails if a module declares a category this list does not name, or this list
// names one no module declares. Group = the heading the preferences page files a row under; list order =
// the page's row order. No event type is mandatory (DEC-186 e1): a member may turn any of them off.
public static class NotificationCategories
{
    public const string MeetingScheduled = "MeetingScheduled";
    public const string AgendaPublished = "AgendaPublished";
    public const string MinutesPublished = "MinutesPublished";
    public const string MinutesChangesRequested = "MinutesChangesRequested";
    public const string TopicPrepared = "TopicPrepared";
    public const string TopicRejected = "TopicRejected";
    public const string TopicSlaBreach = "TopicSlaBreach";
    public const string VoteOpened = "VoteOpened";
    public const string DecisionIssued = "DecisionIssued";
    public const string ActionAssigned = "ActionAssigned";
    public const string ActionDueReminder = "ActionDueReminder";
    public const string ActionOverdue = "ActionOverdue";
    public const string ActionOverdueEscalation = "ActionOverdueEscalation";
    public const string ActionVerified = "ActionVerified";
    public const string RiskAssigned = "RiskAssigned";
    public const string RiskEscalated = "RiskEscalated";
    public const string AdrProposed = "AdrProposed";
    public const string AdrApproved = "AdrApproved";
    public const string AdrSuperseded = "AdrSuperseded";
    public const string InvariantProposed = "InvariantProposed";
    public const string InvariantActivated = "InvariantActivated";
    public const string InvariantSuperseded = "InvariantSuperseded";
    public const string DailyDigest = "DailyDigest";
    public const string WeeklyDigest = "WeeklyDigest";

    public static IReadOnlyList<NotificationCategory> All { get; } = new NotificationCategory[]
    {
        new(MeetingScheduled, "meetings"),
        new(AgendaPublished, "meetings"),
        new(MinutesPublished, "meetings"),
        new(MinutesChangesRequested, "meetings"),
        new(TopicPrepared, "topics"),
        new(TopicRejected, "topics"),
        new(TopicSlaBreach, "topics"),
        new(VoteOpened, "decisions"),
        new(DecisionIssued, "decisions"),
        new(ActionAssigned, "actions"),
        new(ActionDueReminder, "actions"),
        new(ActionOverdue, "actions"),
        new(ActionOverdueEscalation, "actions"),
        new(ActionVerified, "actions"),
        new(RiskAssigned, "risks"),
        new(RiskEscalated, "risks"),
        new(AdrProposed, "governance"),
        new(AdrApproved, "governance"),
        new(AdrSuperseded, "governance"),
        new(InvariantProposed, "governance"),
        new(InvariantActivated, "governance"),
        new(InvariantSuperseded, "governance"),
        new(DailyDigest, "notifications"),
        new(WeeklyDigest, "notifications"),
    };

    private static readonly HashSet<string> Names = new(All.Select(c => c.Name), StringComparer.Ordinal);

    public static bool Contains(string category) => Names.Contains(category);
}

public sealed record NotificationCategory(string Name, string Group);

// The delivery channels a stored preference can name (DOC-025 §4.1's ChannelId; DEC-186 e6). Only InApp is
// read today; WBS-40.19 adds Webex for the per-user DM cards. The shared committee Webex space is never
// governed by a member's choice (DEC-186 e5), so it has no channel id here.
public static class NotificationChannels
{
    public const string InApp = "InApp";
}
