using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.SubmitMinutesForReview;

// W10: submit the draft for review. Draft → InReview. RBAC = Minutes.Capture (the author submits).
public sealed record SubmitMinutesForReviewCommand(Guid MinutesId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Secretary, AcmpRoles.Chairman };
}

public sealed class SubmitMinutesForReviewHandler : IRequestHandler<SubmitMinutesForReviewCommand>
{
    private readonly IMeetingsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public SubmitMinutesForReviewHandler(IMeetingsDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(SubmitMinutesForReviewCommand request, CancellationToken ct)
    {
        var minutes = await _db.Minutes.FirstOrDefaultAsync(m => m.PublicId == request.MinutesId, ct)
            ?? throw new KeyNotFoundException("Minutes not found.");

        minutes.SubmitForReview(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitAsync("Meetings.MinutesInReview", _user.UserId, new { minutes.PublicId, minutes.Key }, ct);
    }
}
