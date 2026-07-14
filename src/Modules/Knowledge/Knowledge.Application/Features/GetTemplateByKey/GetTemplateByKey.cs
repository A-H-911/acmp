using Acmp.Modules.Knowledge.Application.Abstractions;
using Acmp.Modules.Knowledge.Application.Contracts;
using Acmp.Modules.Knowledge.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Knowledge.Application.Features.GetTemplateByKey;

// Template detail by display key (TPL-YYYY-###): the template fields incl. its Markdown Body (in-module lookup
// over the knowledge schema — no cross-module read). Readable by any authenticated committee member (read-all).
public sealed record GetTemplateByKeyQuery(string Key) : IRequest<TemplateDetailDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetTemplateByKeyHandler : IRequestHandler<GetTemplateByKeyQuery, TemplateDetailDto?>
{
    private readonly IKnowledgeDbContext _db;

    public GetTemplateByKeyHandler(IKnowledgeDbContext db) => _db = db;

    public async Task<TemplateDetailDto?> Handle(GetTemplateByKeyQuery request, CancellationToken ct)
    {
        var template = await _db.Templates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Key == request.Key, ct);
        return template is null ? null : KnowledgeMapping.ToDetail(template);
    }
}
