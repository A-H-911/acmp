namespace Acmp.Modules.Meetings.Domain.Enums;

// Per-item outcome recorded as the committee works through the agenda (docs/domain/domain-model.md §C AgendaItem).
public enum AgendaItemOutcome
{
    Pending = 0,
    Discussed = 1,
    Deferred = 2,
    CarriedOver = 3,
}
