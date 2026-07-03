namespace Acmp.Modules.Dependencies.Domain.Enums;

// The kinds of governed artifact that can be an endpoint of a dependency edge. An edge stores the type + the
// artifact's PublicId + a display-key/title snapshot — never an EF navigation into the owning module
// (ADR-0001, ADR-0019). Explicit int values start at 1 so a default(0) can never silently pass IsInEnum.
// Serialized on the wire as the string name (localized in the SPA).
public enum DependencyEndpointType
{
    Topic = 1,
    Action = 2,
    System = 3,
    Decision = 4,
}
