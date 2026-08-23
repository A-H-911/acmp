using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.ReclassifyTopic;

// FR-164 / DW-032 (DEC-070): correct a topic's TYPE and SOURCE before Acceptance — the Secretary's
// triage-time correction path. Before this the only remedy for a topic submitted under the wrong type
// was reject-and-resubmit, which discards the record and its comments.
//
// NOT AN EDIT, and that line is drawn in the SPA already: EditTopic's own header excludes a type picker
// with the words "type is reclassification, not an edit". Folding Type into UpdateTopicCommand would
// have widened it to the SUBMITTER, who may edit their own pre-Accept topic with no policy check at all —
// so classification would become self-service. It is a triage act, gated like the other triage acts.
//
// NOT Topic.Convert. Convert requires Decided and creates a SUCCESSOR artifact (FR-030); this changes two
// fields in place and creates nothing. The two guards are DISJOINT — Convert requires Decided, Reclassify
// forbids anything past Triage — so no topic is ever a candidate for both, and neither can stand in for
// the other.
//
// ROLE-GATED ON THE COMMAND AS WELL AS THE ENDPOINT, mirroring SetTopicConfidentialityCommand. The
// endpoint policy is the outer door; AllowedRoles is what still refuses if this command is ever
// dispatched from somewhere other than that endpoint.
public sealed record ReclassifyTopicCommand(Guid TopicId, TopicType Type, TopicSource Source)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class ReclassifyTopicValidator : AbstractValidator<ReclassifyTopicCommand>
{
    public ReclassifyTopicValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Source).IsInEnum();
    }
}

public sealed class ReclassifyTopicHandler : IRequestHandler<ReclassifyTopicCommand>
{
    private readonly ITopicsDbContext _db;
    private readonly IAuditSink _audit;

    public ReclassifyTopicHandler(ITopicsDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task Handle(ReclassifyTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        // Nothing changed: no save, no audit row. A reclassification that did not happen must not appear
        // in the audit trail as though it did — the same rule SetTopicConfidentiality applies, and it
        // matters more here because the triage UI submits both fields whether or not either moved.
        // ⚠ Checked BEFORE the domain call on purpose: Reclassify would otherwise throw on a past-Triage
        // topic even when the request asks for the values it already has, turning a no-op into a 409.
        if (topic.Type == request.Type && topic.Source == request.Source)
            return;

        // The status guard is the aggregate's, not this handler's (DEF-059's rule: the invariant belongs
        // where every caller passes). Past Triage it throws, and that is the control.
        topic.Reclassify(request.Type, request.Source);

        await _db.SaveChangesAsync(ct);

        // After the save so AuditCaptureInterceptor has filled before/after — an emit before it records
        // an empty diff (the DW-017 failure mode). The diff IS the record here: unlike reject or defer,
        // reclassification writes no status transition and carries no reason, so the audit row is the
        // only place the old classification survives.
        await _audit.EmitEnrichedAsync("Topics.TopicReclassified", nameof(Topic), topic.PublicId.ToString(), ct: ct);
    }
}
