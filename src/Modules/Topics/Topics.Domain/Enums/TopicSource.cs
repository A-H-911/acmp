namespace Acmp.Modules.Topics.Domain.Enums;

// Submitter channel — informational, not a workflow driver (docs/domain/topic-taxonomy.md §B.3). Defaults to CommitteeMember
// at submission; the Secretary may adjust during triage. Used for reporting/filtering and attribution.
public enum TopicSource
{
    CommitteeMember = 1,
    StreamRequest = 2,
    UrgentOrgNeed = 3,
    OperationalIncident = 4,
    SecurityFinding = 5,
    Modernization = 6,
    InnovationInitiative = 7,
    CrossStreamProblem = 8,
    Regulatory = 9,
    External = 10,
}
