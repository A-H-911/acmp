namespace Acmp.Modules.Governance.Domain.Enums;

// ADR lifecycle (README §E, docs/12 §8, docs/22 §A.7): Draft → Proposed → Approved → (Superseded | Deprecated),
// with Proposed → Draft on requested changes. Once Approved the record is immutable — a correction is a NEW
// ADR that supersedes it (ADR-0009, supersede-not-edit), never an edit. Serialized on the wire as the string
// name (localized in the SPA). NOTE: the design ADR detail illustrates only proposed/accepted/superseded; the
// canonical model is these five (behaviour SoT), and "Approved" is the canonical label (not "accepted").
public enum AdrStatus
{
    Draft = 1,
    Proposed = 2,
    Approved = 3,
    Superseded = 4,
    Deprecated = 5,
}
