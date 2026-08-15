# findings_20 — v4.4.0 upgrade

2026-08-16. **One item, and it is minor** — a clause in `package_migrate`'s own report that is not
true. Reported anyway because it is a false statement in tool output, and because the same wording
will front every future registry-sync. The rest of this upgrade was clean, and two findings_19 fixes
were verified by reproduction rather than read off the release notes.

---

## 1. "only the entity-type registry changes" is inaccurate — the migration also ships DDL

`package_migrate("tamheed-package")` reports, in both preview and applied stages:

```
"mode": "registry-sync",
"note": "v4 store: only the entity-type registry changes — no data transform,
         no backup taken (pure append)",
"entity_types_added": ["skill"]
```

Two of those three clauses hold. **`no data transform` is true** — I verified every shared value on
the one affected row byte-identical. **`entity_types_added` is accurate.** But
**`only the entity-type registry changes` is false**, and `003_skills.sql` is where it comes apart:

- `lessons` gains a `promoted_to` column and a new `lifecycle_status` value, `Promoted`;
- `trg_lessons_immutable` is **replaced** with a wider `WHEN` (now covering `Promoted`, and freezing
  `promoted_to` on a promoted row);
- `progress_entries` gains two `event_type` values, `lesson-confirmed` and `lesson-promoted`;
- both tables are `DROP`ped and recreated to do it (safe, and the SQL says why — migrations apply on
  a fresh in-memory DB before the JSONL load).

**The observable consequence** is that `git status` after the migration shows
`tamheed-package/data/lessons.jsonl` **modified**, not appended — because every lesson row is
re-serialised with the new column. Under a banner reading *pure append* and *only the entity-type
registry changes*, a modified data file is alarming, and the honest response is to stop and diff it.
That cost a verification cycle here (result: `promoted_to: null` added, all shared values identical).

⚠ **The forward-looking half is why this is worth a line at all.** `registry-sync` is now the
standing mode for lesson- and skill-shaped releases, and this note is generated per-mode, so the same
"only the entity-type registry changes" will accompany every future one — including releases that
ship more DDL than this one did. An operator who learns that registry-sync means *nothing but the
registry* has learned something that was already untrue at 4.4.0.

**Remedy, cheapest first.**
1. Enumerate what actually changed beside `entity_types_added` — the applied migration files, or the
   schema deltas (columns added, CHECK values added, triggers replaced). `["003_skills.sql"]` alone
   would have answered the question the git diff raised.
2. Failing that, soften the clause: *"no data transform; existing rows may be re-serialised if a
   table gained a column."* That one sub-clause removes the surprise entirely.

**Not a request to change the behaviour.** The migration did the right thing, took the right
decision not to back up, and the recreation is sound. Only the description is wrong.

---

## Not findings — the two findings_19 fixes, verified by reproduction

Recorded because a fix confirmed only by its release note is not confirmed.

**§1 (the destructive note advice) — FIXED, proven by re-running the misfire.** Rather than aiming
correctly, I aimed `handoff_emit` at the **repo root** again, which is what produced the original
finding. It now returns:

```
C:\...\acmp\CLAUDE.md imports the package note (@tamheed-package/CLAUDE.md) — the managed
span lives at C:\...\acmp\tamheed-package\CLAUDE.md and was updated there; the root file
was left untouched
```

Both full paths named, the pointer pattern recognised, and **the "delete that section" advice is
gone**. I also checked the claim rather than the wording — the root file is untouched and the managed
span is byte-identical at `v4` — because *"was updated there"* sitting next to an `unchanged` listing
is the exact shape that produced §1(c) in the first place. It holds.

**§2 (content unprotected on the write that makes it immutable) — CLOSED, and more completely than
I proposed.** I asked for a *warning* when content drifts on a `Proposed → Approved` transition. The
server instead **refuses the write**:

> `approval/promotion is not an edit — content drifted on [...]; send the stored content
> byte-identical, or supersede first`

And it goes further than the report did, in three ways worth naming:

- the guard fires on **any** write landing a lesson in `Approved`/`Promoted` from a different state,
  **including a row born there** — closing an insert-as-Approved bypass I had not considered;
- `confirmed_by` is required **on** the transition write (*"attribution lands WITH approval —
  confirmed_by can never be added later"*), which was previously only discoverable by reading the DDL;
- promotion additionally requires prior approval and an existing `SKL-` row.

So `LL-001`'s own discipline is now a **precondition of the store** rather than a habit an agent has
to remember — which is the strongest possible outcome for a lesson about the hand being the untrusted
transport.

**§3 (FK errors naming the column and value) — NOT VERIFIED.** No foreign-key failure occurred in
this run and I did not inject one. Recorded as unverified rather than claimed.

---

## Not findings — the rest of the run

- The registry-sync preview matched its documented shape exactly; the applied `entity_types` delta was
  one row (`skill`, `SKL-`, `On-request`).
- `refresh_stock` refreshed four stale-stock prompts (`register-liveness`, `loop-guard`,
  `loop-iteration`, `README`) and emitted the new `skill-promote.md`.
- `gate_run` 7/7 after every write.
- **The promotion ceremony was run and the operator DECLINED** (`PE-368`). `skill-promote.md` stops
  correctly at step 1 for the cluster pick, and declining is one of its stated outcomes — nothing was
  written, and `LL-001` remains Approved and pinned. The ceremony's shape held up: the pinned-lesson
  warning it mandates is what made the graduation trade-off explicit before the decision.

## Still operator-reserved (unchanged from findings_19)

The three CUSTOMISED prompts lag again. `orient-resume.md` and `slice-review.md` still report
`stock_last_changed: 4.3.0` — so they still lack the **lesson** steps — and `integrity-check.md` sits
at 4.2.1. 4.4.0 moved `register-liveness` and the loop pair, which were stale-stock and refreshed
automatically. The hand-merge from `stock-history.json` remains an operator call.
