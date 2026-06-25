using Acmp.Shared.Domain.Enums;

namespace Acmp.Shared.Domain.ValueObjects;

// A bilingual string. Both EN and AR are required (guardrail 9: no single-language user-facing
// strings). Resolve per request locale with For(language).
public sealed record LocalizedString(string En, string Ar)
{
    public string For(Language language) => language == Language.Ar ? Ar : En;

    public static LocalizedString Create(string en, string ar)
    {
        if (string.IsNullOrWhiteSpace(en)) throw new ArgumentException("EN text is required", nameof(en));
        if (string.IsNullOrWhiteSpace(ar)) throw new ArgumentException("AR text is required", nameof(ar));
        return new LocalizedString(en.Trim(), ar.Trim());
    }
}
