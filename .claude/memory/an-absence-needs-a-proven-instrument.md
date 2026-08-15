---
name: an-absence-needs-a-proven-instrument
description: "A parked \"blocker\" said an audit emitter never ran; the rows were being written the whole time and the test read a null column. An absence measured by an unproven instrument is not a measurement."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: b83c15a9-41a5-4f5b-906e-6aa00322b1c0
  modified: 2026-08-15T18:12:52.129Z
---

**2026-08-16, `DEF-056`.** A parked branch recorded a measured blocker: *"both positive
tests fail with an empty audit table; a temporary `if (true)` probe in the handler still
produced no row → it never runs in that harness."* Every word of the conclusion was wrong.

**The rows were being written the whole time.** The failing run's own log printed
`AuditEvent Authorization.Forbidden by kc-roleless seq=1` **inside the test that failed**.
The test helper selected `AuditEvent.Action`, and `IAuditSink.EmitAsync` writes a **lean v1
row**: `EventType` set, the enriched `Action` column **NULL**. Every authorization refusal
in ACMP takes that path — the pre-existing `AuthorizationBehavior` one included — so the
assertion read null for exactly the rows it existed to find. `/api/audit` normalises as
`(Action ?? EventType)`; the helper must too.

**Why:** this is [[verify-mechanically-not-carefully]]'s sibling and trap 25b's exact shape —
a mutant that "survived" because the patch never applied. Both are *an absence with nothing
proving the instrument was in the binary that ran*. The parked session wrote the probe,
believed its silence, and built a whole theory of the ASP.NET pipeline on top of it.

**How to apply:**
- **An absence is only evidence if the instrument is proven present.** Before believing "no
  row / no output / mutant survived", make the instrument prove itself: a throw-probe that
  MUST 500, an assertion that MUST fail, a `--force` rebuild. A silent probe and a missing
  registration are indistinguishable.
- **Read the run log before theorising about the framework.** The answer was already printed.
- **Inherit a parked blocker as a hypothesis, never as data** — re-derive it. Cost here:
  the whole "blocker" dissolved in one test run.
- **Corollary — a `NotContain` over the wrong column can never fail.** The two *controls* in
  the same file were passing **vacuously** the entire time. See [[controls-must-detect-and-tell]]:
  a rule that cannot fail is not a green light (the `risk-liveness` hollow-pass class).
- **The "unresolvable symbol" was a missing `using`.** `IAuthorizationMiddlewareResultHandler`
  lives in `Microsoft.AspNetCore.Authorization.Policy`, which is **not** in the Web SDK's
  implicit usings. A compile-time namespace fact was written up as a runtime DI mystery.
