namespace Acmp.Modules.Traceability.Domain.Enums;

// The curated typed-edge vocabulary (docs/30 §2.2). Each type is DIRECTED (Source → RelType → Target); the
// inverse reading (Target → Source) is resolved at read time, never stored as a second row. Names here are
// the catalog's kebab-case labels in PascalCase (the wire serializes the enum name; the SPA maps each to a
// localized forward + inverse label, so no English label is stored — guardrail #9). The vocabulary is
// curated/versioned deliberately (ADR-0008): adding a type is a governance act, not an ad-hoc string.
public enum RelationshipType
{
    DecidedBy = 1,      // Topic → Decision      (inverse: decides)
    RecordedAs = 2,     // Decision → ADR        (inverse: records)
    Produces = 3,       // Topic → Action        (inverse: produced-by)
    Mitigates = 4,      // Action → Risk         (inverse: mitigated-by)
    Addresses = 5,      // Topic → Risk          (inverse: addressed-by)
    Supersedes = 6,     // ADR/Decision → ADR/Decision (inverse: superseded-by)
    Governs = 7,        // Invariant → ADR       (inverse: governed-by)
    Violates = 8,       // Topic → Invariant     (inverse: violated-by)
    DependsOn = 9,      // Topic → Topic         (inverse: dependency-of)
    Informs = 10,       // Research* → Topic     (inverse: informed-by)
    IllustratedBy = 11, // * → Diagram           (inverse: illustrates)
    References = 12,    // * → *                 (inverse: referenced-by; last-resort fallback)
    DerivedFrom = 13,   // ADR/Decision → Decision/ADR (inverse: basis-for)
    Implements = 14,    // Action → Decision     (inverse: implemented-by)
    Blocks = 15,        // Topic/Action/Risk → Topic/Action (inverse: blocked-by)
    Resolves = 16,      // Decision/Action → Risk (inverse: resolved-by)
}
