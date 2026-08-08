# Tamheed 2.6.0 — field report: a confirmed `_next_id` ceiling, one retracted accusation, two hazards

**Context.** Different in kind from findings_1–9. Those were *migration/fixture* runs against a frozen
Keystone v1 package. This is the first report from **sustained execution use**: a month of PH-5 cloud
deployment work driving `progress_update` / `audit_record` / `work_bind` / `entity_upsert` many times a
day. The package went from PE-150 to **PE-181** and AV-096 to **AV-104** in this run alone. Gates 7/7
throughout, audit 92 evidenced / 12 narrated. No migration, no adopt, no parser involvement.

**Bottom line.** One **confirmed, reproduced defect** that will eventually break every long-lived
package (§A). One **retraction** — I publicly accused Tamheed of silently losing writes; it did not,
and the cause was mine (§B). Two **hazards** that follow from a package being a git working tree, which
findings_1–9 could not have surfaced because they never held a package open across a git operation
(§C, §D). Nothing in §A–§D was patched: the tool is not this project's to change.

---

## A. CONFIRMED DEFECT — `_next_id` sorts ids as TEXT, so every table dies at 1000 rows

`plugins/tamheed/server/tamheed_server.py`:

```python
row = _CURRENT.conn.execute(
    f"SELECT id FROM {table} WHERE id GLOB ? ORDER BY id DESC LIMIT 1", (prefix + "*",)
).fetchone()
n = int(row[0][len(prefix):]) + 1 if row else 1
return f"{prefix}{n:0{width}d}"
```

`ORDER BY id DESC` is a **lexicographic** sort on a value that is semantically numeric. Inside the
zero-padded width (`PE-001`…`PE-999`) text order and numeric order agree, so this is correct for
exactly the first 999 rows. At 1000 it stops being correct **permanently**, because `n:03d` widens
rather than truncates and `"PE-999" > "PE-1000"` as text.

**Reproduction** (stdlib, no package needed):

```python
import sqlite3
c = sqlite3.connect(":memory:"); c.execute("CREATE TABLE t (id TEXT PRIMARY KEY)")
c.executemany("INSERT INTO t VALUES (?)", [(f"PE-{n:03d}",) for n in range(1, 1000)])
def next_id(prefix="PE-", width=3):
    row = c.execute("SELECT id FROM t WHERE id GLOB ? ORDER BY id DESC LIMIT 1", (prefix+"*",)).fetchone()
    return f"{prefix}{(int(row[0][len(prefix):]) + 1 if row else 1):0{width}d}"
nid = next_id(); print(nid)                     # PE-1000
c.execute("INSERT INTO t VALUES (?)", (nid,))
print(next_id())                                # PE-1000  <-- again
print(next_id())                                # PE-1000  <-- forever
```

**Impact.** `id` is the primary key, so the second insert raises `IntegrityError`, the handler calls
`conn.rollback()` and the tool returns an error. It is **loud, not silent** — but it is also
**permanent and unrecoverable through the MCP surface**: from row 1000 onward no progress entry and
no audit verdict can ever be written to that package again. `work_bind` goes with them, since it
allocates a `PE-` id too.

**Why nine prior findings runs never saw it, and why every execution run eventually will.** `_next_id`
has exactly three call sites, and all three are the **append-only, unbounded-growth** tables:

| Call site | Table | Growth |
|---|---|---|
| `progress_update` | `progress_entries` | one row per narrative, forever |
| `work_bind` | `progress_entries` | one row per commit/PR binding, forever |
| `audit_record` | `audit_verdicts` | one row per verdict, re-verdicts append |

Every other id in the schema is **caller-supplied** (`FR-`, `AC-`, `DEC-`, `DEF-`…) and bounded by the
size of the plan. So the ceiling is invisible to planning-shaped and migration-shaped workloads — the
entire findings_1–9 corpus — and is guaranteed for any package that is *executed* long enough. This
package took ~3 weeks of daily execution to reach PE-181; the same cadence reaches PE-1000 inside a year.

**Suggested fix** — order by value, and let `CAST` absorb a non-numeric suffix that slips past the GLOB
(which the current Python-side `int(...)` cannot survive either):

```python
row = _CURRENT.conn.execute(
    f"SELECT MAX(CAST(SUBSTR(id, ?) AS INTEGER)) FROM {table} WHERE id GLOB ?",
    (len(prefix) + 1, prefix + "*"),
).fetchone()
return f"{prefix}{(row[0] or 0) + 1:0{width}d}"
```

`MAX` also removes the empty-table branch. Suggested regression test: seed 999 rows, allocate twice,
assert the two ids differ.

---

## B. RETRACTION — "Tamheed silently loses writes" was wrong, and the cause was mine

I recorded, in this package and in a memory file, that two package sessions had silently lost every
write and that ids were being reused to overwrite earlier rows. **That diagnosis was wrong.** Retracted
in PE-181; the incorrect memory file is corrected.

What actually happened: `tamheed-package/data/*.jsonl` is **git-tracked** — 29 files — and canonical
state is written there rather than to a private store. I was merging PRs throughout the run and ran
`git reset --hard origin/main` repeatedly. Any package write not yet git-committed was discarded **by
my own reset**. Checked before retracting: only two commits in the whole run ever carried package data,
`git reflog` shows the resets sitting between the writes and any commit, and
`git show <sha>:tamheed-package/data/audit_verdicts.jsonl` finds `AV-103` in none of the intermediate
commits.

Tamheed's behaviour was **correct at every step**: on each re-open it loaded the canonical state as it
actually stood on disk and allocated the next free id from it. What I read as id reuse was the
allocator doing precisely the right thing against state I had rewound underneath it. Apologies for the
noise — I am flagging it prominently because a false "this tool loses data" report is expensive.

**The genuine lesson, which is the inverse of the one I first recorded:** a Tamheed package *is* a git
working tree, so `git reset --hard`, `git checkout` and `git stash` destroy uncommitted package writes
exactly as they destroy uncommitted source. Package writes must be committed before any branch
operation. This is not currently stated anywhere in the docs I read, and §C argues it deserves more
than a doc line.

---

## C. HAZARD — `commit()` will clobber a `data/` that moved underneath it

`PackageStore.commit()` calls `dump()`, which **rewrites every table from memory unconditionally**.
There is no check that `data/` still matches what `load()` read. So the mirror image of §B is a live
data-loss path:

> package_open → someone runs `git checkout` / `git pull` / a second writer edits → any subsequent
> write tool calls `commit()` → **every incoming change is silently overwritten** by the session's
> older in-memory copy. No error, no warning.

§B is the reason to take this seriously rather than treat it as theoretical: the package lives in git,
and branch operations during a working session are routine, not exotic. §B happened to fire in the
harmless direction (my writes lost, disk state intact). The same setup fires the other way just as
easily, and that direction destroys *committed* work.

**Suggested fix** — fingerprint at load, verify before dump:

```python
def _fingerprint(data_dir):                      # *.jsonl only; .lock is ours
    return {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(data_dir.glob("*.jsonl"))}
```

Record it in `__enter__` after `load()`; in `commit()`, recompute and raise a distinct
`StoreStaleError` naming the changed files if it differs; refresh after a successful `dump()`. A whole
package is a few MB, so this is milliseconds. Keep the guard in `PackageStore.commit()` rather than in
`dump()` so `migrate`/`adopt`, which legitimately write fresh trees, are unaffected. For the close
path, dumping the in-memory state to a timestamped sibling directory before releasing the lock would
mean neither side's work is lost.

---

## D. HAZARD — the lock records a bare PID, and PID reuse makes liveness checks lie

`store.py` writes `str(os.getpid())` into `data/.lock`, and `StoreLockedError` says only "remove the
stale lock deliberately if the writer crashed". After a crash the operator is left holding a bare
number, and the obvious check — *is that PID alive?* — **is unsound**, because the OS reuses PIDs: it
answers "yes" for an unrelated program and talks you out of clearing a genuinely stale lock, or
"no" in a way that is only accidentally right. In practice this has been a recurring source of friction
here.

**Suggested fix** — cheap and non-behavioural: write JSON (`pid`, `host`, `taken_at`) instead of a bare
integer, and include it in the `StoreLockedError` message so the operator sees *who* holds it and
*since when* without opening the file. Parse tolerantly so locks written by older versions still
describe themselves. I would **not** auto-reclaim on a "dead" PID — that trades a visible annoyance for
an invisible two-writer corruption, and PID reuse is exactly why the check cannot be trusted.

---

## E. What held up

Worth stating plainly, since §A–§D are all problems. Under a month of heavy execution use the store
did not corrupt anything, the gates stayed 7/7, FK enforcement and the immutability triggers never
misfired, `export_html` stayed consistent with the JSONL, and — per §B — it recovered cleanly and
correctly from a working tree I repeatedly rewound underneath it. The one behaviour I mistook for a bug
was the tool being right. The `entity_upsert` full-row contract also made the §B repair mechanical:
correcting a defect's evidence text and re-applying a lost `work_bind` needed no hand-editing of
canonical files.

## F. Asks, in priority order

1. **§A `_next_id`** — confirmed, reproduced, one-line fix, and it silently sets an expiry date on every
   executed package. This is the one that matters.
2. **§C stale-overwrite guard** — real silent-data-loss path, made routine by packages living in git.
3. **§D lock metadata** — diagnostics only, but it recurs.
4. **Docs** — state somewhere load-bearing that package writes are working-tree changes and must be
   committed before branch operations. §B is what that omission costs a careful operator.
