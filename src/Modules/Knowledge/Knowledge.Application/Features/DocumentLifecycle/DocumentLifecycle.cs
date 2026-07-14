using Acmp.Modules.Knowledge.Application.Abstractions;
using Acmp.Modules.Knowledge.Domain;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Knowledge.Application.Features.DocumentLifecycle;

// FR-116 lifecycle transitions (Publish: Draft → Published; Archive: Draft/Published → Archived). RBAC =
// Document.Manage. Each loads the document, applies the aggregate transition (which 409s an illegal transition
// via InvalidOperationException), saves, and audits. Status-only changes append no new version snapshot.

public sealed record PublishDocumentCommand(Guid DocumentId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Internal.KnowledgeRoles.DocumentManage;
}

public sealed record ArchiveDocumentCommand(Guid DocumentId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Internal.KnowledgeRoles.DocumentManage;
}

public sealed class PublishDocumentHandler : IRequestHandler<PublishDocumentCommand>
{
    private readonly IKnowledgeDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public PublishDocumentHandler(IKnowledgeDbContext db, IClock clock, IAuditSink audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(PublishDocumentCommand request, CancellationToken ct)
    {
        var document = await Load(_db, request.DocumentId, ct);
        document.Publish(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Knowledge.DocumentPublished", nameof(Document), document.PublicId.ToString(), ct: ct);
    }

    internal static async Task<Document> Load(IKnowledgeDbContext db, Guid id, CancellationToken ct) =>
        await db.Documents.FirstOrDefaultAsync(d => d.PublicId == id, ct)
        ?? throw new KeyNotFoundException("Document not found.");
}

public sealed class ArchiveDocumentHandler : IRequestHandler<ArchiveDocumentCommand>
{
    private readonly IKnowledgeDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public ArchiveDocumentHandler(IKnowledgeDbContext db, IClock clock, IAuditSink audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(ArchiveDocumentCommand request, CancellationToken ct)
    {
        var document = await PublishDocumentHandler.Load(_db, request.DocumentId, ct);
        document.Archive(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Knowledge.DocumentArchived", nameof(Document), document.PublicId.ToString(), ct: ct);
    }
}
