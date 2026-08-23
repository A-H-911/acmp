---
name: a-green-control-can-be-blind
description: Three controls in one session were green, gating, trusted — and structurally unable to see what they were believed to cover; a green tick suppresses suspicion in a way a missing control never does
metadata:
  type: feedback
---

A control that is **present and green** is more dangerous than a control that is **absent**. An absent
control is a gap someone eventually notices and builds. A green one actively suppresses the suspicion
that would find the gap — the tick is right there on every PR, every `docker ps`, every dashboard.

Found three in one session (2026-08-16), all the same shape:

- **`DEF-079`** — the api container reported `Up 3 days (healthy)` while its own `/readyz` returned
  **503**. Its healthcheck probed `/healthz`, mapped with `Predicate = _ => false`, which selects
  **zero** registered checks. It could only fail if the process stopped answering TCP.
- **`DEF-081`** — gitleaks runs **gating, full history** (`fetch-depth: 0`, `--exit-code=1`) and passed
  on all **153** commits carrying a plaintext password, because `.gitleaks.toml:13` allowlists
  `.*\.md` — *every markdown file in the repo, by path*, so the value inside is never examined.
  ⚠ A **path** allowlist and a **value** allowlist sit under one heading and read as the same kind of
  thing. They are not: one exempts a known string, the other exempts every string that will ever be
  written there.
- **`DEF-078`'s own prescribed measurement** — `curl https://<host>/api/readyz` is **404 by
  construction**, and the obvious correction is worse: bare `/readyz` returns **200 serving
  `index.html`** through the SPA fallback. **A status-code-only check passes with the API stone dead.**

## How each was actually caught

Never by the control. Always by **reading one layer past the green signal**:

- the 503 surfaced only because `docker ps` and `curl /readyz` ran in the *same* command, so the
  contradiction was visible in one screen;
- the SPA fallback surfaced only from reading **body + headers** (`Content-Type: text/html`) instead of
  stopping at the status code — I had already drafted "prediction refuted";
- the gitleaks hole surfaced only because I wrote *"there is no secret-scanning gate"* into a defect
  row, then noticed a `secrets` job in CI and **checked my own claim**. The claim was false, and
  checking it produced the better finding.

## The rule

**Before trusting a green control, ask what it would take for it to go red — then confirm that is
actually reachable.** If you cannot name a concrete failure the control would catch, it is decoration.
Applies equally to your own new tests: mutate them and watch them fail (both mutants were killed here),
because a test that cannot fail is the same defect in a smaller box.

**And a confident absence-claim is a measurement, not an observation.** "There is no X", "the value
never leaves this file", "the handler never ran" — each was written confidently in this project and each
was false. See [[an-absence-needs-a-proven-instrument]] and [[scan-must-prove-it-had-a-subject]].

Related: [[controls-must-detect-and-tell]] (this is the *detect* half failing, the rarer variant),
[[read-before-calling-it-a-defect]], [[verify-mechanically-not-carefully]].
