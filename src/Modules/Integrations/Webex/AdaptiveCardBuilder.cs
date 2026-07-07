using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Integrations.Webex;

// Turns a NotificationMessage into a Webex POST /messages body carrying a single Adaptive Card v1.3
// (webex-feasibility.md §3.3 — Webex renders v1.3 only, ≤80 KB, ≤10 image links). The card is deliberately
// minimal and carries NO sensitive content (notification-strategy.md §6): a title, a one-line body, and one
// "Open in ACMP" deep-link button. Zero images, so the 10-image cap is trivially met; the 80 KB cap is still
// asserted to guard against a pathologically long user-supplied title.
public static class AdaptiveCardBuilder
{
    private const int MaxPayloadBytes = 80 * 1024;

    // Relaxed encoder so Arabic card text is emitted literally (not \uXXXX) — smaller payload and an accurate
    // byte count for the 80 KB check. Card text is rendered by Webex as plain TextBlock text, never HTML.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Returns the serialized POST /messages request body (roomId + adaptive-card attachment).
    public static string BuildSpaceMessageJson(string roomId, NotificationMessage message, string acmpBaseUrl, string language)
    {
        // Dual-language card (the committee is bilingual, INV-009): render BOTH EN and AR. DefaultLanguage only
        // decides which is shown first; Arabic blocks are right-aligned so they read correctly RTL.
        var arFirst = string.Equals(language, "ar", StringComparison.OrdinalIgnoreCase);
        object EnTitle() => new { type = "TextBlock", text = message.Title.En, weight = "Bolder", size = "Medium", wrap = true, horizontalAlignment = "Left" };
        object ArTitle() => new { type = "TextBlock", text = message.Title.Ar, weight = "Bolder", size = "Medium", wrap = true, horizontalAlignment = "Right" };
        object EnBody() => new { type = "TextBlock", text = message.Body.En, wrap = true, spacing = "Small", horizontalAlignment = "Left" };
        object ArBody() => new { type = "TextBlock", text = message.Body.Ar, wrap = true, spacing = "Small", horizontalAlignment = "Right" };

        var card = new Dictionary<string, object?>
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.3",
            ["body"] = arFirst
                ? new[] { ArTitle(), EnTitle(), ArBody(), EnBody() }
                : new[] { EnTitle(), ArTitle(), EnBody(), ArBody() },
        };

        var url = AbsoluteLink(message.DeepLink, acmpBaseUrl);
        if (url is not null)
        {
            card["actions"] = new object[]
            {
                new { type = "Action.OpenUrl", title = "Open in ACMP · افتح في ACMP", url },
            };
        }

        var request = new
        {
            roomId,
            markdown = $"{message.Title.En} / {message.Title.Ar}", // plain fallback for clients that don't render cards
            attachments = new[]
            {
                new { contentType = "application/vnd.microsoft.card.adaptive", content = card },
            },
        };

        var json = JsonSerializer.Serialize(request, Json);
        var bytes = Encoding.UTF8.GetByteCount(json);
        if (bytes > MaxPayloadBytes)
            throw new InvalidOperationException(
                $"Webex card payload is {bytes} bytes, over the {MaxPayloadBytes}-byte Adaptive Cards limit.");
        return json;
    }

    private static string? AbsoluteLink(string? relative, string acmpBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(relative) || string.IsNullOrWhiteSpace(acmpBaseUrl)) return null;
        return $"{acmpBaseUrl.TrimEnd('/')}/{relative.TrimStart('/')}";
    }
}
