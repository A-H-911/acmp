using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Infrastructure.Integrity;

// D-13 / C-INS-02 (ADR-0030) — the Decisions module's own tamper check for the nightly integrity verifier:
// every sealed vote's per-ballot chain still verifies, and its frozen tally still matches its ballots. Reads
// only decisions-schema tables (ADR-0001); owned ballots load with the aggregate.
public sealed class VoteChainIntegrityCheck : IIntegrityCheck
{
    private readonly DecisionsDbContext _db;

    public VoteChainIntegrityCheck(DecisionsDbContext db) => _db = db;

    public string Name => "vote-ballot-chain";

    public async Task<IntegrityCheckResult> RunAsync(CancellationToken ct = default)
    {
        var sealedVotes = await _db.Votes.Where(v => v.ChainSealedAt != null).ToListAsync(ct);

        foreach (var vote in sealedVotes)
        {
            var chain = vote.VerifyBallotChain();
            if (!chain.IsValid)
                return IntegrityCheckResult.Broken(Name, sealedVotes.Count,
                    $"{vote.Key} ballot {chain.BrokenAtIndex}: {chain.Reason}");

            if (!vote.VerifyTally())
                return IntegrityCheckResult.Broken(Name, sealedVotes.Count,
                    $"{vote.Key} tally no longer matches its ballots");
        }

        return IntegrityCheckResult.Ok(Name, sealedVotes.Count);
    }
}
