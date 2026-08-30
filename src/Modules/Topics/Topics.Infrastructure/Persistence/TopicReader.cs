using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Topics.Infrastructure.Persistence;

// Topics-side implementation of the cross-module ITopicReader seam (ADR-0001, P15c / FR-115): another module
// reads a topic's key + title snapshot (e.g. Research recording a Topic→Mission source edge) without touching
// Topics' tables. An unknown id returns null (a since-deleted or unknown source topic → the caller records no
// edge). Read-only; no lifecycle side effects (mirrors TopicStreamReader).
//
// FR-159 added the /session reads: the presenter's topic card and a pre-signed URL for one of its
// materials. Both stay HERE rather than becoming a Meetings query, because the attachment rows and the
// object keys behind them are Topics-owned and the storage key must never leave this module.
public sealed class TopicReader : ITopicReader
{
    private readonly TopicsDbContext _db;
    private readonly IFileStore _files;
    private readonly string _bucket;

    // NFR-027: sensitive files are served by short-lived pre-signed URL, never streamed through the API
    // and never a durable link. Fifteen minutes is enough to click "Open" and no longer.
    private static readonly TimeSpan UrlLifetime = TimeSpan.FromMinutes(15);

    private readonly ITopicVisibility _visibility;

    public TopicReader(TopicsDbContext db, IFileStore files, IOptions<StorageOptions> storage, ITopicVisibility visibility)
    {
        _db = db;
        _files = files;
        _bucket = storage.Value.AttachmentsBucket;
        _visibility = visibility;
    }

    public async Task<TopicSummary?> GetSummaryAsync(Guid topicId, CancellationToken ct = default)
    {
        var topic = await _db.Topics.AsNoTracking()
            .VisibleTo(await _visibility.ResolveAsync(ct))
            .FirstOrDefaultAsync(t => t.PublicId == topicId, ct);
        // C-AUTHZ-04: a cross-module summary is still a title. Filtering in the QUERY rather than after
        // it means the caller cannot tell a restricted topic from a missing one.
        return topic is null ? null : new TopicSummary(topic.PublicId, topic.Key, topic.Title);
    }

    // C-AUTH-05 SoD-4 / NFR-064: the Decisions module asks whether the person recording a decision owns the
    // topic it is about. Null when the topic is unknown or has no owner yet (Triage) - both ordinary.
    //
    // ⚠⚠ DELIBERATELY NOT `.VisibleTo(...)`-FILTERED, UNLIKE EVERY OTHER READ ON THIS CLASS, AND THE
    // DIFFERENCE IS THE WHOLE POINT. The others answer a USER's question and must not let a caller tell a
    // Restricted topic from a missing one (C-AUTHZ-04). This one answers a CONTROL's question about the
    // caller themselves, and applying the confidentiality filter here would mean a recorder who cannot see
    // a Restricted topic gets `null` - "no owner" - and is therefore never flagged on it. A segregation-of-
    // duties signal that switches itself off exactly where the material is most sensitive is worse than
    // none, and it would fail silently (LL-030: a defence layer invisible to every front-door test).
    //
    // IT LEAKS NOTHING, and that is why the exception is safe rather than merely convenient: the Guid is
    // compared inside Decisions and discarded. The only thing derived from it that ever reaches a client is
    // a boolean about the CALLER'S OWN relationship to a topic they are already recording a decision on.
    public Task<Guid?> GetOwnerIdAsync(Guid topicId, CancellationToken ct = default) =>
        _db.Topics.AsNoTracking()
            .Where(t => t.PublicId == topicId)
            .Select(t => t.OwnerId)
            .FirstOrDefaultAsync(ct);

    public async Task<TopicBrief?> GetBriefAsync(Guid topicId, CancellationToken ct = default)
    {
        // C-AUTHZ-04 / FR-163. This serves the guest /session brief — the topic's full Description and
        // its material list — to an EXTERNAL presenter, so it is the highest-consequence read in the
        // module. A guest presenter holds a Presenter capability grant, so the grantee leg lets them
        // through to their own slot; anyone else is refused as not-found.
        var topic = await _db.Topics.AsNoTracking()
            .Include(t => t.Attachments)
            .VisibleTo(await _visibility.ResolveAsync(ct))
            .FirstOrDefaultAsync(t => t.PublicId == topicId, ct);

        if (topic is null)
            return null;

        // Description is the topic's own body text — the design's "Topic summary" block. StorageKey is
        // deliberately not projected: the caller gets an id it can ask for a URL with, nothing more.
        var materials = topic.Attachments
            .OrderBy(a => a.UploadedAt)
            .Select(a => new TopicMaterial(a.PublicId, a.FileName, a.ContentType, a.SizeBytes))
            .ToList();

        return new TopicBrief(topic.PublicId, topic.Key, topic.Title, topic.Description, materials);
    }

    public async Task<string?> GetMaterialUrlAsync(Guid topicId, Guid attachmentId, CancellationToken ct = default)
    {
        // ONE query joining both ids. Looking the attachment up alone and comparing its topic afterwards
        // would work, but it invites a later refactor to drop the comparison; this way the scope IS the
        // lookup and there is nothing to forget.
        // The pre-signed URL is the actual file. Scoping the lookup by visibility keeps "the scope IS
        // the lookup" property this method was already written around.
        var storageKey = await _db.Topics.AsNoTracking()
            .VisibleTo(await _visibility.ResolveAsync(ct))
            .Where(t => t.PublicId == topicId)
            .SelectMany(t => t.Attachments)
            .Where(a => a.PublicId == attachmentId)
            .Select(a => a.StorageKey)
            .FirstOrDefaultAsync(ct);

        return storageKey is null ? null : await _files.GetPreSignedUrlAsync(_bucket, storageKey, UrlLifetime, ct);
    }
}
