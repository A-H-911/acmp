namespace Acmp.Modules.Meetings.Application.Abstractions;

// Allocates human-readable, year-scoped keys (MTG-YYYY-### / AGN-YYYY-###, README §F). Implemented in
// Infrastructure against per-year counter rows so numbering is gap-free and concurrency-safe.
public interface IMeetingKeyGenerator
{
    Task<string> NextMeetingKeyAsync(int year, CancellationToken ct = default);
    Task<string> NextAgendaKeyAsync(int year, CancellationToken ct = default);
    Task<string> NextMinutesKeyAsync(int year, CancellationToken ct = default);
}
