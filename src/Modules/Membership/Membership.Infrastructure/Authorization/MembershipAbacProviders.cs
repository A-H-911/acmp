using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization.Abac;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Infrastructure.Authorization;

// Membership-owned implementations of the shared ABAC ports (docs/10 §D/§E). They read ONLY
// Membership tables and are injected into the shared-kernel authorization handlers — the
// in-process public-contract pattern that keeps module boundaries intact (ADR-0001). userId is the
// Keycloak subject (ICurrentUser.UserId).

public sealed class UserStreamProvider : IUserStreamProvider
{
    private readonly IMembershipDbContext _db;

    public UserStreamProvider(IMembershipDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<string>> GetAssignedStreamsAsync(string userId, CancellationToken ct = default) =>
        await _db.Members.AsNoTracking()
            .Where(m => m.KeycloakUserId == userId)
            .SelectMany(m => m.Streams)
            .Join(_db.Streams, a => a.StreamId, s => s.Id, (a, s) => s.Code)
            .ToListAsync(ct);
}

public sealed class TopicCapabilityResolver : ITopicCapabilityResolver
{
    private readonly IMembershipDbContext _db;
    private readonly IClock _clock;

    public TopicCapabilityResolver(IMembershipDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyCollection<TopicCapabilityType>> GetCapabilitiesAsync(
        string userId, Guid topicId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var grants = await _db.TopicCapabilities.AsNoTracking()
            .Where(g => g.TopicId == topicId)
            .Join(_db.Members.Where(m => m.KeycloakUserId == userId), g => g.CommitteeMemberId, m => m.Id, (g, _) => g)
            .ToListAsync(ct);

        return grants.Where(g => g.IsActiveAt(now)).Select(g => g.Capability).Distinct().ToList();
    }
}

// Grant-on-accept (W2): Topics calls this when an owner is assigned so the per-topic Owner relationship
// is resolvable by the ABAC CapabilityHandler. ownerMemberId is a CommitteeMember.PublicId; the grant is
// stored against the member's row (keyed internally by Id). Idempotent on repeat.
public sealed class TopicCapabilityWriter : ITopicCapabilityWriter
{
    private readonly IMembershipDbContext _db;

    public TopicCapabilityWriter(IMembershipDbContext db) => _db = db;

    public async Task GrantAsync(Guid topicId, Guid ownerMemberId, TopicCapabilityType capability, CancellationToken ct = default)
    {
        var memberId = await _db.Members.Where(m => m.PublicId == ownerMemberId).Select(m => m.Id).FirstOrDefaultAsync(ct);
        if (memberId == 0) throw new KeyNotFoundException("Owner member not found for capability grant.");

        var exists = await _db.TopicCapabilities
            .AnyAsync(g => g.TopicId == topicId && g.CommitteeMemberId == memberId && g.Capability == capability, ct);
        if (exists) return;

        _db.TopicCapabilities.Add(TopicCapabilityGrant.Grant(memberId, topicId, capability));
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class DelegationResolver : IDelegationResolver
{
    private readonly IMembershipDbContext _db;
    private readonly IClock _clock;

    public DelegationResolver(IMembershipDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<bool> HasActiveDelegationAsync(string userId, string capability, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        return await _db.Delegations.AsNoTracking()
            .Join(_db.Members.Where(m => m.KeycloakUserId == userId), d => d.DelegateMemberId, m => m.Id, (d, _) => d)
            .AnyAsync(d => d.Capability == capability && d.ValidFrom <= now && now <= d.ValidTo, ct);
    }
}
