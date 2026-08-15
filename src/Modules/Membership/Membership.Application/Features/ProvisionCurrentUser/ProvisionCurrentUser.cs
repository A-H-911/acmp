using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Internal;
using Acmp.Modules.Membership.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.ProvisionCurrentUser;

// Just-in-time provisioning of the caller's local profile from Keycloak claims (ADR-0004). The SPA
// calls this on login (POST /api/members/me). Authenticated + a resolvable canonical role only;
// a validated identity with no recognised role is denied (AC-003 deny path, post-authentication).
public sealed record ProvisionCurrentUserCommand : IRequest<MemberProfileDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed record MemberProfileDto(
    Guid PublicId, string FullName, string Email, string Role, IReadOnlyList<string> Roles, bool IsVotingEligible);

public sealed class ProvisionCurrentUserHandler : IRequestHandler<ProvisionCurrentUserCommand, MemberProfileDto>
{
    private readonly IMembershipDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public ProvisionCurrentUserHandler(IMembershipDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<MemberProfileDto> Handle(ProvisionCurrentUserCommand request, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || string.IsNullOrEmpty(_user.UserId))
            throw new UnauthorizedAccessException("Authentication required.");

        // AC-003 — "the system denies access ... and an AuthEvent is emitted to the audit log".
        //
        // ⚠ EMITTED HERE, AND IT CANNOT BE THE DEF-056 MIDDLEWARE'S JOB. That handler records refusals
        // made by the ASP.NET authorization layer, and this one is not: the request passes the policy
        // (this endpoint carries no capability policy — a caller must be able to provision themselves
        // before they have any role at all), reaches the handler, and is refused HERE because the token
        // carries no ACMP role claim to resolve. AV-159 named this "DEF-056's family arriving by a
        // second route", and a single fix for both would have left this clause quietly unmet.
        //
        // FAIL-CLOSED IS THE CHOSEN BRANCH: the AC offers deny OR a configured minimum default role,
        // and there is no configurable default and no Submitter fallback in this system. Denying is
        // the safer of the two branches the AC permits.
        var sub = _user.UserId!;

        var resolved = CommitteeRoleResolver.PrimaryRole(_user.Roles);
        if (resolved is null)
        {
            await _audit.EmitAsync("Authorization.Forbidden", sub,
                new { request = nameof(ProvisionCurrentUserCommand), reason = "no-committee-role", held = _user.Roles },
                ct);
            throw new ForbiddenAccessException("No committee role is assigned to this account.");
        }

        var role = resolved.Value;
        var displayName = _user.DisplayName ?? _user.UserName ?? _user.Email ?? sub;
        var email = _user.Email ?? string.Empty;

        var member = await _db.Members.FirstOrDefaultAsync(m => m.KeycloakUserId == sub, ct);
        var created = member is null;
        var changed = false;
        if (member is null)
        {
            member = CommitteeMember.Provision(sub, displayName, email, role, _clock.UtcNow);
            _db.Members.Add(member);
        }
        else
        {
            changed = member.SyncFromClaims(displayName, email, role);
        }

        // DEF-075 — TWO CONCURRENT FIRST-LOGINS FOR ONE SUBJECT BOTH REACH THE INSERT. The existence
        // check above and this write are not atomic, so two requests that both find no row both Add,
        // and the unique index IX_committee_members_KeycloakUserId refuses the loser. That surfaced as
        // a 500 on an endpoint the SPA and the e2e helpers both document as IDEMPOTENT, and it can
        // only ever happen on a subject's FIRST provisioning — every later call takes the sync path
        // below, where nothing is inserted.
        //
        // ⚠ THE LOSER'S RE-READ IS GUARANTEED TO FIND THE WINNER, and that is why this recovers rather
        // than retries blindly. SQL Server does not fail the second insert immediately: it BLOCKS on
        // the winner's exclusive key lock and only raises the duplicate once that transaction has
        // COMMITTED. So by the time we are in this catch, the winning row exists and is committed, and
        // a read in our own transaction sees it under READ COMMITTED.
        //
        // ⚠ AND THE TRANSACTION IS STILL USABLE, which is the fact the whole shape depends on: a unique
        // violation aborts the STATEMENT, not the transaction (XACT_ABORT is off), so the ambient
        // transaction this command opened is intact and TransactionBehavior still commits it normally.
        // Nothing partial was written either — the failed insert was a single statement — which matters
        // because MARS disables EF's savepoints and there would be no clean point to roll back to.
        // Proven against a REAL SQL Server in ProvisionCurrentUserConcurrencyTests, not on the InMemory
        // provider, which does not enforce unique indexes and would let this pass while broken (DEF-066).
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (created)
        {
            // Added -> Detached. Without this the retry re-submits the same doomed INSERT.
            _db.Members.Remove(member);

            var winner = await _db.Members.FirstOrDefaultAsync(m => m.KeycloakUserId == sub, ct);

            // NOT the race we diagnosed — some other write failure wearing the same exception type.
            // Rethrown so it stays as loud as it was, rather than being absorbed by a recovery path
            // that happens to be nearby.
            if (winner is null) throw;

            member = winner;
            created = false;
            changed = member.SyncFromClaims(displayName, email, role);
            await _db.SaveChangesAsync(ct);
        }

        // AUDIT ONLY A REAL CHANGE. The SPA calls this endpoint once per app mount (AuthProvider's
        // useRef guard), so it fires on every full page load, refresh and login — and this emit used
        // to be unconditional, writing a `ProfileSynced` row that described nothing. Because the
        // chain is hash-chained and append-only (INV-005) those rows can never be removed, so the
        // noise is permanent and buries the governance events the log exists for.
        //
        // `created` still always emits: provisioning is itself the event.
        if (created || changed)
        {
            await _audit.EmitEnrichedAsync(created ? "Membership.MemberProvisioned" : "Membership.ProfileSynced",
                nameof(CommitteeMember), member.PublicId.ToString(), ct: ct);
        }

        return new MemberProfileDto(
            member.PublicId, member.FullName, member.Email, member.Role.ToString(),
            _user.Roles.ToArray(), member.IsVotingEligible);
    }
}
