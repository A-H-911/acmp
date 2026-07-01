---
name: phase-prompt-standard-footer
description: "For every ACMP phase prompt the user pastes, also satisfy the phase-prompts.md Standard Footer."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 714e1aeb-6bb7-4452-bc40-ead6040f6197
---

For every phase prompt the user pastes for ACMP, also satisfy the **Standard Footer** at the top of
`/execution-handoff/phase-prompts.md` — even when the pasted prompt does not restate it:

- ship unit + integration tests
- satisfy the relevant `AC-###` (`/docs/40`) and flip its verdict in `/docs/_progress/acceptance-audit.md`
- enforce authorization (`/docs/10`) and emit `AuditEvent`s
- no hardcoded strings (EN + AR) and verify RTL
- no secrets in source
- update `/docs/_progress/progress-log.md`
- conventional commits (small, reviewable)
- raise an ADR in `/adr` for any new architecture decision

**Why:** these are the project's Definition-of-Done gates (docs/44; Keystone G-PROGRESS / G-TRACE). The
per-phase prompts assume the footer applies to every slice without restating it.

**How to apply:** treat the footer as appended to every phase prompt; do not call a phase done until each
item is met and the progress-log + acceptance-audit reflect it.
