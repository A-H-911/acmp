---
name: a-control-proves-firing-not-coupling
description: "LL-055: a positive control proves an instrument FIRES; it cannot prove the trigger's SUBJECT is the fault's SYMPTOM. Injecting the trigger's own predicate is a tautology that always passes. I then committed the same fault twice inside the work fixing it."
metadata:
  node_type: memory
  type: feedback
  modified: 2026-09-03
---

Read this **before shipping any detector, and before writing the test that proves it works** — and
after [[an-instrument-must-report-on-itself]], which this extends by one step.

## The instance (`DEF-134`, `LL-055`, `DEC-125` d1)

`WBS-27.1` built a stall watchdog to make `DEF-109`'s clause (2) reachable. Its only trigger fired
when a dedicated sampler asked to sleep 5 s and woke ≥ 20 s later — **the PROCESS not being
scheduled**. `StallWatchdogTests` injected a 40-second drift and watched a snapshot appear: 10/10,
mutation-checked twice, `CaptureIfDegraded` made `internal` specifically so that control could exist.

**Then the fault fired.** `DEF-109` occurrence 6: eighteen requests each burned a full 100-second
`HttpClient` ceiling across seventeen classes over 17 minutes — and `windowMaxDrift` was **0.049 s
once and under 3 MILLISECONDS in every other window**, against a 15-second threshold. The process was
scheduled promptly *throughout the failure*. **No threshold on that quantity could ever have fired.**

⛔⛔ **The injected fault WAS the trigger's own predicate**, which makes the control a tautology with
respect to the only question that matters. Injecting drift proves the mechanism responds to drift;
whether *the fault* produces drift is a different claim, was never tested, and is false.

## ⭐ What makes it a lesson and not an oversight: the gap was DECLARED

That test file's header said it, before any test: *"they prove nothing about whether a real `DEF-109`
occurrence produces a large drift. Only an occurrence can show that."* That is `DW-097`'s
falsifiable-instrument model working, and being duly falsified. **But a cheaper injection existed the
whole time** — a request-in-flight register is injectable in-process in milliseconds. The deferral was
reasonable and it cost **two days and three PRs**.

## ⛔⛔ I then committed the same fault TWICE inside the work fixing it

1. `DEF-134`'s first framing said nobody had considered the coupling. **They had, in writing.**
   Corrected by `PE-830` before `LL-055` could be Approved on a false premise (`LL-020`).
2. My replacement test called `InFlightRequests.Begin(...)` **directly** while its own comment claimed
   it went "through the real middleware" — the tautology one layer up. Rebuilt on the real
   `IStartupFilter` + `ApplicationBuilder`; **mutation-proven**: remove the middleware registration and
   exactly that one test fails (12 passed / 1 failed). The first draft would have stayed green.

⭐ **The trap is not carelessness. I had filed the lesson forty minutes earlier and still wrote it.**

## The rule

Write the causal chain from **FAULT → the quantity your trigger reads**, and name the step you have
not measured. Then, *before* deferring it to "only a real occurrence can show this," spend five
minutes asking whether a cheaper injection reaches it. **Prefer a trigger keyed on the fault's own
definition over a proxy that is merely easier to sample** — a seam at the definition is usually the
seam that makes the coupling injectable too.

Related: [[an-instrument-must-report-on-itself]] · [[a-green-control-can-be-blind]] ·
[[scan-must-prove-it-had-a-subject]] · [[read-before-calling-it-a-defect]]
