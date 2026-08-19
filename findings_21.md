# findings_21 — a content gate over an append-only journal has no repair path

2026-08-19, against **4.4.1** (`server_info`: version 4.4.1, `migrations_head` 003_skills.sql,
schema_version 3). **One defect, one design gap, and a documentation request.** All three come out of
a single event: `gate_run()` on this package has been permanently red since 2026-08-19, and no tool
an executing agent has can clear it.

The package is `tamheed-package` in the ACMP repo. It carries `DEF-093` (High, Open) for this, and
its readiness has been `ready:false` ever since — `defects-closed` fails on that one row.

**Nothing here is a data error and nothing here is ACMP's.** It is entirely in the planning
package's own tooling, and I caused it. I wrote the offending row. That is stated up front because
the interesting part is not the mistake — it is that the store offers no sanctioned way to undo one.

---

## 1. `G-COMPLETE` screens a column that lives in an append-only table, so one authoring slip is permanent

`G-COMPLETE` screens every TEXT column of every entity table for unfinished-work markers
(`tamheed_server.py:957-982`; the screening call is `:971`, `_PLACEHOLDER_RE` at `:270-272`).
`progress_entries.entry` is one of those columns. `progress_entries` is append-only by design.

**Those two facts are individually right and jointly a trap.** A row in that table can fail a
mechanical gate, and there is no operation in the server that can make it stop failing:

- `entity_upsert` on the row is refused — `UNIQUE constraint failed: progress_entries.id`, with the
  handler at `:807-811` adding *"append-only journal: append a new entry via progress_update /
  audit_record instead of editing history; corrections are recorded as new entries"*.
- There is **no delete or redact path**. `grep -rn "DELETE FROM progress_entries"` across
  `server/*.py` returns nothing.
- Appending the suggested correction does not help — see §2.

So the row is unreachable by every write path the server exposes, and it fails a gate forever.

**How it happened, because the shape matters more than the incident.** `DW-038` first tripped
`G-COMPLETE`. The mechanic is documented nowhere in the package, so I wrote a progress note
explaining it — and in explaining which tokens the gate screens for, the note names them. **The note
about the rule became the only row breaking it.**

That is not a freak accident. Any honest record of *why a scan fired* tends to contain the thing the
scan looks for. A journal is exactly where such a record belongs, and the journal is the one table
that cannot be repaired.

**Reproduced locally against the 4.4.1 screen rather than inferred.** I re-implemented
`_strip_code` + `_PLACEHOLDER_RE` verbatim and ran them over all 3694 rows / 16903 screened text
cells in `data/*.jsonl`, with `custom_attributes` exempted as the gate exempts it. Result: exactly
one failure, `{id: PE-469, column: entry}` — byte-identical to what `gate_run()` reports. I also
confirmed the instrument fires on a bare token and exempts a backticked one, so the reproduction is
not passing vacuously.

⚠ **The gate is behaving exactly as designed — that is the point.** In `PE-469` there are **seven**
raw matches and **six** survive `_strip_code`. The seventh was inside backticks and was correctly
stripped. The escape hatch works; I simply used it once out of seven times. Please do not read this
as a request to loosen the screen.

**The consequence is why `DEF-093` is High rather than cosmetic.** This project's operating
convention is to run the mechanical gates before declaring package changes done. A gate that cannot
be made green stops being a signal. Every session now has to be told, in the kickoff prompt, *never
report gates green; reconcile the failure list instead, and treat any unexpected id in it as a real
finding hiding behind the known one.* That instruction is doing real work — but it is a written
convention standing in for a mechanical guarantee, and it trains readers to skim a red gate.

### Remedies, cheapest first

1. **Make `G-COMPLETE` skip an entry that a later `correction` supersedes.** The mechanism already
   exists and is already populated — see §2, which is really the same finding seen from the other
   side. This is one join, it needs no new tool, no schema change, and no new concept for the
   operator to learn. It also gives `corrects` a reason to exist.
2. **Exempt `progress_entries.entry` (and `audit_verdicts.evidence`) from the placeholder screen**,
   the way `custom_attributes` is already exempt. The comment at `:961-963` makes the argument for
   me: *"custom_attributes is exempt (C14): it is provenance preserved verbatim, not authored
   content — grading it fails the package for being faithful."* A progress note is the same kind of
   thing. `G-COMPLETE` exists to catch **unfinished plan text**; a journal entry is a report of what
   happened and cannot be "unfinished" in that sense. Grading it fails the package for being
   accurate about its own tooling — which is precisely what happened here.
3. **A sanctioned redaction tool** — `progress_redact(id, reason)` that rewrites `entry` in place,
   stamps who redacted it and why, and appends a journal entry recording the redaction. More work
   than 1 or 2, and it is the only one of the three that also covers the general case of a journal
   entry that must change for a reason other than a marker (an accidentally pasted secret, say).
4. **Failing all of those, document the escape hatch where an agent will meet it.** Two sentences in
   `progress_update`'s docstring — *this text is screened by `G-COMPLETE`; wrap marker tokens in
   backticks* — would have prevented this entirely, and would still be worth adding alongside any of
   the fixes above.

I have not attempted a workaround. Hand-editing `data/progress_entries.jsonl` would fix it in about
four seconds and would violate the never-hand-edit rule the whole package rests on, so it was never
on the table.

---

## 2. `corrects` is written and displayed but never read — the correction mechanism has no effect on anything derived

This one stands on its own regardless of what happens to §1.

`progress_update` accepts and stores `corrects` (`:1373-1378`). `export_html.py:596-600` selects it
into the HTML table. **Nothing else in the server ever reads it.** Every occurrence across
`server/*.py`:

| Location | What it does |
|---|---|
| `tamheed_server.py:1354, :1360, :1946` | docstrings and the tool cheat-sheet |
| `tamheed_server.py:1374, :1377` | the INSERT |
| `export_html.py:596, :600` | a display column |

No gate consults it. No readiness rule consults it. No derived view filters on it.

**So a correction is a comment, not a correction.** The tool description tells the agent that history
is corrected by appending a typed `correction` entry — and the refusal message at `:809` actively
routes you there when an in-place edit is refused. I followed that instruction: `PE-470` exists,
`event_type: correction`, `corrects: PE-469`, and it contains no marker tokens at all (0 raw matches,
verified). `G-COMPLETE` still fails on `PE-469`.

⚠ **The two halves compound.** The refusal message points at the correction path; the correction path
is inert; the caller is left believing history has been corrected. An agent that trusts the tool
output — which is the behaviour the whole design encourages — ends up with a package that reports a
correction it did not actually apply anywhere it matters.

**Remedy.** Give `corrects` at least one consumer. `G-COMPLETE` skipping superseded entries (remedy 1
above) is the obvious first one and closes both findings at once. Beyond that, the natural reading is
that a corrected entry should be visually struck through or collapsed under its correction in
`review.html`, rather than sitting in the timeline as an equal peer to the entry that replaced it.

---

## 3. Documentation request — the screen is invisible until it fires

`G-COMPLETE`'s content tier is not described in the tool descriptions, the prompts, the README, or
the skill. Its existence is discoverable only by tripping it, and its failure output — `{"id":
"DW-038", "column": "title"}` — names the row and the column but not **what** was found in it.

Two small changes would have saved this project two incidents:

- **Name the offending token in the failure.** `{"id": "...", "column": "title", "matched": "TODO"}`.
  The current output sends you to read a 7000-character title and guess. When `DW-038` first tripped,
  identifying the cause meant scanning every `data/*.jsonl` by hand — and the first hypothesis was
  wrong (the working assumption was a length limit, because the failing title was long).
- **Say that the screen exists** in `progress_update` / `entity_upsert`, with the backtick exemption.

---

## Not findings — verified, and what I am not claiming

- **The code-span exemption works.** `_strip_code` (`:42-44`) strips fenced blocks
  (`_CODE_FENCE_RE`, `:38`) and inline spans (`_INLINE_CODE_RE`, `:39`) before screening. Confirmed
  by reproduction on both a positive and a negative, and by the one stripped occurrence inside
  `PE-469` itself.
- **`custom_attributes` is exempt and the reasoning is sound** (`:961-963`, C14). §1 remedy 2 is an
  argument to extend that same reasoning by one table, not to weaken it.
- **The `NEEDS-CLARIFICATION` half of `G-COMPLETE` is not implicated.** `clarifications-open` passes
  on this package and `_scan_markers` contributes nothing to the current failure.
- **NOT CLAIMED: that this is reachable any other way.** I found one route in — a progress note
  quoting a token in prose. An entity `title` can do the same, but an entity row can be upserted, so
  it self-heals; `DW-038` was fixed in seconds by rewording. **The trap is specific to the two
  append-only tables**, and I have only exercised `progress_entries`. `audit_verdicts.evidence` looks
  identical in shape and I have not tested it.
- **NOT CLAIMED: any performance or correctness problem with the screen itself.** 16903 cells
  screened in well under a second locally.
- **NOT CLAIMED: that 4.4.1 introduced this.** The screen and the append-only journal both predate
  it. I have not checked when the two first coexisted.

## Operator disposition on the ACMP side

`DEF-093` stays Open at High until there is a sanctioned route. The kickoff prompt carries the
warnings in the meantime, and the running convention is: never report a gate count, reconcile the
failure list every run, and treat any id other than the known one as a real finding. No waiver has
been authored.
