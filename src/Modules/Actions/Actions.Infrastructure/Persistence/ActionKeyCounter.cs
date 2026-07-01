namespace Acmp.Modules.Actions.Infrastructure.Persistence;

// Per-prefix, per-year allocation counter for human-readable keys (ACT-YYYY-###, README §F).
// Infrastructure concern only — not a domain entity. One row per (Prefix, Year); Next is the next
// ordinal to hand out. ponytail: a get-increment-save on the row; gap-free and fine at committee scale,
// with a unique key index failing loud on the rare concurrent collision.
internal sealed class ActionKeyCounter
{
    public string Prefix { get; set; } = string.Empty;  // "ACT"
    public int Year { get; set; }
    public int Next { get; set; }
}
