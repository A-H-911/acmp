using Acmp.Modules.Knowledge.Domain.Enums;
using Acmp.Modules.Knowledge.Domain.Events;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Knowledge.Domain;

// The Document aggregate root (DOC-YYYY-###; P15d, FR-116/117) — a versioned knowledge-base / wiki page. Title
// and Body are bilingual (LocalizedString, mirrored en===ar — the locked cross-module pattern); Category is
// free-text (FR-116 lists it; no taxonomy is specified). OwnerUserId is a Keycloak-sub attribution snapshot,
// NOT an enforced ownership gate (a Document is not topic-scoped, so — exactly like ADRs — Chairman/Secretary
// are the effective writers; OQ per-document owner enforcement is out of scope). Cross-links to other artifacts
// (FR-116) are external Relationship edges wired at the P15d UI, not stored on the aggregate.
//
// Lifecycle (FR-116): Draft (author/revise) → Published (visible), with a side exit → Archived (retired,
// terminal). Content saves (Create/Edit, FR-117) append an immutable DocumentVersion snapshot and bump Version;
// Publish/Archive change status only — audited, but no new version row. An Archived document is immutable.
public sealed class Document : AuditableEntity
{
    private readonly List<string> _tags = new();
    private readonly List<DocumentVersion> _versions = new();

    private Document() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → 409.
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty;   // DOC-YYYY-###
    public DocumentStatus Status { get; private set; }
    public LocalizedString Title { get; private set; } = null!;
    public LocalizedString Body { get; private set; } = null!;   // Markdown, bilingual (mirrored en===ar)
    public string Category { get; private set; } = string.Empty; // FR-116 free-text category

    // Owner attribution snapshot (Keycloak sub). Attribution only — not an enforced ownership gate.
    public string OwnerUserId { get; private set; } = string.Empty;

    public int Version { get; private set; }   // 1-based; bumped on every content save (Create/Edit)

    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();
    public IReadOnlyCollection<DocumentVersion> Versions => _versions.AsReadOnly();

    // FR-116: author a new document in Draft. Title + Body + owner are required. Version starts at 1 and the
    // first content snapshot (v1) is recorded immediately (FR-117).
    public static Document Create(string key, LocalizedString title, string category, LocalizedString body,
        string ownerUserId, IEnumerable<string>? tags, DateTimeOffset now)
    {
        if (title is null) throw new InvalidOperationException("A document title is required.");
        if (body is null) throw new InvalidOperationException("A document body is required.");
        if (string.IsNullOrWhiteSpace(ownerUserId)) throw new InvalidOperationException("A document owner is required.");

        var document = new Document
        {
            Key = key.Trim(),
            Status = DocumentStatus.Draft,
            Title = title,
            Body = body,
            Category = (category ?? string.Empty).Trim(),
            OwnerUserId = ownerUserId,
            Version = 1,
        };
        document.ReplaceTags(tags);
        document.AppendSnapshot(now, ownerUserId);
        document.Raise(new DocumentCreatedEvent(document.PublicId, document.Key, ownerUserId, now));
        return document;
    }

    // FR-117: revise the content. Allowed while Draft OR Published (a published page can be corrected); rejected
    // once Archived (terminal, immutable). Bumps Version and appends a new immutable snapshot — the prior
    // versions are never mutated.
    public void Edit(LocalizedString title, string category, LocalizedString body, DateTimeOffset now, string editorUserId)
    {
        if (Status == DocumentStatus.Archived)
            throw new InvalidOperationException("An archived document cannot be edited.");

        Title = title ?? throw new InvalidOperationException("A document title is required.");
        Body = body ?? throw new InvalidOperationException("A document body is required.");
        Category = (category ?? string.Empty).Trim();
        Version++;
        AppendSnapshot(now, editorUserId);
        Raise(new DocumentEditedEvent(PublicId, Key, Version, now));
    }

    // FR-116: publish a draft (Draft → Published). Status only — no new version snapshot.
    public void Publish(DateTimeOffset now)
    {
        RequireStatus(DocumentStatus.Draft);
        Status = DocumentStatus.Published;
        Raise(new DocumentPublishedEvent(PublicId, Key, now));
    }

    // FR-116: retire the document (Draft or Published → Archived; terminal). Status only — no new version snapshot.
    public void Archive(DateTimeOffset now)
    {
        RequireStatus(DocumentStatus.Draft, DocumentStatus.Published);
        Status = DocumentStatus.Archived;
        Raise(new DocumentArchivedEvent(PublicId, Key, now));
    }

    private void AppendSnapshot(DateTimeOffset now, string savedByUserId) =>
        _versions.Add(DocumentVersion.Create(Version, Title, Body, now, savedByUserId));

    private void ReplaceTags(IEnumerable<string>? tags)
    {
        _tags.Clear();
        if (tags is null) return;
        foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()))
            _tags.Add(tag);
    }

    private void RequireStatus(params DocumentStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"This operation is not allowed while the document is {Status}.");
    }
}
