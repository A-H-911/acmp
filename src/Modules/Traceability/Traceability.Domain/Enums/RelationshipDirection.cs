namespace Acmp.Modules.Traceability.Domain.Enums;

// A read-time perspective, not a stored column: relative to the artifact whose panel is being viewed, an edge
// is Outgoing (the artifact is the Source) or Incoming (the artifact is the Target). docs/30 §6.1 groups the
// panel by this. Serialized as its string name for the SPA to render the direction glyph + inverse label.
public enum RelationshipDirection
{
    Outgoing = 1,
    Incoming = 2,
}
