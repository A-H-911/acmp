namespace Acmp.Shared.Authorization.Abac;

// The three per-topic relationship capabilities (docs/10 §D). The ABAC vocabulary lives in the
// shared kernel so authorization handlers depend on it; Membership's TopicCapabilityGrant entity
// stores it. A relationship only WIDENS an Allow-if-owner cell for that instance — it never
// overrides a global Deny, SoD, or immutability.
public enum TopicCapabilityType
{
    Owner = 0,
    Assignee = 1,
    Presenter = 2,
}
