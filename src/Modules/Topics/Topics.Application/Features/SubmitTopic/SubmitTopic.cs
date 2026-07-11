using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Topics.Application.Features.SubmitTopic;

// W1: submit a new topic for triage (AC-030). Creates the Draft and submits it in one action ("Submit
// for triage"). Submitter attribution = the current principal. Roles per Policies.TopicSubmit (docs/domain/permission-role-matrix.md).
public sealed record SubmitTopicCommand(
    string Title, string Description, string Justification,
    TopicType Type, TopicUrgency Urgency, TopicSource Source,
    IReadOnlyList<string> Streams, IReadOnlyList<string> Systems, IReadOnlyList<string> Tags)
    : IRequest<SubmitTopicResult>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[]
    {
        AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Member, AcmpRoles.Reviewer, AcmpRoles.Submitter,
    };
}

public sealed record SubmitTopicResult(Guid Id, string Key);

public sealed class SubmitTopicValidator : AbstractValidator<SubmitTopicCommand>
{
    public SubmitTopicValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.Justification).NotEmpty();
        RuleFor(x => x.Streams).NotEmpty().WithMessage("At least one affected stream is required.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Urgency).IsInEnum();
        RuleFor(x => x.Source).IsInEnum();
    }
}

public sealed class SubmitTopicHandler : IRequestHandler<SubmitTopicCommand, SubmitTopicResult>
{
    private readonly ITopicsDbContext _db;
    private readonly ITopicKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public SubmitTopicHandler(ITopicsDbContext db, ITopicKeyGenerator keys, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<SubmitTopicResult> Handle(SubmitTopicCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, name) = CurrentActor.Of(_user);
        var key = await _keys.NextAsync(now.Year, ct);

        var topic = Topic.Draft(key, request.Title, request.Description, request.Justification,
            request.Type, request.Urgency, request.Source, sub, name,
            request.Streams, request.Systems, request.Tags);
        topic.Submit(now);

        _db.Topics.Add(topic);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Topics.TopicSubmitted", nameof(Topic), topic.PublicId.ToString(), ct: ct);
        return new SubmitTopicResult(topic.PublicId, topic.Key);
    }
}
