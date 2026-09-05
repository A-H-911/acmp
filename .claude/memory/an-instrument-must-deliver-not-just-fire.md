---
name: an-instrument-must-deliver-not-just-fire
description: "LL-060 (pinned) and the DEF-140/DEF-139/DEF-129 arc — four instruments in one session named an evidence path that did not exist, or were read wrongly. Read before building ANY detector, before trusting a calibration, and before concluding an artefact is missing something."
metadata:
  node_type: memory
  type: feedback
  modified: 2026-09-05
---

# An instrument must DELIVER, not just fire — and the artefact usually has more in it than you read

Companion to [[a-control-proves-firing-not-coupling]] and [[an-instrument-must-report-on-itself]].
**`LL-060` is Approved and PINNED (`DEC-133` d1).** This file is the worked arc behind it.

## ⛔⛔ The lesson: the act of observing supplies the channel production lacks

`BuildOrFailFastAsync` timed out saying *"read the Docker build output above it"* — and there was
**480 seconds of silence**. I added a Testcontainers logger, calibrated it against a throwaway image
with a deliberate `sleep 4`, watched it emit per-step output with timestamps, shipped it — **and the
next CI run produced the identical silence.**

The logger wrote with `Console.WriteLine`. **CI runs `dotnet test … --collect:…` with no
`--logger "console;verbosity=detailed"`, so console output from a test is discarded.**

⭐⭐ **`LL-055` already says a control proves FIRING, never COUPLING — and knowing it did not save me,
because I ran the probe WITH that flag in order to SEE the output. That flag IS the missing channel.**
The instrument passed its own test because the test environment was the one place the fault could not
occur. **The check is one question: *what did I change in order to watch this, and does production
have it?***

✅ **The fix was to stop relying on a channel: embed the captured output in the EXCEPTION MESSAGE**,
which always reaches the log — the choice `StartOrFailFastAsync` had already made for the container
log. **When a sibling in the same file solved it, copy that rather than inventing a mechanism.**

⚠ **SECOND FORM, hours later, same session.** I "corrected" a comment claiming the 480 s budget is
`~1.1x a cold build` — measured **421 s on a laptop**, for a bound that governs **GitHub runners**.
Measured where it applies: the whole integration suite (build + start + 73 tests) is **2 m 14 s** on a
fresh runner, so the budget is **≥3.6×** and the ORIGINAL `~3x` was right. **Measure in the environment
the number GOVERNS.** ⛔ The wrong figure even carried its own hedge — a hedge is not a measurement.

## ⛔⛔ Four instruments, one session, each naming an artefact that was not there

1. **`DEF-138`'s own remedy (b)** proposed a comment in `.config/dotnet-tools.json`. **JSON admits no
   comments** — it breaks the manifest parser. Caught by running it *before* the option reached the
   operator (`LL-051`).
2. **The guard's message** said *"read the build output above it"*. There was none (above).
3. **`DEF-129`** says its deciding evidence *"sits uncollected in `trace.zip`"*. I filed **`DEF-139`**
   claiming it could never be there because the guest lives in a hand-made `browser.newContext()`.
   **`DEF-139` IS WITHDRAWN — that claim is false.**
4. **My own analysis of that artefact** — see below.

## ⛔⛔⛔ The one that cost a PR: a `trace.zip` holds MORE THAN ONE STREAM

**Playwright's `browser` fixture instruments `newContext()` and applies `use.trace` to it.** The guest
context WAS traced. Proven by the fix for `DEF-139` failing:
`Error: tracing.start: Tracing has been already started`.

**Each `trace.zip` contains `0-trace.*` (the fixture context) AND `1-trace.*` (the hand-made one).**
My analysis globbed `*-trace.network`, **merged both streams into one list**, saw the Secretary's 22
authenticated `/api/` calls and concluded *"both zips are the Secretary's context"*. The guest's
traffic was in the same files, in the stream I never read separately.

⭐ **`LL-052`'s shape: I read an AGGREGATE and a SNAPSHOT and concluded about the WHOLE.** The
discriminating step was one line — analyse `1-trace.network` **alone** — and it was available from the
moment the artefact was downloaded.

⚠ **What survives of `DEF-139`, narrowly:** `error-context.md`'s page snapshot IS the **fixture** page.
On a guest failure it renders as *"E2E Secretary"*, which reads as *the guest session was never
established* — specific, plausible, and wrong. **A caution about one artefact, not a tracing gap.**

## ✅ The payoff: `DEF-129` has a mechanism, and it is neither of its two remedies

Reading the guest stream alone:

| | |
|---|---|
| requests in the guest context | 50 |
| `/api/` requests (and with `Authorization`) | **0** |
| entire span of guest activity | `00:41:11.039` → `00:41:12.059` — **ONE SECOND** |
| last request | `POST …/realms/acmp/protocol/openid-connect/token` → **`status=-1`** |

Then nothing, for the remaining ~20 s of `captureBearer`'s wait.

- ⛔ **(a) "the 20 s bound is too tight" — NO.** A longer bound cannot help; the token POST never
  completes, so no authenticated request would ever appear.
- ⛔ **(b) "`captureBearer` is the wrong instrument" — NO.** It reports the truth: no authenticated
  `/api/` request was issued. **An accurate messenger for an upstream fault.**
- ✅ **The guest's PKCE token exchange hangs.** Upstream of the harness entirely.

⚠ **Not claimed (`LL-029`):** ONE occurrence, and *why* it hangs is not isolated. `status=-1` means no
response was recorded — consistent with a hang and with an abort, though the context was demonstrably
alive since `captureBearer` was still waiting. **Read the guest stream on the NEXT occurrence.**

## ⚠ Delivery mechanics worth not rediscovering

- **CI uploads `src/Acmp.Web/playwright-report` and NOTHING ELSE.** Playwright's default `test-results/`
  is never collected. `testInfo.attach(...)` is what makes the html reporter copy a file into the
  report — it is why the fixture traces appear there at all.
- **A diagnostic must never throw**, or it replaces the failure it was capturing.
  `ContainerStartup.CrashArtefacts` makes the same choice.
- **`DEF-140`'s cause was `archive.ubuntu.com`, which appears NOWHERE in the Dockerfile** — it arrives
  with the base image's `sources.list`. Three rows (`DEF-137`, and `DEF-140` twice) named the registry
  the FILE mentions; the real one came with the base image.

Related: [[a-control-proves-firing-not-coupling]], [[an-instrument-must-report-on-itself]],
[[read-the-artefact-not-the-entry-about-it]], [[ci-run-attribution-and-probability-remedies]].
