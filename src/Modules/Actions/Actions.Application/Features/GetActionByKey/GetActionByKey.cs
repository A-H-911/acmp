using Acmp.Modules.Actions.Application.Abstractions;
using Acmp.Modules.Actions.Application.Contracts;
using Acmp.Modules.Actions.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Actions.Application.Features.GetActionByKey;

// Action detail by display key (ACT-YYYY-###): owner, due date, progress, source link, and the
// completion/verification stamps. Readable by any authenticated committee member (read-all). IsOverdue is
// derived against the request clock.
public sealed record GetActionByKeyQuery(string Key) : IRequest<ActionDetailDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetActionByKeyHandler : IRequestHandler<GetActionByKeyQuery, ActionDetailDto?>
{
    private readonly IActionsDbContext _db;
    private readonly IClock _clock;

    public GetActionByKeyHandler(IActionsDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ActionDetailDto?> Handle(GetActionByKeyQuery request, CancellationToken ct)
    {
        var action = await _db.Actions.AsNoTracking().FirstOrDefaultAsync(a => a.Key == request.Key, ct);
        return action is null ? null : ActionMapping.ToDetail(action, _clock.UtcNow);
    }
}
