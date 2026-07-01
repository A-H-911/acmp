using System.Data.Common;
using Acmp.Modules.Decisions.Domain;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Integration.Tests;

// S5 (ADR-0016 §3) — the DB-enforced backstops InMemory cannot prove. Every test is failure-first and,
// where it is the point, pairs the SQL rejection with the SAME write succeeding on InMemory. That
// contrast is the whole value of the slice: it shows the unit suite's green is, for these rules, a
// false green that only a real database catches.
[Collection(SqlBackstopCollection.Name)]
public sealed class DbBackstopTests
{
    private readonly SqlBackstopFixture _fx;

    public DbBackstopTests(SqlBackstopFixture fx) => _fx = fx;

    private static string UniqueKey(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..32];

    private static Meeting NewMeeting(string key) => Meeting.Schedule(
        key, "Backstop meeting", Meeting.SingleCommitteeId, Guid.NewGuid(), "Chair",
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
        MeetingType.Regular, MeetingMode.InPerson, null, null, DateTimeOffset.UtcNow);

    private static CommitteeMember NewMember(string sub, string email) =>
        CommitteeMember.Provision(sub, "Member", email, CommitteeRole.Member, DateTimeOffset.UtcNow);

    // ---- unique-index backstops -------------------------------------------------------------------

    [Fact] // (top-level unique) two meetings with the same human key
    public async Task MeetingKey_Duplicate_IsRejectedBySql()
    {
        var key = UniqueKey("MTG");
        await using var db = _fx.NewMeetingsSql();
        db.Meetings.Add(NewMeeting(key));
        db.Meetings.Add(NewMeeting(key));

        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [Fact] // the same colliding key sails through InMemory — the false green the unit suite lives with
    public async Task MeetingKey_Duplicate_IsAcceptedByInMemory()
    {
        var key = UniqueKey("MTG");
        await using var db = _fx.NewMeetingsInMemory(nameof(MeetingKey_Duplicate_IsAcceptedByInMemory));
        db.Meetings.Add(NewMeeting(key));
        db.Meetings.Add(NewMeeting(key));

        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().NotThrowAsync();
        (await db.Meetings.CountAsync(m => m.Key == key)).Should().Be(2);
    }

    // (composite owned) one attendance row per member per meeting — (MeetingEntityId, UserId).
    // The aggregate's SeedAttendee dedupes in memory AND EF eagerly loads the owned collection, so a
    // duplicate can only arrive via a NON-aggregate write (a repair script / bulk insert). That path is
    // exactly what the DB index backstops — so we exercise it with a raw INSERT that copies the seeded
    // row (fresh PublicId) and watch SQL Server reject the (MeetingEntityId, UserId) collision.
    [Fact]
    public async Task Attendance_DuplicateMemberOnMeeting_IsRejectedBySql()
    {
        long meetingId;
        await using (var db = _fx.NewMeetingsSql())
        {
            var m = NewMeeting(UniqueKey("MTG"));
            m.SeedAttendee(Guid.NewGuid(), "Member", AttendanceRole.Member, true);
            db.Meetings.Add(m);
            await db.SaveChangesAsync();
            meetingId = m.Id;
        }

        await using (var db = _fx.NewMeetingsSql())
        {
            var insertDuplicate = () => db.Database.ExecuteSqlRawAsync(
                "INSERT INTO meetings.meeting_attendance " +
                "(UserId, Name, Role, Status, IsVotingEligible, JoinedAt, LeftAt, MeetingEntityId, PublicId) " +
                "SELECT UserId, Name, Role, Status, IsVotingEligible, JoinedAt, LeftAt, MeetingEntityId, NEWID() " +
                "FROM meetings.meeting_attendance WHERE MeetingEntityId = {0}", meetingId);

            await insertDuplicate.Should().ThrowAsync<DbException>();
        }
    }

    [Fact] // one agenda per meeting — unique index on Agenda.MeetingId
    public async Task Agenda_SecondAgendaForOneMeeting_IsRejectedBySql()
    {
        var meetingId = Guid.NewGuid();
        await using var db = _fx.NewMeetingsSql();
        db.Agendas.Add(Agenda.Draft(UniqueKey("AGN"), meetingId));
        db.Agendas.Add(Agenda.Draft(UniqueKey("AGN"), meetingId));

        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    // (composite owned) no duplicate topic on an agenda — (AgendaEntityId, TopicId). Same story as
    // attendance: the aggregate dedupes, so the backstop is proven against a raw duplicate INSERT.
    [Fact]
    public async Task AgendaItem_DuplicateTopicOnAgenda_IsRejectedBySql()
    {
        long agendaId;
        await using (var db = _fx.NewMeetingsSql())
        {
            var agenda = Agenda.Draft(UniqueKey("AGN"), Guid.NewGuid());
            agenda.AddItem(Guid.NewGuid(), "TOP-2026-001", "Topic", false, 15, Guid.NewGuid(), "Presenter");
            db.Agendas.Add(agenda);
            await db.SaveChangesAsync();
            agendaId = agenda.Id;
        }

        await using (var db = _fx.NewMeetingsSql())
        {
            // [Order] is a SQL reserved word — must be bracketed in the column list.
            var insertDuplicate = () => db.Database.ExecuteSqlRawAsync(
                "INSERT INTO meetings.agenda_items " +
                "(TopicId, TopicKey, TopicTitle, Urgent, [Order], TimeboxMinutes, PresenterUserId, " +
                "PresenterName, Outcome, ActualMinutes, CarryOverFromAgendaId, AgendaEntityId, PublicId) " +
                "SELECT TopicId, TopicKey, TopicTitle, Urgent, [Order], TimeboxMinutes, PresenterUserId, " +
                "PresenterName, Outcome, ActualMinutes, CarryOverFromAgendaId, AgendaEntityId, NEWID() " +
                "FROM meetings.agenda_items WHERE AgendaEntityId = {0}", agendaId);

            await insertDuplicate.Should().ThrowAsync<DbException>();
        }
    }

    [Fact] // (top-level unique) two decisions with the same human key — DECN-YYYY-### must be unique
    public async Task DecisionKey_Duplicate_IsRejectedBySql()
    {
        var key = UniqueKey("DECN");
        var rationale = LocalizedString.Create("Because", "لأن");
        await using var db = _fx.NewDecisionsSql();
        db.Decisions.Add(Decision.Draft(key, Guid.NewGuid(), null, DecisionOutcome.Approved, rationale, rationale, null, null,
            Array.Empty<DecisionConditionInput>(), "it-actor", DateTimeOffset.UtcNow));
        db.Decisions.Add(Decision.Draft(key, Guid.NewGuid(), null, DecisionOutcome.Approved, rationale, rationale, null, null,
            Array.Empty<DecisionConditionInput>(), "it-actor", DateTimeOffset.UtcNow));

        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [Fact] // stable identity is unique — two local records for one Keycloak subject
    public async Task MemberKeycloakUserId_Duplicate_IsRejectedBySql()
    {
        var sub = Guid.NewGuid().ToString("N");
        await using var db = _fx.NewMembershipSql();
        db.Members.Add(NewMember(sub, $"{Guid.NewGuid():N}@acmp.gov"));
        db.Members.Add(NewMember(sub, $"{Guid.NewGuid():N}@acmp.gov"));

        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [Fact] // FILTERED unique index: email is unique only WHERE present — two real duplicates are rejected
    public async Task MemberEmail_DuplicateRealEmail_IsRejectedBySql()
    {
        var email = $"{Guid.NewGuid():N}@acmp.gov";
        await using var db = _fx.NewMembershipSql();
        db.Members.Add(NewMember(Guid.NewGuid().ToString("N"), email));
        db.Members.Add(NewMember(Guid.NewGuid().ToString("N"), email));

        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [Fact] // ...but the SAME filter must ALLOW two empty-email members (the bootstrap-admin case)
    public async Task MemberEmail_TwoEmptyEmails_AreAllowedBySql()
    {
        await using var db = _fx.NewMembershipSql();
        var a = NewMember(Guid.NewGuid().ToString("N"), "");
        var b = NewMember(Guid.NewGuid().ToString("N"), "");
        db.Members.Add(a);
        db.Members.Add(b);

        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().NotThrowAsync();
    }

    // ---- foreign-key behaviour --------------------------------------------------------------------

    [Fact] // Delegation -> CommitteeMember is OnDelete(Restrict): a referenced member cannot be deleted
    public async Task DeletingMemberReferencedByDelegation_IsRejectedBySql()
    {
        long delegatorId;
        await using (var db = _fx.NewMembershipSql())
        {
            var delegator = NewMember(Guid.NewGuid().ToString("N"), $"{Guid.NewGuid():N}@acmp.gov");
            var delegate2 = NewMember(Guid.NewGuid().ToString("N"), $"{Guid.NewGuid():N}@acmp.gov");
            db.Members.Add(delegator);
            db.Members.Add(delegate2);
            await db.SaveChangesAsync();
            db.Delegations.Add(Delegation.Create(delegator.Id, delegate2.Id, "agenda.publish",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30)));
            await db.SaveChangesAsync();
            delegatorId = delegator.Id;
        }

        await using (var db = _fx.NewMembershipSql())
        {
            // Fresh context loads ONLY the member (the delegation is untracked) — the realistic
            // "admin removes a member" path. SQL's FK Restrict blocks it.
            var member = await db.Members.FirstAsync(m => m.Id == delegatorId);
            db.Members.Remove(member);

            await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact] // InMemory enforces no FK — the same delete orphans the delegation and succeeds
    public async Task DeletingMemberReferencedByDelegation_IsAcceptedByInMemory()
    {
        var dbName = nameof(DeletingMemberReferencedByDelegation_IsAcceptedByInMemory);
        long delegatorId;
        await using (var db = _fx.NewMembershipInMemory(dbName))
        {
            var delegator = NewMember(Guid.NewGuid().ToString("N"), $"{Guid.NewGuid():N}@acmp.gov");
            var delegate2 = NewMember(Guid.NewGuid().ToString("N"), $"{Guid.NewGuid():N}@acmp.gov");
            db.Members.Add(delegator);
            db.Members.Add(delegate2);
            await db.SaveChangesAsync();
            db.Delegations.Add(Delegation.Create(delegator.Id, delegate2.Id, "agenda.publish",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30)));
            await db.SaveChangesAsync();
            delegatorId = delegator.Id;
        }

        await using (var db = _fx.NewMembershipInMemory(dbName))
        {
            var member = await db.Members.FirstAsync(m => m.Id == delegatorId);
            db.Members.Remove(member);

            await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().NotThrowAsync();
        }
    }

    // ---- migrations -------------------------------------------------------------------------------

    [Fact] // every module's migrations applied cleanly in the fixture; nothing should remain pending
    public async Task AllModuleMigrations_AppliedCleanly_NonePending()
    {
        await using var membership = _fx.NewMembershipSql();
        await using var topics = _fx.NewTopicsSql();
        await using var meetings = _fx.NewMeetingsSql();
        await using var decisions = _fx.NewDecisionsSql();
        await using var notifications = _fx.NewNotificationsSql();

        (await membership.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await topics.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await meetings.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await decisions.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await notifications.Database.GetPendingMigrationsAsync()).Should().BeEmpty();

        (await membership.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();
    }
}
