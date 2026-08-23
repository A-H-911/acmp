# findings_13 — Tamheed v3.0.0 acceptance pass

**Verdict: the MAJOR landed clean.** The conversion, the cutover, the relation guard and the
readiness rules all behaved as specified. Everything below is either a UX gap in the new viewer or a
finding *about this package* that v3 surfaced and v2 could not — which is the version doing its job.

Nothing had to be rolled back. No step required `force`.

---

## 1. Conversion — clean, and the guard was never needed

`package_open` converted `data/prompts.jsonl` → three `prompts/prm-*.md`, renamed the source to
`.jsonl.converted`, removed zero trace edges (matching a pre-check that found zero `PRM-` edges) and
raised no inject warnings.

Verified by measurement rather than reading: each file carries the provenance header naming its `PRM`
id and kind, has its `H1`, and contains the original body **verbatim** (substring compare against the
`.converted` source). Re-`open` is idempotent — content is deterministic, as documented.

**One thing worth keeping:** the open first refused on a stale lock, and a naive liveness check would
have stopped the pass. PID 84788 was **alive** — but it was `conhost.exe`, started
`2026-08-13T19:19:40Z`, over a day *after* the lock's `taken_at` of `2026-08-12T13:14:15Z`. A process
cannot hold a lock taken before it existed. Two independent discriminators (identity mismatch and
impossible ordering) made it safe to clear. This is exactly the documented PID-reuse trap, and it
fired for real.

## 2. Cutover — one judgement call I made against the tool's advice

`handoff_emit` produced the marker-managed `<!-- tamheed:note v2 -->` block with the
Recording-obligations table and a cheat-sheet including `readiness_check`. No v1-note warning.
`stale_references: []`. Re-running is idempotent (`written: []`, `unchanged: [CLAUDE.md]`).

The leftover-copies warning named three files. I resolved them **by kind**, not by blanket deletion:

| File | Evidence | Action |
|---|---|---|
| `prm-001/002/003` | proved byte-equivalent to the converted package copies (normalised compare) | deleted |
| `prm-adr0038-role-ui`, `prm-fr159-guest-invite` | literally stamped `⛔ SUPERSEDED — do not use` | deleted |
| `prm-next.md` | the **durable active kickoff**, pointed at by `RESUME.md` *and* `MEMORY.md` | **moved into the package** |

⚠ **Finding:** the warning asks for `prm-next.md` to be *deleted*. Following it literally would have
destroyed live operator content that exists nowhere else — it is not a copy of anything in the
package. Moving it to `<package>/prompts/` satisfied the tool's model exactly (it now appears in
`project_prompts` and the warning cleared) and preserved the content.

**Suggestion:** the warning should distinguish "copy of a converted prompt" (safe to delete, provable
by content compare) from "project prompt not yet in the package" (should be *moved*). Right now both
get the word "delete".

**Also reported, not overwritten (correct behaviour, operator's call):**
`prompts/integrity-check.md`, `orient-resume.md`, `slice-review.md` are project-customised and
diverge from the v3 templates.

## 3. Gates + relations — as specified

Referential gates now say **"verified now"** rather than "enforced at write time":

- `G-IDS` — `foreign_key_check` clean, `entity_index` consistent across **2,099 ids**
- `G-REQ-SRC` — catches NULL/whitespace-only provenance, explicitly noting the DDL CHECK misses
  whitespace-only. That is a real strengthening, not a re-label.

`relation_rules` advisory lists two legacy edges — `ADR-0027 —supersedes→ FR-151` and `→ FR-153`.
Both are genuinely mistyped (`supersedes` is `SAME_TYPE`; an ADR superseding a *requirement* is not
that), so the advisory is correct and correctly non-blocking.

**Deliberate mistype, rejected exactly as promised** — a `tests` edge from a defect:

```
relation 'tests' does not allow defect -> acceptance-criterion (DEF-062 -> AC-010);
allowed from: test; allowed to: acceptance-criterion, adr, assumption, constraint,
decision, defect, invariant, requirement, risk, slice, wbs-item
— use 'relates_to' for an untyped association
```

Per-item, names both types *and* both ids, lists both allowed sets, gives the `relates_to` hint, and
the batch rolled back (`applied: 0`).

## 4. Readiness — the single most valuable thing in this release

### `readiness_check("slice", "SL-004")` found something seven passing gates never did

`SL-004` is marked **`Implemented`**. Readiness blocks on `AC-003`, `AC-006`, `AC-010`, `AC-011` —
exactly the four authorisation ACs whose latest verdict is not Met. **The slice was closed before its
acceptance criteria were ever evidenced,** and `gate_run()` has been returning 7/7 the whole time
without a word about it.

That is the case for this feature existing.

### `readiness_check("package")` — blocking lists judged against a hand-audit

| Rule | Result | My audit |
|---|---|---|
| `acs-met` | 6 entities | ✅ exactly the 6 Partials |
| `defects-closed` | 12 entities | ✅ exactly the 8 previously open + the 4 filed this session |
| `risks-discharged` | 21 entities | ⚠ see below |
| `open-questions-resolved` | 71 entities | ⚠ see below |

⚠ **Three rules key on fields this package has never populated**, so they over- or under-report:

- `risks-discharged` → `discharged_by IS NULL` (**blocking**)
- `open-questions-resolved` → `resolved_by IS NULL` (advisory)
- slice `defects-closed` → defects `found_in` this slice — my `DEF-` rows leave `found_in` null, so
  `SL-004` reported **zero** open defects while `DEF-057` (stream scope, squarely SL-004's subject)
  is open.

The rules are sound; the data does not feed them. But the effect is asymmetric and worth knowing:
`risks-discharged` is **blocking**, so a package with an unpopulated `discharged_by` column can never
reach `ready: true` regardless of whether the risks are actually addressed. Meanwhile the resume
believes **one** open question exists and the tool reports **71** — both defensible under different
definitions, and the tool's is stricter.

**Suggestion:** where a rule keys on a field that is null for *every* row of that type, say so
("0 of 24 risks have `discharged_by` set — this rule cannot discriminate"). A rule that fires on all
rows is indistinguishable from a rule that is measuring nothing.

**No slice transition exercised:** all 27 slices are already `Implemented` (`SL-014` deliberately
Deferred per `DEC-028`), so nothing was genuinely pending closure. I declined to fabricate one — that
would have meant mutating real state to test a guard.

## 5. The `#flow` view — UX verdict

Four labelled columns, left to right: **Needs (232) → Decisions (23) → Work (23) → Verification
(122)**, colour-coded edges, per-relation radio filters, every node labelled and clickable.

**What works.** The layering reads as a story immediately, and the column counts alone are
informative — 232 needs funnelling through 23 decisions is a shape you can see in one glance. Labelled
nodes beat a force-directed hairball with hover-only ids. Splitting *connected* (400) from *isolated*
(1,699) is the right call: drawing 2,099 nodes would have been unreadable.

**What doesn't.**

1. **"What verifies FR-x?" is not answerable by looking.** At `all relations`, 1,042 edges rendered
   as long splines across the full width overlap into a wash; the `derives_from` and `implements`
   bundles cross in the middle. You can see *that* things connect, not *what connects to what*.
   Filtering to one relation helps but still leaves a ~1000px spline to trace by eye.
   **Highest-value fix: hover a node → dim everything except its incident edges.** Everything else in
   the view is already good enough that this one addition would carry it.

2. ⚠ **"What's untested?" — the view shows a rosier picture than reality, by construction.** I
   measured rather than assumed: of 222 requirements, 218 carry a `verifies`/`tests` edge and 4 do
   not. **All four of those are fully isolated** — so they are in the 1,699 the view excludes.
   The requirements with no verification are exactly the ones the flow view does not draw. Read it
   and everything appears verified.

   (I had expected to find connected-but-unverified requirements hiding in plain sight among the 400.
   There are **zero** — the failure mode is the opposite one, and only measuring showed that.)

   **Fix: break the isolated fold down by type.** "1,699 isolated" is a number; "1,699 isolated —
   4 requirements, N acceptance-criteria, …" would have surfaced this without a query.

## 6. Drift behaviour (note-3) — I cannot give you a clean verdict

Honest answer: **this session cannot test it.** Two reasons:

1. The execution work earlier today ran under a kickoff prompt that *explicitly* instructed
   "register every finding needing investigation or a decision as a Tamheed row AS YOU GO". Recording
   was demanded, so recording proves nothing about the note.
2. The v3 note landed in `CLAUDE.md` *after* that work finished.

For the record, that session did register unprompted-by-the-note: `DEF-059`, `DEF-060`, `DEF-061`,
`DEF-062`, `DW-027`, `DEC-043`, `DEC-044`, `ADR-0043` (a supersession), plus progress entries and
`work_bind` on every merge. But attributing any of it to note-3 would be reading a result into an
uncontrolled experiment. **A clean test needs a fresh session with a task prompt that says nothing
about recording.**

## 7. Package finding surfaced by the pass — `DEF-063`

`FR-156`, `FR-157`, `FR-158`, `FR-159` are **shipped and in production** (ADR-0038 across #234–#246;
ADR-0040 across #241–#242; `FR-156` extended *today* by #265) — and are still `lifecycle_status:
Proposed` with **zero trace edges in either direction**.

Root cause, which will recur: requirements created *during execution* never get wired. The
planning-time set (`FR-001`–`FR-155`) carries `derives_from`/`implements`/`tests` edges because they
were authored with their traceability in one pass. `work_bind` stamps commits onto entities but does
not create a requirement→test edge, and no gate asks for one.

The tests already exist (`MembershipApiTests`, `UserManagementFeatureTests`,
`InviteUserPanel.test.tsx`, `GuestPresenterApiTests`), so fixing this records what is already true.

---

## Summary

| Step | Verdict |
|---|---|
| 1 Conversion | ✅ clean, verified verbatim; stale-lock guard exercised for real |
| 2 Cutover | ✅ v2 note correct, idempotent · ⚠ "delete" warning should say "move" for live project prompts |
| 3 Gates + relations | ✅ "verified now" is real; mistype rejection exactly as specified |
| 4 Readiness | ✅ **caught a prematurely-closed slice** · ⚠ 3 rules key on never-populated fields |
| 5 `#flow` | ✅ layering + labels are a clear win · ⚠ no hover-isolate; untested items are structurally hidden |
| 6 Drift | ⚠ not testable this session — needs a fresh, unprompted one |
| 7 Package | `DEF-063` filed |
