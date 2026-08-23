# Invariant audit — ACMP load-bearing subset

Read alongside the stock `integrity-check.md`. This file holds the project-specific half: which
invariants are load-bearing here, and what a passing gate does NOT prove.

Review the change against every applicable row from `entity_query("invariant")` and report each as
Pass / Fail / n-a with one line of evidence. **A single Fail blocks the merge and requires a new
`adr` row before work continues** — never a workaround.

## The load-bearing subset

- The approved stack is unchanged and the system is still a **modular monolith** — no new broker,
  orchestrator, or second datastore.
- Every new endpoint and command passes **role and attribute-based authorization**, least privilege
  held, segregation of duties intact.
- Every state change **emits an audit event** and the hash chain is intact. Votes, issued decisions,
  approved ADRs and published minutes are **immutable**.
- **No secrets in source.**
- The feature ships with **unit and integration tests satisfying its acceptance criteria**.
- **No hardcoded user-facing strings** — EN and AR both present, right-to-left verified on every
  touched screen.
- **No drift from the matching design reference** (see `project-design-review.md`).

## ⚠ THE GATE SET DOES NOT MEASURE FIDELITY

All seven gates are **row-level**: they confirm a row exists, its identifier is well-formed, and its
text is not a placeholder. They cannot see:

- a column silently left empty,
- a title truncated at a fixed cap,
- a value written into the **wrong column of the right row**.

All three occurred in this package's first store migration. They are recorded in the `defect` family
with the repair history in `DOC-069`. Seven green gates were returned throughout.

For an actual fidelity check, run the column profiler under the repair tooling and diff it against
the committed baseline. **`gate_run()` passing is not evidence that the data is right.**

Also report the audit evidence split from `gate_run()`. A narrated verdict is the graded party
grading itself — list every one. And treat a `G-TRACE` result that passes **vacuously** (nothing to
check) as a finding, not a pass.
