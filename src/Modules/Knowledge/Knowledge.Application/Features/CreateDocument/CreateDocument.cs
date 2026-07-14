using Acmp.Modules.Knowledge.Application.Abstractions;
using Acmp.Modules.Knowledge.Application.Contracts;
using Acmp.Modules.Knowledge.Application.Internal;
using Acmp.Modules.Knowledge.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Knowledge.Application.Features.CreateDocument;

// FR-116: author a new knowledge document in Draft. RBAC = Document.Manage (Chairman/Secretary effective). Title
// + Body are entered in one UI language and MIRRORED to both LocalizedString columns, so both EN+AR are
// required. Category (free-text) is required; Tags are optional. Version starts at 1 with a v1 snapshot (FR-117).
public sealed record CreateDocumentCommand(
    LocalizedString Title,
    string Category,
    LocalizedString Body,
    IReadOnlyList<string>? Tags) : IRequest<DocumentSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = KnowledgeRoles.DocumentManage;
}

public sealed class CreateDocumentValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentValidator()
    {
        RuleFor(x => x.Title).NotNull().WithMessage("A title is required.");
        RuleFor(x => x.Title!.En).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (EN) is required (max 512).");
        RuleFor(x => x.Title!.Ar).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (AR) is required (max 512).");

        RuleFor(x => x.Category).NotEmpty().MaximumLength(128).WithMessage("A category is required (max 128).");

        RuleFor(x => x.Body).NotNull().WithMessage("A document body is required.");
        RuleFor(x => x.Body!.En).NotEmpty().When(x => x.Body is not null).WithMessage("Body (EN) is required.");
        RuleFor(x => x.Body!.Ar).NotEmpty().When(x => x.Body is not null).WithMessage("Body (AR) is required.");
    }
}

public sealed class CreateDocumentHandler : IRequestHandler<CreateDocumentCommand, DocumentSummaryDto>
{
    private readonly IKnowledgeDbContext _db;
    private readonly IKnowledgeKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public CreateDocumentHandler(IKnowledgeDbContext db, IKnowledgeKeyGenerator keys, ICurrentUser user,
        IClock clock, IAuditSink audit)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<DocumentSummaryDto> Handle(CreateDocumentCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, _) = CurrentActor.Of(_user);

        var key = await _keys.NextDocumentKeyAsync(now.Year, ct);
        var document = Document.Create(key, request.Title, request.Category, request.Body, sub, request.Tags, now);

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Knowledge.DocumentCreated", nameof(Document), document.PublicId.ToString(), ct: ct);

        return KnowledgeMapping.ToSummary(document);
    }
}
