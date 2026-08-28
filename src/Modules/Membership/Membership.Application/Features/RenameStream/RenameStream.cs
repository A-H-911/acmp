using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.RenameStream;

// SEC-178's "edit inline" half of screen 85, and the other side of NFR-010's configuration-driven
// clause: the committee changes a stream's display text without a migration.
//
// ⚠ ONLY THE BILINGUAL NAME CHANGES. The CODE is not editable here and that is deliberate — topics
// carry stream codes and the ABAC intersect resolves on them, so re-coding a live stream would
// silently re-scope every topic naming the old value. That is a data migration, not an inline edit,
// and it must not be reachable from a text field on an admin screen.
//
// The wildcard row is renameable like any other: its bypass behaviour is carried by the IsWildcard
// COLUMN, never by its name or code (Stream.cs), so display text is free to change without touching
// what ADR-0043 relies on.
public sealed record RenameStreamCommand(Guid PublicId, string NameEn, string NameAr)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { nameof(CommitteeRole.Administrator) };
}

public sealed class RenameStreamValidator : AbstractValidator<RenameStreamCommand>
{
    public RenameStreamValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(128);
    }
}

public sealed class RenameStreamHandler : IRequestHandler<RenameStreamCommand>
{
    private readonly IMembershipDbContext _db;
    private readonly IAuditSink _audit;

    public RenameStreamHandler(IMembershipDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task Handle(RenameStreamCommand request, CancellationToken ct)
    {
        var stream = await _db.Streams.FirstOrDefaultAsync(s => s.PublicId == request.PublicId, ct)
            ?? throw new KeyNotFoundException("Stream not found.");

        stream.Rename(new LocalizedString(request.NameEn.Trim(), request.NameAr.Trim()));
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Membership.StreamRenamed",
            nameof(Acmp.Modules.Membership.Domain.Stream), request.PublicId.ToString(), ct: ct);
    }
}
