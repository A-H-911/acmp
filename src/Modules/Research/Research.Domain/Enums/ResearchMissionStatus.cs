namespace Acmp.Modules.Research.Domain.Enums;

// Research mission lifecycle (P15a; FR-111). Proposed (author/revise) → Active (research under way) →
// Completed (findings + recommendations captured; terminal), with a side exit → Cancelled (reason recorded;
// terminal). Completed and Cancelled are terminal + immutable — no further edits or child mutations. Serialized
// on the wire as the string name (localized in the SPA). Convert-to-Topic and graph edges are P15c, not here.
public enum ResearchMissionStatus
{
    Proposed = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4,
}
