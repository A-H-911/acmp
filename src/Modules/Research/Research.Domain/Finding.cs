using Acmp.Modules.Research.Domain.Enums;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Research.Domain;

// A discovery captured during a research mission (FR-113). Owned child of the ResearchMission aggregate
// (reached only through it) — the same shape as Risk's Mitigation: a BaseEntity identity, a per-mission
// display key (FND-###), a bilingual summary + optional detail, a confidence band, and an independent-
// verification flag. Mutation is driven by the ResearchMission aggregate so its invariants hold.
public sealed class Finding : BaseEntity
{
    private Finding() { }

    public string Key { get; private set; } = string.Empty;   // FND-### (per mission)
    public LocalizedString Summary { get; private set; } = null!;
    public LocalizedString? Detail { get; private set; }
    public Confidence Confidence { get; private set; }
    public bool IsVerified { get; private set; }

    internal static Finding Create(string key, LocalizedString summary, LocalizedString? detail, Confidence confidence)
    {
        if (summary is null) throw new InvalidOperationException("A finding summary is required.");
        if (!Enum.IsDefined(confidence)) throw new InvalidOperationException("A valid confidence is required.");
        return new Finding
        {
            Key = key,
            Summary = summary,
            Detail = detail,
            Confidence = confidence,
        };
    }

    internal void Update(LocalizedString summary, LocalizedString? detail, Confidence confidence)
    {
        Summary = summary ?? throw new InvalidOperationException("A finding summary is required.");
        if (!Enum.IsDefined(confidence)) throw new InvalidOperationException("A valid confidence is required.");
        Detail = detail;
        Confidence = confidence;
    }

    // Flip to verified (idempotent). A finding never un-verifies in P15a.
    internal void Verify() => IsVerified = true;
}
