using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.UpdateTopic;

// Edit a topic (AC-034). Pre-Accept: the submitter or an editor (Owner via ABAC / Secretary) may edit
// content + metadata. Post-Accept: only Secretary/Chairman edit metadata; content is locked by the
// domain. The field-level lock is enforced in the aggregate; this handler enforces the who.
public sealed record UpdateTopicCommand(
    Guid TopicId, string Title, string Description, string Justification,
    TopicUrgency Urgency, IReadOnlyList<string> Streams, IReadOnlyList<string> Systems, IReadOnlyList<string> Tags,
    TopicScope? Scope = null)
    : IRequest;

public sealed class UpdateTopicValidator : AbstractValidator<UpdateTopicCommand>
{
    public UpdateTopicValidator(IStreamCatalog streams)
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Urgency).IsInEnum();
        RuleFor(x => x.Scope!.Value).IsInEnum().When(x => x.Scope.HasValue);
        // ⚠ The same taxonomy rule as submit, and it is load-bearing here rather than symmetric
        // tidiness: this endpoint writes Streams through Topic.AssignStreams exactly as submit does,
        // so enforcing the taxonomy only on submit would leave free text reachable through an edit.
        RuleFor(x => x.Streams).MustBeSeededStreams(streams);
        // DEF-059 / DEC-043 half one, the boundary half. The aggregate refuses to empty a LIVE topic
        // (Topic.AssignStreams) because that guard must hold for every caller; this rule is what
        // turns the refusal into a 400 with a field name rather than a 409 from a domain exception.
        // Both exist on purpose — the validator is the message, the aggregate is the invariant.
        RuleFor(x => x.Streams).NotEmpty()
            .WithMessage("A topic must affect at least one stream.");
    }
}

public sealed class UpdateTopicHandler : IRequestHandler<UpdateTopicCommand>
{
    private readonly ITopicsDbContext _db;
    private readonly IResourceAuthorizer _authz;
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;

    public UpdateTopicHandler(ITopicsDbContext db, IResourceAuthorizer authz, ICurrentUser user, IAuditSink audit)
    {
        _db = db;
        _authz = authz;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(UpdateTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        var (sub, _) = CurrentActor.Of(_user);
        var preAccept = topic.Status is TopicStatus.Draft or TopicStatus.Submitted or TopicStatus.Triage or TopicStatus.Reopened;

        if (preAccept)
        {
            if (topic.SubmittedBySub != sub)
                await _authz.EnsureAsync(topic, Policies.TopicEdit, ct);   // owner (AiO) or Secretary/Chairman
            topic.UpdateContent(request.Title, request.Description, request.Justification);
        }
        else
        {
            await _authz.EnsureAsync(topic, Policies.TopicTriage, ct);     // metadata-only, Secretary/Chairman (AC-034)
        }

        // DEF-058: the only caller of Topic.SetScope. Without it Platform and OrgWide are unreachable
        // enum values, DeriveScope() is the sole writer, and the reviewer/voter set and notification
        // audience TopicScope is documented to drive are computed from stream COUNT alone with no way
        // for a human to correct them.
        //
        // ⚠ GATED SEPARATELY AND AT EVERY STATUS, not folded into the branch above. Elevating a topic
        // to Platform/OrgWide WIDENS write access to every stream-bounded member (ADR-0043 clause 5),
        // and the pre-Accept branch deliberately lets the submitter edit their own topic with no ABAC
        // check at all — so without this the submitter of a topic could grant the whole committee
        // write access to it. Elevation is a triage act (the taxonomy says "the Secretary may elevate
        // during triage"), so it carries the triage policy wherever it happens. Unchanged scope costs
        // nothing: a caller that omits Scope, or resends the current one, is never authorized against.
        var scopeChanged = request.Scope is { } scope && scope != topic.Scope;
        if (scopeChanged)
        {
            await _authz.EnsureAsync(topic, Policies.TopicTriage, ct);
            topic.SetScope(request.Scope!.Value);
        }

        topic.SetUrgency(request.Urgency);
        topic.AssignStreams(request.Streams);
        topic.AssignSystems(request.Systems);
        topic.SetTags(request.Tags);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Topics.TopicUpdated", nameof(Topic), topic.PublicId.ToString(), ct: ct);

        // A SEPARATE ROW, not a field on TopicUpdated. ADR-0043 clause (5) accepts scope elevation as
        // a deliberate escalation path precisely because it is "Secretary/Chairman-gated and
        // audited" — and an escalation buried inside a generic "topic updated" event is not findable
        // by anyone asking who widened access to this topic, which is the only question this row
        // exists to answer.
        // The before/after values are drained from the request-scoped capture buffer by
        // (subjectType, subjectId), so the row carries the old and new scope without this call
        // passing them — the 4th parameter is the OUTCOME vocabulary, not a payload.
        if (scopeChanged)
            await _audit.EmitEnrichedAsync("Topics.TopicScopeChanged", nameof(Topic), topic.PublicId.ToString(), ct: ct);
    }
}
