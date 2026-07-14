using Acmp.Modules.Knowledge.Application.Abstractions;
using Acmp.Modules.Knowledge.Application.Internal;
using Acmp.Modules.Knowledge.Domain;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Knowledge.Application.Features.DeprecateTemplate;

// FR-119: retire a template (Active → Deprecated; terminal). FR-119 says "delete" — realised as a soft Deprecate
// (retention is permanent). RBAC = Template.Manage. An already-Deprecated template 409s (domain
// InvalidOperationException). Status-only change — no version bump.
public sealed record DeprecateTemplateCommand(Guid TemplateId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = KnowledgeRoles.TemplateManage;
}

public sealed class DeprecateTemplateHandler : IRequestHandler<DeprecateTemplateCommand>
{
    private readonly IKnowledgeDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public DeprecateTemplateHandler(IKnowledgeDbContext db, IClock clock, IAuditSink audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(DeprecateTemplateCommand request, CancellationToken ct)
    {
        var template = await _db.Templates.FirstOrDefaultAsync(t => t.PublicId == request.TemplateId, ct)
            ?? throw new KeyNotFoundException("Template not found.");

        template.Deprecate(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitEnrichedAsync("Knowledge.TemplateDeprecated", nameof(Template), template.PublicId.ToString(), ct: ct);
    }
}
