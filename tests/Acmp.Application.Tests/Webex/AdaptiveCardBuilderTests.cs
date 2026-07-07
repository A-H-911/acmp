using System.Text;
using System.Text.Json;
using Acmp.Modules.Integrations.Webex;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Application.Tests.Webex;

// The Adaptive Card the Webex adapter posts to the committee space: v1.3 schema, ≤80 KB, one deep-link
// button, no sensitive content, and correct language selection.
public class AdaptiveCardBuilderTests
{
    private static NotificationMessage Msg(string? link = "/meetings/MTG-2026-001") =>
        new("kc-a", LocalizedString.Create("Agenda published", "تم نشر جدول الأعمال"),
            LocalizedString.Create("Open it to review.", "افتحه للمراجعة."), "AgendaPublished", link);

    [Fact]
    public void Builds_a_v1_3_card_with_room_and_an_absolute_deep_link_button()
    {
        var json = AdaptiveCardBuilder.BuildSpaceMessageJson("room-1", Msg(), "https://acmp.local/", "en");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("roomId").GetString().Should().Be("room-1");

        var card = root.GetProperty("attachments")[0];
        card.GetProperty("contentType").GetString().Should().Be("application/vnd.microsoft.card.adaptive");
        var content = card.GetProperty("content");
        content.GetProperty("version").GetString().Should().Be("1.3");

        var action = content.GetProperty("actions")[0];
        action.GetProperty("type").GetString().Should().Be("Action.OpenUrl");
        action.GetProperty("url").GetString().Should().Be("https://acmp.local/meetings/MTG-2026-001");
        action.GetProperty("title").GetString().Should().Be("Open in ACMP");
    }

    [Fact]
    public void Uses_arabic_text_and_button_when_language_is_ar()
    {
        var json = AdaptiveCardBuilder.BuildSpaceMessageJson("room-1", Msg(), "https://acmp.local", "ar");

        json.Should().Contain("تم نشر جدول الأعمال");
        json.Should().Contain("افتح في ACMP");
    }

    [Fact]
    public void Omits_the_action_when_there_is_no_deep_link()
    {
        var json = AdaptiveCardBuilder.BuildSpaceMessageJson("room-1", Msg(link: null), "https://acmp.local", "en");

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("attachments")[0].GetProperty("content")
            .TryGetProperty("actions", out _).Should().BeFalse();
    }

    [Fact]
    public void Rejects_a_payload_over_the_80kb_adaptive_cards_limit()
    {
        var huge = new string('x', 90 * 1024);
        var message = new NotificationMessage("kc-a", LocalizedString.Create(huge, huge),
            LocalizedString.Create("b", "b"), "AgendaPublished", "/x");

        var act = () => AdaptiveCardBuilder.BuildSpaceMessageJson("room-1", message, "https://acmp.local", "en");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Adaptive Cards limit*");
    }
}
