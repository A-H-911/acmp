using Acmp.Modules.Knowledge.Application.Abstractions;
using Acmp.Modules.Knowledge.Application.Contracts;
using Acmp.Modules.Knowledge.Application.Internal;
using Acmp.Modules.Knowledge.Domain;
using Acmp.Modules.Knowledge.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Knowledge.Application.Features.CreateTemplate;

// FR-119: author a new reusable template (Active). RBAC = Template.Manage (Chairman/Secretary/Administrator). Name
// is entered in one UI language and MIRRORED to both LocalizedString columns, so both EN+AR are required. Body is
// a single Markdown string (placeholder fields). TargetType is fixed at creation. Version starts at 1.
public sealed record CreateTemplateCommand(
    LocalizedString Name,
    TemplateTargetType TargetType,
    string Body) : IRequest<TemplateSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = KnowledgeRoles.TemplateManage;
}

public sealed class CreateTemplateValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateValidator()
    {
        RuleFor(x => x.Name).NotNull().WithMessage("A name is required.");
        RuleFor(x => x.Name!.En).NotEmpty().MaximumLength(256).When(x => x.Name is not null).WithMessage("Name (EN) is required (max 256).");
        RuleFor(x => x.Name!.Ar).NotEmpty().MaximumLength(256).When(x => x.Name is not null).WithMessage("Name (AR) is required (max 256).");
        RuleFor(x => x.TargetType).IsInEnum().WithMessage("A valid target type is required.");
        RuleFor(x => x.Body).NotEmpty().WithMessage("A template body is required.");
    }
}

public sealed class CreateTemplateHandler : IRequestHandler<CreateTemplateCommand, TemplateSummaryDto>
{
    private readonly IKnowledgeDbContext _db;
    private readonly IKnowledgeKeyGenerator _keys;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public CreateTemplateHandler(IKnowledgeDbContext db, IKnowledgeKeyGenerator keys, IClock clock, IAuditSink audit)
    {
        _db = db;
        _keys = keys;
        _clock = clock;
        _audit = audit;
    }

    public async Task<TemplateSummaryDto> Handle(CreateTemplateCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var key = await _keys.NextTemplateKeyAsync(now.Year, ct);
        var template = Template.Create(key, request.Name, request.TargetType, request.Body, now);

        _db.Templates.Add(template);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Knowledge.TemplateCreated", nameof(Template), template.PublicId.ToString(), ct: ct);

        return KnowledgeMapping.ToSummary(template);
    }
}
