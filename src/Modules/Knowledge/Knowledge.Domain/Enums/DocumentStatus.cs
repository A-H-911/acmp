namespace Acmp.Modules.Knowledge.Domain.Enums;

// Knowledge document lifecycle (P15d; FR-116/117). Draft (author/revise) → Published (visible in the wiki) →
// Archived (retired; terminal). A document may also be Archived straight from Draft. Content edits (Create/Edit)
// snapshot a new version; Publish/Archive change status only. Serialized on the wire as the string name
// (localized in the SPA).
public enum DocumentStatus
{
    Draft = 1,
    Published = 2,
    Archived = 3,
}
