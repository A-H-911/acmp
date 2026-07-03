using Acmp.Modules.Traceability.Application.Contracts;
using Acmp.Modules.Traceability.Domain;
using Acmp.Modules.Traceability.Domain.Enums;

namespace Acmp.Modules.Traceability.Application.Internal;

// Projects a stored edge into the panel row shape, from the perspective of the artifact whose panel is being
// viewed. Outgoing → the "other" endpoint is the Target; Incoming → the "other" endpoint is the Source.
internal static class RelationshipMapping
{
    public static RelationshipEdgeDto ToEdge(Relationship r, RelationshipDirection direction) =>
        direction == RelationshipDirection.Outgoing
            ? new RelationshipEdgeDto(r.PublicId, r.RelType.ToString(), direction.ToString(),
                r.TargetType.ToString(), r.TargetId, r.TargetKey, r.TargetTitle, r.Notes)
            : new RelationshipEdgeDto(r.PublicId, r.RelType.ToString(), direction.ToString(),
                r.SourceType.ToString(), r.SourceId, r.SourceKey, r.SourceTitle, r.Notes);
}
