using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Governance.Domain;

// A considered option in the MADR "Considered options" section (docs/22 §A.7; the design's Options cards).
// Owned child of the Adr aggregate (reached only through it) — the same shape as Risk's Mitigation: a
// BaseEntity identity + a bilingual name, an optional bilingual body, and a chosen flag (exactly one option
// is normally the chosen one, but the domain does not force it — an early-draft ADR may have none chosen yet).
public sealed class AdrOption : BaseEntity
{
    private AdrOption() { }

    public LocalizedString Name { get; private set; } = null!;
    public LocalizedString? Body { get; private set; }
    public bool IsChosen { get; private set; }

    internal static AdrOption Create(LocalizedString name, LocalizedString? body, bool isChosen)
    {
        if (name is null) throw new InvalidOperationException("An option name is required.");
        return new AdrOption { Name = name, Body = body, IsChosen = isChosen };
    }
}

// Input shape for authoring an option (name + optional body + chosen flag). Lives in the domain so the
// factory signature is stable; the application layer maps request data into it.
public sealed record AdrOptionInput(LocalizedString Name, LocalizedString? Body, bool IsChosen);
