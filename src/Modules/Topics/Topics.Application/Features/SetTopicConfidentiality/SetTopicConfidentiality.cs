using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.SetTopicConfidentiality;

// FR-163 / C-AUTHZ-04 — classify or declassify a topic (DEC-063 d2: Chairman and Secretary only).
//
// ROLE-GATED, NOT ABAC, and that is the decision rather than a shortcut. The Owner is deliberately
// excluded even though they often recognise sensitivity first: letting an owner classify would let a
// plain Member hide a topic from the committee, which cuts against the read-visible/write-scoped
// default (OQ-AUTH-001) that Restricted is a narrow carve-out FROM. So the gate is the role, and
// AllowedRoles on the command is what enforces it — mirroring ConvertResearchToTopicCommand.
//
// ⚠ NOT routed through Policies.TopicEdit, which now carries ConfidentialityRequirement. Doing so
// would make declassification depend on being able to see the topic, which is circular for the exact
// case that matters: a Secretary lifting a classification they did not set.
public sealed record SetTopicConfidentialityCommand(Guid TopicId, bool Restricted)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class SetTopicConfidentialityValidator : AbstractValidator<SetTopicConfidentialityCommand>
{
    public SetTopicConfidentialityValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
    }
}

public sealed class SetTopicConfidentialityHandler : IRequestHandler<SetTopicConfidentialityCommand>
{
    private readonly ITopicsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public SetTopicConfidentialityHandler(ITopicsDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(SetTopicConfidentialityCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        var before = topic.IsRestricted;
        var (sub, name) = CurrentActor.Of(_user);
        var now = _clock.UtcNow;

        if (request.Restricted) topic.Restrict(sub, name, now);
        else topic.Declassify(sub, name, now);

        // Nothing changed: no save, no audit row. A classification change that did not happen must not
        // appear in the audit trail as though it did — the aggregate is idempotent by state for the
        // same reason, and this keeps the two consistent.
        if (topic.IsRestricted == before) return;

        await _db.SaveChangesAsync(ct);

        // Distinct action verbs rather than one with a payload flag: "who restricted this topic" is a
        // question an auditor asks directly, and it should be answerable by filtering the action rather
        // than by parsing a diff. Emitted after the save so AuditCaptureInterceptor has the before/after.
        await _audit.EmitEnrichedAsync(
            request.Restricted ? "Topics.TopicRestricted" : "Topics.TopicDeclassified",
            nameof(Topic), topic.PublicId.ToString(), ct: ct);
    }
}
