using Acmp.Modules.Knowledge.Domain.Enums;
using Acmp.Modules.Knowledge.Domain.Events;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Knowledge.Domain;

// The Template aggregate root (TPL-YYYY-###; P15d-2, FR-119) — a reusable Markdown skeleton (with placeholder
// fields) that pre-fills a new artifact's content at creation time (the pre-fill wiring is P15h). Name is
// bilingual (LocalizedString, mirrored en===ar — the locked cross-module pattern); Body is a single Markdown
// string (domain-model §403 deliberately marks Name bilingual but Body plain — and en===ar makes a second locale
// pointless anyway). TargetType is fixed at creation (a template's target artifact is its identity; re-create if
// wrong). Unlike Document, a Template has NO version-snapshot history — Version is a bare counter (FR-117
// versioning is a wiki-page concern; FR-119 asks only for edit).
//
// Lifecycle (FR-119): Active (usable) → Deprecated (retired, terminal). Edit revises Name+Body and bumps Version;
// Deprecate is a soft delete (permanent retention). A Deprecated template is immutable.
public sealed class Template : AuditableEntity
{
    private Template() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → 409.
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty;   // TPL-YYYY-###
    public TemplateStatus Status { get; private set; }
    public LocalizedString Name { get; private set; } = null!;
    public TemplateTargetType TargetType { get; private set; }
    public string Body { get; private set; } = string.Empty;   // Markdown (with placeholder fields)
    public int Version { get; private set; }   // 1-based; bumped on every Edit (no snapshot — FR-119, not FR-117)

    // FR-119: author a new template (Active). Name + Body are required; TargetType is fixed for the template's life.
    public static Template Create(string key, LocalizedString name, TemplateTargetType targetType, string body,
        DateTimeOffset now)
    {
        if (name is null) throw new InvalidOperationException("A template name is required.");
        if (string.IsNullOrWhiteSpace(body)) throw new InvalidOperationException("A template body is required.");

        var template = new Template
        {
            Key = key.Trim(),
            Status = TemplateStatus.Active,
            Name = name,
            TargetType = targetType,
            Body = body.Trim(),
            Version = 1,
        };
        template.Raise(new TemplateCreatedEvent(template.PublicId, template.Key, targetType, now));
        return template;
    }

    // FR-119: revise the template's Name + Body (TargetType is immutable). Rejected once Deprecated (terminal).
    // Bumps Version — a plain counter, no snapshot history (unlike a wiki Document).
    public void Edit(LocalizedString name, string body, DateTimeOffset now)
    {
        if (Status == TemplateStatus.Deprecated)
            throw new InvalidOperationException("A deprecated template cannot be edited.");

        Name = name ?? throw new InvalidOperationException("A template name is required.");
        if (string.IsNullOrWhiteSpace(body)) throw new InvalidOperationException("A template body is required.");
        Body = body.Trim();
        Version++;
        Raise(new TemplateEditedEvent(PublicId, Key, Version, now));
    }

    // FR-119: retire the template (Active → Deprecated; terminal). A soft delete — retention is permanent.
    public void Deprecate(DateTimeOffset now)
    {
        if (Status != TemplateStatus.Active)
            throw new InvalidOperationException($"This operation is not allowed while the template is {Status}.");
        Status = TemplateStatus.Deprecated;
        Raise(new TemplateDeprecatedEvent(PublicId, Key, now));
    }
}
