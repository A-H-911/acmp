# `SL-034` — the slate generator's three refusals, and the ASVS pack

2026-08-29. `SL-034` closed (`DEC-093`); `WBS-25.1` (`DEC-092`) and `WBS-25.2` (`DEC-093`) both
`Implemented`. Two PRs: `#326` → `964ab01a`, `#327` → `58b900b0`, ten checks green each.

## ⭐⭐⭐ The instrument that discharges `LL-011` refused its own subject — three times

`scripts/gen-slice-review-slate.mjs` is *how* `LL-011` is discharged, so **an item it cannot render is
one whose review must be hand-built** — the page the operator once refused an interview over.

| | refused | fixed by |
|---|---|---|
| `DEF-116` | other than **exactly one** AC | widened to one-or-more |
| `DEF-117` | **zero** ACs, even when the reason is recorded | criterion-less arm |
| `DEF-119` | criteria spanning **many requirements** | one section per distinct requirement |

- ⚠⚠⚠ **`DEF-117`: the zero-arm's comment claimed such an item is *"genuinely unreviewable"* — a claim
  about EVERY criterion-less item, falsified 2h38m later by the same day's work.** Comment landed
  `3f93ba66` 03:57; `WBS-25.1` created criterion-less at `Review` `20f0b61c` 06:35. ⭐ The fix keeps a
  fail-closed arm: zero ACs **and** no `DW-`/`DEC-` row recording why → still exits 2.
- ⚠⚠⚠ **`DEF-119` WAS FOUND BY THE REGRESSION CASE WRITTEN AS A CONTROL** — the case proving the
  *untouched* path still worked. `DEF-116` was marked `Fixed` while `WBS-24.5` (one of the two rows it
  names) still aborted: its 3 criteria answer to **3 different requirements** (`FR-155`/`NFR-059`/
  `NFR-060`), and `DEF-116` was verified against `WBS-24.8`, whose 2 criteria happen to **share** one.
  ⭐ **Multi-criterion and multi-requirement are different quantities** — `LL-015` on a guard, not a scanner.
- ⚠ `DEF-116`'s fix also left its own **prose** describing the old predicate in two places.

## ⭐⭐ `LL-032` (Approved + pinned) — a test whose fixture is the LIVE register

`DEF-120`. The suite committed in `#326` set ONE row to `Review` and left siblings as the register had
them. Promoting `WBS-25.1` **deleted the calibration's subject**: mutation applied, generator ran, but on
an item never under test. ⚠⚠ **It failed loudly ONLY BY LUCK** — the surviving row happened to carry a
citation. Had it lacked one, the generator would have exited 2 **with the expected message for a
different reason** and reported PASS forever. ⭐ Fix: **stage the whole selection** (row in, siblings out,
shape written into the title). ⭐ Proof the coupling is gone: it passes under the state that broke it.
⭐ **Discriminator:** *what would a normal day's work have to change for this test to stop testing what it
names?* If the answer is a status flip or an added row, the pass is not load-bearing.

## ⭐⭐ The ASVS pack — `DOC-070`, first narrative doc outside frozen `docs/`

`tamheed-package/docs/asvs-l2-evidence-pack.md` (`DEC-091` d3 established the location; all 69 prior
`DOC-` rows point into the frozen archive).

- ⚠⚠ **THE SIZING MOVED TWICE, BOTH FROM THE KEYWORD SWEEP, NEITHER FROM THE ROW.** Bigger: `DOC-067`
  already existed (Approved, titled for ASVS) and `DW-079` never names it. Smaller: `security-controls.md`
  §20 already carried a full V1–V17 map. `LL-008` + `LL-025` on one item.
- ⛔⛔ **THE SENTENCE THE PACK MUST REFUSE:** §20 concludes *"L2 is met across all applicable chapters"* —
  the **self-assertion of conformance `DW-079` forbids**, one copy-paste from a false claim handed to a
  paid third party.
- ⭐ **From the standard itself: 17 chapters, 345 requirements, L1 70 / L2 183 / L3 92. LEVELS ARE
  CUMULATIVE — "Level 2" = the 253 at L1+L2**, not the 183 tagged L2. Getting that wrong is silent.
- ⭐ `[unverified titles]` **discharged**: 17/17, 0 mismatched — **calibrated first** by injecting ASVS
  4.0's `Malicious Code` for V5.
- ⚠⚠ **The control→chapter map needed TWO keys** (per-control ASVS column + section-heading `V`-suffix):
  **41 of 72 controls disagree**, so one key silently drops mappings (`LL-009`).
- ⚠ `scripts/check-asvs-pack-paths.mjs` — asserts **existence, never sufficiency**; **fails closed below
  12 citations** (trap 31). In `ci.yml`'s **`compose`** job, positioned so it fires when **SOURCE moves**
  (markdown and `tamheed-package` are path-ignored, so editing the pack never runs it).
  ⭐ One of ten paths I wrote from memory was wrong — the whole argument for checking mechanically.

## ⚠ Gaps found by RE-VERIFYING, not by building (all filed, all in the pack)

- **`DW-093`** — `C-AUTH-05`'s **SoD-4 has no hard guard**: siblings appear 11/10/13/11 times in `src`,
  **SoD-4 zero**. Only prior record was *"verify in a later batch"* in the frozen archive; no register row
  knew. ⛔ **Required strength is unsettled** — the same catalogue calls SoD-2 *warn+audit*. Answer that
  before building; a hard *recorder ≠ owner* guard could refuse legitimate minute-taking.
- **`DW-092`** — `C-INS-01`'s Restricted-access alert. `DOC-067` declined **two** signals on two premises,
  both now false; `DW-087` corrected only the export half. ⭐ **One sentence justified two exclusions and
  only one was corrected** — grep found it via a two-key sweep (0 hits, control term non-zero in 6 of 7).
- **`DEF-118`** — `DOC-067` stale; its title claims an ASVS mapping its body lacks (only the title line
  matches). Its `C-AUTHZ-04` row says no `Confidentiality` field exists; `SL-030` shipped it.
- ⚠ **At least FOUR plaintext internal hops** (`keycloak:8080`, `minio:9000`, `seq:5341`, nginx→api).
  `DOC-067` names two; `DEF-100`/`DW-074` name two **different** ones. Neither wrong, neither complete.

## ⚠ Instrument traps I hit myself this session

- ⚠⚠ **`jq` IS NOT INSTALLED — I read that in `prm-next.md` and built a monitor on it anyway.** It does
  not error; it **emits nothing**, and silence is indistinguishable from "still running". ⭐ Use `gh`'s own
  `--jq`. Caught by checking the instrument produced output before trusting it.
- ⚠⚠ **A poller that re-evaluates `git rev-parse HEAD` each iteration** started asking about a sha with no
  runs once package commits landed, and printed `0 running / 0 runs` — **which reads exactly like
  "finished"**. Pin the sha.
- ⚠ `$?` after a pipe is the pipe's last command (trap 23b) — fired twice, once on the command verifying
  a fix for `LL-001`. ⚠ An `&&` chain stops at `grep -c` returning 0, so the second half never runs.
- ⚠ `*/` inside a glob (`src/**/*.cs`) **closes a `/* */` block comment early** — use line comments.

## Store mechanics proven here

- ⚠⚠ **Approving a lesson needs `"operator_confirm": true`** — on TOP of trap 14b's byte-identity
  requirement and `confirmed_by` (which can never be added later). **Three guards on one transition.**
- ⭐ **`handoff_emit` ran in the SAME batch as the approval** (`DEF-107`'s failure mode) — verified
  `LL-032` present in `tamheed-package/CLAUDE.md`.
- ⭐ Closing three `DW-` rows: titles appended and **verified byte-identical** against a generated
  pre-image, with the comparison **calibrated** by corrupting one character first. `activation_trigger`
  (1570/619/546 chars) **preserved by omission** exactly as trap 14c predicts.
- ⚠ `lesson` rows have no `impacts`/`source_kind`/`source_span` columns — they use `context`,
  `recommendation`, `rationale`, `impact_if_followed`, `impact_if_ignored`.
