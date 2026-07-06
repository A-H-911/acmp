namespace Acmp.Modules.Traceability.Application.Contracts;

// Read models returned to the SPA. Enums (artifact/relationship type, direction) project as their string
// names — a stable wire contract the SPA localizes (the forward + inverse relationship labels are i18n keys,
// never English text on the wire, guardrail #9). "Other" is the far endpoint relative to the viewed artifact:
// for an Outgoing edge it is the Target, for an Incoming edge it is the Source. The deep-link key + title are
// the create-time snapshots carried on the edge (ADR-0019).

public sealed record RelationshipEdgeDto(
    Guid Id,
    string RelType,
    string Direction,
    string OtherType,
    Guid OtherId,
    string OtherKey,
    string OtherTitle,
    string? Notes);

// The artifact's traceability panel (docs/domain/search-and-traceability.md §6.1): its outgoing and incoming typed edges, one hop.
public sealed record ArtifactRelationshipsDto(
    IReadOnlyList<RelationshipEdgeDto> Outgoing,
    IReadOnlyList<RelationshipEdgeDto> Incoming);
