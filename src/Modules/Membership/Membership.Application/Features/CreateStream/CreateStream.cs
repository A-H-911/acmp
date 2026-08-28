using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Implicit usings pull in System.IO, so the bare name is ambiguous. ⚠ The alias must NOT be used
// inside nameof(): nameof(DomainStream) yields "DomainStream" and would write a subject type that
// matches nothing into the audit trail. The qualified form below yields "Stream", which is the name
// every other audit row for this entity already uses.
using DomainStream = Acmp.Modules.Membership.Domain.Stream;

namespace Acmp.Modules.Membership.Application.Features.CreateStream;

// NFR-010's CONFIGURATION-DRIVEN clause (DW-063 / WBS-24.7). The no-hard-limit half already held —
// nothing in src caps the count and StreamCatalog projects whatever rows exist — but the five streams
// were seeded by raw SQL inside migration Membership_StreamTaxonomy_ADR0042 and Stream.Create had NO
// CALLER, so a sixth stream meant writing a migration and deploying. This is that missing caller.
//
// Administrator only, matching the sibling stream-assignment endpoint and SEC-178's `[Ad]` on screen
// 85. ⚠ A new stream is NEVER a wildcard: Stream.Create does not set IsWildcard, and a filtered unique
// index enforces at most one row that is — so ADR-0043's fail-closed bypass surface cannot be widened
// through this path by construction rather than by a check somebody could forget.
public sealed record CreateStreamCommand(string Code, string NameEn, string NameAr)
    : IRequest<Guid>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { nameof(CommitteeRole.Administrator) };
}

public sealed class CreateStreamValidator : AbstractValidator<CreateStreamCommand>
{
    public CreateStreamValidator()
    {
        // Bounds mirror the column definitions in StreamConfiguration; the database is the backstop,
        // not the message. The pattern keeps Code a URL- and claim-safe scope key, because it is what
        // topics carry and what the ABAC intersect resolves on — not a display string.
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64)
            .Matches("^[a-zA-Z0-9][a-zA-Z0-9-]*$")
            .WithMessage("Stream code may contain only letters, digits and hyphens, and must not start with a hyphen.");
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(128);
    }
}

public sealed class CreateStreamHandler : IRequestHandler<CreateStreamCommand, Guid>
{
    private readonly IMembershipDbContext _db;
    private readonly IAuditSink _audit;

    public CreateStreamHandler(IMembershipDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Guid> Handle(CreateStreamCommand request, CancellationToken ct)
    {
        // Stream.Create lowercases and trims, so the duplicate check must compare the NORMALISED code
        // — checking the raw input would let "Platform" through beside an existing "platform" and then
        // fail on the unique index as a 500 instead of a legible refusal.
        var code = request.Code.Trim().ToLowerInvariant();

        if (await _db.Streams.AnyAsync(s => s.Code == code, ct))
            throw new InvalidOperationException($"A stream with code '{code}' already exists.");

        var stream = DomainStream.Create(code, new LocalizedString(request.NameEn.Trim(), request.NameAr.Trim()));
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Membership.StreamCreated", nameof(Acmp.Modules.Membership.Domain.Stream),
            stream.PublicId.ToString(), ct: ct);

        return stream.PublicId;
    }
}
