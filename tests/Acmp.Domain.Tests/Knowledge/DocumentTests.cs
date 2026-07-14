using Acmp.Modules.Knowledge.Domain;
using Acmp.Modules.Knowledge.Domain.Enums;
using Acmp.Modules.Knowledge.Domain.Events;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Domain.Tests.Knowledge;

// Unit tests for the Document aggregate (P15d) — the Draft→Published→Archived lifecycle, the content-versioning
// on Create/Edit (FR-117), terminal (Archived) immutability, and the owned immutable version snapshots. No EF:
// the aggregate invariants are exercised directly.
public class DocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static LocalizedString L(string s = "x") => LocalizedString.Create(s, s);

    private static Document Draft() =>
        Document.Create("DOC-2026-001", L("Title"), "Guides", L("Body"), "kc-owner", new[] { "wiki", "guide" }, Now);

    // ── Create ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Create_makes_a_draft_v1_with_a_snapshot_and_raises_the_event()
    {
        var d = Draft();

        d.Key.Should().Be("DOC-2026-001");
        d.Status.Should().Be(DocumentStatus.Draft);
        d.OwnerUserId.Should().Be("kc-owner");
        d.Category.Should().Be("Guides");
        d.Version.Should().Be(1);
        d.Tags.Should().BeEquivalentTo("wiki", "guide");
        d.Versions.Should().ContainSingle();

        var snapshot = d.Versions.Single();
        snapshot.Version.Should().Be(1);
        snapshot.Title.En.Should().Be("Title");
        snapshot.Body.En.Should().Be("Body");
        snapshot.SavedByUserId.Should().Be("kc-owner");
        snapshot.SavedAt.Should().Be(Now);

        d.DomainEvents.OfType<DocumentCreatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Create_trims_blank_tags_and_tolerates_a_null_tag_list()
    {
        Document.Create("DOC-2026-001", L(), "Cat", L(), "kc-owner", new[] { " keep ", "  ", "" }, Now)
            .Tags.Should().BeEquivalentTo("keep");
        Document.Create("DOC-2026-001", L(), "Cat", L(), "kc-owner", null, Now)
            .Tags.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Create_requires_title_body_and_owner(bool nullTitle, bool nullBody, bool blankOwner)
    {
        var act = () => Document.Create("DOC-2026-001",
            nullTitle ? null! : L(), "Cat", nullBody ? null! : L(),
            blankOwner ? " " : "kc-owner", null, Now);
        act.Should().Throw<InvalidOperationException>();
    }

    // ── Edit (FR-117 versioning) ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Edit_bumps_version_and_appends_a_snapshot_while_draft()
    {
        var d = Draft();
        d.Edit(L("Title v2"), "Playbooks", L("Body v2"), Now.AddDays(1), "kc-editor");

        d.Version.Should().Be(2);
        d.Title.En.Should().Be("Title v2");
        d.Category.Should().Be("Playbooks");
        d.Versions.Should().HaveCount(2);

        var latest = d.Versions.Single(v => v.Version == 2);
        latest.Title.En.Should().Be("Title v2");
        latest.Body.En.Should().Be("Body v2");
        latest.SavedByUserId.Should().Be("kc-editor");
        d.DomainEvents.OfType<DocumentEditedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Edit_is_allowed_after_publish_and_keeps_prior_snapshots_immutable()
    {
        var d = Draft();
        d.Publish(Now);
        d.Edit(L("Corrected"), "Guides", L("Fixed body"), Now.AddDays(1), "kc-editor");

        d.Status.Should().Be(DocumentStatus.Published);
        d.Version.Should().Be(2);
        d.Versions.Should().HaveCount(2);
        // The v1 snapshot still holds the original content — snapshots are never mutated.
        d.Versions.Single(v => v.Version == 1).Title.En.Should().Be("Title");
    }

    [Fact]
    public void Edit_snapshot_does_not_carry_category()
    {
        // Category is intentionally not part of the versioned content — only the bilingual Title + Body are.
        var snapshot = Draft().Versions.Single();
        snapshot.GetType().GetProperty("Category").Should().BeNull();
    }

    [Fact]
    public void Edit_is_rejected_once_archived()
    {
        var d = Draft();
        d.Archive(Now);
        d.Invoking(x => x.Edit(L(), "Cat", L(), Now, "kc-editor")).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Edit_requires_title_and_body()
    {
        var d = Draft();
        d.Invoking(x => x.Edit(null!, "Cat", L(), Now, "kc-e")).Should().Throw<InvalidOperationException>();
        d.Invoking(x => x.Edit(L(), "Cat", null!, Now, "kc-e")).Should().Throw<InvalidOperationException>();
    }

    // ── Publish ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Publish_moves_draft_to_published_without_a_new_version()
    {
        var d = Draft();
        d.Publish(Now);
        d.Status.Should().Be(DocumentStatus.Published);
        d.Version.Should().Be(1);
        d.Versions.Should().ContainSingle();
        d.DomainEvents.OfType<DocumentPublishedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Publish_is_rejected_when_not_draft()
    {
        var d = Draft();
        d.Publish(Now);
        d.Invoking(x => x.Publish(Now)).Should().Throw<InvalidOperationException>();
    }

    // ── Archive ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Archive_from_draft_is_allowed()
    {
        var d = Draft();
        d.Archive(Now);
        d.Status.Should().Be(DocumentStatus.Archived);
        d.DomainEvents.OfType<DocumentArchivedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Archive_from_published_is_allowed_and_terminal()
    {
        var d = Draft();
        d.Publish(Now);
        d.Archive(Now);
        d.Status.Should().Be(DocumentStatus.Archived);
        // Archived is terminal — no re-archive, no publish.
        d.Invoking(x => x.Archive(Now)).Should().Throw<InvalidOperationException>();
        d.Invoking(x => x.Publish(Now)).Should().Throw<InvalidOperationException>();
    }
}
