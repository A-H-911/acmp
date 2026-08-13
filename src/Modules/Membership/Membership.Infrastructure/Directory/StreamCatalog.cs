using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Infrastructure.Directory;

// Membership-owned implementation of the shared IStreamCatalog port (ADR-0001): lets Topics validate
// a submitted stream against the committee's taxonomy without reading membership.streams itself.
// Sits beside CommitteeDirectory because it is the same kind of seam — reference data Membership owns
// and another module needs to resolve.
public sealed class StreamCatalog : IStreamCatalog
{
    private readonly IMembershipDbContext _db;

    public StreamCatalog(IMembershipDbContext db) => _db = db;

    // ⚠ The !s.IsWildcard filter is the enforcement half of ADR-0042 clause (4). It is applied here,
    // once, rather than left to callers: the wildcard states that a PERSON is unrestricted, and a
    // topic claiming it would assert universal scope — the opposite of what the marker means.
    public async Task<IReadOnlyCollection<string>> GetAssignableStreamCodesAsync(CancellationToken ct = default) =>
        await _db.Streams.AsNoTracking()
            .Where(s => !s.IsWildcard)
            .Select(s => s.Code)
            .ToListAsync(ct);
}
