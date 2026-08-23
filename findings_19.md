# findings_19 — v4.3.0 upgrade + the first lesson

2026-08-15. Short by design, in the findings_18 mould: most of this run was clean and clean runs
need no report. **Two things are worth recording and one is not minor** — a `handoff_emit`
misclassification whose remedy advice is destructive, and a gap in the brand-new lesson feature that
its first real use walks straight into. A third is a one-paragraph DX request.

> ⚠ **This report exists because the first call was wrong.** I closed the run saying "findings_19:
> not written, deliberately — the run was clean." I had applied *clean* to my own execution. The
> operator's rule is "**only if something looks wrong**", and what looks wrong here is the tool.

---

## 1. ⚠⚠ `handoff_emit` CALLED A BARE HEADING A "v1 NOTE" AND ADVISED DELETING THE IMPORT

`handoff_emit(target_dir=<repo root>, refresh_stock=true)` returned `ok: true`, `written: []`,
`unchanged: ["CLAUDE.md"]`, and this warning:

> `CLAUDE.md carries the v1 Tamheed operating note — delete the '## Tamheed progress tracking'
> section and re-run handoff_emit; the v2 note is marker-managed and self-updates thereafter`

Three separate problems, in ascending severity.

**(a) It matched a HEADING, not a managed span.** This project's *root* `CLAUDE.md` has a
`## Tamheed progress tracking` section with **no `<!-- tamheed:note -->` markers** and no obligations
table — it is a pointer. The genuine marker-managed span lives in `tamheed-package/CLAUDE.md` and was
sitting at `<!-- tamheed:note v3 -->` at that moment. The classifier never checked for markers, and
never checked whether the package's own `CLAUDE.md` already carries a span.

**(b) THE ADVICE IS DESTRUCTIVE, AND IT RECREATES THE EXACT FAILURE THE DELETED TEXT DOCUMENTS.**
The section it says to delete is nine lines, and its last line is:

```
@tamheed-package/CLAUDE.md
```

That is **the import**. Deleting the section therefore (i) stops the tool-owned obligations table
loading into any future session at all, and (ii) leaves `handoff_emit` free to write a *fresh* note
into the root file — so the project ends with **two copies** of a mandatory protocol. The prose being
deleted is, verbatim, the record of that having already happened here: the inlined copy "went **two
generations stale** — it still carried `tamheed:note v2` … against a v4 store", and "a stale copy of
a *mandatory* protocol is worse than no copy". An operator who trusted the warning would undo the fix
and re-open the wound in one step.

**(c) A SUCCESS-SHAPED NO-OP — the tool's own hollow pass.** `ok: true`, no error, `CLAUDE.md` listed
under `unchanged`, six prompts genuinely refreshed. Everything reads like a completed upgrade. The
real note was untouched at v3 and **the lesson-capture obligation had not been added**. A session
that trusted the summary would believe the v4 note had landed. This project has now recorded this
shape three times — `risk-liveness` passing because its predicate could not be evaluated
(findings_18 §3), two `NotContain` controls passing over a null column (`DEF-056`, this week), and
now a tool reporting `unchanged` for a file it was never going to touch.

**What produced the wrong aim**, and it is worth fixing too: `target_dir` is **asymmetric**. The
prompt library resolves against the *package* regardless of `target_dir` — `prompts/*` correctly
landed in `tamheed-package/prompts/` on the root-aimed call — while the note follows `target_dir`.
So the same call was simultaneously right about nine files and wrong about the one that mattered,
which is precisely why the mistake was not obvious from the result.

**Remedies, cheapest first.**
1. **Require the markers.** Do not classify anything a "v1 note" on a heading match alone.
2. **Look where the note actually lives.** If `<package>/CLAUDE.md` already carries a managed span,
   say so and name that path, instead of advising deletion of a same-named heading elsewhere.
3. **Name the full path** in every note-related warning. `"CLAUDE.md"` is ambiguous in any repo with
   more than one.
4. Minor: the warning still says *"the **v2** note is marker-managed"* — stale wording against a
   store that now emits v4.

*(Not fixed here. Aimed at `tamheed-package`, the span rebuilt v3 → v4 with the lesson obligation
added, and the root file was verified untouched. Recorded as `PE-364`.)*

---

## 2. ⚠ A LESSON'S CONTENT IS UNPROTECTED ON EXACTLY THE WRITE THAT MAKES IT IMMUTABLE

`trg_lessons_immutable` fires `WHEN OLD.lifecycle_status = 'Approved'`. The transition
**Proposed → Approved** therefore has `OLD.lifecycle_status = 'Proposed'`, so the trigger does not
fire and every content column is freely writable on that call.

That would be harmless if approval were a status flip. It is not: **`entity_upsert` requires FULL
rows** — proven by the store's own refusal of a minimal update, `NOT NULL constraint failed:
defects.title — the row exists; entity_upsert requires FULL rows even for updates` — so approving a
lesson **necessarily re-sends `title`, `statement`, `context`, `recommendation`, `rationale`,
`category` and both impact columns**. Immutability begins **one write too late**, and the write it
skips is the one carrying the most re-transcribed text in the row's life.

The gap is also, precisely, `LL-001`'s own subject. Approving `LL-001` was the first thing `LL-001`
governed: a pre-image was saved beforehand and all ten frozen columns verified byte-identical
afterwards. That verification found nothing this time — which is the point. It is a workaround an
agent has to know to perform, not a property the store guarantees.

⚠ Note also that `confirmed_by` and `confirmed_at` are inside the frozen set, so **approval and
attribution must land in a single upsert** — approve first and the attribution can never be added.
That is defensible (facts about a person are never back-filled) but it is not stated anywhere the
caller sees; it was read out of the migration DDL.

**Remedy.** On a `Proposed → Approved` transition, diff the content columns against the stored row
and either warn in the item result when they differ, or require an explicit flag to accept the
change. Cheap, and it closes the only window in which the immutability guarantee does not apply.

---

## 3. FK failures name nothing, while NOT NULL failures name everything

Setting `DEF-056` to Fixed, I invented a prose value for `found_in`, not knowing it is a foreign key
and was null. The store refused correctly and rolled the whole batch back with `applied: 0` and no
partial state — the mechanism is right and is not the finding. The **message** is:

```
FOREIGN KEY constraint failed
```

Compare the NOT NULL path in the same tool, which names the table and column *and* explains the rule
that caused it:

```
NOT NULL constraint failed: defects.title — the row exists; entity_upsert requires FULL rows
even for updates (INSERT evaluates NOT NULL before conflict resolution)
```

Request: parity. Naming the offending column (and ideally that it is an FK, so the caller stops
trying synonyms) would have saved a round-trip on a row whose title is 3102 characters.

---

## Not findings — recorded so the next run does not re-derive them

- **`package_migrate` preview matched the documented shape exactly**: `mode: "registry-sync"`,
  `entity_types_added: ["lesson"]`, nothing else, no backup taken. The applied diff was **two lines**
  — one `entity_types` row (`lesson`, `LL-`, `Continuous`) and `PE-363`. The existing
  `data-v3-backup/` did not block it, as documented.
- **`refresh_stock` had a real subject, not just a green exit**: 6 stale-stock prompts refreshed,
  **14 lesson mentions added**, including `register-liveness`'s new **step 14** (the operator
  interview).
- **`learned_from` endpoint rules work and are correctly tight** — `lesson →`
  {`defect`,`decision`,`risk`,`slice`,`wbs-item`,`progress-entry`}. `PE-336` is a `progress-entry`
  and was accepted.
- **Same-batch endpoint visibility works**: `LL-001` and its `learned_from` edge went in one
  `entity_upsert` (`applied: 2`) — the entity_index trigger fires per statement, as the code comment
  claims.
- **`trace-edge` is upsertable although absent from `entity_types.jsonl`** — correct (that registry is
  for ID-prefixed entities), but it costs a reader a detour to establish.
- **`lessons-confirmed` surfaced `LL-001` as an advisory failure** the moment it was written, and the
  note + `review.html` both render the Lessons section once it was Approved and pinned.
- **`gate_run` 7/7 after every write**, `G-COMPLETE` untripped.

---

## Still operator-reserved (carried from findings_18 §2, and now sharper)

The three CUSTOMISED prompts lag again, and this release makes it matter more than last time:
`orient-resume.md` and `slice-review.md` both report `stock_last_changed: 4.3.0`, so **those two
specifically lack the new lesson steps** — and `slice-review` is where lessons are meant to be
captured. `integrity-check.md` lags at 4.2.1. Reconciling is a hand-merge from
`stock-history.json` and remains an operator call; `force=true` would overwrite all three.
