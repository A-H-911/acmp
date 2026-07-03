namespace Acmp.Modules.Traceability.Domain.Enums;

// The kinds of governed artifact that can be an endpoint of a traceability edge (docs/30 §1.1). An edge
// stores the type + the artifact's PublicId + a display-key/title snapshot — never an EF navigation into the
// owning module (ADR-0001, ADR-0019). New artifact types join the graph by adding a value here; no schema
// change to the Relationship table. Serialized on the wire as the string name (localized in the SPA).
public enum ArtifactType
{
    Topic = 1,
    Meeting = 2,
    Agenda = 3,
    MinutesOfMeeting = 4,
    Vote = 5,
    Decision = 6,
    Action = 7,
    Risk = 8,
    Dependency = 9,
    Adr = 10,
    Invariant = 11,
    Diagram = 12,
    ResearchMission = 13,
    Finding = 14,
    Recommendation = 15,
    Document = 16,
}
