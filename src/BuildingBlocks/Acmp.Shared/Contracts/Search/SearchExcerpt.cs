using System.Globalization;

namespace Acmp.Shared.Contracts.Search;

// FR-144 "matched excerpt". SQL FTS returns no snippet, so each provider synthesizes one from the searchable
// text: a window around the first case-insensitive occurrence of the query, else the leading slice (FTS can
// match via the word-breaker/LIKE-booster without a raw substring hit, so "not found" is expected and falls
// back cleanly). Pure string work — no engine dependency.
public static class SearchExcerpt
{
    private const int Window = 160;

    public static string Around(string? text, string query, int window = Window)
    {
        var body = (text ?? string.Empty).Trim();
        if (body.Length <= window)
            return body;

        var q = (query ?? string.Empty).Trim();
        var at = q.Length == 0 ? -1 : body.IndexOf(q, StringComparison.CurrentCultureIgnoreCase);

        if (at < 0)
            return body[..window].TrimEnd() + "…";

        var start = Math.Max(0, at - window / 3);
        var end = Math.Min(body.Length, start + window);
        var slice = body[start..end].Trim();
        return (start > 0 ? "…" : string.Empty) + slice + (end < body.Length ? "…" : string.Empty);
    }
}
