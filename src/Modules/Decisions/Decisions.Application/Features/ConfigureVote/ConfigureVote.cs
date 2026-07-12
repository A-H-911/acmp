using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Modules.Decisions.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Decisions.Application.Features.ConfigureVote;

// W11 (configure): the Secretary (or Chairman) configures a ballot against a topic. Creates a Configured
// vote — still mutable-by-replacement (no field setters), locked once opened (AC-021). RBAC = Vote.Manage.
// Eligibility is seeded as one awaiting ballot per eligible voter (eligibility = "has a ballot row").
public sealed record ConfigureVoteCommand(
    Guid TopicId,
    Guid? MeetingId,
    IReadOnlyList<string> Options,
    bool AllowAbstain,
    int MinPresent,
    int MinCast,
    IReadOnlyList<VoteEligibleVoterRequest> EligibleVoters) : IRequest<VoteSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class ConfigureVoteValidator : AbstractValidator<ConfigureVoteCommand>
{
    public ConfigureVoteValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty().WithMessage("A topic is required.");
        RuleFor(x => x.Options).NotNull().Must(o => o is { Count: >= 2 })
            .WithMessage("A vote requires at least two options.");
        RuleForEach(x => x.Options).NotEmpty().WithMessage("An option code cannot be blank.");
        RuleFor(x => x.MinCast).GreaterThanOrEqualTo(1).WithMessage("The cast quorum (MinCast) must be at least 1.");
        RuleFor(x => x.MinPresent).GreaterThanOrEqualTo(0).WithMessage("The present quorum (MinPresent) cannot be negative.");
        RuleFor(x => x.EligibleVoters).NotNull().Must(v => v is { Count: > 0 })
            .WithMessage("A vote requires at least one eligible voter.");
        RuleForEach(x => x.EligibleVoters).ChildRules(v =>
            v.RuleFor(r => r.UserId).NotEmpty().WithMessage("An eligible voter requires a user id."));
    }
}

public sealed class ConfigureVoteHandler : IRequestHandler<ConfigureVoteCommand, VoteSummaryDto>
{
    private readonly IDecisionsDbContext _db;
    private readonly IDecisionKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public ConfigureVoteHandler(IDecisionsDbContext db, IDecisionKeyGenerator keys,
        ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<VoteSummaryDto> Handle(ConfigureVoteCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, _) = CurrentActor.Of(_user);

        var key = await _keys.NextVoteKeyAsync(now.Year, ct);
        var voters = (request.EligibleVoters ?? Array.Empty<VoteEligibleVoterRequest>())
            .Select(v => new VoteEligibleVoter(v.UserId, v.Name));

        var vote = Vote.Configure(key, request.TopicId, request.MeetingId, request.Options,
            request.AllowAbstain, new QuorumRule(request.MinPresent, request.MinCast), voters, sub, now);

        _db.Votes.Add(vote);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Decisions.VoteConfigured", nameof(Vote), vote.PublicId.ToString(), ct: ct);

        return VoteMapping.ToSummary(vote);
    }
}
