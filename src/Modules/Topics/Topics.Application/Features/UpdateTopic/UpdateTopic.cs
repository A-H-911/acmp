using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.UpdateTopic;

// Edit a topic (AC-034). Pre-Accept: the submitter or an editor (Owner via ABAC / Secretary) may edit
// content + metadata. Post-Accept: only Secretary/Chairman edit metadata; content is locked by the
// domain. The field-level lock is enforced in the aggregate; this handler enforces the who.
public sealed record UpdateTopicCommand(
    Guid TopicId, string Title, string Description, string Justification,
    TopicUrgency Urgency, IReadOnlyList<string> Streams, IReadOnlyList<string> Systems, IReadOnlyList<string> Tags)
    : IRequest;

public sealed class UpdateTopicValidator : AbstractValidator<UpdateTopicCommand>
{
    public UpdateTopicValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Urgency).IsInEnum();
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

        topic.SetUrgency(request.Urgency);
        topic.AssignStreams(request.Streams);
        topic.AssignSystems(request.Systems);
        topic.SetTags(request.Tags);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Topics.TopicUpdated", nameof(Topic), topic.PublicId.ToString(), ct: ct);
    }
}
