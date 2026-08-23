# findings_14 — Tamheed v3.1.0 acceptance pass

**Verdict: the MINOR delivers what findings_13 asked for, and one thing it claims does not happen.**
Every §2/§4/§5 recommendation from findings_13 is implemented and verified. Two negatives below, plus
an honest list of what I did not complete.

`migrations_head` unchanged at `004`, `schema_version` 4 — "no schema migration" confirmed.

---

## ✅ What was verified working

**Per-file leftover verdicts match my hand triage exactly.** I had no leftovers left, so I recreated
one of each to test it:

```
handoff/prm-001-follow-up.md: copy of prompts/prm-001-follow-up.md — safe to delete
handoff/prm-unique-thing.md:  NOT a copy of any package prompt — MOVE it into <package>/prompts/
                              (deleting would destroy live content)
```

That is precisely the distinction I had to make by hand in findings_13 §2 — including the parenthetical
reasoning I used to *override* v3.0.0's blanket "delete" advice for `prm-next.md`. The copy was detected
even though my recreated copy carried the provenance header, so it is a real content compare.

**`converted_prompts`** lists all three with per-kind hints naming stock counterparts
(prm-001→orient-resume/replan-deferred/slice-review, prm-002→package-onboarding/slice-kickoff,
prm-003→integrity-check/slice-review), each ending "this hint clears itself".

**`restated_content`** flagged prm-002 line 24 (`labeled-snapshot`) — and *also* `prm-next.md` line 16
(`unlabeled`), which the brief did not predict. That second hit exists because I moved `prm-next.md`
into the package last pass, so it is now in scope. Good catch by the tool.

**Readiness discrimination — exactly the findings_13 §4 ask.**

- `risks-discharged`: `discriminating: false` + *"0 of 23 risks rows have discharged_by set; this rule
  cannot discriminate (populate discharged_by to make it meaningful)"*. It names the column to populate.
- slice `defects-closed`: `discriminating: false` + *"0 of 63 defects rows have found_in set"* — the
  silent under-report from findings_13 is now visible.
- `open-questions-resolved` correctly does **not** carry the flag: 5 of 76 OQs do have `resolved_by`,
  so it genuinely discriminates. **The brief expected the flag here; the tool is right and the
  expectation was wrong.**

**Hover-isolate works.** Hovering one node dimmed **all 1,052** edges (`stroke-opacity` → `.04`) and
revealed **6** hidden `.hl` copies — that node's incident edges. `:has()` supported.
⚠ My first probe reported *0 dimmed* — it targeted the wrong element (`#flow svg a`) and the wrong
property (`opacity`, not `stroke-opacity`). A working feature nearly written up as broken; reading the
CSS rule rather than trusting the probe is what caught it.

**Isolated fold is now a per-family breakdown** — `constraint 15, assumption 16, … audit-verdict 160,
progress-entry 311, document-section 633 (1694 rows)`. findings_13 §5's ask, implemented.

**`requirements_unwired`** (gate) and **`requirements-wired`** (readiness) both listed exactly
FR-156–159, and both carry my root cause in their note: *"work_bind stamps commits, it does not wire
traceability"*. After wiring: both empty. **DEF-063 closed** (details in the row).

---

## ⚠ Negative 1 — the note does **not** self-update, and the code says it does

`handoff_emit` returned `written: []`, `diverged: ["CLAUDE.md (tamheed:note)"]`. The v2 note in
CLAUDE.md was **not** updated to v3.1.0 content. The code:

```python
elif note_m.group(0) != note_block.rstrip("\n"):
    if force:  content = content.replace(...)   # updates
    else:      diverged.append("CLAUDE.md (tamheed:note)")
```

…while the adjacent warning string promises the v2 note *"is marker-managed and **self-updates**
thereafter"*. It does not, without `force`.

This matters beyond documentation: **step 6 of this brief is premised on the note having self-updated.**
It hadn't, so the drift test could not run against v3.1.0 note content.

## ⚠ Negative 2 — `force` is all-or-nothing, so the fix for Negative 1 is destructive

`handoff_emit(force=True)` passes `force` into `_emit_prompt_library` → `_managed_emit` for **every**
prompt. Applying the tool-owned note therefore also overwrites the five diverged prompts
(`drift-register`, `integrity-check`, `orient-resume`, `progress-sync`, `slice-review`) — three of which
carried project customisation since before v3.0.0.

There is no way to update the block the tool owns without clobbering files the operator owns. The two
kinds of divergence are also indistinguishable in the output: `integrity-check` (operator-customised)
and `drift-register` (template moved on in 3.1.0) are reported identically.

**Suggestion:** either scope `force` (`force="note"` / `force="prompts"`), or make the marker-delimited
note self-update as documented and reserve `force` for operator-owned files.

## ⚠ Negative 3 (minor) — `pass` + `discriminating: false` still reads as green

Slice `defects-closed` reports `status: "pass"` alongside `discriminating: false`. "Verified clean" and
"cannot measure" are different claims, and a reader skimming statuses sees green. A distinct status
(`indeterminate`) would carry the meaning without relying on the reader noticing the flag. The blocking
case (`risks-discharged`) is fine — it fails loudly.

---

## What I did NOT complete (and why)

- **§2 prompt curation — not done.** The hints are read and judged (above), but the three converted
  files are un-curated and their provenance headers remain. This is a substantial authoring task and I
  ran out of context budget before it. `prompts/README.md` was emitted but I have not read it, so I owe
  no verdict on it.
- **§4 "⚠ 4 requirement(s) first" ordering — unverifiable, my error.** I ran §5 (wiring) before §4
  (viewer), which removed the only isolated requirements. The per-family breakdown is confirmed; the
  ⚠-first ordering for requirements is not, because I destroyed its precondition. Sequencing mistake,
  not a tool defect.
- **§6 drift verdict — still not delivered.** Blocked twice over: it needs a genuinely fresh session
  (I cannot start one from inside this one), *and* the note it would test was never applied
  (Negative 1). Running it here would have measured v3.0.0 note content in a session already saturated
  with recording instructions — the same uncontrolled experiment I declined in findings_13 §6.

## Summary

| Step | Verdict |
|---|---|
| 1 handoff_emit surfaces | ✅ all three; per-file verdicts match hand triage exactly |
| 2 Curation + README | ❌ not done — out of context budget |
| 3 Readiness discrimination | ✅ notes name the column to populate · ⚠ `pass` + non-discriminating |
| 4 Viewer | ✅ hover-isolate + per-family fold · ⚠ ⚠-first ordering unverifiable (my sequencing) |
| 5 Wire FR-156..159 | ✅ list → empty; DEF-063 closed, both halves |
| 6 Drift verdict | ❌ blocked — needs a fresh session AND the note was never applied |
| 7 This file | filed (three negatives) |
