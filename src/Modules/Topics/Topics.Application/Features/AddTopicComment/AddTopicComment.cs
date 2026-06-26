using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Features.AddTopicComment;

// BL-033: post a comment on a topic — any authenticated committee member (discussion is committee-wide).
// Immutable after post (enforced by the aggregate).
public sealed record AddTopicCommentCommand(Guid TopicId, string Body) : IRequest<Guid>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class AddTopicCommentValidator : AbstractValidator<AddTopicCommentCommand>
{
    public AddTopicCommentValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class AddTopicCommentHandler : IRequestHandler<AddTopicCommentCommand, Guid>
{
    private readonly ITopicsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public AddTopicCommentHandler(ITopicsDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Guid> Handle(AddTopicCommentCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.Include(t => t.Comments).FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        var (sub, name) = CurrentActor.Of(_user);
        topic.AddComment(request.Body, sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Topics.CommentAdded", sub, new { topic.PublicId, topic.Key }, ct);
        return topic.Comments.Last().PublicId;
    }
}
