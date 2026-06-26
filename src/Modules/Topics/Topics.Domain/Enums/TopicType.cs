namespace Acmp.Modules.Topics.Domain.Enums;

// Canonical Topic Type taxonomy (docs/09 §A, README §D) — deliberately exactly four. Type drives the
// required template, the default triage workflow, and the SLA urgency thresholds. Subtypes are tags,
// never types (docs/09 §F). The design file shows three sample cards; the fourth (EnhancementInnovation)
// is mandated by the authoritative taxonomy — a data difference, not a design drift (guardrail 14).
public enum TopicType
{
    ResearchDiscovery = 1,
    ArchitectureDecision = 2,
    EnhancementInnovation = 3,
    GovernanceStandardization = 4,
}
