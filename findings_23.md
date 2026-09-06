# findings_23 — a new relation that cannot replace the old one, and an honesty counter pointed at the wrong population

2026-09-06, against **4.5.0** (`server_info`: version 4.5.0, `migrations_head` 004_amends_verify.sql,
schema_version 4, 17 tools). **Two design gaps, one wording fix — and every section of `findings_22`
verified closed by observation rather than by the release notes.**

The package is `tamheed-package` in the ACMP repo. All three findings came out of *using* 4.5.0 to do
the close-out, not from reading it. **The report leads with the complaints because that is what a
maintainer can act on, but the honest headline is the last section: four for four, and the two fixes I
cared most about landed better than I asked.**

Nothing here is a regression. §1 is a gap the new feature created by arriving alone.

---

## 1. `amends` is additive-only — the server exposes no way to delete a trace edge, and `gate_run` advises exactly that

4.5.0 shipped the `amends` relation so a `scope-change` can be typed against the ruling it modifies —
`findings_22` §2, and it works: `SC-046 → DEC-128 amends` and `SC-049 → DEC-135 amends` were both
accepted first try, where `scope_modifies` had been refused.

**But `trace_edges` has composite primary key `(from_id, to_id, relation)`, so adding `amends` does not
replace the `relates_to` it was meant to retype — it sits beside it.** Both edges now stand on both
pairs, and `trace_query` returns them together:

```
SC-049 → DEC-135  amends
SC-049 → DEC-135  relates_to
```

**There is no delete. Proven three ways rather than asserted:**

| # | Check | Result |
|---|---|---|
| 1 | `grep "DELETE FROM trace_edges"` across `server/*.py` | **nothing** |
| 2 | `entity_upsert`'s trace-edge path, `tamheed_server.py:820-822` | `INSERT OR IGNORE` only |
| 3 | The only edge-removal code, `:511-518` | `_filter_jsonl` over `PRM-`-prefixed edges inside legacy prompt conversion — a one-shot v3.0.0 path, unreachable from any tool |

⚠ **And the gate actively recommends the missing operation.** `G-REL`'s note, verbatim from this
morning's `gate_run`:

> *"stored edges must satisfy RELATION_RULES; **retype a wrong edge to `relates_to` (delete + re-add)**
> if the link itself is real"*

**This is `findings_21` §1's shape in a new place** — a message routing the caller to a remedy the
server cannot perform. It bites harder now than when that was written, because 4.5.0 created the
*reason* to retype and shipped no way to complete it.

⚠ **The residue is redundant rather than false**, which is why I applied it anyway on the operator's
call: `relates_to` is the documented untyped escape hatch, so `amends` subsumes it, and `G-REL` passes
on both because each is independently well-typed. But it is exactly the noise `findings_22` §2 was
about — anything counting `relates_to` still sees these. `SC-050` records the two deletions as owed.

### Remedies, cheapest first

1. **A `retire` flag on the trace-edge write path.** `entity_upsert` already resolves the composite PK
   at `:843-848` to distinguish an idempotent duplicate from a constraint rejection; a
   `{"type":"trace-edge", "from_id":…, "to_id":…, "relation":…, "retire": true}` item could reuse that
   same lookup to `DELETE`. No schema change, no new tool.
2. **Or make `amends` (and the `scope_*` family) REPLACE rather than insert** when an edge already
   exists between the same ordered pair — a retype is what the caller means, and it is what `G-REL`'s
   note already tells them to do.
3. **Failing both, fix the note.** `G-REL` should not name an operation the server does not expose.
   *"…retype it by adding the correct relation; the old edge cannot currently be removed"* is two
   sentences and stops the caller hunting for a delete that is not there.

---

## 2. `audit_evidence.narrated` counts a different population than its note describes

`gate_run` reports `{"evidenced": 225, "narrated": 12, "note": "narrated verdicts are the graded party
grading itself (C7)"}`, and 4.5.0 now names them — `narrated_ids`, which is `findings_22` §3 delivered
and genuinely useful. **Reading the twelve is what showed the count means something else.**

The predicate (`tamheed_server.py:1002-1008`) is a clean `SUM(CASE WHEN evidence IS NULL OR evidence =
'' …)` over **all of `audit_verdicts`**. Two consequences, both measured on this package:

**(a) It counts placeholders nobody ever graded.** All twelve — `AV-004`, `AV-075`–`AV-085` — are
`verdict: "Pending"` with `verified_by`, `verification_method`, `against_commit` **and** `evidence` all
`null`. Nobody graded their own work; **nobody graded anything.** *"The graded party grading itself"*
describes a verdict asserting `Met` on prose instead of a reference — a different and more concerning
row than an untouched placeholder.

**(b) It counts superseded verdicts.** Verdicts append, and `acs-met` correctly reads only the LATEST
per AC. `narrated` does not. `AC-004` carries three:

```
AV-004  Pending   (counted as narrated)
AV-148  Partial
AV-152  Met       ← the live verdict
```

Eleven of the twelve ACs are live `Approved` (only `AC-084` is Superseded), and `acs-met` **passes** —
because every one has a later Met verdict. **So the honesty counter is reporting rows the package
already replaced**, and the number cannot go down as work improves, only up as history grows.

⚠ **The harm is that the one C7 signal in the report is unactionable in the direction it matters.** An
operator asking *"which verdicts did the agent grade without evidence?"* gets twelve ids, reads them,
and finds twelve rows that answer a different question. A genuine self-graded `Met` would be invisible
in that list among placeholders.

### Remedy

**Count latest-per-AC, as `acs-met` already does**, and split the two populations:
`{"evidenced": n, "narrated": n, "ungraded": n}` — `narrated` = latest verdict is a real verdict with
empty evidence (the C7 case); `ungraded` = latest verdict is `Pending`. On this package that would
report `narrated: 0`, which is the true and much more reassuring answer.

---

## 3. `package_migrate`'s relocate justification names the weaker of the two safety nets

The registry-sync preview reported, correctly and usefully:

```json
{"file": "data/prompts.jsonl.converted",
 "action": "remove (identical copy already in data-v3-backup/)"}
```

I verified the claim before confirming (`cmp` → byte-identical, 11,003 bytes) and it is true. **But
`data-v3-backup/` is gitignored** — `.gitignore:65`, by tamheed's own generated block. A copy in an
ignored directory is not a durable backup: it exists on one machine and is one `git clean -xdf` from
gone.

The actual safety net is that `data/prompts.jsonl.converted` was a **tracked** file, so the removal is
an ordinary recoverable deletion. That is the stronger guarantee and the message does not mention it.

**Remedy.** One clause: `"remove (tracked in git; identical copy also in data-v3-backup/)"`. Small, but
this string is the entire basis on which an operator approves a destructive step, and it currently
points at the half that a clean checkout does not preserve.

---

## Not findings — verified, and what I am not claiming

- ✅ **All four `findings_22` sections are closed, each verified by observation.** §1 by walking the
  defect family — **6 pages, 141 ids, `total` constant, `next_after` null**, with `ids=[...]` returning
  full rows and `search` proven against a positive control. §2 by `amends` being accepted
  `scope-change → decision`. §3 by `narrated_ids` appearing. §4 by `foreign: []`.
- ✅ **`package_verify` is better than the `package_verify()` I asked for**, and the detail that proves
  someone thought about it: the journalled row states *"of the store state BEFORE this row (recording
  rewrites `progress_entries.jsonl`, so the next verify's digest differs by construction)"*. **The row
  forecloses the exact confusion its own existence creates.** Confirmed: `def033d6…` → `d81c9185…`.
- ✅ **`foreign: []` is a better instrument than the finding that produced it.** `findings_22` §4 was
  found as an unexplained residue falling out of a hand-built round-trip; 4.5.0 runs that round-trip
  and reports the residue as a field.
- ✅ **`entity_query`'s three new parameters compose exactly as the slate generators need**: `search`
  to find a set, `ids` to quote it verbatim, `after_id` to walk a register. `DW-100` is unblocked.
- ✅ **`RELATION_RULES` refuses well.** The `scope_modifies → decision` rejection named the relation,
  the direction, both endpoints, the full allowed set and the escape hatch, and rolled the batch back
  **atomically** rather than half-applying.
- **NOT CLAIMED: that §1 is a regression.** Nothing that worked before stopped working. `amends` is
  new; the missing delete is a gap it revealed by arriving alone.
- **NOT CLAIMED: that §2 miscounts on any other package.** Measured here only. The predicate is
  package-independent, so I expect it generalises, but I have not run it elsewhere.
- **NOT CLAIMED: that the twelve placeholders are a tamheed defect.** They are ours. The finding is
  that the counter's *note* describes a population it does not select.
- **NOT CLAIMED: any verification of `findings_19` §3** (FK errors naming the column and value). No FK
  failure occurred and I did not inject one — carried unverified for a fourth release.

## Operator disposition on the ACMP side

`DEC-137` records the session; `SC-050` records §1's residue with an explicit cleanup condition — remove
exactly `(SC-046, DEC-128, relates_to)` and `(SC-049, DEC-135, relates_to)` when a delete exists, and
it stays `Approved` rather than `Merged` precisely so the debt is visible. §2 changed nothing: the
twelve were read and **repaired nothing**, which is a separate act with the operator present.

Two of our own documents were found stale and are ours to fix, not tamheed's: `AGENTS.md` names three
customised prompts where only **two** are (`orient-resume.md` matches stock 4.3.0 byte-for-byte), and
`PE-367`/`PE-370` both claim `slice-review.md` still lacks the 4.3.0 lesson steps — it carries all four
markers. **The real hand-merge debt is `integrity-check.md` alone**, which carries zero of five 4.5.0
markers. No waiver has been authored, and nothing here asks for the MCP-exclusive rule to be relaxed.
