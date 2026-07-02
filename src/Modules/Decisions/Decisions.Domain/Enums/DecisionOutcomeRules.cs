namespace Acmp.Modules.Decisions.Domain.Enums;

// Which decision outcomes carry follow-through, and therefore fall under the AC-029 downstream-link gate
// (FR-067, OQ-045; locked P8 fork, operator GO). Only affirmative/actionable outcomes require ≥1 downstream
// link before Issue; a Rejected/Deferred/etc. decision closes the topic with nothing to follow up, so it
// issues freely. Pure policy (no infra) so it is trivially unit-testable and lives next to the enum it reads.
public static class DecisionOutcomeRules
{
    // The five follow-up-bearing outcomes. Anything not in this set (Rejected, MoreInfoRequired,
    // FeedbackProvided, Deferred, Escalated, Converted) is exempt from the link requirement.
    public static bool RequiresDownstreamLink(DecisionOutcome outcome) => outcome switch
    {
        DecisionOutcome.Approved => true,
        DecisionOutcome.ConditionallyApproved => true,
        DecisionOutcome.EnhancementsRequired => true,
        DecisionOutcome.DesignChangesRequired => true,
        DecisionOutcome.ResearchRequired => true,
        _ => false,
    };
}
