using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Knowledge.Domain;

// An immutable content snapshot of a Document at a point in time (FR-117). Owned child of the Document aggregate
// (reached only through it) — the same owned-collection shape as Research's Finding: a BaseEntity identity plus
// the version ordinal, the bilingual Title + Body as they stood at that save, and who saved it when. Snapshots
// are append-only: once written they are never mutated (Category is intentionally NOT snapshotted — only the
// bilingual content is versioned per the P15d spec).
public sealed class DocumentVersion : BaseEntity
{
    private DocumentVersion() { }

    public int Version { get; private set; }
    public LocalizedString Title { get; private set; } = null!;
    public LocalizedString Body { get; private set; } = null!;
    public DateTimeOffset SavedAt { get; private set; }
    public string SavedByUserId { get; private set; } = string.Empty;

    internal static DocumentVersion Create(int version, LocalizedString title, LocalizedString body,
        DateTimeOffset savedAt, string savedByUserId) =>
        new()
        {
            Version = version,
            Title = title,
            Body = body,
            SavedAt = savedAt,
            SavedByUserId = savedByUserId,
        };
}
