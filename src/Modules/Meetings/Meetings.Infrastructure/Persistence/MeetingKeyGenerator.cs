using Acmp.Modules.Meetings.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Infrastructure.Persistence;

// Allocates the next MTG-/AGN-/MIN-YYYY-### key from a per-prefix, per-year counter row (README §F).
public sealed class MeetingKeyGenerator : IMeetingKeyGenerator
{
    private readonly MeetingsDbContext _db;

    public MeetingKeyGenerator(MeetingsDbContext db) => _db = db;

    public Task<string> NextMeetingKeyAsync(int year, CancellationToken ct = default) => NextAsync("MTG", year, ct);

    public Task<string> NextAgendaKeyAsync(int year, CancellationToken ct = default) => NextAsync("AGN", year, ct);

    public Task<string> NextMinutesKeyAsync(int year, CancellationToken ct = default) => NextAsync("MIN", year, ct);

    private async Task<string> NextAsync(string prefix, int year, CancellationToken ct)
    {
        var counter = await _db.KeyCounters.FirstOrDefaultAsync(c => c.Prefix == prefix && c.Year == year, ct);
        if (counter is null)
        {
            counter = new MeetingKeyCounter { Prefix = prefix, Year = year, Next = 1 };
            _db.KeyCounters.Add(counter);
        }

        var ordinal = counter.Next;
        counter.Next++;
        await _db.SaveChangesAsync(ct);

        return $"{prefix}-{year}-{ordinal:D3}";
    }
}
