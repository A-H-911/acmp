namespace Acmp.Modules.Knowledge.Infrastructure.Persistence;

// Per-prefix, per-year allocation counter for module display keys (DOC-YYYY-###; TPL-YYYY-### in P15d-2).
// Infrastructure concern only — not a domain entity. One row per (Prefix, Year); Next is the next ordinal to
// hand out. ponytail: a get-increment-save on the row; gap-free and fine at committee scale, with a unique key
// index failing loud on the rare concurrent collision.
internal sealed class KnowledgeKeyCounter
{
    public string Prefix { get; set; } = string.Empty;  // "DOC"
    public int Year { get; set; }
    public int Next { get; set; }
}
