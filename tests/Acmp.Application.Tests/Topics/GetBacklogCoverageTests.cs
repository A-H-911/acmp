using Acmp.Modules.Topics.Application.Features.GetBacklog;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// Exercises the filter/sort/paging branches in GetBacklogHandler that are not touched by the baseline
// Backlog_and_detail test (which only hits: no-filters, age/desc sort, page 1 / pageSize 25).
public class GetBacklogCoverageTests
{
    private static readonly DateTimeOffset T0 = new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);

    private static TopicsDbContext NewDb()
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-seed");
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(T0);
        return new TopicsDbContext(
            new DbContextOptionsBuilder<TopicsDbContext>()
                .UseInMemoryDatabase("backlog-cov-" + Guid.NewGuid())
                .Options,
            clock, user);
    }

    private static IClock ClockAt(DateTimeOffset t)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(t);
        return c;
    }

    // ---- Domain helpers (mirror SeedTopicAsync in TopicHandlerTests) ----

    private static Topic Submitted(string key, TopicType type = TopicType.ArchitectureDecision,
        TopicUrgency urgency = TopicUrgency.Normal, string stream = "platform", string title = "Test Topic")
    {
        var t = Topic.Draft(key, title, "Desc", "Just", type, urgency, TopicSource.CommitteeMember,
            "kc-sub", "Actor", new[] { stream }, Array.Empty<string>(), Array.Empty<string>());
        t.Submit(T0);
        return t;
    }

    private static Topic InTriage(string key, TopicType type = TopicType.ArchitectureDecision,
        TopicUrgency urgency = TopicUrgency.Normal, string stream = "platform")
    {
        var t = Submitted(key, type, urgency, stream);
        t.BeginTriage("kc-sec", "Sec", T0);
        return t;
    }

    private static Topic Accepted(string key, Guid ownerId, string stream = "platform",
        TopicType type = TopicType.ArchitectureDecision, TopicUrgency urgency = TopicUrgency.Normal)
    {
        var t = InTriage(key, type, urgency, stream);
        t.Accept(ownerId, "Owner", "kc-sec", "Sec", T0);
        return t;
    }

    private static Topic Rejected(string key)
    {
        var t = InTriage(key);
        t.Reject("Duplicate", "kc-sec", "Sec", T0);
        return t;
    }

    // ---- Filter: explicit Statuses ----

    [Fact]
    public async Task Filter_explicit_statuses_returns_only_matching_topics()
    {
        // Arrange
        await using var db = NewDb();
        db.Topics.AddRange(
            Submitted("TOP-2026-001"),
            InTriage("TOP-2026-002"));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var result = await handler.Handle(
            new GetBacklogQuery(Statuses: new[] { TopicStatus.Submitted }),
            default);

        // Assert
        result.Total.Should().Be(1);
        result.Items.Single().Key.Should().Be("TOP-2026-001");
    }

    // ---- Filter: IncludeClosed ----

    [Fact]
    public async Task IncludeClosed_false_excludes_terminal_rejected_topics()
    {
        // Arrange
        await using var db = NewDb();
        db.Topics.AddRange(
            Submitted("TOP-2026-010"),
            Rejected("TOP-2026-011"));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var withoutClosed = await handler.Handle(new GetBacklogQuery(IncludeClosed: false), default);

        // Assert
        withoutClosed.Total.Should().Be(1);
        withoutClosed.Items.Single().Key.Should().Be("TOP-2026-010");
    }

    [Fact]
    public async Task IncludeClosed_true_includes_rejected_topics()
    {
        // Arrange
        await using var db = NewDb();
        db.Topics.AddRange(
            Submitted("TOP-2026-012"),
            Rejected("TOP-2026-013"));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var withClosed = await handler.Handle(new GetBacklogQuery(IncludeClosed: true), default);

        // Assert
        withClosed.Total.Should().Be(2);
    }

    // ---- Filter: Type ----

    [Fact]
    public async Task Filter_by_type_returns_only_matching_type()
    {
        // Arrange
        await using var db = NewDb();
        db.Topics.AddRange(
            Submitted("TOP-2026-020", type: TopicType.ArchitectureDecision),
            Submitted("TOP-2026-021", type: TopicType.GovernanceStandardization));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var result = await handler.Handle(
            new GetBacklogQuery(Type: TopicType.ArchitectureDecision), default);

        // Assert
        result.Total.Should().Be(1);
        result.Items.Single().Key.Should().Be("TOP-2026-020");
    }

    // ---- Filter: Urgency ----

    [Fact]
    public async Task Filter_by_urgency_returns_only_matching_urgency()
    {
        // Arrange
        await using var db = NewDb();
        db.Topics.AddRange(
            Submitted("TOP-2026-030", urgency: TopicUrgency.Urgent),
            Submitted("TOP-2026-031", urgency: TopicUrgency.Normal));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var result = await handler.Handle(
            new GetBacklogQuery(Urgency: TopicUrgency.Urgent), default);

        // Assert
        result.Total.Should().Be(1);
        result.Items.Single().Key.Should().Be("TOP-2026-030");
    }

    // ---- Filter: OwnerId ----

    [Fact]
    public async Task Filter_by_owner_returns_only_that_owners_topics()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        await using var db = NewDb();
        db.Topics.AddRange(
            Accepted("TOP-2026-040", ownerId),
            Accepted("TOP-2026-041", Guid.NewGuid()));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var result = await handler.Handle(new GetBacklogQuery(OwnerId: ownerId), default);

        // Assert
        result.Total.Should().Be(1);
        result.Items.Single().Key.Should().Be("TOP-2026-040");
    }

    // ---- Filter: Stream (in-memory, post-materialisation) ----

    [Fact]
    public async Task Filter_by_stream_returns_only_topics_in_that_stream()
    {
        // Arrange
        await using var db = NewDb();
        db.Topics.AddRange(
            Submitted("TOP-2026-050", stream: "identity"),
            Submitted("TOP-2026-051", stream: "platform"));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var result = await handler.Handle(new GetBacklogQuery(Stream: "identity"), default);

        // Assert — stream filter is case-insensitive (AffectedStreams.Contains with OrdinalIgnoreCase)
        result.Total.Should().Be(1);
        result.Items.Single().Key.Should().Be("TOP-2026-050");
    }

    // ---- Filter: Search (title, case-insensitive; key substring) ----

    [Fact]
    public async Task Search_by_title_is_case_insensitive_and_returns_matching_topics()
    {
        // Arrange
        await using var db = NewDb();
        db.Topics.AddRange(
            Submitted("TOP-2026-060", title: "Adopt Keycloak"),
            Submitted("TOP-2026-061", title: "Migrate Database"));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var result = await handler.Handle(new GetBacklogQuery(Search: "keycloak"), default);

        // Assert
        result.Total.Should().Be(1);
        result.Items.Single().Key.Should().Be("TOP-2026-060");
    }

    [Fact]
    public async Task Search_by_key_substring_returns_matching_topic()
    {
        // Arrange
        await using var db = NewDb();
        db.Topics.AddRange(
            Submitted("TOP-2026-070", title: "Topic A"),
            Submitted("TOP-2026-071", title: "Topic B"));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var result = await handler.Handle(new GetBacklogQuery(Search: "TOP-2026-070"), default);

        // Assert
        result.Total.Should().Be(1);
        result.Items.Single().Key.Should().Be("TOP-2026-070");
    }

    // ---- Sort: "priority" ----

    [Fact]
    public async Task SortBy_priority_ascending_puts_lowest_priority_number_first()
    {
        // Arrange
        var owner = Guid.NewGuid();
        await using var db = NewDb();
        var high = Accepted("TOP-2026-080", owner);
        high.SetPriority(10, T0);
        var low = Accepted("TOP-2026-081", owner);
        low.SetPriority(3, T0);
        db.Topics.AddRange(high, low);
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var result = await handler.Handle(
            new GetBacklogQuery(SortBy: "priority", SortDir: "asc"), default);

        // Assert
        result.Items[0].Priority.Should().Be(3);
        result.Items[1].Priority.Should().Be(10);
    }

    // ---- Sort: "title" — also covers SortDir "asc" (desc=false) vs "desc" (desc=true) ----

    [Fact]
    public async Task SortBy_title_asc_returns_alphabetical_order_and_desc_reverses_it()
    {
        // Arrange
        await using var db = NewDb();
        db.Topics.AddRange(
            Submitted("TOP-2026-090", title: "Zebra Topic"),
            Submitted("TOP-2026-091", title: "Alpha Topic"));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var asc = await handler.Handle(new GetBacklogQuery(SortBy: "title", SortDir: "asc"), default);
        var desc = await handler.Handle(new GetBacklogQuery(SortBy: "title", SortDir: "desc"), default);

        // Assert — covers both the (desc ? ordered.Reverse() : ordered) branches
        asc.Items[0].Title.Should().Be("Alpha Topic");
        asc.Items[1].Title.Should().Be("Zebra Topic");
        desc.Items[0].Title.Should().Be("Zebra Topic");
        desc.Items[1].Title.Should().Be("Alpha Topic");
    }

    // ---- Sort: "status" ----

    [Fact]
    public async Task SortBy_status_ascending_follows_enum_ordinal_order()
    {
        // Arrange — Submitted=1 < Triage=2
        await using var db = NewDb();
        db.Topics.AddRange(
            InTriage("TOP-2026-100"),
            Submitted("TOP-2026-101"));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var result = await handler.Handle(
            new GetBacklogQuery(SortBy: "status", SortDir: "asc"), default);

        // Assert
        result.Items[0].Status.Should().Be(TopicStatus.Submitted.ToString());
        result.Items[1].Status.Should().Be(TopicStatus.Triage.ToString());
    }

    // ---- Sort: "urgency" ----

    [Fact]
    public async Task SortBy_urgency_ascending_puts_Normal_before_Critical()
    {
        // Arrange — Normal=1 < Critical=3
        await using var db = NewDb();
        db.Topics.AddRange(
            Submitted("TOP-2026-110", urgency: TopicUrgency.Critical),
            Submitted("TOP-2026-111", urgency: TopicUrgency.Normal));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var result = await handler.Handle(
            new GetBacklogQuery(SortBy: "urgency", SortDir: "asc"), default);

        // Assert
        result.Items[0].Urgency.Should().Be(TopicUrgency.Normal.ToString());
        result.Items[1].Urgency.Should().Be(TopicUrgency.Critical.ToString());
    }

    // ---- Paging ----

    [Fact]
    public async Task Paging_page_2_returns_correct_slice_and_full_total()
    {
        // Arrange — 5 topics alphabetically by title
        await using var db = NewDb();
        db.Topics.AddRange(Enumerable.Range(1, 5)
            .Select(i => Submitted($"TOP-2026-{200 + i:D3}", title: $"Topic {i:D2}")));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act
        var page2 = await handler.Handle(
            new GetBacklogQuery(SortBy: "title", SortDir: "asc", Page: 2, PageSize: 2),
            default);

        // Assert
        page2.Total.Should().Be(5);
        page2.Items.Should().HaveCount(2);
        page2.Items[0].Title.Should().Be("Topic 03");
        page2.Items[1].Title.Should().Be("Topic 04");
    }

    [Fact]
    public async Task Page_zero_and_PageSize_zero_default_to_page_1_size_25()
    {
        // Arrange
        await using var db = NewDb();
        db.Topics.AddRange(Enumerable.Range(1, 3)
            .Select(i => Submitted($"TOP-2026-{300 + i:D3}")));
        await db.SaveChangesAsync();
        var handler = new GetBacklogHandler(db, ClockAt(T0));

        // Act — both Page=0 and PageSize=0 should be clamped to 1 and 25
        var result = await handler.Handle(new GetBacklogQuery(Page: 0, PageSize: 0), default);

        // Assert — all 3 fit within the default page size of 25
        result.Total.Should().Be(3);
        result.Items.Should().HaveCount(3);
    }
}
