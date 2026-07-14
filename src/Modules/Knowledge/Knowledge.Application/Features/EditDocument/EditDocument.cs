using Acmp.Modules.Knowledge.Application.Abstractions;
using Acmp.Modules.Knowledge.Application.Contracts;
using Acmp.Modules.Knowledge.Application.Internal;
using Acmp.Modules.Knowledge.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Knowledge.Application.Features.EditDocument;

// FR-117: revise a document's content. Allowed while Draft OR Published (rejected once Archived — a domain
// InvalidOperationException → 409). RBAC = Document.Manage. Both bilingual fields are required (mirrored EN+AR);
// the edit bumps Version and appends a new immutable version snapshot.
public sealed record EditDocumentCommand(
    Guid DocumentId,
    LocalizedString Title,
    string Category,
    LocalizedString Body) : IRequest<DocumentSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = KnowledgeRoles.DocumentManage;
}

public sealed class EditDocumentValidator : AbstractValidator<EditDocumentCommand>
{
    public EditDocumentValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Title).NotNull().WithMessage("A title is required.");
        RuleFor(x => x.Title!.En).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (EN) is required (max 512).");
        RuleFor(x => x.Title!.Ar).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (AR) is required (max 512).");
        RuleFor(x => x.Category).NotEmpty().MaximumLength(128).WithMessage("A category is required (max 128).");
        RuleFor(x => x.Body).NotNull().WithMessage("A document body is required.");
        RuleFor(x => x.Body!.En).NotEmpty().When(x => x.Body is not null).WithMessage("Body (EN) is required.");
        RuleFor(x => x.Body!.Ar).NotEmpty().When(x => x.Body is not null).WithMessage("Body (AR) is required.");
    }
}

public sealed class EditDocumentHandler : IRequestHandler<EditDocumentCommand, DocumentSummaryDto>
{
    private readonly IKnowledgeDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public EditDocumentHandler(IKnowledgeDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<DocumentSummaryDto> Handle(EditDocumentCommand request, CancellationToken ct)
    {
        var document = await _db.Documents.Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.PublicId == request.DocumentId, ct)
            ?? throw new KeyNotFoundException("Document not found.");

        var (sub, _) = CurrentActor.Of(_user);
        document.Edit(request.Title, request.Category, request.Body, _clock.UtcNow, sub);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Knowledge.DocumentEdited", nameof(Document), document.PublicId.ToString(), ct: ct);

        return KnowledgeMapping.ToSummary(document);
    }
}
