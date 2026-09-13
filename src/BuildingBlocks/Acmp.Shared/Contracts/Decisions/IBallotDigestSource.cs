namespace Acmp.Shared.Contracts.Decisions;

// Cross-module read seam (ADR-0001) for the notification digest (FR-134 / AC-161, DEC-188): the Notifications
// module asks "on how many OPEN votes is this member still awaited?" without reading the Decisions tables.
// Awaited = the member holds a ballot (eligibility is a ballot row) that is neither cast nor recused.
// Implemented in Decisions.Infrastructure.
public interface IBallotDigestSource
{
    Task<int> CountAwaitingAsync(string voterUserId, CancellationToken ct = default);
}
