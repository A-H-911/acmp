namespace Acmp.Shared.Domain.Entities;

// Lightweight creation/modification stamps, filled by the module DbContext on SaveChanges.
// The immutable, hash-chained AuditEvent log (ADR-0009) is a separate Audit-module concern.
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
