using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Features.SubmitTopic;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Traceability;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.ConvertTopic;

// W17 / FR-030 (SC-018, DEC-060): convert a DECIDED topic to a different type. The original is retired to
// Converted carrying the reason; a NEW topic of the target type is created; the two are linked by a typed
// ConvertedTo edge (DEC-061). "Original AND converted artifact" in FR-030 means two artifacts, which is why
// this creates a successor rather than editing the type in place.
//
// NOT Topic.Reclassify. That method changes Type/Source pre-Accept and its guard is DISJOINT from Convert's
// (Draft/Submitted/Triage/Reopened vs Decided), so no topic is ever a candidate for both. Reclassify stays
// deliberately unwired under DW-032 — do not fold it in here.
//
// EVERYTHING CARRIES OVER (DEC-060 d4, operator decision). Two things made that cheap and honest rather than
// dangerous, both established by reading the domain instead of assuming:
//   * AddComment takes authorSub/authorName/postedAt explicitly, so a carried comment keeps its ORIGINAL
//     author and timestamp. Nothing is re-attributed and no new domain path was needed.
//   * AddAttachment takes storageKey explicitly, so the successor's record points at the SAME stored object.
//     Attachments are immutable once uploaded, so sharing the blob is safe and no MinIO copy happens.
// Provenance is carried by the typed edge and the original's status history — NOT by rewriting comment bodies,
// which would violate the immutable-after-post contract TopicComment states in its own header (BL-033).
public sealed record ConvertTopicCommand(Guid TopicId, TopicType TargetType, string Reason)
    : IRequest<SubmitTopicResult>;

public sealed class ConvertTopicValidator : AbstractValidator<ConvertTopicCommand>
{
    public ConvertTopicValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.TargetType).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A conversion reason is required.").MaximumLength(1000);
    }
}

public sealed class ConvertTopicHandler : IRequestHandler<ConvertTopicCommand, SubmitTopicResult>
{
    private readonly ITopicsDbContext _db;
    private readonly ITopicKeyGenerator _keys;
    private readonly IResourceAuthorizer _authz;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ITraceabilityWriter _trace;

    public ConvertTopicHandler(ITopicsDbContext db, ITopicKeyGenerator keys, IResourceAuthorizer authz,
        ICurrentUser user, IClock clock, IAuditSink audit, ITraceabilityWriter trace)
    {
        _db = db;
        _keys = keys;
        _authz = authz;
        _user = user;
        _clock = clock;
        _audit = audit;
        _trace = trace;
    }

    public async Task<SubmitTopicResult> Handle(ConvertTopicCommand request, CancellationToken ct)
    {
        var original = await _db.Topics
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        await _authz.EnsureAsync(original, Policies.TopicTriage, ct);

        // Converting to the type it already is would retire a Decided topic and hand back a duplicate — a
        // destructive no-op. Refused here rather than in the validator because it needs the loaded topic.
        if (original.Type == request.TargetType)
            throw new InvalidOperationException("The topic is already of that type.");

        var now = _clock.UtcNow;
        var (sub, name) = CurrentActor.Of(_user);
        var key = await _keys.NextAsync(now.Year, ct);

        // Source side: retire the original. Throws if it is not Decided (the transition guard is the control).
        original.Convert(request.Reason, sub, name, now);

        var successor = Topic.Draft(key, original.Title, original.Description, original.Justification,
            request.TargetType, original.Urgency, original.Source, sub, name,
            original.AffectedStreams, original.Systems, original.Tags);

        // Scope is not a Draft() parameter (it defaults to SingleStream) and it drives stream-scoped
        // authorization, so carrying it explicitly matters: a Platform topic that silently became
        // SingleStream would narrow who can act on the successor.
        successor.SetScope(original.Scope);

        foreach (var c in original.Comments.OrderBy(c => c.PostedAt))
            successor.AddComment(c.Body, c.AuthorSub, c.AuthorName, c.PostedAt);

        foreach (var a in original.Attachments.OrderBy(a => a.UploadedAt))
            successor.AddAttachment(a.FileName, a.ContentType, a.SizeBytes, a.StorageKey,
                a.UploadedBySub, a.UploadedByName, a.UploadedAt);

        successor.Submit(now);

        _db.Topics.Add(successor);
        await _db.SaveChangesAsync(ct);

        // The typed link FR-030 demands. Written AFTER the save so both keys are real, and idempotent per
        // (source, target, relType) so a retry heals rather than duplicates.
        // The relation travels as its enum NAME so the Traceability vocabulary never leaks into Topics
        // (ADR-0001); an unknown name is rejected by the impl. Inline, matching ConvertResearchToTopic's "Informs".
        await _trace.RecordEdgeAsync(
            "Topic", original.PublicId, original.Key, original.Title,
            "Topic", successor.PublicId, successor.Key, successor.Title,
            relTypeName: "ConvertedTo", ct);

        // Emitted after SaveChangesAsync: AuditCaptureInterceptor only fills before/after on save, so an
        // emit before it would record an empty diff — the DW-017 failure mode.
        await _audit.EmitEnrichedAsync("Topics.TopicConverted", nameof(Topic), original.PublicId.ToString(), ct: ct);

        return new SubmitTopicResult(successor.PublicId, successor.Key);
    }
}
