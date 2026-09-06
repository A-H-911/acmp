# findings_24 — MCP-exclusivity has no story for a committed generator, and that is the one artifact tamheed's own doctrine most depends on

2026-09-06, against **4.6.0** (`server_info`: version 4.6.0, `migrations_head` 004_amends_verify.sql,
schema_version 4). One finding, and it is a **gap rather than a defect** — nothing that worked has
stopped working.

The package is `tamheed-package` in the ACMP repo. This one did not come out of using a new feature.
It came out of applying a project rule that tamheed's own documentation states, and discovering that
the rule and one of tamheed's own recommended practices cannot both be satisfied.

**The honest headline is in §"Not findings": `entity_query` is now fully sufficient on the two things
I was afraid of, and I was wrong about the second for five days in a pinned lesson.** The gap that
remains is narrow, and it is transport.

---

## 1. A committed script has no sanctioned route to the store, so "all reads go through the MCP tools" and `LL-011`-style evidence artifacts are mutually exclusive

### The rule

`tamheed-package/CLAUDE.md` — tool-owned, emitted by `handoff_emit` — states:

> *"All package reads/writes go through the `tamheed` MCP tools"*

On 2026-09-06 our operator made that unconditional for this project (`DEC-135` d1: **no exceptions**),
after two mid-session interventions caught an agent reading `data/*.jsonl` directly. 78 ad-hoc scripts
were deleted; an integrity audit followed (`DEC-136`). **Nothing here asks for that rule to be
relaxed.** It is the right rule and the audit it triggered was worth having.

### The practice the rule collides with

We have a lesson, `LL-011`, that exists because the operator once **refused an interview** held against
a slate that cited ~40 package records by identifier alone. The remedy became doctrine here: any
artifact the operator reads *in order to decide* must carry each cited record's **own text**, quoted
mechanically from the canonical store — never paraphrased, never re-typed by the agent. Four committed,
reviewed generators discharge it:

| Script | Produces | Reads |
|---|---|---|
| `gen-slice-review-slate.mjs` | the per-item slice-review page a verdict is taken against | 7 families |
| `gen-record-slate.mjs` | a cross-register slate for an interview | any family |
| `gen-lesson-docket.mjs` | the docket a lesson is approved against | `lessons` |
| `gen-dw-disposition-slate.mjs` | the deferred-work disposition slate | 5 families |

This is not a local invention. `prompts/slice-review.md` and `prompts/README.md` both put a per-item
verdict against quoted canonical text at the centre of the ceremony, and the note's own recording
obligations say `Implemented` means *verified*, adjudicated per item.

### The gap

**All four are `node` scripts, and a `node` process has no MCP client.** There is no in-language route
from a committed script to the store. So under an MCP-exclusive read rule the generators are
non-compliant on their read path, and the only two ways out are both bad:

1. **Hand-transport.** The agent runs `entity_query`, then pastes the rows into a file the script
   formats. That is exactly what `LL-011` and `LL-001` forbid — the hand becomes the transport, and we
   have a measured instance of a 4,296-character field losing a whole paragraph mid-write with
   `ok: true` returned (`LL-063`, and our trap 14a). A slate with a hole *reads exactly like a slate
   without one*, which that generator's own header calls the most dangerous shape it can produce.
2. **Re-implement an MCP client per consumer.** Which is what I proposed, and what our operator
   declined — see below.

### What I proposed, proved, and did not ship

I wrote a throwaway probe that spawns the server and speaks the protocol:

```
uv run <plugin>/server/tamheed_server.py --package-dir <repo>
  -> initialize / notifications/initialized
  -> tools/call package_open      -> ok
  -> tools/call entity_query      -> both Approved slices
  -> tools/call entity_query ids  -> DW-100's title WHOLE at 4,405 chars
  -> tools/call package_close     -> ok
```

**It works.** It is the MCP protocol itself, so it cannot be read as an exception to the rule.

**Our operator declined it anyway** (`DEC-138` d1, overriding my recommendation), and on reflection the
reasoning is worth passing upstream because it is about tamheed's shape rather than about our project:
*it is a workaround shaped like compliance*. Every consumer that must quote the store re-implements a
JSON-RPC handshake, a lock dance and a paging walk, and **each copy is a place `LL-011`'s guarantee can
rot silently** — a short read, a dropped page, an un-closed lock. `DEC-135` d1's own rationale had
already said the general form: *a gap in the MCP surface is a reason to ASK, not a licence to route
around the rule.* This report is that sentence applied to tamheed rather than to the agent.

⚠ **And the lock makes it worse than plumbing.** `package_open` is `O_EXCL` on `data/.lock`
(`db/store.py:205-214`) and `entity_query` guards on `_need_open()`. An agent session holding the
package open **must `package_close` before the generator runs and re-open after** — my probe only
worked because it did exactly that. A forgotten re-open is unrecorded work, and a crashed generator
leaves a stale lock, which is `DEC-137` d1's shape arriving on a schedule instead of by accident.

### The constraint a fix has to satisfy — deliberately not a design

I am not going to name the shape; that is the maintainer's call, and a report that pre-designs
someone else's fix ages against their release. What a fix has to be true of:

- **A committed, reviewed, version-controlled script can obtain whole canonical rows** without opening
  `data/*.jsonl` and without re-implementing the protocol.
- **Whole rows.** `LL-011` needs byte-exact field text, not a rendering, not a summary, not a cap.
- **It composes with `columns` / `ids` / `after_id` / `search`**, so a generator can page a family and
  quote a known set — all four already exist and are the right primitives.
- **It does not require the caller to reason about the writer lock**, or it makes the failure loud and
  the remedy obvious when it does.
- **Read-only is sufficient.** No consumer here needs a write path; the MCP-only write rule has held
  for six sessions and nobody is asking to weaken it.

Shapes I can imagine and am explicitly *not* recommending: a `--export` CLI mode beside `--selftest`;
a read-only open that skips the lock; an `entity_export` tool writing JSON to a path the way
`export_html` writes HTML. **Any of these, or none of them.** The gap is what I am reporting.

### What it costs us today

Our `SL-038` has two items at `Review` — `WBS-29` and `WBS-30` — and `DEC-079` d3 makes their verdicts
the operator's alone, against a generated slate. **The slate is currently non-compliant, so both are
held**, scheduled as `WBS-31` and blocked on this report. We are choosing that over adjudicating them
outside the slate, because `DEF-142` exists precisely to record that four earlier items were
adjudicated that way with nothing noting the exception.

---

## Not findings — verified, and what I am not claiming

- ✅ **`entity_query` does not truncate fields, and I had this wrong in a PINNED lesson for five
  days.** `tamheed_server.py:993-1076` is `SELECT {', '.join(cols)} FROM {table}{where_sql} ORDER BY
  id LIMIT {limit + 1}` then `dict(zip(cols, r))` — **no slice and no cap on any value anywhere
  between the SELECT and the return**, and the docstring says so in terms. Our `LL-045` (Approved,
  pinned) opened by asserting the opposite as settled fact, and three other surfaces quoted it. It is
  superseded. **Four documents agreeing was one instrument, because all four were quoting each other.**
- ✅ **The client-side payload cap is not the same failure and is handled correctly.** A broad query
  here really did fail with *"88,131 characters exceeds maximum allowed tokens"*. **That is an ERROR,
  never a silent short row** — which is the whole difference. `columns`, `ids` and `after_id` are the
  remedy and all three work.
- ✅ **`trg_lessons_immutable` is the best guard in this schema and it earned its keep this session.**
  Superseding `LL-045` meant re-transmitting its NOT NULL content columns, which is the exact hazard
  that lesson is *about*. The trigger makes the paste self-verifying: byte-identical or `ABORT`.
  Calibrated rather than trusted (`LL-013`) — a deliberately corrupted title against another Approved
  lesson returned *"approved/promoted lessons are immutable: supersede, never edit"* and rolled the
  batch back atomically. ⚠ **The registers that carry comparably long text have no such guard** —
  `deferred_work.title`, `wbs_items.title`, `scope_changes.description`. Not filed as a finding; the
  asymmetry may well be deliberate, and I mention it only because §1's hand-transport route would land
  squarely in the unguarded half.
- ✅ **`retire` works exactly as `findings_23` §1 asked**, and it caught an error of mine within an
  hour of my writing the rule that catches it: I added `SC-051 → DEC-138 amends` by habit, and
  `DEC-137` d4's discriminator — *when a scope-change's `decision_ref` names the same row as the
  proposed `amends` target, the edge is provenance, not amendment* — refuses it without reading a word
  of prose. Retired in one call, journalled.
- ✅ **`ids` and `search` compose**, which is what made §1's verification possible:
  `entity_query(type, ids=["DW-100"], search="<phrase>")` scopes a substring test to one row. I used
  it as a paragraph-level survival check on a 4,405-character re-transmission — seven distinctive
  phrases, one per paragraph, plus a negative control differing by **one word** that correctly
  returned zero.
- **NOT CLAIMED: that this is a regression.** MCP-exclusive *reads* are new here — the write rule
  dates from our first package commit, the read rule from 2026-09-06. For almost all of this history
  there was no rule for the generators to violate.
- **NOT CLAIMED: that the four generators are wrong.** They work, they quote the canonical store, and
  they are the reason `LL-011` has been discharged mechanically eight times. The problem is their read
  path's *compliance*, not their correctness.
- **NOT CLAIMED: that spawning the server is unsafe.** It is proven to work. It was declined as a
  pattern to institutionalise, not as a technique that fails.
- **NOT CLAIMED: any measurement of how other packages use the generators pattern.** This is one
  project's practice, formalised in one lesson. If nobody else builds evidence artifacts from the
  store, §1 is a request from a single user and should be weighed as one.

## Operator disposition on the ACMP side

`DEC-138` records the round (one override of four, on d1). `SC-051` scopes `WBS-31` into `SL-038`,
created `Approved` and **blocked**, so the hold is visible to `wbs-done` rather than floating.
`DW-100` is updated in place: its first task is marked settled with the measurement, its original
trigger is kept for its reasoning but demoted, and the new blocker is named. `LL-045` is superseded by
`LL-063`, which carries its surviving half plus the reason the circularity broke. `PE-910` is the
measurement of record.

**No waiver has been authored, and nothing here asks for the MCP-exclusive rule to be relaxed.**
