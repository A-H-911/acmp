namespace Acmp.Modules.Research.Domain.Enums;

// A recommendation's disposition (FR-113). P15a allows Proposed → Accepted | Rejected only. The Converted
// status (promote a recommendation into a Topic/Action) is the P15c convert flow — deliberately absent here.
public enum RecommendationStatus
{
    Proposed = 1,
    Accepted = 2,
    Rejected = 3,
}
