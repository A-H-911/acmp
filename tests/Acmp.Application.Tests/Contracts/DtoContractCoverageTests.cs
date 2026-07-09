using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Shared.Contracts.Dependencies;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Application.Tests.Contracts;

// Construct-and-read coverage for read-model records whose positional members are otherwise only exercised
// over the wire (deserialized into local test shapes, never as the source DTO). Each touches every getter
// plus value equality / with-expression / ToString so the compiler-generated members are covered.
public class DtoContractCoverageTests
{
    [Fact] // Decisions: DecisionConditionDto
    public void DecisionConditionDto_exposes_its_members()
    {
        var id = Guid.NewGuid();
        var actionId = Guid.NewGuid();
        var due = DateTimeOffset.UtcNow;
        var dto = new DecisionConditionDto(id, LocalizedString.Create("Do X", "افعل"), "Open", due, actionId);

        dto.Id.Should().Be(id);
        dto.Text.En.Should().Be("Do X");
        dto.Status.Should().Be("Open");
        dto.DueDate.Should().Be(due);
        dto.LinkedActionId.Should().Be(actionId);

        dto.Should().Be(dto with { });
        (dto with { Status = "Met" }).Status.Should().Be("Met");
        dto.ToString().Should().Contain("Open");
    }

    [Fact] // Meetings: RecordingDto
    public void RecordingDto_exposes_its_members()
    {
        var dto = new RecordingDto("Uploaded", "board.mp4", "video/mp4", 1024L, 600, "https://minio.test/x");

        dto.Source.Should().Be("Uploaded");
        dto.FileName.Should().Be("board.mp4");
        dto.ContentType.Should().Be("video/mp4");
        dto.SizeBytes.Should().Be(1024L);
        dto.DurationSeconds.Should().Be(600);
        dto.PlaybackUrl.Should().Be("https://minio.test/x");

        dto.Should().Be(dto with { });
        (dto with { Source = "Webex" }).Source.Should().Be("Webex");
        dto.ToString().Should().Contain("Uploaded");
    }

    [Fact] // Meetings: AttendanceDto + DiscussionDto (name/role snapshots read only over the wire elsewhere)
    public void AttendanceDto_and_DiscussionDto_expose_their_members()
    {
        var userId = Guid.NewGuid();
        var att = new AttendanceDto(userId, "Alice", "Chairman", "Present", true, DateTimeOffset.UtcNow);
        att.UserId.Should().Be(userId);
        att.Name.Should().Be("Alice");
        att.Role.Should().Be("Chairman");
        att.Status.Should().Be("Present");
        att.IsVotingEligible.Should().BeTrue();
        att.Should().Be(att with { });
        (att with { Status = "Absent" }).Status.Should().Be("Absent");

        var topicId = Guid.NewGuid();
        var disc = new DiscussionDto(topicId, "Noted the rollback plan.", "Bob", DateTimeOffset.UtcNow);
        disc.TopicId.Should().Be(topicId);
        disc.Body.Should().Be("Noted the rollback plan.");
        disc.AuthorName.Should().Be("Bob");
        disc.Should().Be(disc with { });
        disc.ToString().Should().Contain("Bob");
    }

    [Fact] // Shared contract: DependencyGraphEdge
    public void DependencyGraphEdge_exposes_its_members()
    {
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var edge = new DependencyGraphEdge(id, "DEP-2026-001", "Topic", otherId, "TOP-2026-004", "Adopt Keycloak",
            "BlockedBy", "Open", true);

        edge.Id.Should().Be(id);
        edge.Key.Should().Be("DEP-2026-001");
        edge.OtherType.Should().Be("Topic");
        edge.OtherId.Should().Be(otherId);
        edge.OtherKey.Should().Be("TOP-2026-004");
        edge.OtherTitle.Should().Be("Adopt Keycloak");
        edge.Kind.Should().Be("BlockedBy");
        edge.Status.Should().Be("Open");
        edge.IsBlocker.Should().BeTrue();

        edge.Should().Be(edge with { });
        (edge with { Status = "Resolved" }).Status.Should().Be("Resolved");
        edge.ToString().Should().Contain("DEP-2026-001");
    }
}
