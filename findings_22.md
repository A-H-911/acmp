# findings_22 — a query surface with no paging pushes agents onto the files, and everything else follows

2026-09-06, against **4.4.2** (`server_info`: version 4.4.2, `migrations_head` 003_skills.sql,
schema_version 3). **One design gap that caused an incident, two smaller gaps, one migration leftover,
and a strong positive result that deserves to be recorded as loudly as the complaints.**

The package is `tamheed-package` in the ACMP repo. All of this came out of an operator-commissioned
integrity audit after the operator caught an agent — me — reading `data/*.jsonl` directly instead of
going through the MCP tools.

**§1 is ACMP's fault and tamheed's gap at the same time, and the report separates the two.** The
project wrote a false sentence into its own standing prompt and then followed it for three weeks. That
sentence would never have been written if the tool had a way to page. Both halves are stated because
only one of them is yours to fix.

---

## 1. `entity_query` has `limit` but no offset or cursor, so a register that overflows the client's payload cap is unreachable through the tool

`entity_query(type, id, status, columns, limit=100)` (`tamheed_server.py:833-834`) issues
`SELECT {cols} FROM {table}{where} ORDER BY id LIMIT {n}` and returns `{rows, count, total}`
(`:861-867`). **The `total` disclosure is good and it is doing its job** — the comment at `:861-862`
records why it exists (field-evidence C17: `limit=100` silently truncated a 218-row family). You know
when you have been cut.

**You just cannot do anything about it.** There is no `offset`, no cursor, no `after_id`. The only
ways to reach past the cut are to narrow `columns`, to partition on `status`, or to fetch one `id` at
a time.

That is survivable until the rows are large, and in this store they are. Measured this session, not
recalled:

```
entity_query("defect", status="Open",
             columns=["id","title","severity","lifecycle_status","found_in"])
→ client refusal: "result (88,131 characters across 91 lines) exceeds maximum allowed tokens"
```

**Ninety-one rows was too many**, because a `defect.title` in this package routinely runs several
thousand characters. The cap is the MCP client's, not tamheed's — but tamheed offers no mechanism to
work around a cap that every client will have, and the store's own house style (long narrative titles)
guarantees the collision.

⚠ **The consequence is not inconvenience, it is that `LL-011` cannot be discharged through the tool.**
This project's binding lesson says an artefact the operator reads to decide must carry each cited
record's **own full text**, quoted from the canonical store by a generator — the operator once refused
an interview held against a slate that cited ~40 records by id alone. Full text of many large rows is
exactly the payload shape that cannot be retrieved. So all four of this project's slate generators
(`scripts/gen-*.mjs`) read `data/*.jsonl` with `readFileSync`, and the read practice under audit grew
from there.

**Reproduced, and the reproduction includes the negative control.** `columns` *does* narrow the
payload: `entity_query("requirement", columns=["id","lifecycle_status","kind","priority"], limit=200)`
returned 200 rows compactly with `total: 230`. There is no per-field truncation anywhere in the query
path — I read it looking for one. So the tool is honest about width; it is only mute about depth.

### The half that is ACMP's fault, stated plainly

`prm-next.md:1268-1271` — this project's own standing kickoff prompt — says:

> the whole register is tens of KB even with `columns` set, because **`columns` does not actually
> narrow the payload**. … **Count from the canonical JSONL instead**, which is also what trap 13
> already tells you to do when building any payload

**That claim is false**, refuted by the measurement above. An agent hit the overflow, misattributed it
to `columns` rather than to the missing paging, wrote the wrong cause into the file every later session
reads, and the file then recruited every later session onto the JSONL. We are fixing our sentence.
**We cannot fix the gap that made the sentence attractive.**

### Remedies, cheapest first

1. **Add `offset: int = 0`** to `entity_query`. One clause in an already-parameterised SQL string, no
   schema change, no new concept. `ORDER BY id` is already deterministic, so offset paging is stable.
2. **Or a keyset cursor** — `after_id: str | None`, `WHERE id > ?`. Cheaper than OFFSET at depth and
   it composes with the existing `ORDER BY id`. Return it as `next_after` beside `total`, so a caller
   pages without constructing anything.
3. **Or a per-id batch read** — `ids: list[str]`. This is the shape the slate generators actually
   want: they know exactly which forty records they must quote verbatim, and today the only way to get
   forty full rows is forty calls or one file read.
4. **Failing all of those, say so in the docstring.** *"`limit` truncates; there is no paging. For a
   register whose rows exceed your client's payload budget, narrow `columns` or filter by `status`."*
   Two sentences would at least have prevented the wrong diagnosis.

---

## 2. `RELATION_RULES` cannot express a scope-change that amends a decision

`scope_modifies` is defined as `(frozenset({"scope-change"}), _PLAN_ROWS)` (`:148`), and `_PLAN_ROWS`
(`:117-118`) is requirement-like ∪ work ∪ `{acceptance-criterion, risk, kpi, open-question}`.
**`decision` and `adr` are not in it.**

Hit live this session. The operator granted a bounded, audit-only exception to a standing ruling
recorded in a `decision` row; the scope-change row recording that exception could not be typed:

```
relation 'scope_modifies' does not allow scope-change -> decision (SC-049 -> DEC-135);
allowed from: scope-change; allowed to: acceptance-criterion, assumption, constraint, defect,
deferred-work, execution-plan, invariant, kpi, open-question, phase, requirement, risk, slice,
wbs-item — use 'relates_to' for an untyped association
```

✅ **The refusal is excellent behaviour and I want to be clear about that.** It names the relation, the
direction, both endpoints, the full allowed set, and the escape hatch. The batch rolled back
atomically rather than half-applying. This is what a good constraint error looks like.

⚠ **The gap is that the modelled world assumes scope changes only ever touch the plan.** In a package
this mature, a large share of scope changes amend a *ruling* — they carve out an exception, narrow a
standing rule, or re-scope a decision's applicability. Those edges collapse to `relates_to`, and
`relates_to` is by design the untyped bucket (`:120-121`: *"Absent relation ('relates_to') = the
untyped escape hatch"*). So the `scope-changes-merged` advisory and any future delta-intent tooling
cannot see them, and the register loses the distinction between *this SC touches that DEC* and *these
two rows are vaguely related*.

**Remedy.** Either add `decision`/`adr` to `_PLAN_ROWS` for the three `scope_*` relations, or — better,
because the semantics really are different — add an `amends` relation typed
`scope-change → {decision, adr}`. A governance ruling is not a plan row and probably should not be
merged into that set wholesale.

---

## 3. `audit_evidence` counts the narrated verdicts and will not name them

`gate_run()` reports `{"evidenced": 225, "narrated": 12, "note": "narrated verdicts are the graded
party grading itself (C7)"}`. The computation (`:1002-1008`) is a clean `SUM(CASE WHEN evidence IS
NULL OR evidence = '' …)` over `audit_verdicts` — **so the predicate is exact and mechanically
available; only the ids are withheld.**

That leaves the one C7 signal in the whole report unactionable. An operator who wants to review the
twelve verdicts where the agent graded its own work cannot get to them: `entity_query("audit-verdict")`
has no predicate for empty `evidence`, and pulling all 237 rows to filter locally runs straight into §1.

**This is findings_21 §3's shape again** — *name the offending token in the failure*. The count tells
you a problem exists and refuses to tell you where.

**Remedy.** Return the ids: `{"evidenced": 225, "narrated": 12, "narrated_ids": ["AV-…", …]}`. It is
the same query with a second `SELECT id`. Twelve ids is not a payload problem, and if it ever were,
that is §1 again.

---

## 4. `package_migrate` leaves a non-table file in the canonical data directory

`db/CANONICAL.md:11-12` is unambiguous about what `data/` contains:

> `data/<table>.jsonl` — exactly one file per **non-empty** table; a table with zero rows has **no
> file** (a stale file for a now-empty table is deleted on write-back).

`tamheed-package/data/prompts.jsonl.converted` has sat in that directory since `a92e1b32`
(2026-08-13, *"convert legacy prompts to files (v3.0.0 migration 003)"*) and has never been modified
or removed.

**Found by the engine's own round-trip, not by looking for it.** `store.load()` → `store.dump()` on a
worktree at HEAD reproduced all 29 register files byte-identically, and the *only* difference between
the committed directory and the engine's output was this file — it is the one thing in `data/` that
`dump()` does not own.

Harmless in itself. It matters because the directory's whole value is that it is *exactly* the store:
the byte-stability guarantee (§5) invites operators to treat `git status` on `data/` as the integrity
question, and a foreign object living there permanently weakens that. A migration that converts a table
away should delete its leftover, or write it somewhere that is not the canonical directory.

---

## 5. Documentation / design request — the integrity instrument exists but nothing records that it ran

`db/CANONICAL.md:49-55` promises that an idle `package_open` → `package_close` round-trip on a
committed store produces **zero git diff**, and explicitly invites the operator to lean on it:
*"'did anything change?' is a `git status` question."*

**That guarantee held exactly, and it was the single most useful thing in this audit** (see below). But
it is a property you must know to exercise. There is no `package_verify` tool, nothing records that a
state was ever verified, and consequently every audit starts from zero. For a governance store there is
also no tamper-evidence of any kind — no row hash chain, no signature, no external anchor — so a clean
verification is not durable evidence of anything a week later.

**Cheapest useful version:** a `package_verify()` that performs the round-trip in a temp directory,
returns per-file byte-equality, and optionally appends a journal entry recording the verified digest.
That turns an undocumented property into a citable fact, and it costs one function over machinery that
already exists.

---

## Not findings — verified, and what I am not claiming

- ✅ **The byte-stability guarantee is real and I exercised it.** `store.load()` → `store.dump()` at
  commit `10e4be96` reproduced **all 29 register files byte-identically** (40 tables, 7,819 rows).
  This is the load-bearing claim in `CANONICAL.md` and it survived a deliberate attempt to falsify it.
- ✅ **`store.py` drives standalone.** `sys.path.insert` on the plugin's `db/` and calling
  `load()` (`store.py:118`) / `dump()` (`:158`) against an arbitrary directory works with no
  modification. That made a 488-commit historical audit possible; please do not accidentally couple
  those two functions to server state.
- ✅ **`sqlite3.connect(":memory:")` (`store.py:91`) is the right call and is worth keeping.** Because
  there is no persistent database, there is no second copy of the truth to drift — the JSONL *is* the
  store. It collapsed this audit's attack surface to files plus git.
- ✅ **`entity_upsert`'s failures are good failures.** `NOT NULL constraint failed:
  deferred_work.severity` named the table and column, and the batch rolled back atomically. The
  approving-upsert drift check (content must be resent byte-identical) also fired exactly as
  documented.
- ✅ **`entity_query` does not truncate field text.** I went looking for the truncation this project's
  trap 13 asserts and it is not in the query path. The original concern was row-*count* truncation, and
  `total` already fixed it.
- **NOT CLAIMED: that §1 is a tamheed bug.** It is a missing capability. The client's payload cap is
  not yours, and `total` means the tool never lies about the truncation — it just cannot help you past
  it.
- **NOT CLAIMED: that any of this caused data damage.** The audit found none. 488 commits, 1,675
  blobs, 5,207 row-level events: exactly two row deletions in the whole window (both `trace_edges`,
  both at the v4 migration), zero resurrections, no unexplained modification of an append-only table.
- **NOT CLAIMED: that 4.4.2 introduced any of this.** `entity_query`'s signature, `RELATION_RULES` and
  the `audit_evidence` shape all predate it; I have not checked when each first appeared.
- **NOT CLAIMED: that the audit could have detected a determined tamper.** It could not, and the
  reasons are architectural rather than instrumental — see §5. A hand-edit followed by any tool call
  before the commit is rewritten into perfect canonical form with a journal entry naming the row.

## Operator disposition on the ACMP side

`DEC-135` d1 (operator override) now makes package reads **and** writes MCP-exclusive for this project,
and 78 ad-hoc scripts were deleted. `DEC-136` commissions the audit and grants two audit-only carve-outs
recorded in `SC-049`. `DW-100` carries the consequence — the four committed slate generators read the
JSONL directly and are now non-compliant — and it is **blocked on §1**: they cannot be rebuilt on
`entity_query` until there is a way to page a register, because `LL-011` requires their full text. The
false sentence at `prm-next.md:1268-1271` is ours and is being corrected.

No waiver has been authored, and nothing in this file asks for the MCP-only rule to be relaxed.
