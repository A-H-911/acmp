---
name: localhost-ci-hides-load-races
description: CI on localhost always wins data-load races, so async-data races only appear against a real remote host — and derived error state erases its own evidence.
metadata:
  node_type: memory
  type: project
---

**Two failure shapes that CI structurally cannot catch, both found in DEF-028 (2026-08-09).**

**1. Localhost hides load races.** In the e2e workflow every service is on localhost, so a
React Query fetch always resolves before a test can click. Over the internet it does not.
`SchedulePage` accepted a submit while `useMembers()` was still loading, `onSubmit`'s
`!chair` guard returned early, and the click issued **no request at all**. 140 green
frontend test files and a green e2e workflow never saw it; the first run against
`https://uat.acmp.anas7ammo.dev` failed immediately. It is a real user bug too — anyone on a
slow connection who clicks promptly gets the same silence.

**2. Derived error state erases its own evidence.** `chairError` was computed from current
props, not latched at submit time. So the moment the query resolved, the error vanished and
the chair filled itself in. The Playwright failure screenshot — taken 180 s later — showed a
**perfectly valid form**, which is why the cause looked impossible for so long. The
diagnostic had been overwritten by the recovery.

**How to apply:**
- Any submit handler with a bare `return` guard must either disable its button until the
  data it guards on has arrived, or **latch** the reason so it survives the recovery.
- Make the guard and the message derive from the **same** expression. `chairError` tested
  `!effectiveChairId` while the guard tested `!chair` — strictly weaker, so they could
  disagree and show nothing.
- Prefer `disabled` over a spinner when the *form* is not ready rather than the button busy.
  It also makes Playwright wait, so the race cannot occur — the e2e spec needed no change.
- When a UAT failure looks impossible from its screenshot, suspect derived state and re-run
  with a settle wait as the single changed variable. That falsification test settled this in
  two runs after reasoning had gone in circles.

See [[ph5-sl025-uat-live]] and [[coverage-and-e2e-mandate]].
