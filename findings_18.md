# findings_18 — v4.2.0 upgrade + the findings_17 repairs

2026-08-15. Short by design: most of this run was clean and clean runs need no report. Two things
are worth recording — one is a mistake I made and caught, the other is a gap the tooling created
correctly and cannot close by itself.

---

## 1. ⚠ I CORRUPTED A ROW BY RETYPING A PAYLOAD I HAD JUST GENERATED CORRECTLY

**What happened.** The risk-scale recovery was built by script from `data/risks.jsonl` — exactly as
findings_17 C1 demands — and the script's output was right for all 23 rows. Then, transcribing that
output into the `entity_upsert` call, I typed `RISK-012` as `probability: "medium"` when the stash
said `H` → `high`. One character class of error, silently wrong, in the middle of a repair whose
entire purpose was to stop silently-wrong data.

**How it was caught.** A verifier that re-reads the JSONL after the write and re-derives every value
from `custom_attributes.v3_*`:

```
mismatched mappings: [('RISK-012', 'H', 'medium', 'H', 'high')]
```

Fixed, then re-verified: scale mismatches NONE, lost stash NONE, titles byte-identical to
`v1.Risk`, none still 200 chars.

**The lesson, and it is sharper than findings_17 C1.** C1 said *build the payload from the JSONL,
not from `entity_query`*. That is necessary and it was followed. What it did not say is the second
half: **a generated payload must be PASTED, not re-typed.** Generating correct bytes and then
copying them by hand reintroduces exactly the risk the generator was there to remove — the hand is
the untrusted transport. The generator is only as good as the channel between it and the tool call.

**And the real control was not the care, it was the verifier.** I was being careful; care did not
catch it. A post-write check that re-derives the value from its source did, in one line. Any repair
that transforms N rows should end with a re-read that recomputes the expected value independently —
otherwise the repair's own correctness rests on the same hand that made the error.

## 2. `slice-review.md` is customised, so `refresh_stock` correctly skipped it — and it now lags

v4.2.0 changed `prompts/README.md` and `prompts/slice-review.md`. `handoff_emit(refresh_stock=true)`
refreshed README (it matched the 4.1.0 stock template) but **left `slice-review.md` alone**, because
our copy is one of the three deliberately CUSTOMISED stock prompts and `refresh_stock` never
overwrites those. That is the tool behaving exactly as designed.

The consequence is still real: **our `slice-review.md` does not carry this release's changes.** The
same is true of `orient-resume.md` and `integrity-check.md` whenever their templates move. There is
no automatic answer — `force=true` would overwrite ALL diverged stock including the customisations,
and delete-then-re-emit discards ours for that one file. Reconciling means a hand-merge, and it is
an operator call whether this release's delta is worth one.

⚠ Worth knowing structurally: **customising a stock prompt opts it out of every future refresh,
silently and permanently.** Three files are in that state today.

---

## Not findings — recorded so the next run does not re-derive them

- `open-questions-resolved` went **72 → 1** (`OQ-074`) on the 4.2.0 rule fix. findings_17 B1 was a
  correct diagnosis; nothing further to do.
- The risk scale is fully recovered from the migration's own stash (23/23), and `probability`/
  `impact` are no longer null — findings_17 **C3 is closed**.
- Risk titles are fully recovered from `custom_attributes.v1.Risk` (11 rows) — findings_17 **A4 is
  closed**. None remain at exactly 200 characters.
- The six milestone statuses are stashed as `custom_attributes.v3_status` with the `v1` blob merged,
  not replaced — findings_17 **A5 is closed**, on the row itself rather than only in git.
- findings_17 **C2 re-verified on 4.2.0**: omitting `custom_attributes` still preserves the blob.
  Zero rows lost their stash across 34 full-row upserts.

## 3. ⚠ Populating the scale turned `risk-liveness` from a HOLLOW PASS into a real finding

Last session's sweep moved `risk-liveness` `indeterminate` → `pass` and I reported that as the
outcome. **That pass was hollow.** The rule tests *open **high-probability/high-impact** risks
missing an owner* — and with `probability`/`impact` null on every row, **no row could satisfy the
"high" predicate**, so nothing could ever be flagged. It passed because it could not fail.

With the scale recovered it went **`pass` → `fail`**, naming six rows:

> `RISK-013`, `RISK-016`, `RISK-017`, `RISK-018`, `RISK-019`, `RISK-020`

That is the instrument working for the first time, not a regression — and it is the same lesson
findings_17 B2 made about `risks-discharged`, now demonstrated from the other side: **a rule that
cannot discriminate is not a green light, and its green is the most misleading state it has.** An
`indeterminate` at least announces itself; a hollow `pass` looks like health.

**It also sharpens the operator ask.** findings_17 §E said "assign owners to `RISK-013`…`024`" —
twelve rows. The rule now says which six actually carry high probability or high impact. Those are
the ones where nobody monitoring is a real exposure; the other six can be carried.

---

## Still operator-reserved (refined from findings_17 §E)

`DW-027` close · `DW-026` build-or-carry · `OQ-074` answer · **owners for the six `risk-liveness`
names above** (was: all of `RISK-013`…`024`) · binding the 20 unbound ACs. These need your words;
nothing in this run touched them.
