using Acmp.Modules.Dependencies.Application.Abstractions;
using Acmp.Modules.Dependencies.Application.Internal;
using Acmp.Modules.Dependencies.Domain;
using Acmp.Modules.Dependencies.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;

namespace Acmp.Modules.Dependencies.Application.Features.CreateDependency;

// A Secretary/Chairman creates an explicit typed dependency between two artifacts. Both endpoints carry a
// display-key + title snapshot (captured from what the creator was viewing) so the panel/register can render
// + deep-link without reading the owning modules' tables (ADR-0001, ADR-0019). RBAC = Dependency.Create
// (Chairman/Secretary; the endpoint policy is the true gate). The create is audited (Dependency.Created).
public sealed record CreateDependencyCommand(
    DependencyEndpointType FromType, Guid FromId, string FromKey, string FromTitle,
    DependencyEndpointType ToType, Guid ToId, string ToKey, string ToTitle,
    DependencyKind Kind, string? Note) : IRequest<string>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class CreateDependencyValidator : AbstractValidator<CreateDependencyCommand>
{
    public CreateDependencyValidator()
    {
        RuleFor(x => x.FromType).IsInEnum();
        RuleFor(x => x.ToType).IsInEnum();
        RuleFor(x => x.Kind).IsInEnum().WithMessage("A valid dependency kind is required.");
        RuleFor(x => x.FromId).NotEmpty().WithMessage("A source artifact is required.");
        RuleFor(x => x.ToId).NotEmpty().WithMessage("A target artifact is required.");
        RuleFor(x => x.FromKey).NotEmpty().MaximumLength(32);
        RuleFor(x => x.ToKey).NotEmpty().MaximumLength(32);
        RuleFor(x => x.FromTitle).NotEmpty().MaximumLength(512);
        RuleFor(x => x.ToTitle).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Note).MaximumLength(1000);

        // No self-loop: an artifact cannot depend on itself (defence-in-depth mirror of the domain guard).
        RuleFor(x => x).Must(c => !(c.FromType == c.ToType && c.FromId == c.ToId))
            .WithMessage("A dependency cannot link an artifact to itself.");
    }
}

public sealed class CreateDependencyHandler : IRequestHandler<CreateDependencyCommand, string>
{
    private readonly IDependenciesDbContext _db;
    private readonly IDependencyKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public CreateDependencyHandler(IDependenciesDbContext db, IDependencyKeyGenerator keys,
        ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<string> Handle(CreateDependencyCommand request, CancellationToken ct)
    {
        var (sub, _) = CurrentActor.Of(_user);
        var key = await _keys.NextDependencyKeyAsync(_clock.UtcNow.Year, ct);

        var edge = Dependency.Create(
            key,
            request.FromType, request.FromId, request.FromKey, request.FromTitle,
            request.ToType, request.ToId, request.ToKey, request.ToTitle,
            request.Kind, request.Note);

        _db.Dependencies.Add(edge);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Dependency.Created", nameof(Dependency), edge.PublicId.ToString(), ct: ct);

        return edge.Key;
    }
}
