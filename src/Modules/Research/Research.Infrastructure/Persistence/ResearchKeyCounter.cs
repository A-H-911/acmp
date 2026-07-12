namespace Acmp.Modules.Research.Infrastructure.Persistence;

// Per-prefix, per-year allocation counter for the mission key (RMS-YYYY-###). Infrastructure concern only —
// not a domain entity. One row per (Prefix, Year); Next is the next ordinal to hand out. ponytail: a
// get-increment-save on the row; gap-free and fine at committee scale, with a unique key index failing loud on
// the rare concurrent collision.
internal sealed class ResearchKeyCounter
{
    public string Prefix { get; set; } = string.Empty;  // "RMS"
    public int Year { get; set; }
    public int Next { get; set; }
}
