namespace Acmp.Modules.Knowledge.Domain.Enums;

// The artifact type a Template pre-fills at creation time (P15d-2; FR-119, P15h wiring). The value set is
// FR-119's exactly — {Topic, ADR, MoM, Research Mission} — NOT the domain-model §403 sketch {Topic, ADR, MoM,
// Document, Action}: the functional requirement is the contract, and §403 is illustrative (it even contradicts
// itself — §404 omits Action and neither line lists ResearchMission). Requirement wins (OWASP LLM01); OQ-051.
// Member spelling mirrors Traceability's ArtifactType (Adr, MinutesOfMeeting) so template + graph share one
// vocabulary. Serialized on the wire as the string name (localized in the SPA); stored as int. Adding a value
// later (e.g. Document/Action, if an FR ever lands) is additive — no migration (SQL Server has no native enum,
// EF adds no CHECK).
public enum TemplateTargetType
{
    Topic = 1,
    Adr = 2,
    MinutesOfMeeting = 3,
    ResearchMission = 4,
}
