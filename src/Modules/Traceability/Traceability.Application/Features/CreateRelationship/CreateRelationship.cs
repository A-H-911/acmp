using Acmp.Modules.Traceability.Application.Abstractions;
using Acmp.Modules.Traceability.Application.Internal;
using Acmp.Modules.Traceability.Domain;
using Acmp.Modules.Traceability.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Traceability.Application.Features.CreateRelationship;

// AC-063: a Secretary/Chairman creates an explicit typed edge between two artifacts. Both endpoints carry a
// display-key + title snapshot (captured from what the creator was viewing) so the panel can render + deep-link
// without reading the owning modules' tables (ADR-0001, ADR-0019). RBAC = Traceability.Link (Chairman/Secretary;
// the topic-Owner AiO create path is deferred — ASM-P10c-4). The create is audited (Relationship.Created,
// docs/domain/search-and-traceability.md §5, guardrail #5).
public sealed record CreateRelationshipCommand(
    ArtifactType SourceType, Guid SourceId, string SourceKey, string SourceTitle,
    ArtifactType TargetType, Guid TargetId, string TargetKey, string TargetTitle,
    RelationshipType RelType, string? Notes) : IRequest<Guid>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class CreateRelationshipValidator : AbstractValidator<CreateRelationshipCommand>
{
    public CreateRelationshipValidator()
    {
        RuleFor(x => x.SourceType).IsInEnum();
        RuleFor(x => x.TargetType).IsInEnum();
        RuleFor(x => x.RelType).IsInEnum().WithMessage("A valid relationship type is required.");
        RuleFor(x => x.SourceId).NotEmpty().WithMessage("A source artifact is required.");
        RuleFor(x => x.TargetId).NotEmpty().WithMessage("A target artifact is required.");
        RuleFor(x => x.SourceKey).NotEmpty().MaximumLength(32);
        RuleFor(x => x.TargetKey).NotEmpty().MaximumLength(32);
        RuleFor(x => x.SourceTitle).NotEmpty().MaximumLength(512);
        RuleFor(x => x.TargetTitle).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Notes).MaximumLength(1000);

        // No self-loop: an artifact cannot be related to itself (defence-in-depth mirror of the domain guard).
        RuleFor(x => x).Must(c => !(c.SourceType == c.TargetType && c.SourceId == c.TargetId))
            .WithMessage("A relationship cannot link an artifact to itself.");
    }
}

public sealed class CreateRelationshipHandler : IRequestHandler<CreateRelationshipCommand, Guid>
{
    private readonly ITraceabilityDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;

    public CreateRelationshipHandler(ITraceabilityDbContext db, ICurrentUser user, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _audit = audit;
    }

    public async Task<Guid> Handle(CreateRelationshipCommand request, CancellationToken ct)
    {
        var (sub, _) = CurrentActor.Of(_user);

        var edge = Relationship.Create(
            request.SourceType, request.SourceId, request.SourceKey, request.SourceTitle,
            request.TargetType, request.TargetId, request.TargetKey, request.TargetTitle,
            request.RelType, request.Notes);

        _db.Relationships.Add(edge);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Relationship.Created", sub, new
        {
            edge.PublicId,
            SourceType = edge.SourceType.ToString(),
            edge.SourceId,
            edge.SourceKey,
            TargetType = edge.TargetType.ToString(),
            edge.TargetId,
            edge.TargetKey,
            RelType = edge.RelType.ToString(),
        }, ct);

        return edge.PublicId;
    }
}
