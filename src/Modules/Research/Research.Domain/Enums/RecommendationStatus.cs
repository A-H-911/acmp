namespace Acmp.Modules.Research.Domain.Enums;

// A recommendation's disposition (FR-113). Proposed → Accepted | Rejected; an Accepted recommendation may then
// be Converted (P15c / W16) once it has been promoted into an execution Topic. Converted is a display
// disposition — the authoritative one-per-recommendation guard is Topic.SourceRecommendationId's unique index.
public enum RecommendationStatus
{
    Proposed = 1,
    Accepted = 2,
    Rejected = 3,
    Converted = 4,
}
