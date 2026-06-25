using Acmp.Shared.Domain.Events;

namespace Acmp.Shared.Domain.Entities;

// Root for persisted entities. Clustered key Id = BIGINT IDENTITY (OQ-014); PublicId is a stable
// GUID alternate key for external references. Human-readable display keys (TOP-YYYY-###) are
// modelled separately on the owning aggregate.
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public long Id { get; set; }

    public Guid PublicId { get; set; } = Guid.NewGuid();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
