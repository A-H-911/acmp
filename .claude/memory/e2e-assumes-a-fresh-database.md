---
name: e2e-assumes-a-fresh-database
description: "The Playwright suite assumed a database containing exactly its own fixtures — four different failure shapes, invisible to CI, that get worse every run."
metadata: 
  node_type: memory
  type: project
  originSessionId: 4515f496-f3b7-46a8-8285-244b75d6b513
  modified: 2026-08-11T12:42:54.458Z
---

The first ever **full**-suite run against a deployed environment (UAT, 2026-08-11) was
**56 passed / 6 failed**. Every failure was one assumption in four disguises: **the
database contains exactly this suite's fixtures and nothing else.** CI rebuilds that
database each run, so it is blind to the whole class **by construction** — the difference
was never the code. Zero product defects. `DEF-045`.

| Cause | Shape | Fix |
|---|---|---|
| **Identity** | `.find(m => m.role === X)` — role is not unique. Each fixture identity had **two** rows with distinct subs ([[immutable-history-cleanup-asymmetry]] / `DEF-029` orphans) | `meMember()` — resolve via `POST /members/me` → `publicId` |
| **Pagination** | `GetBacklog` pages at **25**; UAT held 75 topics, 52 in Triage. New topics are priority 0 and sort **last** | filter the backlog to the fixture's own key/stamp first |
| **Absolute count** | `toHaveCount(2)` while the dialog pre-selects **every** eligible voter — UAT had 4 | assert "eligible in, ineligible out", not a number |
| **Config** | polls 150 s for a sweep; `e2e.yml` sets a **minutely** cron, cloud keeps `0 6 * * *` (**daily**) | `test.skip` keyed on that exact variable |

**⚠ It ratchets.** A full run creates ~60 topics, so **each run makes the next more likely
to fail**. `core-loop` passed at 12:37 and timed out at 16:00 with no code change — it
pushed its own fixture off page 1 *mid-run*. So the failure count is not fixed; it grows.
**Sweep for the pattern, never fix only the reported victim.**

**How to apply:**

1. A spec must **locate its own fixtures**, never rely on them being the only rows.
2. Never assert an absolute count of anything the environment can add to.
3. When a test needs an env-only override, `test.skip` on **that exact condition** — so it
   still runs where it can pass. A blanket skip is an un-failable check
   ([[baselines-as-numbers-not-properties]]).
4. **A `409` can lie.** "not eligible" and "already voted" are both
   `InvalidOperationException` with an empty body — the same status hid two very different
   faults for an hour.
5. **Suspecting yourself is not evidence.** Twice a failure appeared in the same run as my
   edit to that file. `git stash` + re-run on the original code, and one grep for whether
   the spec even imports the changed helper, disproved both. Without that I'd have "fixed"
   regressions I never caused ([[read-before-calling-it-a-defect]]).

**Open:** whether UAT is reset before runs, or the suite stays hardened. Hardening is the
durable answer — a reset only restarts the ratchet.
