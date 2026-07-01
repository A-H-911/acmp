using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Decisions.Application.Internal;

// Shared validation predicate for decision bilingual content. Content is entered in a single UI language
// (the other LocalizedString column stays empty; reads fall back to the populated one), so a field is
// valid when AT LEAST ONE language carries text — not both. Used by the record + supersede validators.
public static class DecisionText
{
    public static bool HasEitherLanguage(LocalizedString value) =>
        !string.IsNullOrWhiteSpace(value.En) || !string.IsNullOrWhiteSpace(value.Ar);
}
