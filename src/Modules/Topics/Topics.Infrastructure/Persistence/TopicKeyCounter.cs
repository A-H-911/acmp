namespace Acmp.Modules.Topics.Infrastructure.Persistence;

// Per-year allocation counter for human-readable topic keys (TOP-YYYY-###, README §F). Infrastructure
// concern only — not a domain entity. One row per year; Next is the next ordinal to hand out.
internal sealed class TopicKeyCounter
{
    public int Year { get; set; }
    public int Next { get; set; }
}
