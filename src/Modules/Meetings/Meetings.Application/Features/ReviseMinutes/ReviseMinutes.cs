using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.ReviseMinutes;

// W10 (edit the draft): the Secretary curates the MoM body while it is Draft. Draft-only — the aggregate
// rejects a revise once Approved/Published (immutability, AC-036 → 409). RBAC = Minutes.Capture.
public sealed record ReviseMinutesCommand(Guid MinutesId, LocalizedString Summary)
    : IRequest<MinutesSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Secretary, AcmpRoles.Chairman };
}

public sealed class ReviseMinutesValidator : AbstractValidator<ReviseMinutesCommand>
{
    public ReviseMinutesValidator()
    {
        RuleFor(x => x.MinutesId).NotEmpty();
        RuleFor(x => x.Summary).NotNull().WithMessage("A summary is required.");
        RuleFor(x => x.Summary!.En).NotEmpty().When(x => x.Summary is not null).WithMessage("Summary (EN) is required.");
        RuleFor(x => x.Summary!.Ar).NotEmpty().When(x => x.Summary is not null).WithMessage("Summary (AR) is required.");
    }
}

public sealed class ReviseMinutesHandler : IRequestHandler<ReviseMinutesCommand, MinutesSummaryDto>
{
    private readonly IMeetingsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public ReviseMinutesHandler(IMeetingsDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<MinutesSummaryDto> Handle(ReviseMinutesCommand request, CancellationToken ct)
    {
        var minutes = await _db.Minutes.FirstOrDefaultAsync(m => m.PublicId == request.MinutesId, ct)
            ?? throw new KeyNotFoundException("Minutes not found.");

        minutes.Revise(request.Summary, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Meetings.MinutesRevised", _user.UserId, new { minutes.PublicId, minutes.Key }, ct);
        return MinutesMapping.ToSummary(minutes);
    }
}
