namespace Acmp.Modules.Topics.Domain.Enums;

// Handling-speed attribute, orthogonal to TopicType (docs/09 §B.1, devil's-advocate §D). Drives the
// aging SLA threshold (Normal 21d / Urgent 7d / Critical 3d), reminder cadence, and backlog aging badge.
// The design's "low" sample value is non-canonical; the authoritative taxonomy is the three below.
public enum TopicUrgency
{
    Normal = 1,
    Urgent = 2,
    Critical = 3,
}
