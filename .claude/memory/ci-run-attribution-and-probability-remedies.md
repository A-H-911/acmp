---
name: ci-run-attribution-and-probability-remedies
description: "The 2026-08-31 session — why a merge-commit CI run is its own run, why `skipped` is not a reason, and why a fix that lowers a probability can never be proven wrong."
metadata: 
  node_type: memory
  type: project
  originSessionId: 5bebccc0-0f40-4264-8664-0e4b90008986
  modified: 2026-08-31T20:44:58.234Z
---

The 2026-08-31 session began by finding `main` **red and unrecorded** and ended with `WBS-26.3` built.
Four durable findings came out of it, three now Approved lessons. Read this before recording anything
about CI, and before proposing a fix to an intermittent failure.

## 1. A PR-head run and a merge-commit run are different runs (`LL-036`, Approved + pinned)

After a squash-merge the tree that lands on `main` gets **its own CI run**, and it can disagree with the
run that gated the PR even though `git diff -- src/ tests/` between them is **empty**. On this repository
they have now disagreed **twice** — `DEF-108` occurrence 4, and `DEF-121`.

- **`gh pr checks` shows only the PR run.** It will report all-green while the branch is red.
- **Cite the run id, never a colour.** A run id cannot be attributed to the wrong tree.
- A merge **is** a push to the target branch, so `DEC-077` d2's poll-CI-to-completion rule applies to it.

**What it cost:** the previous session recorded `#331` as merging *"with ten checks green"* — true of the
PR run, false of the push run beside it — then made three package commits without running `gh run list`.
`main` sat red for a whole session. It also put a **false clause inside a `Met` verdict** (`AV-235`,
corrected by appending `AV-236`). ⚠⚠ **No gate can see that:** a verdict's evidence is free text and
nothing in the store compares it to the runs it names.

## 2. `skipped` is not a reason (`LL-039`, Proposed)

A GitHub Actions job reports `skipped` when its `if:` evaluated false **and** when a job in its `needs:`
did not succeed. **The API prints the same word for both.**

I read `ci.yml`'s `publish` job's `if:` (`vars.AWS_ROLE_ARN != ''`), saw `skipped`, and concluded the
variable was unset. Measured with a control: `AWS_ROLE_ARN` has been set since **2026-08-04** and `publish`
runs on every push to `main`; it was skipped that once because **`backend` failed** and it `needs:` it.
**The `needs:` line was one line above the `if:` and I did not read it.**

⭐ **Discriminator, one query:** read the conclusions of the jobs it needs, or find a control run where they
passed. ⚠ The false claim reached `DW-090`, `DEC-102` d2, `PE-725` and **three commit messages**, which
cannot be amended — `PE-731` is their correction of record.

## 3. A remedy that reduces a probability cannot be falsified (`LL-035`, Approved + pinned)

State the residual honestly — *"it removes the dominant cause, not every case"* — and **every later
recurrence has a ready explanation that is not *the fix was wrong***. The register then carries a remedy
nothing can retire, on a row that reads as diligent *because* it disclosed its limits.

**Prefer a remedy that changes the CONDITION over one that changes the ODDS.** `DEF-122`'s fix went from
*warm the host so the first request is less likely to be slow* (a mitigation) to *issue the requests
concurrently so the window cannot roll at all* (removes the condition). ⭐ Companion: **a re-run is a fresh
sample of every question the suite asks, not just yours** (`LL-037`) — the re-run I argued against found
`DEF-122`.

## 4. Isolation, not consistency — and the shape of a good one

`DEC-101` d1 refused a fix built on a *consistent-with* reading. The intervention that settled it moved
**one variable** and stated its own falsifier in advance:

| variant | wall clock | third response |
|---|---|---|
| control, no delay | 0.57 s | **429** |
| 65 s delay **before** request 1 | 1 m 5 s | **429** |
| 65 s delay **between** r1 and r2 | 1 m 6 s | **200** |

Identical wall clock, opposite outcomes → *"the test was slow"* is **eliminated**. The control proves the
harness reproduces the passing behaviour (`LL-013`).

## 5. `DEF-121` — the open row

Sole blocking readiness failure. `Open`/high, with `DEC-089`'s end-condition structure added by `DEC-103`
d1: chased when **(1)** the cause is diagnosed by an isolating intervention, **(2)** an instrumented
recurrence's log names a cause, or **(3)** the operator disposes of it. ⛔ **Greens satisfy no clause, by
design** — an end condition greens could satisfy would repeal the rule that a backend integration failure
is not called flaky on one more green.

⭐ **Its signature discriminates:** `Reason 0x00000006`, `Last errno 2` (ENOENT), an `AppLoader`-reported
LOAD failure, **no `lsasrv.dll` or `lsass.exe` frames** — against `DEF-108`'s occurrence-3/4 family
(`Reason 0x00000002`, errno 11 EAGAIN, `lsasrv`+`lsass` frames). Record which, with frames quoted, or the
accumulation is uninterpretable.

⚠ **`DEC-077` d3 has now been overridden twice** (`DEC-097` d2, `DEC-100` d2) while remaining unconditional
in its own text. `DEC-100`'s rationale flags that **a third should reopen whether the rule still says what
the practice does.**

## 6. `scripts/check-image-contract.mjs` — read before editing

`WBS-26.3`'s gate. Per image (`api`, `worker`, `web`): size ≤ 500 MB **decimal**, plus **two** base
assertions.

- **(A)** the Dockerfile's `FROM` repo:tag equals a **committed expectation in the script**;
- **(B)** the **pulled** base's `RootFS.Layers` are a **prefix** of the **built** image's.

⛔ **Neither alone is sufficient, and this was measured.** (B) cannot see `-extra` disappearing — repoint the
`FROM` to plain chiseled and (B) pulls plain chiseled, whose layers are a perfect prefix, so it *passes*.
(A) alone is circular. ⚠ The expectation is deliberately **not** derived from the Dockerfile, or (A) becomes
the file compared against itself. A **digest** bump within a tag needs no edit (Dependabot unblocked); a
**repo:tag** change fails until a human edits the table.

`ci.yml`'s `compose` job now **builds** (`docker compose build api web worker`, `-p acmpimg`), 12 s → 1 m 20 s,
`timeout-minutes` raised 10 → 30 in the same change. `NFR-054` finally reached `Implemented` (`AC-158`,
`AV-237`). See [[read-before-calling-it-a-defect]] and [[controls-must-detect-and-tell]].
