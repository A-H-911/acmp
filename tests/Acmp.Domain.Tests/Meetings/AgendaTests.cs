using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Domain.Events;
using FluentAssertions;

namespace Acmp.Domain.Tests.Meetings;

public class AgendaTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 18, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Meeting = Guid.NewGuid();
    private static readonly Guid Presenter = Guid.NewGuid();

    private static Agenda DraftWith(params (string key, string title)[] topics)
    {
        var agenda = Agenda.Draft("AGN-2026-019", Meeting);
        foreach (var (key, title) in topics)
            agenda.AddItem(Guid.NewGuid(), key, title, urgent: false, timeboxMinutes: 15, Presenter, "Omar H.");
        return agenda;
    }

    [Fact]
    public void AddItem_appends_in_order_and_rejects_duplicate_topics()
    {
        var agenda = Agenda.Draft("AGN-2026-019", Meeting);
        var topic = Guid.NewGuid();

        agenda.AddItem(topic, "TOP-2026-022", "API pagination", false, 15, Presenter, "Omar H.");
        agenda.Items.Single().Order.Should().Be(1);

        var dup = () => agenda.AddItem(topic, "TOP-2026-022", "API pagination", false, 15, Presenter, "Omar H.");
        dup.Should().Throw<InvalidOperationException>().WithMessage("*already*");
    }

    [Fact]
    public void Timebox_is_clamped_to_the_allowed_band()
    {
        var agenda = Agenda.Draft("AGN-2026-019", Meeting);
        var topic = Guid.NewGuid();
        agenda.AddItem(topic, "TOP-1", "T", false, timeboxMinutes: 999, Presenter, "Omar H.");
        agenda.Items.Single().TimeboxMinutes.Should().Be(AgendaItem.MaxTimebox);

        agenda.SetTimebox(topic, 1);
        agenda.Items.Single().TimeboxMinutes.Should().Be(AgendaItem.MinTimebox);
    }

    [Fact]
    public void MoveItem_reorders_and_is_a_no_op_at_the_ends()
    {
        var agenda = DraftWith(("TOP-1", "A"), ("TOP-2", "B"), ("TOP-3", "C"));
        var first = agenda.Items.First().TopicId;
        var last = agenda.Items.Last().TopicId;

        agenda.MoveItem(first, +1); // A and B swap
        agenda.Items.Select(i => i.TopicTitle).Should().ContainInOrder("B", "A", "C");

        agenda.MoveItem(last, +1); // C is already last → no-op
        agenda.Items.Select(i => i.TopicTitle).Should().ContainInOrder("B", "A", "C");
    }

    [Fact]
    public void RemoveItem_renumbers_remaining_items()
    {
        var agenda = DraftWith(("TOP-1", "A"), ("TOP-2", "B"), ("TOP-3", "C"));
        var middle = agenda.Items.Skip(1).First().TopicId;

        agenda.RemoveItem(middle);

        agenda.Items.Select(i => i.Order).Should().ContainInOrder(1, 2);
        agenda.Items.Select(i => i.TopicTitle).Should().ContainInOrder("A", "C");
    }

    [Fact]
    public void Publish_requires_items_and_a_presenter_on_each_then_versions_and_raises()
    {
        var empty = Agenda.Draft("AGN-2026-020", Meeting);
        var noItems = () => empty.Publish(Now);
        noItems.Should().Throw<InvalidOperationException>().WithMessage("*at least one*");

        var noPresenter = Agenda.Draft("AGN-2026-021", Meeting);
        noPresenter.AddItem(Guid.NewGuid(), "TOP-1", "A", false, 15, presenterUserId: null, presenterName: null);
        var act = () => noPresenter.Publish(Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*presenter*");

        var agenda = DraftWith(("TOP-1", "A"));
        agenda.Publish(Now);
        agenda.Status.Should().Be(AgendaStatus.Published);
        agenda.Version.Should().Be(1);
        agenda.PublishedAt.Should().Be(Now);
        agenda.DomainEvents.OfType<AgendaPublishedEvent>().Should().ContainSingle(e => e.ItemCount == 1);
    }

    [Fact]
    public void Republish_bumps_the_version()
    {
        var agenda = DraftWith(("TOP-1", "A"));
        agenda.Publish(Now);
        agenda.AddItem(Guid.NewGuid(), "TOP-2", "B", false, 15, Presenter, "Omar H."); // editable while Published
        agenda.Publish(Now.AddMinutes(5));
        agenda.Version.Should().Be(2);
    }

    [Fact]
    public void Lock_then_close_follow_the_meeting_and_gate_editing()
    {
        var agenda = DraftWith(("TOP-1", "A"));
        agenda.Publish(Now);
        agenda.Lock();
        agenda.Status.Should().Be(AgendaStatus.Locked);

        var editLocked = () => agenda.AddItem(Guid.NewGuid(), "TOP-9", "X", false, 15, Presenter, "Omar H.");
        editLocked.Should().Throw<InvalidOperationException>().WithMessage("*Locked*");

        agenda.Close();
        agenda.Status.Should().Be(AgendaStatus.Closed);
    }

    [Fact]
    public void Actual_time_and_outcome_are_recorded_only_while_locked()
    {
        var agenda = DraftWith(("TOP-1", "A"));
        var topic = agenda.Items.Single().TopicId;

        var tooEarly = () => agenda.RecordActualMinutes(topic, 12);
        tooEarly.Should().Throw<InvalidOperationException>();

        agenda.Publish(Now);
        agenda.Lock();
        agenda.RecordActualMinutes(topic, 12);
        agenda.SetOutcome(topic, AgendaItemOutcome.Discussed);

        var item = agenda.Items.Single();
        item.ActualMinutes.Should().Be(12);
        item.Outcome.Should().Be(AgendaItemOutcome.Discussed);
    }

    [Fact]
    public void Total_timebox_sums_the_items()
    {
        var agenda = DraftWith(("TOP-1", "A"), ("TOP-2", "B"));
        agenda.SetTimebox(agenda.Items.First().TopicId, 20);
        agenda.TotalTimeboxMinutes.Should().Be(35);
    }
}
