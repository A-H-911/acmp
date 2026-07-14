using Acmp.Modules.Knowledge.Application.Abstractions;
using Acmp.Modules.Knowledge.Application.Contracts;
using Acmp.Modules.Knowledge.Application.Internal;
using Acmp.Modules.Knowledge.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Knowledge.Application.Features.EditTemplate;

// FR-119: revise a template's Name + Body (TargetType is immutable). Rejected once Deprecated (a domain
// InvalidOperationException → 409). RBAC = Template.Manage. Both bilingual name fields are required; the edit
// bumps Version (a plain counter — no snapshot history, unlike a wiki Document).
public sealed record EditTemplateCommand(
    Guid TemplateId,
    LocalizedString Name,
    string Body) : IRequest<TemplateSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = KnowledgeRoles.TemplateManage;
}

public sealed class EditTemplateValidator : AbstractValidator<EditTemplateCommand>
{
    public EditTemplateValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.Name).NotNull().WithMessage("A name is required.");
        RuleFor(x => x.Name!.En).NotEmpty().MaximumLength(256).When(x => x.Name is not null).WithMessage("Name (EN) is required (max 256).");
        RuleFor(x => x.Name!.Ar).NotEmpty().MaximumLength(256).When(x => x.Name is not null).WithMessage("Name (AR) is required (max 256).");
        RuleFor(x => x.Body).NotEmpty().WithMessage("A template body is required.");
    }
}

public sealed class EditTemplateHandler : IRequestHandler<EditTemplateCommand, TemplateSummaryDto>
{
    private readonly IKnowledgeDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public EditTemplateHandler(IKnowledgeDbContext db, IClock clock, IAuditSink audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task<TemplateSummaryDto> Handle(EditTemplateCommand request, CancellationToken ct)
    {
        var template = await _db.Templates.FirstOrDefaultAsync(t => t.PublicId == request.TemplateId, ct)
            ?? throw new KeyNotFoundException("Template not found.");

        template.Edit(request.Name, request.Body, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Knowledge.TemplateEdited", nameof(Template), template.PublicId.ToString(), ct: ct);

        return KnowledgeMapping.ToSummary(template);
    }
}
