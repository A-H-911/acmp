namespace Acmp.Modules.Topics.Domain.Enums;

// How broadly the topic's decision affects the technical landscape (docs/09 §B.2). Drives the required
// reviewer/voter set and notification audience. Derived from affected-stream count at submission
// (1 → SingleStream, ≥2 → MultiStream); the Secretary may elevate to Platform/OrgWide during triage.
public enum TopicScope
{
    SingleStream = 1,
    MultiStream = 2,
    Platform = 3,
    OrgWide = 4,
}
