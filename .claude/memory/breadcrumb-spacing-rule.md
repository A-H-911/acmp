---
name: breadcrumb-spacing-rule
description: "ACMP design rule: a breadcrumb always sits 12px above the content beneath it, owned on the shared .breadcrumb component."
metadata: 
  node_type: memory
  type: project
  originSessionId: 3ed5b626-2110-45aa-a9d8-b997346483a7
---

ACMP UI design rule (operator, 2026-06-28): on **every** screen, the breadcrumb must have a
**12px gap** (`margin-block-end: var(--sp-3)`) to whatever sits beneath it (page title, state banner,
tabs). This is owned globally on the shared `.breadcrumb` class in
`src/Acmp.Web/src/styles/controls.css` — do NOT re-add a per-page breadcrumb margin.

**Why:** the operator twice flagged the breadcrumb sitting too close to the content under it (meeting
detail, then `/topics/TOP-2026-012`) and asked to "make it a design rule for all upcomings."

**How to apply:** the rule is already in `.breadcrumb`. New screens get it for free. A pre-existing
per-page override (`.mt-detail .breadcrumb` in the P6 meeting-detail branch / PR #28) is now redundant
(same 12px) — remove it when convenient. See [[exact-design-fidelity-visual-loop]].
