using Acmp.Modules.Knowledge.Application.Abstractions;
using Acmp.Modules.Knowledge.Application.Contracts;
using Acmp.Modules.Knowledge.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Knowledge.Application.Features.GetDocumentByKey;

// Document detail by display key (DOC-YYYY-###): the document fields plus its owned version history (in-module
// lookups over the knowledge schema — no cross-module read). Readable by any authenticated committee member
// (read-all).
public sealed record GetDocumentByKeyQuery(string Key) : IRequest<DocumentDetailDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetDocumentByKeyHandler : IRequestHandler<GetDocumentByKeyQuery, DocumentDetailDto?>
{
    private readonly IKnowledgeDbContext _db;

    public GetDocumentByKeyHandler(IKnowledgeDbContext db) => _db = db;

    public async Task<DocumentDetailDto?> Handle(GetDocumentByKeyQuery request, CancellationToken ct)
    {
        var document = await _db.Documents.AsNoTracking()
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Key == request.Key, ct);
        return document is null ? null : KnowledgeMapping.ToDetail(document);
    }
}
