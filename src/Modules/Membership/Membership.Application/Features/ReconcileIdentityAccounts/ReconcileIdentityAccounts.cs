using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Internal;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.ReconcileIdentityAccounts;

// DEC-046 d2 / DEF-065 / DEF-071 — give the accounts that were seeded straight into Keycloak a
// committee_members row, WITH the wildcard stream, before stream scope starts refusing them.
//
// WHY THIS EXISTS AT ALL. A committee_members row is created by exactly two paths: JIT provisioning
// on first login (ADR-0004) and MemberInvitation.InviteAsync. A Keycloak account is neither, so the
// 26 accounts seeded by seed-users.sh held ONE member row between them at the last live observation
// (DEF-038, measured 2026-08-10). ADR-0043's step-5 backfill assigns the wildcard to members holding
// no stream — and a migration can only reach rows that EXIST when it runs, so in production it
// reaches that one row and cannot see the other ~25 (DEF-065).
//
// ⚠ WHY IT MUST ASSIGN THE WILDCARD ITSELF, WHICH IS THE HALF DEC-046 DID NOT SPELL OUT (DEF-071).
// The decision says to reconcile "before the deploy", but reconciliation is an application feature,
// so it runs on a host that is already up — and Program.cs applies migrations at BOOT. The deploy
// carrying this therefore runs the step-5 backfill FIRST, against the same ~1 row. Creating the
// member rows afterwards and leaving assignment to a later administrative step would produce ~25
// rows holding ZERO streams: DEF-065's exact outcome, reached by another route. So creating the row
// and granting the wildcard are ONE operation here, or this command is inert.
//
// ⚠ AND ONLY FOR ROWS IT CREATES. An administrator may have deliberately narrowed someone to a
// single stream; widening that on a reconciliation run would silently hand them universal write
// access. The step-5 backfill encodes the same asymmetry with its NOT EXISTS clause.
//
// ⚠ THE RESIDUAL, STATED RATHER THAN ENGINEERED AROUND: a member who signs in BETWEEN the deploy and
// this run gets a JIT row with no streams, and this command will not touch it — it is no longer a
// row this run creates. That person falls back to ADR-0043 clause (2): the roster shows them
// unassigned and an Administrator assigns streams. Production has one sign-in in its history, so the
// window is minutes wide and its cost is one refusal.
public sealed record ReconcileIdentityAccountsCommand : IRequest<IdentityReconciliationResult>, IAuthorizedRequest
{
    // Administrator only — the same gate as PUT /api/members/{id}/streams (permission-role-matrix
    // row 27, Admin.Users / SoD-5), because this writes exactly what that endpoint writes.
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { nameof(CommitteeRole.Administrator) };
}

/// <summary>
/// What the run did, as a PARTITION of the accounts the identity provider returned.
/// </summary>
/// <remarks>
/// Every account lands in exactly one bucket and the buckets sum to <paramref name="IdentityAccounts"/>.
/// That is a deliberate property rather than tidiness: a single "created" count cannot be told apart
/// from a run that silently skipped half the realm, and a control that detects without telling is the
/// failure mode this project has recorded nine times.
/// </remarks>
public sealed record IdentityReconciliationResult(
    int IdentityAccounts,
    int Created,
    int AlreadyProvisioned,
    int SkippedDisabled,
    int SkippedNoCommitteeRole,
    int SkippedDuplicateEmail);

public sealed class ReconcileIdentityAccountsHandler
    : IRequestHandler<ReconcileIdentityAccountsCommand, IdentityReconciliationResult>
{
    private readonly IMembershipDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    // IEnumerable for the same reason ExpireGuestAccessHandler uses one: IIdentityProvider is
    // registered ONLY when KeycloakAdmin is configured (ADR-0038), and demanding it here would make
    // the whole host unbootable wherever in-app user management is off — which is every environment
    // today. Unlike the guest sweep, an absent provider does not degrade this to a partial run: there
    // is nothing to reconcile against, so it refuses and says why.
    private readonly IEnumerable<IIdentityProvider> _identity;

    public ReconcileIdentityAccountsHandler(
        IMembershipDbContext db, IClock clock, IAuditSink audit, IEnumerable<IIdentityProvider> identity)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _identity = identity;
    }

    public async Task<IdentityReconciliationResult> Handle(
        ReconcileIdentityAccountsCommand request, CancellationToken ct)
    {
        var identity = _identity.FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Reconciliation cannot run because no identity provider is configured, so the accounts to " +
                "reconcile cannot be read. Set KeycloakAdmin:Enabled=true AND pin a real " +
                "KeycloakAdmin:ClientSecret — enabling the feature is two variables, not one (DEC-047).");

        var accounts = await identity.ListUsersAsync(ct);

        var provisioned = await _db.Members
            .Select(m => new { m.KeycloakUserId, m.Email })
            .ToListAsync(ct);
        var knownSubjects = provisioned.Select(m => m.KeycloakUserId).ToHashSet(StringComparer.Ordinal);
        var knownEmails = provisioned
            .Where(m => m.Email.Length > 0)
            .Select(m => m.Email)
            .ToHashSet(StringComparer.Ordinal);

        var pending = new List<(IdentityAccount Account, CommitteeRole Role)>();
        int alreadyProvisioned = 0, skippedDisabled = 0, skippedNoRole = 0, skippedDuplicateEmail = 0;

        foreach (var account in accounts)
        {
            // Ordered so the buckets partition: the FIRST reason an account is not created is the one
            // reported, and every account reaches exactly one of them.
            if (knownSubjects.Contains(account.SubjectId))
            {
                alreadyProvisioned++;
                continue;
            }

            // An account that cannot sign in gets no roster line. Reported rather than filtered out
            // silently, because "we saw it and left it" and "we never saw it" look identical in a
            // count that only reports what it created.
            if (!account.Enabled)
            {
                skippedDisabled++;
                continue;
            }

            // The service account and the bootstrap admin land here, which is correct — neither is a
            // committee member. ProvisionCurrentUser refuses the same principals on the same test, so
            // an account skipped here is one that could not have used the application anyway.
            var role = CommitteeRoleResolver.PrimaryRole(account.RealmRoles);
            if (role is null)
            {
                skippedNoRole++;
                continue;
            }

            // Email is uniquely indexed where non-empty, so a collision would throw at SaveChanges and
            // abandon the WHOLE run. Skipping it keeps the other accounts reconciled and names the
            // count. `Add` returning false also catches a duplicate WITHIN the realm listing, which is
            // the shape DEF-045's duplicate identities took.
            var email = account.Email.Trim().ToLowerInvariant();
            if (email.Length > 0 && !knownEmails.Add(email))
            {
                skippedDuplicateEmail++;
                continue;
            }

            pending.Add((account, role.Value));
        }

        var created = new List<CommitteeMember>(pending.Count);
        if (pending.Count > 0)
        {
            // Resolved BEFORE anything is written, and only when there is something to write. Failing
            // here leaves no half-reconciled state; checking unconditionally would stop a run that had
            // nothing to do, which trains the operator to ignore the message.
            var wildcardId = await _db.Streams
                .Where(s => s.IsWildcard)
                .Select(s => s.Id)
                .FirstOrDefaultAsync(ct);

            // The same failure the step-5 backfill throws on, for the same reason: the ADR-0042 seed
            // skips a code that already exists, so a database that already carried an 'all-streams'
            // row has NO wildcard. Creating the rows anyway would produce exactly the locked-out
            // members this command exists to prevent, while reporting success.
            if (wildcardId == 0)
                throw new InvalidOperationException(
                    "Reconciliation stopped before writing anything: no membership.streams row has " +
                    "IsWildcard = 1, so there is no wildcard to grant and every row created here would " +
                    "be refused every stream-scoped write. Insert the wildcard row and re-run.");

            var now = _clock.UtcNow;
            foreach (var (account, role) in pending)
            {
                // PreRegister, not Provision: these accounts exist in Keycloak and have never signed
                // in, which is exactly the Invited state. First login flips it to Active through the
                // existing SyncFromClaims path — the subject id is known here, so nothing is guessed
                // and there is no second identity to reconcile later.
                var member = CommitteeMember.PreRegister(
                    account.SubjectId, account.FullName, account.Email, role, now);
                member.AssignStreams(new[] { wildcardId });
                _db.Members.Add(member);
                created.Add(member);
            }

            await _db.SaveChangesAsync(ct);

            // After the save, matching MemberInvitation.InviteAsync: the audit row describes a member
            // that exists. A distinct verb from MemberProvisioned/UserInvited because this is a
            // distinct governance event — nobody invited these people and nobody signed in.
            foreach (var member in created)
            {
                await _audit.EmitEnrichedAsync(
                    "Membership.AccountReconciled", nameof(CommitteeMember), member.PublicId.ToString(), ct: ct);
            }
        }

        return new IdentityReconciliationResult(
            accounts.Count, created.Count, alreadyProvisioned,
            skippedDisabled, skippedNoRole, skippedDuplicateEmail);
    }
}
