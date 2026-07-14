namespace Acmp.Modules.Knowledge.Domain.Enums;

// Knowledge template lifecycle (P15d-2; FR-119). Active (usable at artifact-creation time) → Deprecated (retired;
// terminal — kept for provenance, never hard-deleted). FR-119 says "delete"; retention is permanent
// (domain-model), so delete is realised as a soft Deprecate. Serialized on the wire as the string name (localized
// in the SPA).
public enum TemplateStatus
{
    Active = 1,
    Deprecated = 2,
}
