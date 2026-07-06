---
name: phase-prompt-standard-footer
description: "For every ACMP phase prompt the user pastes, also satisfy the follow-up-prompts.md Standard Footer."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 714e1aeb-6bb7-4452-bc40-ead6040f6197
---

For every phase prompt the user pastes for ACMP, also satisfy the **Standard Footer** at the top of
`/docs/handoff/follow-up-prompts.md` — even when the pasted prompt does not restate it:

- ship unit + integration tests
- satisfy the relevant `AC-###` (`/docs/validation/acceptance-criteria.md`) and flip its verdict in `/docs/validation/acceptance-audit.md`
- enforce authorization (`/docs/domain/permission-role-matrix.md`) and emit `AuditEvent`s
- no hardcoded strings (EN + AR) and verify RTL
- no secrets in source
- update `/docs/progress/progress-log.md`
- conventional commits (small, reviewable)
- raise an ADR in `/docs/adrs` for any new architecture decision

**Why:** these are the project's Definition-of-Done gates (docs/execution/definition-of-done.md; Keystone G-PROGRESS / G-TRACE). The
per-phase prompts assume the footer applies to every slice without restating it.

**How to apply:** treat the footer as appended to every phase prompt; do not call a phase done until each
item is met and the progress-log + acceptance-audit reflect it.
