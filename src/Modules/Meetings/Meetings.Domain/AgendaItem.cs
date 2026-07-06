using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Meetings.Domain;

// Placement of one Topic on an Agenda with order, time-box, presenter (docs/domain/domain-model.md §C, W6). The topic
// key/title/urgent are display snapshots captured when the item is added — Meetings never reads Topics
// tables (ADR-0001); the real link is TopicId. ActualMinutes is the per-item time actually spent,
// recorded during the live meeting (the design's "12:30 / 20:00"). Mutated only via the Agenda root.
public sealed class AgendaItem : BaseEntity
{
    public const int MinTimebox = 5;
    public const int MaxTimebox = 120;

    private AgendaItem() { }

    public Guid TopicId { get; private set; }
    public string TopicKey { get; private set; } = string.Empty;    // display snapshot (TOP-YYYY-###)
    public string TopicTitle { get; private set; } = string.Empty;  // display snapshot
    public bool Urgent { get; private set; }                        // display snapshot
    public int Order { get; private set; }
    public int TimeboxMinutes { get; private set; }
    public Guid? PresenterUserId { get; private set; }
    public string? PresenterName { get; private set; }
    public AgendaItemOutcome Outcome { get; private set; }
    public int ActualMinutes { get; private set; }
    public Guid? CarryOverFromAgendaId { get; private set; }

    internal AgendaItem(Guid topicId, string topicKey, string topicTitle, bool urgent, int order,
        int timeboxMinutes, Guid? presenterUserId, string? presenterName, Guid? carryOverFromAgendaId = null)
    {
        if (topicId == Guid.Empty) throw new InvalidOperationException("An agenda item must reference a topic.");
        TopicId = topicId;
        TopicKey = topicKey.Trim();
        TopicTitle = topicTitle.Trim();
        Urgent = urgent;
        Order = order;
        TimeboxMinutes = Clamp(timeboxMinutes);
        if (presenterUserId is { } presenter && presenter != Guid.Empty)
        {
            PresenterUserId = presenter;
            PresenterName = presenterName?.Trim();
        }
        Outcome = AgendaItemOutcome.Pending;
        CarryOverFromAgendaId = carryOverFromAgendaId;
    }

    internal void SetOrder(int order) => Order = order;

    internal void SetTimebox(int minutes) => TimeboxMinutes = Clamp(minutes);

    internal void AssignPresenter(Guid presenterUserId, string presenterName)
    {
        if (presenterUserId == Guid.Empty) throw new InvalidOperationException("A presenter must be a valid user.");
        PresenterUserId = presenterUserId;
        PresenterName = presenterName.Trim();
    }

    internal void RecordActualMinutes(int minutes) => ActualMinutes = minutes < 0 ? 0 : minutes;

    internal void SetOutcome(AgendaItemOutcome outcome) => Outcome = outcome;

    private static int Clamp(int minutes) => Math.Max(MinTimebox, Math.Min(MaxTimebox, minutes));
}
