using Acmp.Modules.Membership.Application.Features.AssignStreams;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Membership;

// AssignStreamsValidator — was 0% covered; covers the single RuleFor(NotEmpty) exhaustively.
public class AssignStreamsValidatorTests
{
    private static AssignStreamsCommand Valid() => new(Guid.NewGuid(), Array.Empty<Guid>());

    [Fact]
    public void Valid_command_with_a_real_member_id_passes()
    {
        new AssignStreamsValidator().Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_MemberPublicId_fails_validation_on_the_correct_property()
    {
        var result = new AssignStreamsValidator().Validate(Valid() with { MemberPublicId = Guid.Empty });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AssignStreamsCommand.MemberPublicId));
    }
}

// MemberStreamAssignment private ctor — EF materialization path.
// A fresh DbContext (no tracked entities) forces EF to instantiate MemberStreamAssignment via its
// private parameterless ctor when hydrating the owned Streams collection from the InMemory store.
// This is the same execution path the real SQL Server runtime takes on every query.
public class MemberStreamAssignmentEfTests
{
    private static MembershipDbContext MakeDb(string dbName)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-sys");
        user.Roles.Returns(Array.Empty<string>());
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase(dbName).Options,
            clock, user);
    }

    [Fact]
    public async Task EF_materializes_MemberStreamAssignment_via_private_ctor_and_FK_equals_owner_Id()
    {
        var dbName = "msa-ef-" + Guid.NewGuid();
        long memberId;

        // Arrange + Act — first context saves the member with one stream assignment
        await using (var db1 = MakeDb(dbName))
        {
            var member = CommitteeMember.Provision(
                "kc-msa", "MSA User", "msa@x.com", CommitteeRole.Member, DateTimeOffset.UtcNow);
            member.AssignStreams(new long[] { 7L });
            db1.Members.Add(member);
            await db1.SaveChangesAsync();
            memberId = member.Id; // EF assigns a positive IDENTITY value
        }

        memberId.Should().BePositive(); // sanity: EF assigned a non-zero Id

        // Assert — second context has no tracked entities; EF must materialise MemberStreamAssignment
        // through its private parameterless ctor and populate CommitteeMemberId from the FK column.
        await using var db2 = MakeDb(dbName);
        var stored = await db2.Members
            .Include(m => m.Streams)
            .SingleAsync(m => m.KeycloakUserId == "kc-msa");

        stored.Streams.Should().HaveCount(1);
        stored.Streams.Single().StreamId.Should().Be(7L);
        stored.Streams.Single().CommitteeMemberId.Should().Be(memberId);
    }
}
